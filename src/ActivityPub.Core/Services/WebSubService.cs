using System.Text;

namespace ActivityPub.Core.Services;

public interface IWebSubService
{
    Task<bool> SubscribeAsync(string hubUrl, string topicUrl, string callbackUrl, CancellationToken cancellationToken = default);
    Task<bool> UnsubscribeAsync(string hubUrl, string topicUrl, string callbackUrl, CancellationToken cancellationToken = default);
    Task<bool> PublishAsync(string hubUrl, string topicUrl, string content, string contentType = "application/activity+json", CancellationToken cancellationToken = default);
    string VerifySubscriptionAsync(string mode, string topic, string leaseSeconds, string challenge, string callbackUrl);
}

public class WebSubOptions
{
    public string HubUrl { get; set; } = "https://pubsubhubbub.appspot.com";
    public TimeSpan SubscriptionLeaseDuration { get; set; } = TimeSpan.FromHours(24);
    public int MaxVerificationRetries { get; set; } = 3;
    public TimeSpan VerificationRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}

public class WebSubService : IWebSubService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebSubOptions _options;

    public WebSubService(IHttpClientFactory httpClientFactory, WebSubOptions options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<bool> SubscribeAsync(string hubUrl, string topicUrl, string callbackUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hubUrl)) throw new ArgumentException("Hub URL is required", nameof(hubUrl));
        if (string.IsNullOrEmpty(topicUrl)) throw new ArgumentException("Topic URL is required", nameof(topicUrl));
        if (string.IsNullOrEmpty(callbackUrl)) throw new ArgumentException("Callback URL is required", nameof(callbackUrl));

        var formContent = new Dictionary<string, string>
        {
            { "hub.mode", "subscribe" },
            { "hub.topic", topicUrl },
            { "hub.callback", callbackUrl },
            { "hub.lease_seconds", _options.SubscriptionLeaseDuration.TotalSeconds.ToString() }
        };

        var content = new FormUrlEncodedContent(formContent);

        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, hubUrl)
        {
            Content = content
        };

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UnsubscribeAsync(string hubUrl, string topicUrl, string callbackUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hubUrl)) throw new ArgumentException("Hub URL is required", nameof(hubUrl));
        if (string.IsNullOrEmpty(topicUrl)) throw new ArgumentException("Topic URL is required", nameof(topicUrl));
        if (string.IsNullOrEmpty(callbackUrl)) throw new ArgumentException("Callback URL is required", nameof(callbackUrl));

        var formContent = new Dictionary<string, string>
        {
            { "hub.mode", "unsubscribe" },
            { "hub.topic", topicUrl },
            { "hub.callback", callbackUrl }
        };

        var content = new FormUrlEncodedContent(formContent);

        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, hubUrl)
        {
            Content = content
        };

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PublishAsync(string hubUrl, string topicUrl, string content, string contentType = "application/activity+json", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(hubUrl)) throw new ArgumentException("Hub URL is required", nameof(hubUrl));
        if (string.IsNullOrEmpty(topicUrl)) throw new ArgumentException("Topic URL is required", nameof(topicUrl));

        using var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, hubUrl)
        {
            Content = new StringContent(content, Encoding.UTF8, contentType)
        };

        request.Headers.Add("X-Hub-Origin", topicUrl);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public string VerifySubscriptionAsync(string mode, string topic, string leaseSeconds, string challenge, string callbackUrl)
    {
        if (string.IsNullOrEmpty(mode)) throw new ArgumentException("Mode is required", nameof(mode));
        if (string.IsNullOrEmpty(topic)) throw new ArgumentException("Topic is required", nameof(topic));
        if (string.IsNullOrEmpty(challenge)) throw new ArgumentException("Challenge is required", nameof(challenge));
        if (string.IsNullOrEmpty(callbackUrl)) throw new ArgumentException("Callback URL is required", nameof(callbackUrl));

        if (mode == "subscribe" || mode == "unsubscribe")
        {
            return challenge;
        }

        throw new InvalidOperationException($"Unsupported WebSub mode: {mode}");
    }
}
