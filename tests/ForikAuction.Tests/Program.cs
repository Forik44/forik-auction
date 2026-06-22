using ForikAuction.Game;

int failed = 0, passed = 0;
void Check(string name, bool cond)
{
    if (cond) { passed++; Console.WriteLine($"  PASS  {name}"); }
    else { failed++; Console.WriteLine($"  FAIL  {name}"); }
}

Console.WriteLine("== WeightedPick ==");
Check("r=0 -> index 0", WheelEngine.WeightedPick(new double[]{1,1,1}, 0.0) == 0);
Check("r just below 1 -> last", WheelEngine.WeightedPick(new double[]{1,1,1}, 0.999) == 2);
Check("zero total -> 0", WheelEngine.WeightedPick(new double[]{0,0}, 0.5) == 0);

Console.WriteLine("== Распределение победителя пропорционально весам ==");
{
    var segs = new List<WheelSegment>
    {
        new(1, "A", "Игрок1", 700),
        new(2, "B", "Игрок1", 200),
        new(3, "C", "Игрок2", 100),
    };
    var rng = new Random(12345);
    int N = 200_000;
    var wins = new Dictionary<int,int>{{1,0},{2,0},{3,0}};
    for (int i = 0; i < N; i++)
    {
        var res = WheelEngine.Compute(segs, rng);
        wins[res.WinnerEntryId]++;
        // победитель НИКОГДА не выбывает
        if (res.Steps.Any(s => s.EliminatedEntryId == res.WinnerEntryId)) { Console.WriteLine("WINNER ELIMINATED!"); }
        // ровно n-1 шагов
        if (res.Steps.Count != segs.Count - 1) Console.WriteLine("WRONG STEP COUNT");
    }
    double p1 = wins[1]/(double)N, p2 = wins[2]/(double)N, p3 = wins[3]/(double)N;
    Console.WriteLine($"   A={p1:P2} (ожид 70%), B={p2:P2} (ожид 20%), C={p3:P2} (ожид 10%)");
    Check("A ~ 0.70", Math.Abs(p1 - 0.70) < 0.01);
    Check("B ~ 0.20", Math.Abs(p2 - 0.20) < 0.01);
    Check("C ~ 0.10", Math.Abs(p3 - 0.10) < 0.01);
}

Console.WriteLine("== Победитель не выбивается, шаги корректны (разные размеры) ==");
{
    var rng = new Random(7);
    for (int trial = 0; trial < 5000; trial++)
    {
        int n = 2 + rng.Next(6);
        var segs = new List<WheelSegment>();
        for (int i = 0; i < n; i++) segs.Add(new(i+1, $"E{i}", "p", 1 + rng.Next(500)));
        var res = WheelEngine.Compute(segs, rng);
        var eliminated = res.Steps.Select(s => s.EliminatedEntryId).ToHashSet();
        if (eliminated.Contains(res.WinnerEntryId)) { Console.WriteLine("BUG winner eliminated"); failed++; break; }
        if (res.Steps.Count != n - 1) { Console.WriteLine("BUG step count"); failed++; break; }
        if (eliminated.Count != n - 1) { Console.WriteLine("BUG duplicate elimination"); failed++; break; }
    }
    Check("5000 случайных прогонов: победитель выживает, выбывания уникальны", true);
}

Console.WriteLine("== AngleForTarget попадает в нужный сектор ==");
{
    var segs = new List<WheelSegment>{ new(1,"A","p",100), new(2,"B","p",100), new(3,"C","p",100), new(4,"D","p",100) };
    var byId = segs.ToDictionary(s => s.EntryId);
    var order = segs.Select(s => s.EntryId).ToList();
    var rng = new Random(3);
    bool ok = true;
    foreach (var target in order)
        for (int k = 0; k < 1000; k++)
        {
            double a = WheelEngine.AngleForTarget(order, target, byId, 4, rng);
            // указатель сверху: какой сектор оказался под ним?
            double rot = ((a % 360) + 360) % 360;            // фактический поворот колеса
            double pointer = (360 - rot) % 360;              // позиция под указателем в координатах секторов
            double total = 400, start = 0; int landed = -1;
            foreach (var id in order)
            {
                double sweep = byId[id].Weight / total * 360.0;
                if (pointer >= start && pointer < start + sweep) { landed = id; break; }
                start += sweep;
            }
            if (landed != target) { ok = false; break; }
        }
    Check("Угол всегда приводит указатель в целевой сектор", ok);
}

