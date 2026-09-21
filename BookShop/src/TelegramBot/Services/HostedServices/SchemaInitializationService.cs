using Fsm.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BookShop.TelegramBot.Services.HostedServices
{
    public sealed class SchemaInitializationService : IHostedService
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly IOptions<SessionStorageOptions> _options;
        private readonly ILogger<SchemaInitializationService> _logger;

        public SchemaInitializationService(
            NpgsqlDataSource dataSource,
            IOptions<SessionStorageOptions> options,
            ILogger<SchemaInitializationService> logger)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ChatSessionSchema.EnsureCreatedAsync(_dataSource, _options, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize chat session schema; continuing without it.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}