using IntelliTrans.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MainCommands> _logger;
    private readonly IConfiguration _configuration;

    public MainCommands(
        IServiceScopeFactory scopeFactory,
        ILogger<MainCommands> logger,
        IConfiguration configuration
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// 数据库迁移
    /// </summary>
    public async Task Migrate(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database migration...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IntelliSenseDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during database migration.");
            throw;
        }
    }
}
