namespace ForikAuction.Data.Entities;

/// <summary>
/// Запись-источник стартовых очков игрока на аукцион — основа всплывающей карточки
/// «почему у игрока столько очков». Сохраняется при открытии аукциона.
/// </summary>
public class PointsLedgerEntry
{
    public int Id { get; set; }
    public int RoomMemberId { get; set; }
    public int AuctionNumber { get; set; }
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public int Delta { get; set; }
}
