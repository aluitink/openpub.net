using System.CommandLine;
using System.CommandLine.Invocation;
using ActivityPub.Cli.Commands;

var rootCommand = new RootCommand("ActivityPub CLI - Command-line administration tool");

var actorCommand = ActorCommand.Create();
var activityCommand = ActivityCommand.Create();
var inboxCommand = InboxCommand.Create();
var webhookCommand = WebhookCommand.Create();

rootCommand.AddCommand(actorCommand);
rootCommand.AddCommand(activityCommand);
rootCommand.AddCommand(inboxCommand);
rootCommand.AddCommand(webhookCommand);

return await rootCommand.InvokeAsync(args);
