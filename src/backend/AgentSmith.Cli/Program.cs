using System.CommandLine.Parsing;
using AgentSmith.Cli;
using AgentSmith.Cli.Commands;

Banner.Print();

return await CliRootCommand.BuildParser().InvokeAsync(args);
