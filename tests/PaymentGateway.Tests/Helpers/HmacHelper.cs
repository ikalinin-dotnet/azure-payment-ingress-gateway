using System.Security.Cryptography;
using System.Text;

namespace PaymentGateway.Tests.Helpers;

public static class HmacHelper
{
    /// <summary>
    /// Computes an HMAC-SHA256 signature in the same format as PaymentIngressFunction.
    /// Format: "sha256={lowercasehex}"
    /// </summary>
    public static string Compute(string rawBody, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(bodyBytes);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
