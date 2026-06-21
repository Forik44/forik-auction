namespace ForikAuction.Data.Entities;

/// <summary>Ставка: аниме + сколько очков игрок на него поставил.</summary>
public class AuctionEntry
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public int RoomMemberId { get; set; }
    public RoomMember RoomMember { get; set; } = null!;

    public string AnimeTitle { get; set; } = "";
    public int Points { get; set; }
}
