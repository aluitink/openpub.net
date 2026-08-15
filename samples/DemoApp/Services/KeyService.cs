using System.Security.Cryptography;
using System.Text;
using System;

namespace ActivityPub.Core.Interfaces;

public class KeyService : IKeyGenerationService
{
    public (string privateKeyPem, string publicKeyPem) GenerateRSAKeyPair()
    {
        using RSA rsa = RSA.Create();
        string privateKeyPem = ExportPrivateKeyToPem(rsa);
        string publicKeyPem = ExportPublicKeyToPem(rsa);
        return (privateKeyPem, publicKeyPem);
    }

    public string ExportPrivateKeyToPem(RSA rsa)
    {
        RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: true);
        return PemEncoding.Write("RSA PRIVATE KEY", parameters);
    }

    public string ExportPublicKeyToPem(RSA rsa)
    {
        RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: false);
        return PemEncoding.Write("RSA PUBLIC KEY", parameters);
    }
}

public static class PemEncoding
{
    public static string Write(string label, RSAParameters parameters)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportParameters(parameters);
        byte[] bytes = rsa.ExportSubjectPublicKeyInfo();
        return Write(label, bytes);
    }

    public static string Write(string label, byte[] data)
    {
        string base64 = Convert.ToBase64String(data);
        StringBuilder sb = new();
        sb.AppendLine($"-----BEGIN {label}-----");
        for (int i = 0; i < base64.Length; i += 64)
        {
            sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
        }
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }
}
