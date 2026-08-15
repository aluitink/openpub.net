using System.CommandLine;
using System.CommandLine.Invocation;

namespace ActivityPub.Cli.Commands;

public static class WebhookCommand
{
    public static Command Create()
    {
        var webhookCommand = new Command("webhook", "Manage webhook configurations and deliveries");

        var listCommand = new Command("list", "List webhook configurations");
        var actorFilter = new Option<string?>("--actor", "Filter by actor ID");
        var eventType = new Option<string?>("--event-type", "Filter by event type");
        listCommand.AddOption(actorFilter);
        listCommand.AddOption(eventType);
        listCommand.SetHandler((InvocationContext ctx) =>
        {
            var actor = ctx.ParseResult.GetValueForOption(actorFilter);
            var type = ctx.ParseResult.GetValueForOption(eventType);
            Console.WriteLine($"Listing webhooks{(actor != null ? $" for actor '{actor}'" : "")}{(type != null ? $" with event type '{type}'" : "")}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to list webhooks.");
            return Task.CompletedTask;
        });

        var pendingCommand = new Command("pending", "Show pending webhook deliveries");
        var limitOption = new Option<int?>("--limit", "Maximum pending deliveries to display");
        pendingCommand.AddOption(limitOption);
        pendingCommand.SetHandler((InvocationContext ctx) =>
        {
            var limit = ctx.ParseResult.GetValueForOption(limitOption) ?? 50;
            Console.WriteLine($"Showing up to {limit} pending webhook deliveries...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to show pending webhook deliveries.");
            return Task.CompletedTask;
        });

        var historyCommand = new Command("history", "Show webhook delivery history");
        var configId = new Option<int?>("--config-id", "Filter by webhook config ID");
        historyCommand.AddOption(configId);
        historyCommand.SetHandler((InvocationContext ctx) =>
        {
            var id = ctx.ParseResult.GetValueForOption(configId);
            Console.WriteLine($"Showing webhook delivery history{(id != null ? $" for config {id}" : "")}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to view delivery history.");
            return Task.CompletedTask;
        });

        var deleteCommand = new Command("delete", "Delete a webhook configuration");
        var deleteId = new Option<int>("--id", "Webhook config ID to delete");
        deleteCommand.AddOption(deleteId);
        deleteCommand.SetHandler((InvocationContext ctx) =>
        {
            var id = ctx.ParseResult.GetValueForOption(deleteId);
            Console.WriteLine($"Deleting webhook config {id}...");
            Console.WriteLine("NOTE: Connect to an ActivityPub server to delete webhook config.");
            return Task.CompletedTask;
        });

        webhookCommand.AddCommand(listCommand);
        webhookCommand.AddCommand(pendingCommand);
        webhookCommand.AddCommand(historyCommand);
        webhookCommand.AddCommand(deleteCommand);

        return webhookCommand;
    }
}