Console.WriteLine("== PointsCalculator ==");
{
    var t = new TalentLevels();
    var b0 = PointsCalculator.ComputeStartingPoints(t, new AuctionInput());
    Check("Чистый старт = 1000", b0.Total == 1000);

    Check("Накопитель +200 (один проигрыш) = 1200",
        PointsCalculator.ComputeStartingPoints(t, new AuctionInput{ Carry = 200 }).Total == 1200);
    Check("Накопитель -200 (одна победа) = 800",
        PointsCalculator.ComputeStartingPoints(t, new AuctionInput{ Carry = -200 }).Total == 800);
    Check("Накопитель -400 (две победы подряд) = 600",
        PointsCalculator.ComputeStartingPoints(t, new AuctionInput{ Carry = -400 }).Total == 600);
    Check("Глубокий минус клампится в 0",
        PointsCalculator.ComputeStartingPoints(t, new AuctionInput{ Carry = -1500 }).Total == 0);

    // эффект талантов теперь применяется при НАКОПЛЕНии (в FinishAuction), проверяем хелперы:
    Check("Штраф победителя при Благородство x2 = 100", TalentEffects.WinPenalty(new TalentLevels{ Nobility = 2 }) == 100);
    Check("Бонус проигравшего при Стипендия x1 = 250", TalentEffects.LossBonus(new TalentLevels{ Stipend = 1 }) == 250);

    var tCap = new TalentLevels{ Capital = 3 };
    var bCap = PointsCalculator.ComputeStartingPoints(tCap, new AuctionInput());
    Check("Капитал x3 = 1120", bCap.Total == 1120);

    var tMot = new TalentLevels{ Motivation = 2 };
    var bQ = PointsCalculator.ComputeStartingPoints(tMot, new AuctionInput{ CompletedQuestReward = 100 });
    Check("Квесты 100 при Мотивация x2 = 1130 (x1.3)", bQ.Total == 1130);

    Check("Разбивка содержит источник base", b0.Sources.Any(x => x.Code == "base"));
    Check("Накопитель попадает в разбивку как источник carry",
        PointsCalculator.ComputeStartingPoints(t, new AuctionInput{ Carry = -200 }).Sources.Any(x => x.Code == "carry"));
}

Console.WriteLine("== TalentCatalog ==");
{
    Check("Стоимость 1-го ур. Капитала = 3", TalentCatalog.CostForNextLevel("capital", 0) == 3);
    Check("Стоимость растёт", TalentCatalog.CostForNextLevel("capital", 1) == 5);
    Check("На максимуме нет след. уровня", TalentCatalog.CostForNextLevel("capital", 5) == null);
    Check("WheelWeight: Фартовый x2 даёт +6%", Math.Abs(TalentEffects.WheelWeight(1000, new TalentLevels{Luck=2}) - 1060) < 1e-6);
    Check("Реванш не работает, пока не отстаёшь (carry<=0)", Math.Abs(TalentEffects.WheelWeight(1000, new TalentLevels{Comeback=2}, -100) - 1000) < 1e-6);
    Check("Реванш +10% при carry>0 (Comeback x2)", Math.Abs(TalentEffects.WheelWeight(1000, new TalentLevels{Comeback=2}, 200) - 1100) < 1e-6);
    Check("Меценат: +12 базовых очков за уровень", TalentEffects.PatronBonus(new TalentLevels{Patron=3}) == 36);
    Check("Эндшпиль слабый рано (ур1, аук2) = +4", TalentEffects.EndgameBonus(new TalentLevels{Endgame=1}, 2) == 4);
    Check("Эндшпиль сильный поздно (ур3, аук30) = +180", TalentEffects.EndgameBonus(new TalentLevels{Endgame=3}, 30) == 180);
    Check("Капитализация почти 0 рано", TalentEffects.InvestorCrystals(1, 1, 1000) == 0);
    Check("Капитализация растёт поздно (ур3, аук40, 1000) = 24", TalentEffects.InvestorCrystals(3, 40, 1000) == 24);
    Check("QuestCount базово 5", TalentEffects.QuestCount(new TalentLevels()) == 5);
    Check("QuestCount с Пытливый ум x2 = 7", TalentEffects.QuestCount(new TalentLevels{Curiosity=2}) == 7);
    Check("Талантов в дереве: 11", TalentCatalog.All.Count == 11);
    Check("Есть «Меценат», «Реванш», «Эндшпиль», «Капитализация»",
        new[]{"patron","comeback","endgame","investor"}.All(c => TalentCatalog.All.Any(t => t.Code == c)));
    Check("Меценат бесконечный: стоимость растёт (6,9,12...)",
        TalentCatalog.CostForNextLevel("patron",0)==6 && TalentCatalog.CostForNextLevel("patron",1)==9 && TalentCatalog.CostForNextLevel("patron",10)==36);
    Check("Есть талант «Авантюрист»", TalentCatalog.All.Any(t => t.Code == "adventurer"));
    Check("Авантюрист: рероллов = уровню", TalentEffects.QuestRerolls(new TalentLevels{Adventurer=2}) == 2);
    Check("Без Авантюриста рероллов нет", TalentEffects.QuestRerolls(new TalentLevels()) == 0);
    Check("Стоимость Авантюрист ур.1 = 6", TalentCatalog.CostForNextLevel("adventurer", 0) == 6);
}

Console.WriteLine("== QuestCatalog ==");
{
    Check("Квестов стало 105", QuestCatalog.All.Count == 105);
    Check("Есть категория «Нелепое»", QuestCatalog.All.Any(q => q.Category == "Нелепое"));
    Check("Все Id уникальны", QuestCatalog.All.Select(q=>q.Id).Distinct().Count() == QuestCatalog.All.Count);
    var rng = new Random(1);
    var drawn = QuestCatalog.Draw(4, rng);
    Check("Draw(4) -> 4 квеста", drawn.Count == 4);
    Check("Draw(4) без повторов", drawn.Select(q=>q.Id).Distinct().Count() == 4);
    Check("Draw(6) -> 6 квестов", QuestCatalog.Draw(6, rng).Count == 6);
}

Console.WriteLine();
Console.WriteLine($"ИТОГО: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
