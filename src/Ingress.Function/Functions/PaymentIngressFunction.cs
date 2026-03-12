using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ingress.Function.Functions;

public class PaymentIngressFunction
{
    private const string HmacSecretName = "webhook-signing-key";
    private const string SignatureHeader = "X-Signature";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ServiceBusClient _serviceBusClient;
    private readonly SecretClient _secretClient;
    private readonly ILogger<PaymentIngressFunction> _logger;

    public PaymentIngressFunction(
        ServiceBusClient serviceBusClient,
        SecretClient secretClient,
        ILogger<PaymentIngressFunction> logger)
    {
        _serviceBusClient = serviceBusClient;
        _secretClient = secretClient;
        _logger = logger;
    }

    [Function(nameof(PaymentIngressFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "payment/ingress")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // ---------------------------------------------------------------
        // 1. Read raw body — must happen before any stream consumption
        // ---------------------------------------------------------------
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning("Received empty request body.");
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Request body is empty.");
        }

        // ---------------------------------------------------------------
        // 2. HMAC-SHA256 validation
        // ---------------------------------------------------------------
        if (!req.Headers.TryGetValues(SignatureHeader, out var signatureValues))
        {
            _logger.LogWarning("Missing {Header} header.", SignatureHeader);
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var providedSignature = signatureValues.FirstOrDefault();
        if (string.IsNullOrEmpty(providedSignature))
        {
            _logger.LogWarning("Empty {Header} header.", SignatureHeader);
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        KeyVaultSecret hmacSecret;
        try
        {
            hmacSecret = await _secretClient.GetSecretAsync(HmacSecretName, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve HMAC secret '{SecretName}' from Key Vault.", HmacSecretName);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        if (!IsValidHmacSignature(rawBody, providedSignature, hmacSecret.Value))
        {
            _logger.LogWarning("HMAC signature validation failed for incoming webhook.");
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        // ---------------------------------------------------------------
        // 3. Deserialize payload
        // ---------------------------------------------------------------
        PaymentWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize payment webhook payload.");
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Invalid JSON payload.");
        }

        if (payload is null)
        {
            return await CreateTextResponse(req, HttpStatusCode.BadRequest, "Payload deserialized to null.");
        }

        _logger.LogInformation(
            "Validated webhook: TransactionId={TransactionId}, Provider={Provider}, Amount={Amount} {Currency}, Status={Status}",
            payload.TransactionId,
            payload.Provider,
            payload.Amount,
            payload.Currency,
            payload.Status);

        // ---------------------------------------------------------------
        // 4. Publish to Service Bus
        // ---------------------------------------------------------------
        await using var sender = _serviceBusClient.CreateSender("payment-ingress");

        var messageBody = BinaryData.FromString(rawBody);
        var message = new ServiceBusMessage(messageBody)
        {
            ContentType = "application/json",
            MessageId = payload.TransactionId,
            Subject = payload.Provider,
            ApplicationProperties =
            {
                ["Status"] = payload.Status,
                ["Currency"] = payload.Currency
            }
        };

        await sender.SendMessageAsync(message, cancellationToken);

        _logger.LogInformation(
            "Published TransactionId={TransactionId} to Service Bus queue 'payment-ingress'.",
            payload.TransactionId);

        return req.CreateResponse(HttpStatusCode.Accepted);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static bool IsValidHmacSignature(string rawBody, string providedSignature, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);

        using var hmac = new HMACSHA256(keyBytes);
        var computedHash = hmac.ComputeHash(bodyBytes);
        var computedSignature = $"sha256={Convert.ToHexString(computedHash).ToLowerInvariant()}";

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(providedSignature));
    }

    private static async Task<HttpResponseData> CreateTextResponse(
        HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        await response.WriteStringAsync(message);
        return response;
    }
}
