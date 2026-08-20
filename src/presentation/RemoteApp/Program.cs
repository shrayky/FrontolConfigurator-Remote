using Capture;
using CommanderExchange;
using RemoteApp;
using Serilog;

if (args.Contains("--help"))
{
    Console.WriteLine("Использование:");
    Console.WriteLine("--service - запуск как дочерний процесс host-службы");
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
    Log.Information("Старт fc-remote");

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
catch (Exception ex)
{
    Log.Fatal(ex, "fc-remote аварийно завершился");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
