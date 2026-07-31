using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Startup;

DispatcherBanner.Print();

// p0391a: aborting the startup is never justifiable. A dead process reports nothing; a
// degraded one reports exactly what is broken, and the operator needs the second. Every
// dependency is composed behind a probe inside the factory, so this file has no failure
// path left of its own — the host either binds its port or there was never a channel to
// report through.
var app = await ServerHostFactory.CreateAsync(args);
app.Run();

public partial class Program;
