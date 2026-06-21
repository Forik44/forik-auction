namespace ForikAuction.Data.Entities;

/// <summary>Бессрочная комната. Заходят по коду + паролю.</summary>
public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string JoinCode { get; set; } = "";        // короткий код для приглашения
    public string PasswordHash { get; set; } = "";     // PBKDF2
    public string PasswordSalt { get; set; } = "";
    public int OwnerId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Номер текущего (открытого) аукциона; растёт после каждого розыгрыша.</summary>
    public int CurrentAuctionNumber { get; set; } = 1;

    public List<RoomMember> Members { get; set; } = new();
    public List<Auction> Auctions { get; set; } = new();
}
