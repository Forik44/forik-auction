namespace ForikAuction.Data.Entities;

public enum AuctionStatus { Open = 0, Spinning = 1, Finished = 2 }

/// <summary>Один аукцион внутри комнаты.</summary>
public class Auction
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int Number { get; set; }
    public AuctionStatus Status { get; set; } = AuctionStatus.Open;

    public int? WinnerEntryId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedUtc { get; set; }

    public List<AuctionEntry> Entries { get; set; } = new();
}
