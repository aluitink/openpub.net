using ActivityPub.Core.Models;

namespace ActivityPub.Core.Services;

public interface IKeyFetchingService
{
    Task<PublicKey?> FetchPublicKeyAsync(string keyId);
}
