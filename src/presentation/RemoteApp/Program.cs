using Capture;
using CommanderExchange;
using RemoteApp;
using RemoteApp.Services;
using RemoteApp.Workers;
using Serilog;
using System.Diagnostics;

if (args.Contains("--help"))
{
    Console.WriteLine("Использование:");
    Console.WriteLine("--service - запуск как дочерний процесс host-службы");
    Console.WriteLine("--desktop - процесс на столе кассира (поднимает сторож в Session 0)");
    Console.WriteLine("--help - эта справка");
    return;
}

if (args.Length > 0 && !args.Contains("--service"))
    return;

var logFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "Automation",
    "fc-remote",
    "log");

Directory.CreateDirectory(logFolder);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logFolder, "fc-remote-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var sessionId = Process.GetCurrentProcess().SessionId;
    Log.Information("Старт fc-remote. SessionId: {SessionId}", sessionId);

    if (ProcessRole.IsWatchdog(args, sessionId))
    {
        await RunWatchdogAsync(args);
        return;
    }

    await RunDesktopWorkerAsync(args);
}
catch (Exception ex)
{
    Log.Fatal(ex, "fc-remote аварийно завершился");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task RunWatchdogAsync(string[] args)
{
    Log.Information("Режим сторожа Session 0");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();
    builder.Services.AddSingleton<IWindowsSessionApi, WindowsSessionApi>();
    builder.Services.AddSingleton<DesktopWatchdog>();
    builder.Services.AddHostedService<DesktopWatchdogWorker>();

    if (OperatingSystem.IsWindows())
        builder.Services.AddWindowsService();

    var host = builder.Build();
    await host.RunAsync();
}

static async Task RunDesktopWorkerAsync(string[] args)
{
    var settings = AgentConfigLoader.Load();

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();
    builder.Services.AddSingleton(settings);
    builder.Services.AddScreenCapture();
    builder.Services.AddCommanderExchange();

    if (OperatingSystem.IsWindows())
        builder.Services.AddWindowsService();

    var host = builder.Build();
    await host.RunAsync();
}
