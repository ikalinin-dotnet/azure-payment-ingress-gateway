using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System.Net;
using System.Text.Json;

namespace Processor.Function.Functions;

public class PaymentProcessorFunction
{
    private const string DatabaseName = "PaymentGateway";
    private const string ContainerName = "InboundWebhooks";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<PaymentProcessorFunction> _logger;

    public PaymentProcessorFunction(
        CosmosClient cosmosClient,
        ILogger<PaymentProcessorFunction> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
    }

    [Function(nameof(PaymentProcessorFunction))]
    public async Task Run(
        [ServiceBusTrigger("payment-ingress", Connection = "ServiceBusConnection")] string message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Payment processor received message.");

        var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(message, JsonOptions);

        if (payload is null)
        {
            _logger.LogWarning("Failed to deserialize payment message — skipping.");
            return;
        }

        _logger.LogInformation(
            "Processing payment: TransactionId={TransactionId}, Provider={Provider}, Amount={Amount} {Currency}, Status={Status}",
            payload.TransactionId,
            payload.Provider,
            payload.Amount,
            payload.Currency,
            payload.Status);

        var container = _cosmosClient
            .GetDatabase(DatabaseName)
            .GetContainer(ContainerName);

        var document = new
        {
            id = $"{payload.Provider}-{payload.TransactionId}",
            payload.TransactionId,
            payload.Amount,
            payload.Currency,
            payload.Status,
            payload.Timestamp,
            payload.Provider,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await container.CreateItemAsync(
                document,
                new PartitionKey(payload.Provider),
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Saved payment document id={DocumentId} to Cosmos DB container '{Container}'.",
                document.id,
                ContainerName);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning(
                "Duplicate message detected — document id={DocumentId} already exists. Skipping.",
                document.id);
        }
    }
}
