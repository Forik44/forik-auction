namespace ForikAuction.Data.Entities;

/// <summary>Состояние квеста: открыт → отправлен на одобрение → одобрен/отклонён.</summary>
public enum QuestApproval { Open = 0, Pending = 1, Approved = 2, Rejected = 3 }

/// <summary>Выданный игроку квест на конкретный аукцион. QuestId — из QuestCatalog.</summary>
public class RoomQuest
{
    public int Id { get; set; }
    public int RoomMemberId { get; set; }
    public RoomMember RoomMember { get; set; } = null!;
    public int ForAuctionNumber { get; set; }
    public int QuestId { get; set; }

    /// <summary>Статус одобрения. Награда засчитывается только при Approved.</summary>
    public QuestApproval Status { get; set; } = QuestApproval.Open;

    /// <summary>Награда уже начислена в стартовые очки следующего аукциона.</summary>
    public bool Claimed { get; set; }

    public List<QuestApprovalVote> Votes { get; set; } = new();
}
