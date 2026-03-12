using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Shared.Models;
using System.Net;
using System.Text.Json;

namespace Ingress.Function.Functions;

public class PaymentIngressFunction
{
    private readonly ILogger<PaymentIngressFunction> _logger;

    public PaymentIngressFunction(ILogger<PaymentIngressFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(PaymentIngressFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "payment/ingress")] HttpRequestData req)
    {
        _logger.LogInformation("Payment ingress webhook received.");

        var payload = await JsonSerializer.DeserializeAsync<PaymentWebhookPayload>(
            req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (payload is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Invalid payload.");
            return badRequest;
        }

        _logger.LogInformation(
            "Received payment: TransactionId={TransactionId}, Provider={Provider}, Amount={Amount} {Currency}, Status={Status}",
            payload.TransactionId,
            payload.Provider,
            payload.Amount,
            payload.Currency,
            payload.Status);

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        return response;
    }
}
