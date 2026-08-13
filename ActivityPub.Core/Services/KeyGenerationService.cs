using System.Security.Cryptography;
using System.Text;

namespace ActivityPub.Core.Interfaces;

public class KeyGenerationService : IKeyGenerationService
{
    public (string privateKeyPem, string publicKeyPem) GenerateRSAKeyPair()
    {
        using RSA rsa = RSA.Create(2048);
        string privateKeyPem = ExportPrivateKeyToPem(rsa);
        string publicKeyPem = ExportPublicKeyToPem(rsa);
        return (privateKeyPem, publicKeyPem);
    }

    public string ExportPrivateKeyToPem(RSA rsa)
    {
        byte[] privateKeyBytes = rsa.ExportRSAPrivateKey();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
        string base64 = Convert.ToBase64String(privateKeyBytes);
        for (int i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        sb.AppendLine("-----END RSA PRIVATE KEY-----");
        return sb.ToString();
    }

    public string ExportPublicKeyToPem(RSA rsa)
    {
        byte[] publicKeyBytes = rsa.ExportRSAPublicKey();
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PUBLIC KEY-----");
        string base64 = Convert.ToBase64String(publicKeyBytes);
        for (int i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        sb.AppendLine("-----END PUBLIC KEY-----");
        return sb.ToString();
    }
}
