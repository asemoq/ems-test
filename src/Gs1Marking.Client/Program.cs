using Gs1Marking.Client.Application;
using Gs1Marking.Client.Infrastructure;
using Gs1Marking.Client.Options;
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
    if (key is not ("--host" or "--port" or "--output" or "--reconnect-ms" or "--help" or "-h"))
    {
        Console.Error.WriteLine($"Unknown argument: '{arg}'.");
        return;
    }
}

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    ["--host"] = $"{ClientOptions.SectionName}:Host",
    ["--port"] = $"{ClientOptions.SectionName}:Port",
    ["--output"] = $"{ClientOptions.SectionName}:OutputPath",
    ["--reconnect-ms"] = $"{ClientOptions.SectionName}:ReconnectDelayMs",
});

builder.Services
    .AddOptions<ClientOptions>()
    .BindConfiguration(ClientOptions.SectionName)
    .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Host must not be empty.")
    .Validate(o => o.Port is >= 1 and <= 65535, "Port must be in the range 1..65535.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.OutputPath), "OutputPath must not be empty.")
    .Validate(o => o.ReconnectDelayMs > 0, "ReconnectDelayMs must be positive.")
    .ValidateOnStart();

builder.Services.AddSingleton<IMarkingCodeWriter>(sp =>
{
    var options = sp.GetRequiredService<IOptions<ClientOptions>>().Value;
    return new MarkingCodeFileWriter(options.OutputPath);
});

builder.Services.AddHostedService<MarkingCodeClient>();

using var host = builder.Build();

var options = host.Services.GetRequiredService<IOptions<ClientOptions>>().Value;

Console.WriteLine($"Starting GS1 marking code client. Connecting to {options.Host}:{options.Port}.");
Console.WriteLine($"Codes will be appended to '{options.OutputPath}'.");
Console.WriteLine("Press Ctrl+C to stop.");

await host.RunAsync();

Console.WriteLine("Client stopped.");

static void PrintHelp()
{
    Console.WriteLine(
        """
        GS1 Marking Code Client

        Usage:
          Gs1Marking.Client [options]

        Options:
          --host <host>        Server host name or IP address (default: 127.0.0.1).
          --port <n>           Server TCP port (default: 5000).
          --output <path>      File to append received codes to (default: marking-codes.txt).
          --reconnect-ms <n>   Delay between reconnect attempts in milliseconds (default: 2000).
          --help, -h           Show this help.
        """);
}
