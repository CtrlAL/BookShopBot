namespace Fsm.Session;

/// <summary>
/// Cache options for cached session repositories. Defaults target a long
/// marketplace workflow: sessions stay alive while active (sliding 20 min)
/// with a hard upper bound of 2 hours.
/// </summary>
public sealed class SessionCacheOptions
{
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(20);

    public TimeSpan AbsoluteExpiration { get; set; } = TimeSpan.FromHours(2);
}