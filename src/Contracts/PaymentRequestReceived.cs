namespace Contracts;

public record PaymentRequestReceived(
    Guid TransactionId,
    string OrderId,
    decimal Amount,
    string Currency,
    string CardNumber,
    string WebhookUrl,
    string ApiKey
);
