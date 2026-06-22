namespace ForikAuction.Game;

/// <summary>Разрешённые (просуммированные) уровни талантов игрока.</summary>
public sealed class TalentLevels
{
    public int Capital;     // "Капитал": +40 базовых очков за уровень
    public int Patron;      // "Меценат": +12 базовых очков за уровень (бесконечный)
    public int Investor;    // "Капитализация": неистраченные очки -> кристаллы, растёт с номером аукциона
    public int Stipend;     // "Стипендия неудачника": +50 к бонусу проигравшего
    public int Nobility;    // "Благородство": -50 к штрафу победителя
    public int Comeback;    // "Реванш": +вес на колесе, пока отыгрываешься (накопитель > 0)
    public int Curiosity;   // "Пытливый ум": +1 квест
    public int Motivation;  // "Мотивация": +15% к награде за квесты
    public int Adventurer;  // "Авантюрист": реролл квестов
    public int Luck;        // "Фартовый": +3% к весу на колесе
    public int Endgame;     // "Эндшпиль": +очки, растут с номером аукциона
}

/// <summary>Эффекты талантов в одном месте, чтобы UI и расчёты совпадали.</summary>
public static class TalentEffects
{
    public const int Base = 1000;
    public const int BaseLossBonus = 200;
    public const int BaseWinPenalty = 200;

    public static int CapitalBonus(TalentLevels t) => t.Capital * 40;
    public static int PatronBonus(TalentLevels t) => t.Patron * 12;
    public static int EndgameBonus(TalentLevels t, int auctionNumber) => t.Endgame * auctionNumber * 2;

    public static int LossBonus(TalentLevels t) => BaseLossBonus + t.Stipend * 50;
    public static int WinPenalty(TalentLevels t) => Math.Max(0, BaseWinPenalty - t.Nobility * 50);

    public static int QuestCount(TalentLevels t) => 5 + t.Curiosity;
    public static double QuestMultiplier(TalentLevels t) => 1.0 + 0.15 * t.Motivation;
    public static int QuestRerolls(TalentLevels t) => t.Adventurer;

    public static double WheelWeight(int points, TalentLevels t) => WheelWeight(points, t, 0);

    /// <summary>Вес на колесе: «Фартовый» всегда, «Реванш» — только пока отыгрываешься (carry > 0).</summary>
    public static double WheelWeight(int points, TalentLevels t, int carry)
    {
        double mult = 1.0 + 0.03 * t.Luck + (carry > 0 ? 0.05 * t.Comeback : 0.0);
        return points * mult;
    }

    /// <summary>
    /// «Капитализация»: сколько кристаллов дать за неистраченные очки в конце аукциона.
    /// Намеренно слабый в начале и сильный в долгой игре — растёт с номером аукциона.
    /// </summary>
    public static int InvestorCrystals(int level, int auctionNumber, int unusedPoints)
    {
        if (level <= 0 || unusedPoints <= 0) return 0;
        return (int)Math.Floor(unusedPoints * (double)level * auctionNumber / 5000.0);
    }
}
