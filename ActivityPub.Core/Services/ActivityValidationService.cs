using System.Text.Json;
using System.Text.Json.Nodes;
using ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;

namespace ActivityPub.Core.Services;

/// <summary>
/// Service for validating ActivityPub activities before processing
/// </summary>
public class ActivityValidationService : IActivityValidationService
{
    private readonly ILogger<ActivityValidationService> _logger;

    public ActivityValidationService(ILogger<ActivityValidationService> logger)
    {
        _logger = logger;
    }

    public bool Validate(string activityJson, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(activityJson))
        {
            errors.Add("Activity JSON is null or empty");
            return false;
        }

        try
        {
            var node = JsonNode.Parse(activityJson);
            if (node == null)
            {
                errors.Add("Failed to parse activity JSON");
                return false;
            }

            var jsonObject = node.AsObject();

            if (!ValidateActivityType(jsonObject, errors))
                return false;

            if (!ValidateId(jsonObject, errors))
                return false;

            if (!ValidateActor(jsonObject, errors))
                return false;

            if (!ValidateObject(jsonObject, errors))
                return false;

            if (!ValidateTimestamp(jsonObject, errors))
                return false;
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON parsing error: {ex.Message}");
            return false;
        }

        return errors.Count == 0;
    }

    private bool ValidateActivityType(JsonObject node, List<string> errors)
    {
        if (!node.ContainsKey("type") || node["type"] == null || string.IsNullOrEmpty(node["type"]?.ToString()))
        {
            errors.Add("Activity type is required");
            return false;
        }

        return true;
    }

    private bool ValidateId(JsonObject node, List<string> errors)
    {
        if (node.ContainsKey("id") && node["id"] != null)
        {
            var id = node["id"]?.ToString();
            if (string.IsNullOrEmpty(id))
            {
                errors.Add("Activity ID cannot be empty");
                return false;
            }

            if (!Uri.TryCreate(id, UriKind.Absolute, out _))
            {
                errors.Add("Activity ID must be a valid absolute URI");
                return false;
            }
        }

        return true;
    }

    private bool ValidateActor(JsonObject node, List<string> errors)
    {
        if (!node.ContainsKey("actor") || node["actor"] == null)
        {
            errors.Add("Actor is required");
            return false;
        }

        var actorNode = node["actor"];
        if (actorNode != null)
        {
            var actorObj = actorNode.AsObject();

            if (!ValidateActorObject(actorObj, errors))
                return false;
        }

        return true;
    }

    private bool ValidateActorObject(JsonObject node, List<string> errors)
    {
        if (!node.ContainsKey("id") || node["id"] == null)
        {
            errors.Add("Actor ID is required");
            return false;
        }

        var id = node["id"]?.ToString();
        if (string.IsNullOrEmpty(id))
        {
            errors.Add("Actor ID cannot be empty");
            return false;
        }

        if (!node.ContainsKey("type") || node["type"] == null || string.IsNullOrEmpty(node["type"]?.ToString()))
        {
            errors.Add("Actor type is required");
            return false;
        }

        return true;
    }

    private bool ValidateObject(JsonObject node, List<string> errors)
    {
        if (node.ContainsKey("object") && node["object"] != null)
        {
            var obj = node["object"]?.ToString();
            if (string.IsNullOrEmpty(obj))
            {
                errors.Add("Object cannot be empty");
                return false;
            }

            if (!Uri.TryCreate(obj, UriKind.Absolute, out _))
            {
                errors.Add("Object must be a valid absolute URI");
                return false;
            }
        }

        return true;
    }

    private bool ValidateTimestamp(JsonObject node, List<string> errors)
    {
        if (node.ContainsKey("published") && node["published"] != null)
        {
            var published = node["published"]?.ToString();
            if (!DateTime.TryParse(published, out _))
            {
                errors.Add("Published timestamp must be a valid date");
                return false;
            }
        }

        return true;
    }
}

public interface IActivityValidationService
{
    bool Validate(string activityJson, out List<string> errors);
}
