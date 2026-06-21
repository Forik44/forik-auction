using ForikAuction.Data;
using ForikAuction.Data.Entities;
using ForikAuction.Game;
using Microsoft.EntityFrameworkCore;

namespace ForikAuction.Services;

public class AuctionService
{
    private readonly AppDbContext _db;
    public AuctionService(AppDbContext db) => _db = db;

    // ---------- Очки и разбивка ----------

    /// <summary>Пересчитать и сохранить источники стартовых очков игрока для аукциона N.</summary>
    public async Task WriteLedgerForOpenAuctionAsync(RoomMember member, int auctionNumber)
    {
        await _db.Entry(member).Collection(m => m.Talents).LoadAsync();
        var levels = TalentResolver.Resolve(member.Talents);

        // выполненные квесты, выданные ИМЕННО на этот аукцион
        var questReward = await _db.RoomQuests
            .Where(q => q.RoomMemberId == member.Id && q.ForAuctionNumber == auctionNumber && q.Completed)
            .ToListAsync();
        int rawReward = questReward.Sum(q => QuestCatalog.Get(q.QuestId).PointsReward);

        var input = new AuctionInput
        {
            WonLast = member.WonLast,
            LostLast = member.LostLast,
            CompletedQuestReward = rawReward,
        };
        var breakdown = PointsCalculator.ComputeStartingPoints(levels, input);

        var old = _db.PointsLedger.Where(p => p.RoomMemberId == member.Id && p.AuctionNumber == auctionNumber);
        _db.PointsLedger.RemoveRange(old);
        foreach (var s in breakdown.Sources)
            _db.PointsLedger.Add(new PointsLedgerEntry
            {
                RoomMemberId = member.Id, AuctionNumber = auctionNumber,
                Code = s.Code, Label = s.Label, Delta = s.Delta,
            });
        // отметим выданные квесты как засчитанные
        foreach (var q in questReward) q.Claimed = true;
    }

    public async Task<int> AvailablePointsAsync(int roomMemberId, int auctionNumber)
    {
        var sum = await _db.PointsLedger
            .Where(p => p.RoomMemberId == roomMemberId && p.AuctionNumber == auctionNumber)
            .SumAsync(p => (int?)p.Delta) ?? 0;
        return Math.Max(0, sum);
    }

    public Task<List<PointsLedgerEntry>> LedgerAsync(int roomMemberId, int auctionNumber) =>
        _db.PointsLedger.Where(p => p.RoomMemberId == roomMemberId && p.AuctionNumber == auctionNumber)
           .ToListAsync();

    // ---------- Квесты ----------

    public async Task DrawQuestsAsync(RoomMember member, int forAuctionNumber)
    {
        bool already = await _db.RoomQuests.AnyAsync(q => q.RoomMemberId == member.Id && q.ForAuctionNumber == forAuctionNumber);
        if (already) return;
        await _db.Entry(member).Collection(m => m.Talents).LoadAsync();
        var levels = TalentResolver.Resolve(member.Talents);
        int count = TalentEffects.QuestCount(levels);
        var rng = new Random();
        foreach (var q in QuestCatalog.Draw(count, rng))
            _db.RoomQuests.Add(new RoomQuest
            {
                RoomMemberId = member.Id, ForAuctionNumber = forAuctionNumber, QuestId = q.Id,
            });
    }

    public async Task ToggleQuestAsync(int roomQuestId, bool completed)
    {
        var q = await _db.RoomQuests.FindAsync(roomQuestId);
        if (q is null || q.Claimed) return; // засчитанное не меняем
        q.Completed = completed;
        await _db.SaveChangesAsync();
    }

    // ---------- Ставки ----------

    public async Task<(bool ok, string message)> SetEntryAsync(int auctionId, int roomMemberId, string anime, int points)
    {
        var auction = await _db.Auctions.FindAsync(auctionId);
        if (auction is null || auction.Status != AuctionStatus.Open) return (false, "Аукцион закрыт.");
        if (string.IsNullOrWhiteSpace(anime)) return (false, "Введите название аниме.");
        if (points <= 0) return (false, "Очки должны быть больше нуля.");

        int available = await AvailablePointsAsync(roomMemberId, auction.Number);
        int alreadyBet = await _db.AuctionEntries
            .Where(e => e.AuctionId == auctionId && e.RoomMemberId == roomMemberId)
            .SumAsync(e => (int?)e.Points) ?? 0;
        if (alreadyBet + points > available)
            return (false, $"Не хватает очков: доступно {available - alreadyBet}.");

        _db.AuctionEntries.Add(new AuctionEntry
        {
            AuctionId = auctionId, RoomMemberId = roomMemberId,
            AnimeTitle = anime.Trim(), Points = points,
        });
        await _db.SaveChangesAsync();
        return (true, "Ставка добавлена.");
    }

