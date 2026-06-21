namespace ForikAuction.Data.Entities;

/// <summary>Выданный игроку квест на конкретный аукцион. QuestId — из QuestCatalog.</summary>
public class RoomQuest
{
    public int Id { get; set; }
    public int RoomMemberId { get; set; }
    public RoomMember RoomMember { get; set; } = null!;
    public int ForAuctionNumber { get; set; }
    public int QuestId { get; set; }
    public bool Completed { get; set; }
    /// <summary>Награда уже начислена в стартовые очки следующего аукциона.</summary>
    public bool Claimed { get; set; }
}
