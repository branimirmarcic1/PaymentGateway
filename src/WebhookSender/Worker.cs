using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Polly;
using Polly.Retry;

namespace WebhookSender;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Slanje webhooka nije uspjelo (Status: {StatusCode}). Pokušaj {retryCount}. Čekam {timeSpan}...", result.Result?.StatusCode, retryCount, timeSpan);
                });
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook Sender se pokreće.");
        return Task.Run(() => StartKafkaConsumer(stoppingToken), stoppingToken);
    }

    private void StartKafkaConsumer(CancellationToken stoppingToken)
    {
        var kafkaConnectionString = _configuration.GetConnectionString("Kafka");
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaConnectionString,
            GroupId = $"webhook-sender-group-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        try
        {
            using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
            _logger.LogInformation("Kafka consumer uspješno kreiran.");

            consumer.Subscribe("payment-outcomes");
            _logger.LogInformation("Uspješno subscribean na topic 'payment-outcomes'.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1)); // Čekaj do 1 sekunde

                    if (consumeResult is null)
                    {
                        continue;
                    }

                    _logger.LogInformation("Primljena poruka s offseta: {Offset}", consumeResult.Offset);
                    var processedEvent = JsonSerializer.Deserialize<PaymentProcessed>(consumeResult.Message.Value);

                    _logger.LogInformation("Primljen ishod plaćanja za {TransactionId}. Šaljem webhook na {WebhookUrl}", processedEvent.TransactionId, processedEvent.WebhookUrl);

                    _ = SendWebhook(processedEvent);

                    consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex) when (ex.Error.IsFatal)
                {
                    _logger.LogError(ex, "Fatalna greška Kafka consumera.");
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Došlo je do greške unutar while petlje.");
                }
            }
            consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nije moguće kreirati Kafka consumera ili se spojiti.");
        }

        _logger.LogInformation("Webhook Sender se gasi.");
    }

    private async Task SendWebhook(PaymentProcessed processedEvent)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var webhookPayload = new
            {
                processedEvent.TransactionId,
                processedEvent.OrderId,
                processedEvent.Status,
                processedEvent.FailureReason,
                Timestamp = DateTime.UtcNow
            };

            var jsonPayload = JsonSerializer.Serialize(webhookPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(() => httpClient.PostAsync(processedEvent.WebhookUrl, content));

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook za {TransactionId} uspješno poslan.", processedEvent.TransactionId);
            }
            else
            {
                _logger.LogError("Nakon svih pokušaja, slanje webhooka za {TransactionId} nije uspjelo. Finalni status: {StatusCode}", processedEvent.TransactionId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Greška prilikom slanja webhooka za {TransactionId}", processedEvent.TransactionId);
        }
    }
}