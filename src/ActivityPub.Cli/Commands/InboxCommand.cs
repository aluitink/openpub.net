using System.CommandLine;
using System.CommandLine.Invocation;

namespace ActivityPub.Cli.Commands;

public static class InboxCommand
{
    public static Command Create()
    {
        var inboxCommand = new Command("inbox", "Monitor and manage inbox deliveries");

        var monitorCommand = new Command("monitor", "Monitor incoming inbox deliveries");
        var actorFilter = new Option<string?>("--actor", "Filter by target actor username");
        var maxCount = new Option<int?>("--max", "Maximum deliveries to show");
        monitorCommand.AddOption(actorFilter);
        monitorCommand.AddOption(maxCount);
        monitorCommand.SetHandler((InvocationContext ctx) =>
        {
            var actor = ctx.ParseResult.GetValueForOption(actorFilter);
            var max = ctx.ParseResult.GetValueForOption(maxCount) ?? 50;
            Console.WriteLine($"Monitoring inbox{(actor != null ? $" for '{actor}'" : "")} (max {max})...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to monitor inbox.");
            return Task.CompletedTask;
        });

        var pendingCommand = new Command("pending", "Show pending shared inbox deliveries");
        var limitOption = new Option<int?>("--limit", "Maximum pending deliveries to display");
        pendingCommand.AddOption(limitOption);
        pendingCommand.SetHandler((InvocationContext ctx) =>
        {
            var limit = ctx.ParseResult.GetValueForOption(limitOption) ?? 50;
            Console.WriteLine($"Showing up to {limit} pending shared inbox deliveries...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to show pending deliveries.");
            return Task.CompletedTask;
        });

        var statsCommand = new Command("stats", "Show inbox delivery statistics");
        statsCommand.SetHandler((InvocationContext ctx) =>
        {
            Console.WriteLine("Fetching inbox delivery statistics...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to view statistics.");
            return Task.CompletedTask;
        });

        inboxCommand.AddCommand(monitorCommand);
        inboxCommand.AddCommand(pendingCommand);
        inboxCommand.AddCommand(statsCommand);

        return inboxCommand;
    }
}
