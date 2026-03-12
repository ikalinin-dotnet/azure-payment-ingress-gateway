using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Claims;

namespace PaymentGateway.Tests.Helpers;

/// <summary>
/// Concrete HttpRequestData implementation for isolated worker unit tests.
/// </summary>
public sealed class MockHttpRequestData : HttpRequestData
{
    private readonly Stream _body;
    private readonly HttpHeadersCollection _headers;

    public MockHttpRequestData(
        FunctionContext context,
        Stream body,
        IEnumerable<KeyValuePair<string, string>>? headers = null)
        : base(context)
    {
        _body = body;
        _headers = new HttpHeadersCollection(
            (headers ?? Enumerable.Empty<KeyValuePair<string, string>>())
                .Select(kvp => new KeyValuePair<string, IEnumerable<string>>(
                    kvp.Key, new[] { kvp.Value })));
    }

    public override Stream Body => _body;
    public override HttpHeadersCollection Headers => _headers;
    public override IReadOnlyCollection<IHttpCookie> Cookies => Array.Empty<IHttpCookie>();
    public override Uri Url => new("https://localhost/api/payment/ingress");
    public override IEnumerable<ClaimsIdentity> Identities => Enumerable.Empty<ClaimsIdentity>();
    public override string Method => "POST";

    public override HttpResponseData CreateResponse() => new MockHttpResponseData(FunctionContext);
}
