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
    public AuctionHub(AuctionService auctions) => _auctions = auctions;

    private static string Group(int roomId) => $"room-{roomId}";

    public Task JoinRoom(int roomId) => Groups.AddToGroupAsync(Context.ConnectionId, Group(roomId));
    public Task LeaveRoom(int roomId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(roomId));

    /// <summary>Сообщить комнате, что данные изменились (кто-то поставил ставку и т.п.).</summary>
    public Task NotifyChanged(int roomId) => Clients.Group(Group(roomId)).SendAsync("DataChanged");

    /// <summary>
    /// Хост запускает колесо. Сервер считает результат, СРАЗУ фиксирует итог в БД
    /// (начисляет ОТ, открывает следующий аукцион, выдаёт квесты) и рассылает всем единый план
    /// для анимации. Фиксация на сервере — чтобы итог не зависел от того, доживёт ли клиент
    /// до конца анимации.
    /// </summary>
    public async Task StartSpin(int roomId, int auctionId, int spinSeconds)
    {
        var segs = await _auctions.BuildSegmentsAsync(auctionId);
        if (segs.Count < 2) return; // крутить можно минимум при двух ставках

        // нельзя крутить, пока есть неотвеченные заявки на одобрение квестов
        if (await _auctions.HasPendingApprovalsForAuctionAsync(auctionId))
        {
            await Clients.Caller.SendAsync("SpinBlocked", "Есть квесты, ожидающие одобрения игроками. Дождитесь голосов.");
            return;
        }

        var result = await _auctions.ComputeWheelAsync(auctionId);
        var byId = segs.ToDictionary(s => s.EntryId);

        // На колесе размер сектора ОБРАТНО пропорционален очкам (игра на выбывание):
        // больше очков -> меньше сектор -> меньше шанс быть выбитым.
        var planSegs = segs.Select((s, i) => new SpinSegment(
            s.EntryId, s.AnimeTitle, s.OwnerName,
            ForikAuction.Game.WheelEngine.EliminationWeight(s.Weight),
            Palette[i % Palette.Length])).ToArray();

        var steps = result.Steps.Select(st =>
            new SpinStep(st.EliminatedEntryId, st.RemainingBefore.ToArray(), st.FinalAngleDeg)).ToArray();

        var winner = byId[result.WinnerEntryId];
        var plan = new SpinPlan(
            auctionId, result.WinnerEntryId, winner.AnimeTitle, winner.OwnerName,
            Math.Clamp(spinSeconds, 2, 15), planSegs, steps);

        // Фиксируем итог на сервере ДО анимации — надёжно.
        await _auctions.FinishAuctionAsync(auctionId, result.WinnerEntryId);

        await Clients.Group(Group(roomId)).SendAsync("SpinStarted", plan);
    }
}
