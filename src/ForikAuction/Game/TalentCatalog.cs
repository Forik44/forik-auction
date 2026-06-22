namespace ForikAuction.Game;

/// <summary>Описание таланта в дереве. Для обычных талантов стоимость берётся из CostPerLevel[level].
/// Для бесконечных (Repeatable) — считается по формуле RepeatBaseCost + level*RepeatStep.</summary>
public sealed record TalentDef(
    string Code, string Branch, string Name, string Description,
    int MaxLevel, int[] CostPerLevel, string? RequiresCode, int RequiresLevel,
    bool Repeatable = false, int RepeatBaseCost = 0, int RepeatStep = 0);

/// <summary>
/// Дерево талантов. Прокачка идёт за кристаллы 💎, которые копятся каждый аукцион.
/// ВСЕ эффекты ЛИЧНЫЕ. Стоимость уровней растёт — выгоднее качать вширь.
/// Есть бесконечный талант («Меценат») — чтобы кристаллы было куда тратить и после капа.
/// </summary>
public static class TalentCatalog
{
    public static readonly IReadOnlyList<TalentDef> All = new List<TalentDef>
    {
        // ===== Богатство =====
        new("capital", "Богатство", "Капитал",
            "+40 базовых очков на каждый аукцион за уровень.",
            5, new[]{3,5,8,12,18}, null, 0),
        new("patron", "Богатство", "Меценат",
            "Бесконечная прокачка: +12 базовых очков за уровень. Стоимость постоянно растёт — вечный способ вкладывать кристаллы, когда всё остальное прокачано.",
            int.MaxValue, System.Array.Empty<int>(), null, 0, Repeatable: true, RepeatBaseCost: 6, RepeatStep: 3),
        new("investor", "Богатство", "Капитализация",
            "Долгая игра: в конце аукциона неистраченные на ставки очки превращаются в кристаллы. Сила РАСТЁТ с номером аукциона — в начале почти бесполезен, в долгой партии щедро окупается. Цена выбора: ставить очки сейчас или копить на будущее.",
            3, new[]{6,10,16}, "capital", 2),

        // ===== Сопротивление =====
        new("stipend", "Сопротивление", "Стипендия неудачника",
            "Лично тебе: если ТЫ проиграешь — накопитель растёт сильнее (+50 за уровень). У соперника не меняется.",
            3, new[]{4,7,11}, null, 0),
        new("nobility", "Сопротивление", "Благородство",
            "Лично тебе: если ТЫ выиграешь — накопитель падает слабее (-50 к штрафу за уровень). Соперника не касается.",
            3, new[]{4,7,11}, null, 0),
        new("comeback", "Сопротивление", "Реванш",
            "Пока ты отыгрываешься (накопитель в плюсе после проигрышей) — твой вес на колесе +5% за уровень. Камбэк для отстающего.",
            2, new[]{7,12}, null, 0),

        // ===== Квесты =====
        new("curiosity", "Квесты", "Пытливый ум",
            "+1 квест на следующий аукцион за уровень.",
            2, new[]{5,9}, null, 0),
        new("motivation", "Квесты", "Мотивация",
            "+15% к награде за одобренные квесты за уровень.",
            3, new[]{4,7,11}, null, 0),
        new("adventurer", "Квесты", "Авантюрист",
            "Лично тебе: можно заменить (переролльнуть) неудобный квест — 1 раз за аукцион за уровень.",
            2, new[]{6,10}, null, 0),

        // ===== Удача =====
        new("luck", "Удача", "Фартовый",
            "+3% к твоему весу на колесе за уровень (шанс чуть выше, чем по чистым очкам).",
            3, new[]{6,10,16}, null, 0),
        new("endgame", "Удача", "Эндшпиль",
            "Долгая игра: +2 базовых очка за уровень за КАЖДЫЙ прошедший аукцион. Чем дольше партия, тем мощнее (в начале почти ничего).",
            3, new[]{8,12,18}, null, 0),
    };

    public static TalentDef Get(string code) => All.First(t => t.Code == code);

    /// <summary>Стоимость следующего уровня (или null, если уже максимум).</summary>
    public static int? CostForNextLevel(string code, int currentLevel)
    {
        var d = Get(code);
        if (d.Repeatable) return d.RepeatBaseCost + currentLevel * d.RepeatStep;
        if (currentLevel >= d.MaxLevel) return null;
        return d.CostPerLevel[currentLevel];
    }
}
