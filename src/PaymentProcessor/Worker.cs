using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using MongoDB.Driver;
using PaymentProcessor.Models; // <-- DODAJ OVAJ USING
using Polly;
using Polly.Retry;

namespace PaymentProcessor;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMongoClient _mongoClient;
    private readonly AsyncRetryPolicy _retryPolicy;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IMongoClient mongoClient)
    {
        _logger = logger;
        _configuration = configuration;
        _mongoClient = mongoClient;

        // Polly Retry Policy: Pokušaj ponovno 3 puta s malim zakašnjenjem
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Pokušaj {retryCount} nije uspio zbog {Exception}. Čekam {timeSpan} prije novog pokušaja.", retryCount, exception.Message, timeSpan);
                });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment Processor se pokreće.");

        var kafkaConnectionString = _configuration.GetConnectionString("Kafka");
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = kafkaConnectionString,
            GroupId = "payment-processor-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var producerConfig = new ProducerConfig { BootstrapServers = kafkaConnectionString };

        using var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();

        consumer.Subscribe("payment-requests");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = consumer.Consume(stoppingToken);
                var request = JsonSerializer.Deserialize<PaymentRequestReceived>(consumeResult.Message.Value);

                _logger.LogInformation("Primljen zahtjev za plaćanje: {TransactionId}", request.TransactionId);

                // Simulacija obrade plaćanja s Polly-em
                (bool success, string reason) = await _retryPolicy.ExecuteAsync(() => ProcessPaymentWithBank(request));

                // Logiranje u MongoDB
                await LogTransaction(request, success, reason);

                // Slanje ishoda na drugi Kafka topic
                var processedEvent = new PaymentProcessed(request.TransactionId, request.OrderId, success ? "Success" : "Failure", reason, request.WebhookUrl);
                var message = new Message<Null, string> { Value = JsonSerializer.Serialize(processedEvent) };
                await producer.ProduceAsync("payment-outcomes", message, stoppingToken);

                _logger.LogInformation("Obrada za {TransactionId} je završena sa statusom: {Status}", request.TransactionId, success ? "Success" : "Failure");
            }
            catch (OperationCanceledException)
            {
                // Normalno gašenje
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Došlo je do greške prilikom obrade poruke.");
            }
        }

        consumer.Close();
    }

    private async Task<(bool, string)> ProcessPaymentWithBank(PaymentRequestReceived request)
    {
        // Ovo je mock metoda koja simulira komunikaciju s bankom
        _logger.LogInformation("Kontaktiram banku za transakciju {TransactionId}...", request.TransactionId);
        await Task.Delay(1000); // Simulacija mrežne latencije

        // Simuliraj povremene greške
        if (new Random().Next(1, 4) == 1) // 25% šanse za grešku
        {
            throw new Exception("Bankovni API nije dostupan.");
        }

        return (true, "Plaćanje uspješno autorizirano.");
    }

    private async Task LogTransaction(PaymentRequestReceived request, bool success, string reason)
    {
        var database = _mongoClient.GetDatabase(_configuration["DatabaseName"]);
        var collection = database.GetCollection<TransactionLog>("TransactionLogs");

        var logDocument = new TransactionLog
        {
            TransactionId = request.TransactionId,
            OrderId = request.OrderId,
            Status = success ? "Success" : "Failure",
            Reason = reason,
            Timestamp = DateTime.UtcNow,
            RequestData = request
        };

        await collection.InsertOneAsync(logDocument);
    }
}