using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Startup;
using DemoSeed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Seeds the docs demo story into the database CONFIG_PATH's bootstrap names. Run by
// tools/screenshots/run.sh after `database migrate` and `config import`, before the server starts:
//
//   dotnet tools/demo-seed/bin/Debug/net10.0/DemoSeed.dll <demo-root>
//
// Every name is invented (acme, shop, Sam Sample); no value is a real credential.

if (args.Length != 1) { Console.Error.WriteLine("usage: DemoSeed <demo-root>"); return 2; }
var demoRoot = Path.GetFullPath(args[0]);
var ct = CancellationToken.None;
var clock = new StoryClock();
var app = await ServerHostFactory.CreateAsync([], s =>
{
    s.RemoveAll<TimeProvider>();
    s.AddSingleton<TimeProvider>(clock);
});
var services = app.Services;

using (var scope = services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>();
    if (await db.Runs.AnyAsync(r => r.Project == "shop" && r.TicketId == "41", ct))
    {
        Console.WriteLine("demo story already seeded — nothing to do");
        return 0;
    }
}

DemoRepos.Create(demoRoot);
await Story.BranchesAsync(services, demoRoot, ct);
await Story.RunsAsync(services.GetRequiredService<RunDbProjector>(), ct);
await Story.RecordsAsync(services, ct);
await Story.ConversationsAsync(services, clock, ct);
Console.WriteLine("demo story seeded");
return 0;
