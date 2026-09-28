using System.CommandLine;
using AgentSmith.Cli;
using AgentSmith.Cli.Commands;

Banner.Print();

return await CliRootCommand.Create().InvokeAsync(args);
