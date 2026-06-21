namespace ForikAuction.Data.Entities;

/// <summary>Прокачанный талант игрока в комнате. Code — из TalentCatalog.</summary>
public class UserTalent
{
    public int Id { get; set; }
    public int RoomMemberId { get; set; }
    public RoomMember RoomMember { get; set; } = null!;
    public string Code { get; set; } = "";
    public int Level { get; set; }
}
