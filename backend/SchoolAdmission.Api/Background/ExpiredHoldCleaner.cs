using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Background;

public class ExpiredHoldCleaner : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredHoldCleaner> _logger;
    private readonly TimeSpan _interval;

    public ExpiredHoldCleaner(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExpiredHoldCleaner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalConfig = configuration["CLEANER_INTERVAL_SECONDS"] ?? "5";
        if (!int.TryParse(intervalConfig, out var intervalSeconds) || intervalSeconds <= 0)
        {
            intervalSeconds = 5;
        }

        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredHoldCleaner started with interval {Interval}s.", _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE holds 
                    SET status = 'EXPIRED' 
                    WHERE status = 'HELD' AND expires_at <= clock_timestamp()", stoppingToken);

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("ExpiredHoldCleaner marked {Count} stale hold(s) as EXPIRED.", rowsAffected);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while cleaning expired holds.");
            }
        }

        _logger.LogInformation("ExpiredHoldCleaner stopped.");
    }
}
