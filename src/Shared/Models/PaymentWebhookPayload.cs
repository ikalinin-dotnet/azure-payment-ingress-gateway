namespace Shared.Models;

public record PaymentWebhookPayload(
    string TransactionId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset Timestamp,
    string Provider
);
