using ActivityPub.Core;
using ActivityPub.Core.Models;
using System.Text;
using System.Text.Json;

namespace ActivityPub.Tests.LoadTesting;

public static class ActivityExtensions
{
    public static string ToJson(this Activity activity)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(activity, options);
    }
}
