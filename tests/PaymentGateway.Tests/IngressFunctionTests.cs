using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Ingress.Function.Functions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentGateway.Tests.Helpers;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PaymentGateway.Tests;

public class IngressFunctionTests
{
    private const string TestSecret = "super-secret-signing-key";
    private const string SecretName = "webhook-signing-key";

    private static readonly object DummyPayload = new
    {
        transactionId = "txn_test_001",
        amount = 49.99m,
        currency = "USD",
        status = "captured",
        timestamp = "2026-03-12T10:00:00+00:00",
        provider = "stripe"
    };

    private static string SerializedPayload =>
        JsonSerializer.Serialize(DummyPayload);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static (PaymentIngressFunction sut, Mock<ServiceBusSender> senderMock) BuildSut(
        string secretValue = TestSecret)
    {
        // SecretClient mock
        var secretClientMock = new Mock<SecretClient>();
        var kvSecret = SecretModelFactory.KeyVaultSecret(
            new SecretProperties(SecretName),
            secretValue);
        secretClientMock
            .Setup(x => x.GetSecretAsync(SecretName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(kvSecret, Mock.Of<Response>()));

        // ServiceBusSender mock — DisposeAsync must not throw because CreateSender is called
        // inside an "await using" block in PaymentIngressFunction.Run.
        var senderMock = new Mock<ServiceBusSender>();
        senderMock
            .Setup(x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        senderMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        // ServiceBusClient mock
        var serviceBusClientMock = new Mock<ServiceBusClient>();
        serviceBusClientMock
            .Setup(x => x.CreateSender("payment-ingress"))
            .Returns(senderMock.Object);

        var sut = new PaymentIngressFunction(
            serviceBusClientMock.Object,
            secretClientMock.Object,
            NullLogger<PaymentIngressFunction>.Instance);

        return (sut, senderMock);
    }

    private static MockHttpRequestData BuildRequest(
        string body,
        string? signatureHeaderValue)
    {
        var context = FunctionContextHelper.Create();
        var headers = signatureHeaderValue is not null
            ? new[] { new KeyValuePair<string, string>("X-Signature", signatureHeaderValue) }
            : Enumerable.Empty<KeyValuePair<string, string>>();

        return new MockHttpRequestData(
            context,
            new MemoryStream(Encoding.UTF8.GetBytes(body)),
            headers);
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_WithValidHmacSignature_Returns202Accepted_And_SendsToServiceBus()
    {
        // Arrange
        var body = SerializedPayload;
        var signature = HmacHelper.Compute(body, TestSecret);
        var request = BuildRequest(body, signature);
        var (sut, senderMock) = BuildSut();

        // Act
        var response = await sut.Run(request, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        senderMock.Verify(
            x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithInvalidHmacSignature_Returns401Unauthorized()
    {
        // Arrange
        var body = SerializedPayload;
        var request = BuildRequest(body, signatureHeaderValue: "sha256=000000deadbeef");
        var (sut, senderMock) = BuildSut();

        // Act
        var response = await sut.Run(request, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        senderMock.Verify(
            x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WithMissingSignatureHeader_Returns401Unauthorized()
    {
        // Arrange
        var body = SerializedPayload;
        var request = BuildRequest(body, signatureHeaderValue: null);
        var (sut, senderMock) = BuildSut();

        // Act
        var response = await sut.Run(request, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        senderMock.Verify(
            x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WithEmptyBody_Returns400BadRequest()
    {
        // Arrange
        var signature = HmacHelper.Compute(string.Empty, TestSecret);
        var request = BuildRequest(string.Empty, signature);
        var (sut, senderMock) = BuildSut();

        // Act
        var response = await sut.Run(request, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        senderMock.Verify(
            x => x.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
