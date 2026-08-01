using ActivityPub.Core.Events;
using ActivityPub.Core.Models;
using System.Threading.Tasks;

namespace ActivityPub.Core.Implementations;

/// <summary>
/// Sample interceptor demonstrating how host applications can customize ActivityPub behavior
/// </summary>
public class SampleActivityPubInterceptor : IActivityPubInterceptor
{
    public Task<bool> OnActivityReceivedAsync(Activity activity)
    {
        // Example: Add metadata to all activities
        // In a real implementation, you might add timestamps, validation, etc.
        return Task.FromResult(true);
    }

    public Task<bool> OnActivityPublishedAsync(Activity activity)
    {
        // Example: Add metadata to all activities
        // In a real implementation, you might add timestamps, validation, etc.
        return Task.FromResult(true);
    }

    public Task<bool> OnFollowReceivedAsync(Activity activity)
    {
        // Example: Add metadata to all follow activities
        // In a real implementation, you might add timestamps, validation, etc.
        return Task.FromResult(true);
    }

    public Task<bool> OnPostCreatedAsync(Note note)
    {
        // Example: Add metadata to all posts
        // In a real implementation, you might add timestamps, validation, etc.
        return Task.FromResult(true);
    }
}