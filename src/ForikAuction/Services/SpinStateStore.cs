using System.Collections.Concurrent;
using ForikAuction.Hubs;

namespace ForikAuction.Services;

/// <summary>Активная прокрутка колеса (в памяти процесса). Пока она тут — аукцион ещё НЕ завершён,
/// поэтому обновление страницы не спойлерит победителя, а возобновляет анимацию.</summary>
public sealed class SpinState
{
    public required SpinPlan Plan { get; init; }
    public DateTime StartUtc { get; init; }
    public int TotalMs { get; init; }
}

public sealed class SpinStateStore
{
    private readonly ConcurrentDictionary<int, SpinState> _map = new();

    public bool TryGet(int auctionId, out SpinState state) => _map.TryGetValue(auctionId, out state!);
    public void Set(int auctionId, SpinState state) => _map[auctionId] = state;
    public bool TryRemove(int auctionId, out SpinState state) => _map.TryRemove(auctionId, out state!);
}
