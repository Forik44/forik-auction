namespace ForikAuction.Data.Entities;

/// <summary>Участие игрока в комнате: его очки таланта и история.</summary>
public class RoomMember
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Накопленные очки таланта (ОТ) — тратятся в дереве талантов.</summary>
    public int TalentPoints { get; set; } = 0;

    /// <summary>Итог прошлого аукциона для этого игрока.</summary>
    public bool WonLast { get; set; }
    public bool LostLast { get; set; }

    public DateTime JoinedUtc { get; set; } = DateTime.UtcNow;

    public List<UserTalent> Talents { get; set; } = new();
    public List<RoomQuest> Quests { get; set; } = new();
    public List<AuctionEntry> Entries { get; set; } = new();
}
