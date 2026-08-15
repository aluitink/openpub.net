using System.CommandLine;
using System.CommandLine.Invocation;

namespace ActivityPub.Cli.Commands;

public static class ActorCommand
{
    public static Command Create()
    {
        var actorCommand = new Command("actor", "Manage ActivityPub actors");

        var listCommand = new Command("list", "List all known actors");
        var limitOption = new Option<int?>("--limit", "Maximum number of actors to display");
        listCommand.AddOption(limitOption);
        listCommand.SetHandler((InvocationContext ctx) =>
        {
            var limit = ctx.ParseResult.GetValueForOption(limitOption) ?? 50;
            Console.WriteLine($"Listing up to {limit} actors...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to list actors.");
            Console.WriteLine("Usage: activitypub actor list --limit 100");
            return Task.CompletedTask;
        });

        var getCommand = new Command("get", "Get actor details by URI");
        var uriOption = new Option<Uri>("--uri", "Actor's ActivityPub URI");
        getCommand.AddOption(uriOption);
        getCommand.SetHandler((InvocationContext ctx) =>
        {
            var uri = ctx.ParseResult.GetValueForOption(uriOption);
            Console.WriteLine($"Fetching actor at {uri}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to fetch actor details.");
            return Task.CompletedTask;
        });

        var followCommand = new Command("follow", "Follow a remote actor");
        var followActorUri = new Option<Uri>("--uri", "URI of the actor to follow");
        var localActor = new Option<string>("--actor", "Local actor username");
        followCommand.AddOption(followActorUri);
        followCommand.AddOption(localActor);
        followCommand.SetHandler((InvocationContext ctx) =>
        {
            var actorUri = ctx.ParseResult.GetValueForOption(followActorUri);
            var actor = ctx.ParseResult.GetValueForOption(localActor);
            Console.WriteLine($"Following {actorUri} as {actor}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to perform follow.");
            return Task.CompletedTask;
        });

        var unfollowCommand = new Command("unfollow", "Unfollow a remote actor");
        var unfollowActorUri = new Option<Uri>("--uri", "URI of the actor to unfollow");
        var unfollowLocalActor = new Option<string>("--actor", "Local actor username");
        unfollowCommand.AddOption(unfollowActorUri);
        unfollowCommand.AddOption(unfollowLocalActor);
        unfollowCommand.SetHandler((InvocationContext ctx) =>
        {
            var actorUri = ctx.ParseResult.GetValueForOption(unfollowActorUri);
            var actor = ctx.ParseResult.GetValueForOption(unfollowLocalActor);
            Console.WriteLine($"Unfollowing {actorUri} as {actor}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to perform unfollow.");
            return Task.CompletedTask;
        });

        actorCommand.AddCommand(listCommand);
        actorCommand.AddCommand(getCommand);
        actorCommand.AddCommand(followCommand);
        actorCommand.AddCommand(unfollowCommand);

        return actorCommand;
    }
}
