namespace PaymentAPI.DTOs;

public record CreatePaymentRequestDto(
    string OrderId,
    decimal Amount,
    string Currency,
    string CardNumber,
    string WebhookUrl
);
