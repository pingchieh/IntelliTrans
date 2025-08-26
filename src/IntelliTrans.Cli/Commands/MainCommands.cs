using IntelliTrans.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    private readonly IntelliSenseDbContext _dbContext;
    private readonly ILogger<MainCommands> _logger;
    private readonly IConfiguration _configuration;

    public MainCommands(
        IntelliSenseDbContext dbContext,
        ILogger<MainCommands> logger,
        IConfiguration configuration
    )
    {
        _dbContext = dbContext;
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
            await _dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during database migration.");
            throw;
        }
    }
}