    public async Task RemoveEntryAsync(int entryId, int roomMemberId)
    {
        var e = await _db.AuctionEntries.FindAsync(entryId);
        if (e is null || e.RoomMemberId != roomMemberId) return;
        var auction = await _db.Auctions.FindAsync(e.AuctionId);
        if (auction is null || auction.Status != AuctionStatus.Open) return;
        _db.AuctionEntries.Remove(e);
        await _db.SaveChangesAsync();
    }

    // ---------- Колесо ----------

    /// <summary>Построить сектора с учётом таланта «Фартовый» (личный множитель веса).</summary>
    public async Task<List<WheelSegment>> BuildSegmentsAsync(int auctionId)
    {
        var entries = await _db.AuctionEntries
            .Include(e => e.RoomMember).ThenInclude(m => m.User)
            .Include(e => e.RoomMember).ThenInclude(m => m.Talents)
            .Where(e => e.AuctionId == auctionId)
            .ToListAsync();

        var segs = new List<WheelSegment>();
        foreach (var e in entries)
        {
            var levels = TalentResolver.Resolve(e.RoomMember.Talents);
            double w = TalentEffects.WheelWeight(e.Points, levels);
            segs.Add(new WheelSegment(e.Id, e.AnimeTitle, e.RoomMember.User.DisplayName, w));
        }
        return segs;
    }

    /// <summary>Посчитать заранее победителя и порядок выбывания (один раз на сервере).</summary>
    public async Task<WheelResult> ComputeWheelAsync(int auctionId)
    {
        var segs = await BuildSegmentsAsync(auctionId);
        return WheelEngine.Compute(segs, new Random());
    }

    /// <summary>Зафиксировать результат, начислить ОТ/баффы и открыть следующий аукцион.</summary>
    public async Task FinishAuctionAsync(int auctionId, int winnerEntryId)
    {
        var auction = await _db.Auctions
            .Include(a => a.Room)
            .Include(a => a.Entries).ThenInclude(e => e.RoomMember).ThenInclude(m => m.Talents)
            .FirstAsync(a => a.Id == auctionId);
        if (auction.Status == AuctionStatus.Finished) return;

        var winnerEntry = auction.Entries.First(e => e.Id == winnerEntryId);
        int winnerMemberId = winnerEntry.RoomMemberId;

        // участники = у кого были ставки
        var participants = auction.Entries.Select(e => e.RoomMember).DistinctBy(m => m.Id).ToList();
        foreach (var m in participants)
        {
            bool won = m.Id == winnerMemberId;
            m.WonLast = won;
            m.LostLast = !won;

            // ОТ: +3 всем, +2 проигравшим (резина), + конвертация неистраченного (Инвестор)
            int tp = 3 + (won ? 0 : 2);
            var levels = TalentResolver.Resolve(m.Talents);
            double rate = TalentEffects.InvestorRate(levels);
            if (rate > 0)
            {
                int available = await AvailablePointsAsync(m.Id, auction.Number);
                int spent = auction.Entries.Where(e => e.RoomMemberId == m.Id).Sum(e => e.Points);
                int unused = Math.Max(0, available - spent);
                tp += (int)Math.Floor(unused * rate / 10.0); // 10 очков -> доля -> ОТ
            }
            m.TalentPoints += tp;
        }

        auction.Status = AuctionStatus.Finished;
        auction.WinnerEntryId = winnerEntryId;
        auction.FinishedUtc = DateTime.UtcNow;

        // открыть следующий аукцион
        int next = auction.Number + 1;
        auction.Room.CurrentAuctionNumber = next;
        _db.Auctions.Add(new Auction { RoomId = auction.RoomId, Number = next, Status = AuctionStatus.Open });
        await _db.SaveChangesAsync();

        // пересчитать стартовые очки и выдать новый набор квестов
        var allMembers = await _db.RoomMembers
            .Include(m => m.Talents)
            .Where(m => m.RoomId == auction.RoomId).ToListAsync();
        foreach (var m in allMembers)
        {
            m.QuestRerollsUsed = 0;
            await WriteLedgerForOpenAuctionAsync(m, next);
            await DrawQuestsAsync(m, next + 1);
        }
        await _db.SaveChangesAsync();
    }

