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

    public async Task<(bool ok, string message)> LeaveRoomAsync(int userId, int roomId)
    {
        var room = await _db.Rooms.FindAsync(roomId);
        if (room is null) return (false, "Комната не найдена.");
        if (room.OwnerId == userId) return (false, "Владелец не может выйти из своей комнаты.");
        var member = await _db.RoomMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (member is null) return (false, "Вы не состоите в этой комнате.");
        await RemoveMemberAsync(member);
        return (true, "Вы вышли из комнаты.");
    }

    public async Task<(bool ok, string message)> KickAsync(int ownerUserId, int roomId, int targetMemberId)
    {
        var room = await _db.Rooms.FindAsync(roomId);
        if (room is null) return (false, "Комната не найдена.");
        if (room.OwnerId != ownerUserId) return (false, "Только владелец может удалять игроков.");
        var member = await _db.RoomMembers.FirstOrDefaultAsync(m => m.Id == targetMemberId && m.RoomId == roomId);
        if (member is null) return (false, "Игрок не найден.");
        if (member.UserId == room.OwnerId) return (false, "Нельзя удалить владельца комнаты.");
        await RemoveMemberAsync(member);
        return (true, "Игрок удалён из комнаты.");
    }

    /// <summary>Удаляет игрока и все его записи (ставки, квесты+голоса, таланты, разбивку очков).</summary>
    private async Task RemoveMemberAsync(RoomMember member)
    {
        await _db.QuestApprovalVotes.Where(v => v.VoterRoomMemberId == member.Id).ExecuteDeleteAsync();
        await _db.RoomQuests.Where(q => q.RoomMemberId == member.Id).ExecuteDeleteAsync();
        await _db.AuctionEntries.Where(e => e.RoomMemberId == member.Id).ExecuteDeleteAsync();
        await _db.UserTalents.Where(t => t.RoomMemberId == member.Id).ExecuteDeleteAsync();
        await _db.PointsLedger.Where(p => p.RoomMemberId == member.Id).ExecuteDeleteAsync();
        _db.RoomMembers.Remove(member);
        await _db.SaveChangesAsync();
    }

    public Task<List<Room>> MyRoomsAsync(int userId) =>
        _db.Rooms.Where(r => r.Members.Any(m => m.UserId == userId))
                 .OrderByDescending(r => r.CreatedUtc).ToListAsync();
}
