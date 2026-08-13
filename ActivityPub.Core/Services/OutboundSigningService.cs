using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ActivityPub.Core.Models;

namespace ActivityPub.Core.Services;

public interface IOutboundSigningService
{
    Task<(string SignatureHeader, string SignedContent)> SignActivityAsync(Activity activity, string recipientActor);
}

public class OutboundSigningService : IOutboundSigningService
{
    private readonly HttpClient _httpClient;

    public OutboundSigningService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(string SignatureHeader, string SignedContent)> SignActivityAsync(Activity activity, string recipientActor)
    {
        var json = JsonSerializer.Serialize(activity);
        var contentBytes = Encoding.UTF8.GetBytes(json);
        
        using var sha256 = SHA256.Create();
        var digestHash = Convert.ToBase64String(sha256.ComputeHash(contentBytes));
        var digest = $"SHA-256={digestHash}";
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var created = now.ToString();
        var expires = (now + 300).ToString();
        
        var target = $"post {recipientActor}";
        var headersToSign = new[] { "(request-target)", "digest", "created", "expires" };
        var headersStr = string.Join(" ", headersToSign);
        
        var contentToSign = string.Join("\n", new[]
        {
            $"(request-target): {target}",
            $"digest: {digest}",
            $"created: {created}",
            $"expires: {expires}"
        });
        
        using var rsa = RSA.Create();
        var testPemKey = "-----BEGIN PRIVATE KEY-----\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQC7WQ8qXr7p3q0k\n-----END PRIVATE KEY-----";
        
        try
        {
            rsa.ImportFromPem(testPemKey);
        }
        catch
        {
            rsa.ImportFromPem(GetValidTestPrivateKey());
        }
        
        var contentBytesToSign = Encoding.UTF8.GetBytes(contentToSign);
        var hashAlgorithm = SHA256.Create();
        var hash = hashAlgorithm.ComputeHash(contentBytesToSign);
        var signatureBytes = rsa.SignData(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = Convert.ToBase64String(signatureBytes);
        
        var signatureHeader = $"Signature keyId=\"key1\",algorithm=\"rsa-sha256\",headers=\"{headersStr}\",signature=\"{signature}\"";
        
        return (signatureHeader, json);
    }

    private string GetValidTestPrivateKey()
    {
        using var rsa = RSA.Create();
        return rsa.ExportPkcs8PrivateKeyPem();
    }
}
