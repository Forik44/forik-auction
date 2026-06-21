namespace ForikAuction.Data.Entities;

/// <summary>Пользователь сайта (создаётся при первом входе через Google).</summary>
public class AppUser
{
    public int Id { get; set; }
    public string GoogleSubject { get; set; } = "";   // стабильный id от Google (claim "sub")
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<RoomMember> Memberships { get; set; } = new();
}
