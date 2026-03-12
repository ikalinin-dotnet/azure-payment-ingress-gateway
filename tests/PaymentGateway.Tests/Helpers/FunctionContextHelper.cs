using Microsoft.Azure.Functions.Worker;
using Moq;

namespace PaymentGateway.Tests.Helpers;

public static class FunctionContextHelper
{
    /// <summary>
    /// Creates a minimal mock FunctionContext sufficient for HttpRequestData construction.
    /// </summary>
    public static FunctionContext Create()
    {
        var mock = new Mock<FunctionContext>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        mock.Setup(ctx => ctx.InstanceServices).Returns(serviceProviderMock.Object);
        return mock.Object;
    }
}
