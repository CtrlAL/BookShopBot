namespace Fsm.Session;

/// <summary>
/// Options for the PostgreSQL session storage layer.
/// Configures schema/table names and row lifetime for the
/// <see cref="PostgresChatSessionRepository{TSession}"/> and
/// <see cref="ChatSessionCleanupService"/>.
/// </summary>
public sealed class SessionStorageOptions
{
    public string Schema { get; set; } = "chatfsm";

    public string Table { get; set; } = "chat_session";

    public TimeSpan BaseLifetime { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan LifetimeJitter { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
