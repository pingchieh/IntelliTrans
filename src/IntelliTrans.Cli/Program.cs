using ConsoleAppFramework;
using IntelliTrans.Cli;
using IntelliTrans.Cli.Commands;
using Microsoft.Extensions.Hosting;

Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

var host = Host.CreateApplicationBuilder(args);
host.ConfigureDatabase();
host.ConfigureOpenTelemetry();

var app = host.ToConsoleAppBuilder();

app.Add<MainCommands>();

app.Run(args);
