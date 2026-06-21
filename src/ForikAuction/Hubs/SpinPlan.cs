namespace ForikAuction.Hubs;

public sealed record SpinSegment(int EntryId, string Anime, string Owner, double Weight, string Color);

public sealed record SpinStep(int EliminatedEntryId, int[] RemainingBefore, double FinalAngleDeg);

/// <summary>Полный план прокрутки, который сервер рассылает всем клиентам комнаты для
/// синхронной анимации. Все клиенты видят одно и то же — победитель и углы заранее посчитаны.</summary>
public sealed record SpinPlan(
    int AuctionId,
    int WinnerEntryId,
    string WinnerAnime,
    string WinnerOwner,
    int SpinSeconds,
    SpinSegment[] Segments,
    SpinStep[] Steps);
