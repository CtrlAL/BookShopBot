using System.Text.Json.Serialization;

namespace Fsm.Abstractions;

public class Session<TState>
{
    public long ChatId { get; set; }
    public TState CurrentState { get; set; }
    public TState PreviousState { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int? PagedMessageId { get; set; }

    [JsonIgnore]
    public CancellationTokenSource ActionCts { get; set; }
}