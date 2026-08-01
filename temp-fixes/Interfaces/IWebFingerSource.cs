using ActivityPub.Core.Models;

namespace ActivityPub.Core.Interfaces;

public interface IWebFingerSource
{
    Task<string?> GetWebFingerResourceAsync(string resource);
}