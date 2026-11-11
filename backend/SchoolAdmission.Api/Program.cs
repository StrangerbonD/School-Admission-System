using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Background;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Endpoints;
using SchoolAdmission.Api.Middleware;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var holdDurationConfig = builder.Configuration["HOLD_DURATION_SECONDS"] ?? "600";
if (!int.TryParse(holdDurationConfig, out var holdDurationSeconds) || holdDurationSeconds <= 0)
{
    var startupLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
    startupLogger.LogCritical("Invalid HOLD_DURATION_SECONDS: '{Value}'. Must be a positive integer.", holdDurationConfig);
    throw new InvalidOperationException($"Invalid HOLD_DURATION_SECONDS: '{holdDurationConfig}'. Must be a positive integer.");
}

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Database=school_admission;Username=postgres;Password=postgres;Maximum Pool Size=200;Command Timeout=30;Max Auto Prepare=20;Auto Prepare Min Usages=2";

builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new UtcDateTimeConverter());
});

if (builder.Configuration.GetValue<bool>("ENABLE_EF_SQL_LOGGING"))
{
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
}

var corsOriginsConfig = builder.Configuration["CORS_ORIGINS"] ?? "http://localhost:3000,http://127.0.0.1:3000";
var allowedOrigins = corsOriginsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithHeaders("X-Student-Id", "Content-Type")
              .WithMethods("GET", "POST", "DELETE", "OPTIONS");
    });
});

builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IHoldService, HoldService>();
builder.Services.AddHostedService<ExpiredHoldCleaner>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
await ApplyStartupMigrationsAsync(connectionString, logger);

app.MapGet("/", () => "Hello World!");
app.MapClassesEndpoints();
app.MapHoldsEndpoints();

app.Run();

static async Task ApplyStartupMigrationsAsync(string connectionString, ILogger logger, CancellationToken cancellationToken = default)
{
    const int maxRetries = 30;
    const int delaySeconds = 2;
    const long advisoryLockKey = 727274L;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await using var lockConn = new NpgsqlConnection(connectionString);
            try
            {
                await lockConn.OpenAsync(cancellationToken);
            }
            catch (PostgresException pgEx) when (pgEx.SqlState == "3D000")
            {
                await EnsureDatabaseExistsAsync(connectionString, logger, cancellationToken);
                await lockConn.OpenAsync(cancellationToken);
            }

            await using var pidCmd1 = new NpgsqlCommand("SELECT pg_backend_pid()", lockConn);
            var lockPid = Convert.ToInt32(await pidCmd1.ExecuteScalarAsync(cancellationToken));

            await using (var lockCmd = new NpgsqlCommand($"SELECT pg_advisory_lock({advisoryLockKey})", lockConn))
            {
                await lockCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            logger.LogInformation("Advisory lock acquired on session with backend PID: {Pid}", lockPid);

            try
            {
                var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(lockConn).Options;
                await using var migrationDb = new AppDbContext(options);

                var migrationPid = await migrationDb.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(cancellationToken);
                logger.LogInformation("Migration running on DbContext with backend PID: {Pid}", migrationPid);

                if (lockPid != migrationPid)
                {
                    throw new InvalidOperationException($"Backend PID mismatch! Lock PID: {lockPid}, Migration PID: {migrationPid}");
                }

                await migrationDb.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migration completed successfully on PID: {Pid}", migrationPid);
            }
            finally
            {
                await using var unlockCmd = new NpgsqlCommand($"SELECT pg_advisory_unlock({advisoryLockKey})", lockConn);
                await unlockCmd.ExecuteNonQueryAsync(cancellationToken);
                logger.LogInformation("Advisory lock released on backend PID: {Pid}", lockPid);
            }

            return;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning("Database connection attempt {Attempt}/{MaxRetries} failed: {Message}. Retrying in {Delay}s...", attempt, maxRetries, ex.Message, delaySeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }
    }
}

static async Task EnsureDatabaseExistsAsync(string connectionString, ILogger logger, CancellationToken cancellationToken)
{
    var csb = new NpgsqlConnectionStringBuilder(connectionString);
    var targetDb = csb.Database;
    if (string.IsNullOrWhiteSpace(targetDb) || string.Equals(targetDb, "postgres", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var adminCsb = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Database = "postgres",
        Pooling = false
    };

    await using var adminConn = new NpgsqlConnection(adminCsb.ConnectionString);
    await adminConn.OpenAsync(cancellationToken);

    await using var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = $1", adminConn);
    checkCmd.Parameters.AddWithValue(targetDb);
    var exists = await checkCmd.ExecuteScalarAsync(cancellationToken);

    if (exists == null)
    {
        var safeDbName = targetDb.Replace("\"", "\"\"");
        try
        {
            await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{safeDbName}\"", adminConn);
            await createCmd.ExecuteNonQueryAsync(cancellationToken);
            logger.LogInformation("Created database '{Database}'.", targetDb);
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "42P04" || pgEx.SqlState == "23505")
        {
        }
    }
}

public partial class Program { }
