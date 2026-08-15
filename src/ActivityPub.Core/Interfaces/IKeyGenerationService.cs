using System.Security.Cryptography;

namespace ActivityPub.Core.Interfaces;

public interface IKeyGenerationService
{
    (string privateKeyPem, string publicKeyPem) GenerateRSAKeyPair();
    string ExportPrivateKeyToPem(RSA rsa);
    string ExportPublicKeyToPem(RSA rsa);
}
