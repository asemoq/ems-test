using Gs1Marking.Server.Application;
using Gs1Marking.Server.Infrastructure;
using Gs1Marking.Server.Options;
using Gs1Marking.Contract.Gs1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return;
}

foreach (var arg in args)
{
    if (!arg.StartsWith('-'))
        continue;

    var key = arg.Split('=', 2)[0];
    if (key is not ("--port" or "--bind" or "--interval-ms" or "--help" or "-h"))
    {
        Console.Error.WriteLine($"Unknown argument: '{arg}'.");
        return;
    }
}

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    ["--port"] = $"{ServerOptions.SectionName}:Port",
    ["--bind"] = $"{ServerOptions.SectionName}:BindAddress",
    ["--interval-ms"] = $"{MarkingOptions.SectionName}:GenerationIntervalMs",
});

builder.Services
    .AddOptions<ServerOptions>()
    .BindConfiguration(ServerOptions.SectionName)
    .Validate(o => o.Port is >= 1 and <= 65535, "Port must be in the range 1..65535.")
    .Validate(o => System.Net.IPAddress.TryParse(o.BindAddress, out _), "BindAddress must be a valid IP address.")
    .ValidateOnStart();

builder.Services
    .AddOptions<MarkingOptions>()
    .BindConfiguration(MarkingOptions.SectionName)
    .Validate(o => o.GenerationIntervalMs > 0, "GenerationIntervalMs must be positive.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IGs1CodeGenerator, Gs1CodeGenerator>();

builder.Services.AddSingleton<MarkingCodeBroadcaster>();
builder.Services.AddSingleton<IMarkingCodeBroadcaster>(sp => sp.GetRequiredService<MarkingCodeBroadcaster>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MarkingCodeBroadcaster>());

builder.Services.AddSingleton<IClientConnectionFactory, ClientConnectionFactory>();
builder.Services.AddSingleton<MarkingCodeServer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MarkingCodeServer>());

using var host = builder.Build();

var serverOptions = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var markingOptions = host.Services.GetRequiredService<IOptions<MarkingOptions>>().Value;

Console.WriteLine($"Starting GS1 marking code server on {serverOptions.BindAddress}:{serverOptions.Port} (interval {markingOptions.GenerationIntervalMs} ms)...");
Console.WriteLine("Press Ctrl+C to stop.");

await host.RunAsync();

Console.WriteLine("Server stopped.");

static void PrintHelp()
{
    Console.WriteLine(
        """
        GS1 Marking Code Server

        Usage:
          Gs1Marking.Server [options]

        Options:
          --port <n>         TCP port to listen on (default: 5000).
          --interval-ms <n>  Code generation interval in milliseconds (default: 500).
          --bind <address>   IP address to bind to (default: 127.0.0.1).
          --help, -h         Show this help.
        """);
}
