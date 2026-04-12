using AgentSmith.Dispatcher.Services;
using AgentSmith.Dispatcher.Extensions;

DispatcherBanner.Print();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

builder.Services
    .AddRedis()
    .AddCoreDispatcherServices()
    .AddSlackAdapter()
    .AddIntentHandlers();

await builder.Services.AddJobSpawnerAsync(
    LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup"));

var app = builder.Build();

app.MapHealthEndpoints()
   .MapSlackEndpoints();

app.Run();
