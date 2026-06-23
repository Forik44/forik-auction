using ForikAuction.Services;
using Microsoft.AspNetCore.SignalR;

namespace ForikAuction.Hubs;

/// <summary>Реал-тайм синхронизация комнаты: ставки, запуск колеса, итог.</summary>
public class AuctionHub : Hub
{
    private static readonly string[] Palette =
    {
        "#ef4444","#f59e0b","#10b981","#3b82f6","#8b5cf6","#ec4899",
        "#14b8a6","#f97316","#22c55e","#6366f1","#eab308","#06b6d4",
    };

    private readonly AuctionService _auctions;
    private readonly SpinStateStore _store;
    public AuctionHub(AuctionService auctions, SpinStateStore store) { _auctions = auctions; _store = store; }

    private static string Group(int roomId) => $"room-{roomId}";

    public Task JoinRoom(int roomId) => Groups.AddToGroupAsync(Context.ConnectionId, Group(roomId));
    public Task LeaveRoom(int roomId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(roomId));

    public Task NotifyChanged(int roomId) => Clients.Group(Group(roomId)).SendAsync("DataChanged");

    /// <summary>
    /// Хост запускает колесо. Сервер считает результат и кладёт план прокрутки в память (аукцион
    /// при этом ОСТАЁТСЯ открытым — поэтому обновление страницы не спойлерит победителя). Итог
    /// фиксируется позже, по завершении анимации (FinalizeSpin) или лениво при заходе на страницу.
    /// </summary>
    public async Task StartSpin(int roomId, int auctionId, int spinSeconds)
    {
        if (_store.TryGet(auctionId, out _)) return; // уже крутится

        var segs = await _auctions.BuildSegmentsAsync(auctionId);
        if (segs.Count < 2) return;

        if (await _auctions.HasPendingApprovalsForAuctionAsync(auctionId))
        {
            await Clients.Caller.SendAsync("SpinBlocked", "Есть квесты, ожидающие одобрения игроками. Дождитесь голосов.");
            return;
        }

        if ((await _auctions.OverBudgetEntriesAsync(auctionId)).Count > 0)
        {
            await Clients.Caller.SendAsync("SpinBlocked", "Кому-то не хватает очков на перенесённые ставки (затенённые). Уберите лишние ставки.");
            return;
        }

        var result = await _auctions.ComputeWheelAsync(auctionId);
        var byId = segs.ToDictionary(s => s.EntryId);

        // размер сектора ОБРАТНО пропорционален очкам (игра на выбывание)
        var planSegs = segs.Select((s, i) => new SpinSegment(
            s.EntryId, s.AnimeTitle, s.OwnerName,
            ForikAuction.Game.WheelEngine.EliminationWeight(s.Weight),
            Palette[i % Palette.Length])).ToArray();

        var steps = result.Steps.Select(st =>
            new SpinStep(st.EliminatedEntryId, st.RemainingBefore.ToArray(), st.FinalAngleDeg)).ToArray();

        var winner = byId[result.WinnerEntryId];
        int sec = Math.Clamp(spinSeconds, 2, 15);
        var plan = new SpinPlan(auctionId, result.WinnerEntryId, winner.AnimeTitle, winner.OwnerName, sec, planSegs, steps);

        // ожидаемая длительность всей анимации (для ленивого завершения брошенной прокрутки)
        int total = (segs.Count - 1) * (sec * 1000 + 2100) + 2500;
        _store.Set(auctionId, new SpinState { Plan = plan, StartUtc = DateTime.UtcNow, TotalMs = total });

        await Clients.Group(Group(roomId)).SendAsync("SpinStarted", plan);
    }

    /// <summary>Вызывается клиентом по завершении анимации — один раз фиксирует итог.</summary>
    public async Task FinalizeSpin(int roomId, int auctionId)
    {
        if (!_store.TryRemove(auctionId, out var st)) return; // уже завершено кем-то
        await _auctions.FinishAuctionAsync(auctionId, st.Plan.WinnerEntryId);
        await Clients.Group(Group(roomId)).SendAsync("DataChanged");
    }
}
