using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Fsm.Session;

/// <summary>
/// Background service that periodically deletes expired session rows
/// from the PostgreSQL session table. Uses <see cref="PeriodicTimer"/>
/// with the interval configured in <see cref="SessionStorageOptions.CleanupInterval"/>.
/// Exceptions during a tick are logged and the next tick retries.
/// </summary>
public sealed class ChatSessionCleanupService : BackgroundService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly SessionStorageOptions _options;
    private readonly ILogger<ChatSessionCleanupService> _logger;

    public ChatSessionCleanupService(
        NpgsqlDataSource dataSource,
        IOptions<SessionStorageOptions> options,
        ILogger<ChatSessionCleanupService> logger)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                string sql = BuildDeleteSql(_options);
                await using NpgsqlCommand command = _dataSource.CreateCommand(sql);
                int deleted = await command.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogDebug("Chat session cleanup deleted {Count} expired row(s).", deleted);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat session cleanup failed; will retry next tick.");
            }
        }
    }

    /// <summary>
    /// Builds the DELETE statement for expired rows.
    /// Exposed as a pure function so SQL text can be verified in tests
    /// without a live database connection.
    /// </summary>
    public static string BuildDeleteSql(SessionStorageOptions options)
    {
        return $"""
            DELETE FROM {options.Schema}.{options.Table}
            WHERE expires_at < now()
            """;
    }
}
