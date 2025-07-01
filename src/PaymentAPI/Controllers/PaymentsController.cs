using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using StackExchange.Redis;

namespace PaymentAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IProducer<Null, string> _kafkaProducer;
    private const string KafkaTopic = "payment-requests";

    public PaymentsController(IConnectionMultiplexer redis, IProducer<Null, string> kafkaProducer)
    {
        _redis = redis;
        _kafkaProducer = kafkaProducer;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromHeader(Name = "X-API-Key")] string apiKey, [FromBody] CreatePaymentRequestDto request)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return Unauthorized("API Key is missing.");
        }

        // Korak 2: Validacija API ključa u Redis-u
        var db = _redis.GetDatabase();
        var clientName = await db.StringGetAsync($"apikeys:{apiKey}");

        if (string.IsNullOrEmpty(clientName))
        {
            return Unauthorized("Invalid API Key.");
        }

        // Korak 3: Slanje 'PaymentRequestReceived' događaja na Kafku
        var transactionId = Guid.NewGuid();
        var paymentEvent = new PaymentRequestReceived(
            transactionId,
            request.OrderId,
            request.Amount,
            request.Currency,
            request.CardNumber,
            request.WebhookUrl,
            apiKey
        );

        var message = new Message<Null, string> { Value = JsonSerializer.Serialize(paymentEvent) };
        await _kafkaProducer.ProduceAsync(KafkaTopic, message);

        // Odmah vraćamo 202 Accepted, ne čekamo obradu
        return Accepted(new { TransactionId = transactionId, Status = "Pending" });
    }
}