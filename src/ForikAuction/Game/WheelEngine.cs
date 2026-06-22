namespace ForikAuction.Game;

/// <summary>Один сектор колеса: ставка игрока на конкретное аниме.</summary>
public sealed record WheelSegment(int EntryId, string AnimeTitle, string OwnerName, double Weight);

/// <summary>Один шаг выбывания: кого выбиваем, какие сектора сейчас на колесе и финальный угол прокрутки.</summary>
public sealed record EliminationStep(
    int EliminatedEntryId,
    IReadOnlyList<int> RemainingBefore,
    int ExtraSpins,
    double FinalAngleDeg);

/// <summary>Полный заранее посчитанный результат: победитель + последовательность выбываний.</summary>
public sealed record WheelResult(int WinnerEntryId, IReadOnlyList<EliminationStep> Steps);

/// <summary>
/// Движок колеса. Ключевое требование: победитель определяется ЗАРАНЕЕ строго пропорционально
/// весам (очкам), а затем участники выбывают по одному. Поскольку победитель выбран первым с
/// корректными вероятностями, дальнейший порядок выбывания НЕ влияет на итоговое распределение —
/// изначальные шансы сохраняются точно.
/// </summary>
public static class WheelEngine
{
    /// <summary>Взвешенный выбор индекса. r ожидается в [0,1).</summary>
    public static int WeightedPick(IReadOnlyList<double> weights, double r)
    {
        double total = 0;
        for (int i = 0; i < weights.Count; i++) total += weights[i];
        if (total <= 0) return 0;
        double acc = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            acc += weights[i] / total;
            if (r < acc) return i;
        }
        return weights.Count - 1;
    }

    public static WheelResult Compute(IReadOnlyList<WheelSegment> segments, Random rng)
    {
        if (segments.Count == 0) throw new ArgumentException("Нет секторов для прокрутки.");

        // 1. Победитель — пропорционально весу. Это и гарантирует верность изначальных шансов.
        var weights = new double[segments.Count];
        for (int i = 0; i < segments.Count; i++) weights[i] = segments[i].Weight;
        int winnerIdx = WeightedPick(weights, rng.NextDouble());
        int winnerId = segments[winnerIdx].EntryId;

        var byId = segments.ToDictionary(s => s.EntryId);
        var remaining = segments.Select(s => s.EntryId).ToList();
        var steps = new List<EliminationStep>();

        // 2. Выбиваем проигравших по одному. Для драматизма: чем меньше вес, тем выше шанс
        // выбыть раньше (фавориты держатся дольше). На итог это не влияет.
        // Выбывшие УБИРАЮТСЯ с колеса — каждый круг рисуем только оставшиеся (remaining).
        while (remaining.Count > 1)
        {
            var losers = remaining.Where(id => id != winnerId).ToList();
            var inv = losers.Select(id => 1.0 / Math.Max(1e-9, byId[id].Weight)).ToList();
            int li = WeightedPick(inv, rng.NextDouble());
            int eliminatedId = losers[li];

            var before = remaining.ToList();
            int extraSpins = 3 + rng.Next(3); // 3..5 полных оборотов для красоты
            double angle = AngleForTarget(before, eliminatedId, byId, extraSpins, rng);
            steps.Add(new EliminationStep(eliminatedId, before, extraSpins, angle));
            remaining.Remove(eliminatedId);
        }

        return new WheelResult(winnerId, steps);
    }

    /// <summary>
    /// Абсолютный угол поворота колеса (град.), чтобы указатель сверху (12 часов) попал внутрь
    /// сектора targetId. Сектора выкладываются по порядку списка order по часовой стрелке от 0.
    /// </summary>
    /// <summary>
    /// Вес сектора для ОТОБРАЖЕНИЯ/выбывания: обратно пропорционален очкам. Чем больше очков,
    /// тем меньше сектор и тем реже указатель на него попадёт (меньше шанс выбыть).
    /// </summary>
    public static double EliminationWeight(double pointsWeight) => 1.0 / Math.Max(1e-9, pointsWeight);

    public static double AngleForTarget(
        IReadOnlyList<int> order, int targetId,
        IReadOnlyDictionary<int, WheelSegment> byId, int extraSpins, Random rng)
    {
        double total = 0;
        foreach (var id in order) total += EliminationWeight(byId[id].Weight);

        double start = 0, tStart = 0, tSweep = 0;
        foreach (var id in order)
        {
            double sweep = EliminationWeight(byId[id].Weight) / total * 360.0;
            if (id == targetId) { tStart = start; tSweep = sweep; }
            start += sweep;
        }

        // Точка внутри сектора (не у самого края), чтобы выглядело честно.
        double mid = tStart + tSweep * (0.2 + 0.6 * rng.NextDouble());
        // Колесо крутится, указатель сверху неподвижен: чтобы привести mid наверх, повернуть на (360-mid).
        return extraSpins * 360.0 + (360.0 - mid);
    }
}
