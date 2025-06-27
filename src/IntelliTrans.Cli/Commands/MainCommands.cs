using IntelliTrans.Database;
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
}
