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
    public bool WonLast;
    public bool LostLast;
    /// <summary>Сумма базовых наград за выполненные квесты (до множителя «Мотивация»).</summary>
    public int CompletedQuestReward;
}

/// <summary>
/// Считает стартовые очки игрока и ПОЛНУЮ разбивку источников/дебаффов — это используется
/// и для самого аукциона, и для всплывающей карточки игрока («почему столько очков»).
/// </summary>
public static class PointsCalculator
{
    public static PointsBreakdown ComputeStartingPoints(TalentLevels t, AuctionInput a)
    {
        var s = new List<PointSource> { new("base", "Базовые очки", TalentEffects.Base) };

        int cap = TalentEffects.CapitalBonus(t);
        if (cap > 0) s.Add(new("capital", $"Талант «Капитал» ×{t.Capital}", cap));

        if (a.LostLast)
            s.Add(new("loss", t.Stipend > 0
                ? $"Бонус проигравшему (+стипендия ×{t.Stipend})"
                : "Бонус проигравшему", TalentEffects.LossBonus(t)));

        if (a.WonLast)
        {
            int pen = TalentEffects.WinPenalty(t);
            if (pen > 0)
                s.Add(new("win", t.Nobility > 0
                    ? $"Штраф победителю (-благородство ×{t.Nobility})"
                    : "Штраф победителю", -pen));
        }

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
