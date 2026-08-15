using System.Text.Json.Serialization;

namespace ActivityPub.Core.Models;

/// <summary>
/// Constants for Activity Streams 2.0 vocabulary
/// </summary>
public static class ActivityStreamConstants
{
    /// <summary>
    /// Standard Activity Types
    /// </summary>
    public static class ActivityTypes
    {
        public const string Create = "Create";
        public const string Follow = "Follow";
        public const string Like = "Like";
        public const string Undo = "Undo";
        public const string Accept = "Accept";
        public const string Reject = "Reject";
        public const string Announce = "Announce";
        public const string Delete = "Delete";
        public const string Update = "Update";
        public const string Add = "Add";
        public const string Remove = "Remove";
        public const string Flag = "Flag";
        public const string View = "View";
        public const string Listen = "Listen";
        public const string Watch = "Watch";
        public const string Share = "Share";
    }

    /// <summary>
    /// Standard Object Types
    /// </summary>
    public static class ObjectTypes
    {
        public const string Note = "Note";
        public const string Article = "Article";
        public const string Event = "Event";
        public const string Person = "Person";
        public const string Organization = "Organization";
        public const string Service = "Service";
        public const string Application = "Application";
        public const string Group = "Group";
        public const string Profile = "Profile";
        public const string Tombstone = "Tombstone";
        public const string Collection = "Collection";
        public const string OrderedCollection = "OrderedCollection";
    }

    /// <summary>
    /// Standard Actor Types
    /// </summary>
    public static class ActorTypes
    {
        public const string Person = "Person";
        public const string Organization = "Organization";
        public const string Service = "Service";
        public const string Application = "Application";
        public const string Group = "Group";
    }

    /// <summary>
    /// Standard Properties
    /// </summary>
    public static class Properties
    {
        public const string Id = "id";
        public const string Type = "type";
        public const string Actor = "actor";
        public const string Object = "object";
        public const string Target = "target";
        public const string Name = "name";
        public const string Content = "content";
        public const string MediaType = "mediaType";
        public const string Url = "url";
        public const string AttributedTo = "attributedTo";
        public const string Published = "published";
        public const string Updated = "updated";
        public const string InReplyTo = "inReplyTo";
        public const string Parent = "parent";
        public const string Replies = "replies";
        public const string Summary = "summary";
        public const string First = "first";
        public const string Last = "last";
        public const string OrderedItems = "orderedItems";
        public const string Items = "items";
        public const string TotalItems = "totalItems";
        public const string Inbox = "inbox";
        public const string Outbox = "outbox";
        public const string Followers = "followers";
        public const string Following = "following";
        public const string Liked = "liked";
        public const string Icon = "icon";
        public const string Image = "image";
        public const string PublicKey = "publicKey";
        public const string PreferredUsername = "preferredUsername";
        public const string Domain = "domain";
        public const string StartTime = "startTime";
        public const string EndTime = "endTime";
        public const string Location = "location";
        public const string Attendees = "attendees";
    }
}