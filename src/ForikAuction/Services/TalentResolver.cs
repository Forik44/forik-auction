using ForikAuction.Data.Entities;
using ForikAuction.Game;

namespace ForikAuction.Services;

/// <summary>Преобразует прокачанные таланты игрока в структуру эффектов TalentLevels.</summary>
public static class TalentResolver
{
    public static TalentLevels Resolve(IEnumerable<UserTalent> talents)
    {
        var map = talents.ToDictionary(t => t.Code, t => t.Level);
        int L(string code) => map.TryGetValue(code, out var v) ? v : 0;
        return new TalentLevels
        {
            Capital    = L("capital"),
            Patron     = L("patron"),
            Comeback   = L("comeback"),
            Endgame    = L("endgame"),
            Stipend    = L("stipend"),
            Nobility   = L("nobility"),
            Curiosity  = L("curiosity"),
            Motivation = L("motivation"),
            Luck       = L("luck"),
            Investor   = L("investor"),
            Adventurer = L("adventurer"),
        };
    }
}
