using ActivityPub.Core.Models;
using ActivityPub.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActivityPub.Core.Services;

public interface IMRFService
{
    Task<Activity?> ProcessAsync(Activity activity, CancellationToken cancellationToken = default);
}

public class MRFService : IMRFService
{
    private readonly ActivityPubOptions _options;
    private readonly ILogger<MRFService> _logger;

    public MRFService(IOptions<ActivityPubOptions> options, ILogger<MRFService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Activity?> ProcessAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        if (activity.Type == null)
        {
            _logger.LogWarning("Activity missing type, filtering");
            return null;
        }

        activity = ApplyRules(activity);

        if (activity == null)
        {
            _logger.LogInformation("Activity filtered by MRF rules");
            return null;
        }

        if (activity.Type == "Create" && activity.Object is not ActivityPub.Core.Models.Object)
        {
            _logger.LogInformation("Activity Create with non-Object object, filtering");
            return null;
        }

        return activity;
    }

    private Activity? ApplyRules(Activity activity)
    {
        if (activity.Type == "Create")
        {
            if (activity.Object is not ActivityPub.Core.Models.Object objectObj)
                return null;

            if (objectObj.Content != null && ContainsFilteredContent(objectObj.Content))
            {
                _logger.LogInformation("Content filtered due to prohibited words");
                return null;
            }

            if (!string.IsNullOrEmpty(activity.AttributedTo))
            {
                if (IsBlockedActor(activity.AttributedTo))
                {
                    _logger.LogInformation("Actor blocked by MRF rules");
                    return null;
                }
            }

            if (_options.MRFOptions?.MaxContentLength != null && objectObj.Content?.Length > _options.MRFOptions.MaxContentLength)
            {
                _logger.LogInformation("Content too long, filtered by MRF");
                return null;
            }
        }

        return activity;
    }

    private bool ContainsFilteredContent(string content)
    {
        if (_options.MRFOptions?.ProhibitedWords == null)
            return false;

        var lowerContent = content.ToLowerInvariant();
        return _options.MRFOptions.ProhibitedWords.Any(word => lowerContent.Contains(word.ToLowerInvariant()));
    }

    private bool IsBlockedActor(string actorUrl)
    {
        if (_options.MRFOptions?.BlockedDomains == null)
            return false;

        return _options.MRFOptions.BlockedDomains.Any(domain => actorUrl.Contains(domain, StringComparison.OrdinalIgnoreCase));
    }
}


