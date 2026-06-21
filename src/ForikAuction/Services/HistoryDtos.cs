namespace ForikAuction.Services;

public sealed record AuctionHistoryItem(int Number, string WinnerAnime, string WinnerOwner, DateTime? FinishedUtc);
public sealed record WinStat(string Player, int Wins);
