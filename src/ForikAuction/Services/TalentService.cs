using ForikAuction.Data;
using ForikAuction.Data.Entities;
using ForikAuction.Game;
using Microsoft.EntityFrameworkCore;

namespace ForikAuction.Services;

public class TalentService
{
    private readonly AppDbContext _db;
    public TalentService(AppDbContext db) => _db = db;

    /// <summary>Купить следующий уровень таланта за ОТ. Возвращает (успех, сообщение).</summary>
    public async Task<(bool ok, string message)> BuyAsync(int roomMemberId, string code)
    {
        var def = TalentCatalog.All.FirstOrDefault(t => t.Code == code);
        if (def is null) return (false, "Неизвестный талант.");

        var member = await _db.RoomMembers
            .Include(m => m.Talents)
            .FirstOrDefaultAsync(m => m.Id == roomMemberId);
        if (member is null) return (false, "Игрок не найден.");

        var levels = TalentResolver.Resolve(member.Talents);
        var map = member.Talents.ToDictionary(t => t.Code, t => t);
        int current = map.TryGetValue(code, out var ut) ? ut.Level : 0;

        // проверка требований ветки
        if (def.RequiresCode is not null)
        {
            int reqLvl = member.Talents.FirstOrDefault(t => t.Code == def.RequiresCode)?.Level ?? 0;
            if (reqLvl < def.RequiresLevel)
            {
                var reqDef = TalentCatalog.Get(def.RequiresCode);
                return (false, $"Нужен «{reqDef.Name}» уровня {def.RequiresLevel}.");
            }
        }

        var cost = TalentCatalog.CostForNextLevel(code, current);
        if (cost is null) return (false, "Уже максимальный уровень.");
        if (member.TalentPoints < cost) return (false, $"Не хватает ОТ: нужно {cost}, есть {member.TalentPoints}.");

        member.TalentPoints -= cost.Value;
        if (ut is null)
            _db.UserTalents.Add(new UserTalent { RoomMemberId = roomMemberId, Code = code, Level = 1 });
        else
            ut.Level += 1;

        await _db.SaveChangesAsync();
        return (true, $"Куплен «{def.Name}» ур. {current + 1}.");
    }
}
