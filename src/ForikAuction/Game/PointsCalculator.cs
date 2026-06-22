namespace ForikAuction.Game;

public sealed record PointSource(string Code, string Label, int Delta);

public sealed class PointsBreakdown
{
    public int Total { get; init; }
    public IReadOnlyList<PointSource> Sources { get; init; } = Array.Empty<PointSource>();
}

/// <summary>Исходные данные для расчёта стартовых очков игрока на следующий аукцион.</summary>
public sealed class AuctionInput
{
    /// <summary>Накопленный за ВСЕ аукционы итог исходов: победы в минус, проигрыши в плюс.</summary>
    public int Carry;
    /// <summary>Сумма базовых наград за одобренные квесты (до множителя «Мотивация»).</summary>
    public int CompletedQuestReward;
    /// <summary>Номер аукциона — для талантов, растущих со временем («Эндшпиль»).</summary>
    public int AuctionNumber;
}

public static class PointsCalculator
{
    public static PointsBreakdown ComputeStartingPoints(TalentLevels t, AuctionInput a)
    {
        var s = new List<PointSource> { new("base", "Базовые очки", TalentEffects.Base) };

        int cap = TalentEffects.CapitalBonus(t);
        if (cap > 0) s.Add(new("capital", $"Талант «Капитал» ×{t.Capital}", cap));

        int patron = TalentEffects.PatronBonus(t);
        if (patron > 0) s.Add(new("patron", $"Талант «Меценат» ×{t.Patron}", patron));

        int endgame = TalentEffects.EndgameBonus(t, a.AuctionNumber);
        if (endgame > 0) s.Add(new("endgame", $"Талант «Эндшпиль» ×{t.Endgame} (растёт с аукционом)", endgame));

        if (a.Carry != 0)
            s.Add(new("carry", a.Carry > 0 ? "Накоплено за проигрыши" : "Накоплено за победы", a.Carry));

        if (a.CompletedQuestReward > 0)
        {
            int v = (int)Math.Round(a.CompletedQuestReward * TalentEffects.QuestMultiplier(t));
            s.Add(new("quests", t.Motivation > 0
                ? $"Награды за квесты (+мотивация ×{t.Motivation})"
                : "Награды за квесты", v));
        }

        int total = 0;
        foreach (var x in s) total += x.Delta;
        if (total < 0) total = 0;
        return new PointsBreakdown { Total = total, Sources = s };
    }
}
