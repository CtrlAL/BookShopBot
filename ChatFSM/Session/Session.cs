using System.Text.Json.Serialization;

namespace ChatFSM.Session;

public class Session<TState>
    where TState : Enum
{
    public long ChatId { get; set; }
    public TState CurrentState { get; set; }
    public TState PreviousState { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int? PagedMessageId { get; set; }

    [JsonIgnore]
    public CancellationTokenSource ActionCts { get; set; }
}