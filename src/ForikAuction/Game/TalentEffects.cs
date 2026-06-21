namespace ForikAuction.Game;

/// <summary>
/// Разрешённые (просуммированные) уровни талантов игрока. Заполняется из UserTalent.
/// Коды талантов смотри в TalentCatalog.
/// </summary>
public sealed class TalentLevels
{
    public int Capital;     // "Капитал": +40 базовых очков за уровень
    public int Stipend;     // "Стипендия неудачника": +50 к бонусу проигравшего за уровень
    public int Nobility;    // "Благородство": -50 к штрафу победителя за уровень
    public int Curiosity;   // "Пытливый ум": +1 квест за уровень
    public int Motivation;  // "Мотивация": +15% к награде за квесты за уровень
    public int Luck;        // "Фартовый": +3% к весу на колесе за уровень
    public int Investor;    // "Инвестор": конверсия неистраченных очков в ОТ за уровень
    public int Adventurer;  // "Авантюрист": число рероллов квестов за аукцион
}

/// <summary>Эффекты талантов в одном месте, чтобы и UI, и расчёты совпадали.</summary>
public static class TalentEffects
{
    public const int Base = 1000;
    public const int BaseLossBonus = 200;
    public const int BaseWinPenalty = 200;

    public static int CapitalBonus(TalentLevels t) => t.Capital * 40;
    public static int LossBonus(TalentLevels t) => BaseLossBonus + t.Stipend * 50;
    public static int WinPenalty(TalentLevels t) => Math.Max(0, BaseWinPenalty - t.Nobility * 50);
    public static int QuestCount(TalentLevels t) => 4 + t.Curiosity;
    public static double QuestMultiplier(TalentLevels t) => 1.0 + 0.15 * t.Motivation;
    public static double WheelWeight(int points, TalentLevels t) => points * (1.0 + 0.03 * t.Luck);
    /// <summary>Доля неистраченных очков, которая превращается в очки таланта (ОТ).</summary>
    public static double InvestorRate(TalentLevels t) => t.Investor switch { 0 => 0, 1 => 0.10, 2 => 0.15, _ => 0.20 };
    /// <summary>Сколько раз за аукцион можно переролльнуть квест.</summary>
    public static int QuestRerolls(TalentLevels t) => t.Adventurer;
}
