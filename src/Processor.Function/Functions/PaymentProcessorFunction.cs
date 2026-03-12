using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System.Text.Json;

namespace Processor.Function.Functions;

public class PaymentProcessorFunction
{
    private readonly ILogger<PaymentProcessorFunction> _logger;

    public PaymentProcessorFunction(ILogger<PaymentProcessorFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(PaymentProcessorFunction))]
    public async Task Run(
        [ServiceBusTrigger("payment-ingress", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Payment processor received message: {Message}", message);

        var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(
            message,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload is null)
        {
            _logger.LogWarning("Failed to deserialize payment message.");
            return;
        }

        _logger.LogInformation(
            "Processing payment: TransactionId={TransactionId}, Provider={Provider}, Amount={Amount} {Currency}, Status={Status}",
            payload.TransactionId,
            payload.Provider,
            payload.Amount,
            payload.Currency,
            payload.Status);

        // TODO: implement downstream processing logic
        await Task.CompletedTask;
    }
}
