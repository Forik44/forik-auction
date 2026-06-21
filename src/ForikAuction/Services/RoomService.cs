using ForikAuction.Data;
using ForikAuction.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForikAuction.Services;

public class RoomService
{
    private readonly AppDbContext _db;
    private readonly AuctionService _auctions;
    public RoomService(AppDbContext db, AuctionService auctions) { _db = db; _auctions = auctions; }

    public async Task<Room> CreateRoomAsync(int userId, string name, string password)
    {
        var (hash, salt) = PasswordHasher.Hash(password);
        string code;
        do { code = CodeGenerator.NewJoinCode(); }
        while (await _db.Rooms.AnyAsync(r => r.JoinCode == code));

        var room = new Room
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Аукцион" : name.Trim(),
            JoinCode = code,
            PasswordHash = hash,
            PasswordSalt = salt,
            OwnerId = userId,
            CurrentAuctionNumber = 1,
        };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();

        var member = await AddMemberAsync(room.Id, userId);

        // первый аукцион + первый набор квестов на след. аукцион
        var auction = new Auction { RoomId = room.Id, Number = 1, Status = AuctionStatus.Open };
        _db.Auctions.Add(auction);
        await _db.SaveChangesAsync();
        await _auctions.WriteLedgerForOpenAuctionAsync(member, 1);
        await _auctions.DrawQuestsAsync(member, 2);
        await _db.SaveChangesAsync();
        return room;
    }

    public async Task<(bool ok, string message, Room? room)> JoinRoomAsync(int userId, string joinCode, string password)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.JoinCode == joinCode.Trim().ToUpperInvariant());
        if (room is null) return (false, "Комната не найдена.", null);
        if (!PasswordHasher.Verify(password, room.PasswordHash, room.PasswordSalt))
            return (false, "Неверный пароль.", null);

        var existing = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == room.Id && m.UserId == userId);
        if (existing is null)
        {
            var member = await AddMemberAsync(room.Id, userId);
            await _auctions.WriteLedgerForOpenAuctionAsync(member, room.CurrentAuctionNumber);
            await _auctions.DrawQuestsAsync(member, room.CurrentAuctionNumber + 1);
            await _db.SaveChangesAsync();
        }
        return (true, "Готово.", room);
    }

    private async Task<RoomMember> AddMemberAsync(int roomId, int userId)
    {
        var member = new RoomMember { RoomId = roomId, UserId = userId, TalentPoints = 0 };
        _db.RoomMembers.Add(member);
        await _db.SaveChangesAsync();
        return member;
    }

    public Task<List<Room>> MyRoomsAsync(int userId) =>
        _db.Rooms.Where(r => r.Members.Any(m => m.UserId == userId))
                 .OrderByDescending(r => r.CreatedUtc).ToListAsync();
}
