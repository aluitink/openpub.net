using System.CommandLine;
using System.CommandLine.Invocation;

namespace ActivityPub.Cli.Commands;

public static class ActivityCommand
{
    public static Command Create()
    {
        var activityCommand = new Command("activity", "Manage ActivityPub activities");

        var listCommand = new Command("list", "List recent activities");
        var limitOption = new Option<int?>("--limit", "Maximum number of activities to display");
        var actorFilter = new Option<string?>("--actor", "Filter activities by actor username");
        listCommand.AddOption(limitOption);
        listCommand.AddOption(actorFilter);
        listCommand.SetHandler((InvocationContext ctx) =>
        {
            var limit = ctx.ParseResult.GetValueForOption(limitOption) ?? 20;
            var actor = ctx.ParseResult.GetValueForOption(actorFilter);
            Console.WriteLine($"Listing up to {limit} activities{(actor != null ? $" for '{actor}'" : "")}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to list activities.");
            return Task.CompletedTask;
        });

        var getCommand = new Command("get", "Get activity details by ID");
        var idOption = new Option<string>("--id", "Activity ID");
        getCommand.AddOption(idOption);
        getCommand.SetHandler((InvocationContext ctx) =>
        {
            var id = ctx.ParseResult.GetValueForOption(idOption);
            Console.WriteLine($"Fetching activity {id}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to fetch activity details.");
            return Task.CompletedTask;
        });

        var fetchCommand = new Command("fetch", "Fetch a remote activity by URI");
        var uriOption = new Option<Uri>("--uri", "Activity URI");
        fetchCommand.AddOption(uriOption);
        fetchCommand.SetHandler((InvocationContext ctx) =>
        {
            var uri = ctx.ParseResult.GetValueForOption(uriOption);
            Console.WriteLine($"Fetching remote activity at {uri}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to fetch remote activity.");
            return Task.CompletedTask;
        });

        var deleteCommand = new Command("delete", "Delete an activity (create tombstone)");
        var deleteId = new Option<string>("--id", "Activity ID to delete");
        deleteCommand.AddOption(deleteId);
        deleteCommand.SetHandler((InvocationContext ctx) =>
        {
            var id = ctx.ParseResult.GetValueForOption(deleteId);
            Console.WriteLine($"Creating tombstone for activity {id}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to delete activity.");
            return Task.CompletedTask;
        });

        activityCommand.AddCommand(listCommand);
        activityCommand.AddCommand(getCommand);
        activityCommand.AddCommand(fetchCommand);
        activityCommand.AddCommand(deleteCommand);

        return activityCommand;
    }
}
