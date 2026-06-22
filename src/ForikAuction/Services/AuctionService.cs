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
            .Where(q => q.RoomMemberId == member.Id && q.ForAuctionNumber == auctionNumber && q.Status == QuestApproval.Approved)
            .ToListAsync();
        int rawReward = questReward.Sum(q => QuestCatalog.Get(q.QuestId).PointsReward);

        var input = new AuctionInput
        {
            Carry = member.Carry,
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

    /// <summary>Игрок отмечает квест выполненным -> отправка на одобрение другим игрокам.
    /// Если в комнате он один — засчитывается сразу.</summary>
    public async Task<(bool ok, string message)> SubmitQuestAsync(int roomQuestId, int ownerMemberId)
    {
        var rq = await _db.RoomQuests.Include(q => q.Votes)
            .FirstOrDefaultAsync(q => q.Id == roomQuestId && q.RoomMemberId == ownerMemberId);
        if (rq is null) return (false, "Квест не найден.");
        if (rq.Claimed) return (false, "Квест уже засчитан.");

        int roomId = await _db.RoomMembers.Where(m => m.Id == ownerMemberId).Select(m => m.RoomId).FirstAsync();
        int others = await _db.RoomMembers.CountAsync(m => m.RoomId == roomId && m.Id != ownerMemberId);

        if (rq.Votes.Count > 0) _db.QuestApprovalVotes.RemoveRange(rq.Votes);
        rq.Status = others == 0 ? QuestApproval.Approved : QuestApproval.Pending;
        await _db.SaveChangesAsync();
        return (true, others == 0 ? "Засчитано." : "Отправлено на одобрение другим игрокам.");
    }

    /// <summary>Отозвать заявку (вернуть квест в исходное состояние).</summary>
    public async Task WithdrawQuestAsync(int roomQuestId, int ownerMemberId)
    {
        var rq = await _db.RoomQuests.Include(q => q.Votes)
            .FirstOrDefaultAsync(q => q.Id == roomQuestId && q.RoomMemberId == ownerMemberId);
        if (rq is null || rq.Claimed) return;
        if (rq.Votes.Count > 0) _db.QuestApprovalVotes.RemoveRange(rq.Votes);
        rq.Status = QuestApproval.Open;
        await _db.SaveChangesAsync();
    }

    /// <summary>Другой игрок голосует за/против. Когда проголосовали все остальные — итог фиксируется.</summary>
    public async Task<(bool ok, string message)> VoteQuestAsync(int roomQuestId, int voterMemberId, bool approve)
    {
        var rq = await _db.RoomQuests.Include(q => q.Votes)
            .FirstOrDefaultAsync(q => q.Id == roomQuestId);
        if (rq is null) return (false, "Квест не найден.");
        if (rq.Status != QuestApproval.Pending) return (false, "Голосование уже завершено.");
        if (rq.RoomMemberId == voterMemberId) return (false, "Нельзя голосовать за свой квест.");

        var existing = rq.Votes.FirstOrDefault(v => v.VoterRoomMemberId == voterMemberId);
        if (existing is null)
            rq.Votes.Add(new QuestApprovalVote { RoomQuestId = rq.Id, VoterRoomMemberId = voterMemberId, Approve = approve });
        else existing.Approve = approve;
        await _db.SaveChangesAsync();

        int roomId = await _db.RoomMembers.Where(m => m.Id == rq.RoomMemberId).Select(m => m.RoomId).FirstAsync();
        var otherIds = await _db.RoomMembers
            .Where(m => m.RoomId == roomId && m.Id != rq.RoomMemberId).Select(m => m.Id).ToListAsync();
        var votes = rq.Votes.Where(v => otherIds.Contains(v.VoterRoomMemberId)).ToList();
        if (otherIds.Count > 0 && votes.Count >= otherIds.Count)
        {
            int yes = votes.Count(v => v.Approve), no = votes.Count - yes;
            rq.Status = yes >= no ? QuestApproval.Approved : QuestApproval.Rejected; // ничья -> доверие
            await _db.SaveChangesAsync();
        }
        return (true, "Голос учтён.");
    }

    /// <summary>Чужие квесты на одобрение, по которым этот игрок ещё не голосовал.</summary>
    public Task<List<RoomQuest>> PendingForVoterAsync(int roomId, int voterMemberId, int questAuctionNumber) =>
        _db.RoomQuests
            .Include(q => q.RoomMember).ThenInclude(m => m.User)
            .Include(q => q.Votes)
            .Where(q => q.ForAuctionNumber == questAuctionNumber
                && q.Status == QuestApproval.Pending
                && q.RoomMember.RoomId == roomId
                && q.RoomMemberId != voterMemberId
                && !q.Votes.Any(v => v.VoterRoomMemberId == voterMemberId))
            .OrderBy(q => q.Id).ToListAsync();

    /// <summary>Есть ли в комнате неразрешённые заявки на одобрение (блокируют прокрутку).</summary>
    public Task<bool> HasPendingApprovalsAsync(int roomId, int questAuctionNumber) =>
        _db.RoomQuests.AnyAsync(q => q.ForAuctionNumber == questAuctionNumber
            && q.Status == QuestApproval.Pending && q.RoomMember.RoomId == roomId);

    public async Task<bool> HasPendingApprovalsForAuctionAsync(int auctionId)
    {
        var a = await _db.Auctions.Include(x => x.Room).FirstOrDefaultAsync(x => x.Id == auctionId);
        if (a is null) return false;
        return await HasPendingApprovalsAsync(a.RoomId, a.Room.CurrentAuctionNumber + 1);
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
            .OrderByDescending(e => e.Points).ThenBy(e => e.Id)
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
        var participantIds = auction.Entries.Select(e => e.RoomMemberId).Distinct().ToHashSet();

        var allMembers = await _db.RoomMembers
            .Include(m => m.Talents)
            .Where(m => m.RoomId == auction.RoomId).ToListAsync();

        // ВАЖНО: сбрасываем бафф/дебафф у ВСЕХ, чтобы +200/-200 не повторялись на аукционах,
        // где игрок не участвовал. Эффект применяется только по итогу последнего аукциона.
        foreach (var m in allMembers) { m.WonLast = false; m.LostLast = false; }

        foreach (var m in allMembers.Where(m => participantIds.Contains(m.Id)))
        {
            bool won = m.Id == winnerMemberId;
            if (won) m.WonLast = true; else m.LostLast = true;

            var levels = TalentResolver.Resolve(m.Talents);

            // накопитель: победа вычитает штраф, проигрыш прибавляет бонус — копится через аукционы
            if (won) m.Carry -= TalentEffects.WinPenalty(levels);
            else m.Carry += TalentEffects.LossBonus(levels);

            // ОТ: +3 всем участникам, +2 проигравшим (резина), + конвертация неистраченного (Инвестор)
            int tp = 3 + (won ? 0 : 2);
            double rate = TalentEffects.InvestorRate(levels);
            if (rate > 0)
            {
                int available = await AvailablePointsAsync(m.Id, auction.Number);
                int spent = auction.Entries.Where(e => e.RoomMemberId == m.Id).Sum(e => e.Points);
                int unused = Math.Max(0, available - spent);
                tp += (int)Math.Floor(unused * rate / 10.0);
            }
            m.TalentPoints += tp;
        }

        auction.Status = AuctionStatus.Finished;
        auction.WinnerEntryId = winnerEntryId;
        auction.FinishedUtc = DateTime.UtcNow;

        int next = auction.Number + 1;
        auction.Room.CurrentAuctionNumber = next;
        _db.Auctions.Add(new Auction { RoomId = auction.RoomId, Number = next, Status = AuctionStatus.Open });
        await _db.SaveChangesAsync();

        // пересчитать стартовые очки и выдать новый набор квестов
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
        if (rq.Status != QuestApproval.Open) return (false, "Сначала отмените отправку на одобрение.");

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
        rq.Status = QuestApproval.Open;
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