    // ---------- Реролл квеста (талант «Авантюрист») ----------

    public async Task<(bool ok, string message)> RerollQuestAsync(int roomQuestId, int roomMemberId)
    {
        var rq = await _db.RoomQuests.FindAsync(roomQuestId);
        if (rq is null || rq.RoomMemberId != roomMemberId) return (false, "Квест не найден.");
        if (rq.Claimed) return (false, "Этот квест уже засчитан.");

        var member = await _db.RoomMembers.Include(m => m.Talents)
            .FirstOrDefaultAsync(m => m.Id == roomMemberId);
        if (member is null) return (false, "Игрок не найден.");

        var levels = TalentResolver.Resolve(member.Talents);
        int allowed = TalentEffects.QuestRerolls(levels);
        if (allowed <= 0) return (false, "Нужен талант «Авантюрист».");
        if (member.QuestRerollsUsed >= allowed)
            return (false, $"Рероллы закончились ({member.QuestRerollsUsed}/{allowed}).");

        // выбрать новый квест, которого ещё нет в текущем наборе игрока на этот аукцион
        var current = await _db.RoomQuests
            .Where(q => q.RoomMemberId == roomMemberId && q.ForAuctionNumber == rq.ForAuctionNumber)
            .Select(q => q.QuestId).ToListAsync();
        var pool = QuestCatalog.All.Where(q => !current.Contains(q.Id)).ToList();
        if (pool.Count == 0) return (false, "Нет других квестов для замены.");

        var pick = pool[new Random().Next(pool.Count)];
        rq.QuestId = pick.Id;
        rq.Completed = false;
        member.QuestRerollsUsed++;
        await _db.SaveChangesAsync();
        return (true, $"Квест заменён ({member.QuestRerollsUsed}/{allowed}).");
    }

    // ---------- История и статистика ----------

    public async Task<List<AuctionHistoryItem>> HistoryAsync(int roomId)
    {
        var finished = await _db.Auctions
            .Where(a => a.RoomId == roomId && a.Status == AuctionStatus.Finished)
            .OrderByDescending(a => a.Number)
            .ToListAsync();

        var result = new List<AuctionHistoryItem>();
        foreach (var a in finished)
        {
            string anime = "—", owner = "—";
            if (a.WinnerEntryId is int wid)
            {
                var e = await _db.AuctionEntries.Include(x => x.RoomMember).ThenInclude(m => m.User)
                    .FirstOrDefaultAsync(x => x.Id == wid);
                if (e is not null) { anime = e.AnimeTitle; owner = e.RoomMember.User.DisplayName; }
            }
            result.Add(new AuctionHistoryItem(a.Number, anime, owner, a.FinishedUtc));
        }
        return result;
    }

    public async Task<List<WinStat>> WinStatsAsync(int roomId)
    {
        var finished = await _db.Auctions
            .Where(a => a.RoomId == roomId && a.Status == AuctionStatus.Finished && a.WinnerEntryId != null)
            .Select(a => a.WinnerEntryId!.Value).ToListAsync();
        if (finished.Count == 0) return new();

        var entries = await _db.AuctionEntries.Include(e => e.RoomMember).ThenInclude(m => m.User)
            .Where(e => finished.Contains(e.Id)).ToListAsync();

        return entries
            .GroupBy(e => e.RoomMember.User.DisplayName)
            .Select(g => new WinStat(g.Key, g.Count()))
            .OrderByDescending(w => w.Wins).ToList();
    }

    public Task<Auction?> CurrentAuctionAsync(int roomId) =>
        _db.Auctions.Where(a => a.RoomId == roomId && a.Status != AuctionStatus.Finished)
           .OrderByDescending(a => a.Number).FirstOrDefaultAsync();
}
