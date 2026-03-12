using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Processor.Function.Functions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace PaymentGateway.Tests;

public class ProcessorFunctionTests
{
    private static readonly object ValidPayloadObject = new
    {
        transactionId = "txn_test_002",
        amount = 120.00m,
        currency = "EUR",
        status = "captured",
        timestamp = "2026-03-12T11:00:00+00:00",
        provider = "adyen"
    };

    private static string ValidMessage =>
        JsonSerializer.Serialize(ValidPayloadObject);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static (PaymentProcessorFunction sut, Mock<Container> containerMock) BuildSut()
    {
        var containerMock = new Mock<Container>();

        var databaseMock = new Mock<Database>();
        databaseMock
            .Setup(x => x.GetContainer("InboundWebhooks"))
            .Returns(containerMock.Object);

        var cosmosClientMock = new Mock<CosmosClient>();
        cosmosClientMock
            .Setup(x => x.GetDatabase("PaymentGateway"))
            .Returns(databaseMock.Object);

        var sut = new PaymentProcessorFunction(
            cosmosClientMock.Object,
            NullLogger<PaymentProcessorFunction>.Instance);

        return (sut, containerMock);
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_WhenValidNewMessage_SavesToCosmosDb()
    {
        // Arrange
        var (sut, containerMock) = BuildSut();

        object? capturedDocument = null;
        containerMock
            .Setup(x => x.CreateItemAsync(
                It.IsAny<object>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, PartitionKey?, ItemRequestOptions, CancellationToken>(
                (doc, _, _, _) => capturedDocument = doc)
            .ReturnsAsync(Mock.Of<ItemResponse<object>>());

        // Act
        await sut.Run(ValidMessage, CancellationToken.None);

        // Assert: document was written exactly once
        containerMock.Verify(
            x => x.CreateItemAsync(
                It.IsAny<object>(),
                It.Is<PartitionKey?>(pk => pk == new PartitionKey("adyen")),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: id follows the {provider}-{transactionId} format
        capturedDocument.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedDocument);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("id").GetString()
            .Should().Be("adyen-txn_test_002");
    }

    [Fact]
    public async Task Run_WhenDuplicateMessage_CatchesCosmos409Conflict_And_Completes()
    {
        // Arrange
        var (sut, containerMock) = BuildSut();
        var conflictException = new CosmosException(
            message: "Conflict",
            statusCode: HttpStatusCode.Conflict,
            subStatusCode: 0,
            activityId: Guid.NewGuid().ToString(),
            requestCharge: 1.0);

        containerMock
            .Setup(x => x.CreateItemAsync(
                It.IsAny<object>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(conflictException);

        // Act
        var act = () => sut.Run(ValidMessage, CancellationToken.None);

        // Assert: idempotency — conflict must be swallowed, not rethrown
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Run_WhenNonConflictCosmosException_Rethrows()
    {
        // Arrange
        var (sut, containerMock) = BuildSut();
        var serviceUnavailableException = new CosmosException(
            message: "Service Unavailable",
            statusCode: HttpStatusCode.ServiceUnavailable,
            subStatusCode: 0,
            activityId: Guid.NewGuid().ToString(),
            requestCharge: 0.0);

        containerMock
            .Setup(x => x.CreateItemAsync(
                It.IsAny<object>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(serviceUnavailableException);

        // Act
        var act = () => sut.Run(ValidMessage, CancellationToken.None);

        // Assert: non-conflict errors must propagate so Service Bus retries/DLQs the message
        await act.Should().ThrowAsync<CosmosException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Run_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var (sut, containerMock) = BuildSut();
        const string badMessage = "{ not valid json }}}";

        // Act
        var act = () => sut.Run(badMessage, CancellationToken.None);

        // Assert: PaymentProcessorFunction does not catch JsonException — it propagates,
        // allowing the Service Bus runtime to dead-letter the poison message.
        await act.Should().ThrowAsync<JsonException>();
        containerMock.Verify(
            x => x.CreateItemAsync(
                It.IsAny<object>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
