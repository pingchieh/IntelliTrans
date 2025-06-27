using ConsoleAppFramework;
using IntelliTrans.Cli;
using IntelliTrans.Cli.Commands;
using IntelliTrans.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var app = Host.CreateDefaultBuilder(args)
    .ConfigureServices(
        (builder, service) =>
        {
            string dbType = builder.Configuration.GetValue("DBType", DbType.Sqlite.Name);

            service.AddDbContext<IntelliSenseDbContext>(options =>
            {
                if (dbType == DbType.Sqlite.Name)
                {
                    options.UseSqlite(
                        builder.Configuration.GetConnectionString(DbType.Sqlite.Name)!,
                        x => x.MigrationsAssembly(DbType.Sqlite.Assembly)
                    );
                }
                if (dbType == DbType.Postgres.Name)
                {
                    options.UseNpgsql(
                        builder.Configuration.GetConnectionString(DbType.Postgres.Name)!,
                        x => x.MigrationsAssembly(DbType.Postgres.Assembly)
                    );
                }
            });
        }
    )
    .ToConsoleAppBuilder();

app.Add<MainCommands>();

app.Run(args);
