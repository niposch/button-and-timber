using System.Text.Json.Serialization;

namespace ButtonAndTimber;

internal sealed class Request
{
    public int Toy { get; set; }
    public int Count { get; set; }
    public int Pay { get; set; }
    public int DueDay { get; set; }
    public string Giver { get; set; } = "";
    public int Regular { get; set; } = -1;
}

internal sealed record WorkshopUpgrade(string Name, string Blurb, int[] Cost, int[] Rank);

internal static class Upgrades
{
    public const int SharpTools = 0;
    public const int QuickHands = 1;
    public const int PatternBooks = 2;
    public const int ScrapBin = 3;

    public static readonly WorkshopUpgrade[] All =
    [
        new("Sharp tools", "+3% yield on every toy, per level.", [90, 190, 340], [0, 1, 2]),
        new("Quick hands", "Benches work 12% faster, per level.", [80, 160, 300], [0, 1, 2]),
        new("Pattern books", "New patterns cost 15% less to learn, per level.", [120, 260], [1, 2]),
        new("Scrap bin", "Recover a quarter of the materials from each scrapped toy, per level.", [100, 220], [1, 2])
    ];
}

internal sealed partial class ShopState
{
    private static readonly string[] Townsfolk =
        ["Mrs. Pell", "The Vicar", "Mr. Dunn", "Miss Orla", "The Baker", "Captain Reeve", "Old Tam", "Dr. Hale"];

    public List<Request> Board { get; set; } = [];
    public int[] Workshop { get; set; } = new int[4];

    [JsonIgnore] public int BoardSize => Math.Min(4, 2 + Rank / 2);

    public int UpgradeLevel(int upgrade) => Workshop.Length > upgrade ? Workshop[upgrade] : 0;

    public bool UpgradeMaxed(int upgrade) => UpgradeLevel(upgrade) >= Upgrades.All[upgrade].Cost.Length;

    public int UpgradeCost(int upgrade) => UpgradeMaxed(upgrade) ? 0 : Upgrades.All[upgrade].Cost[UpgradeLevel(upgrade)];

    public int UpgradeRank(int upgrade) => UpgradeMaxed(upgrade) ? 0 : Upgrades.All[upgrade].Rank[UpgradeLevel(upgrade)];

    public bool CanUpgrade(int upgrade) => !UpgradeMaxed(upgrade) && Rank >= UpgradeRank(upgrade) && Cash >= UpgradeCost(upgrade);

    public bool BuyUpgrade(int upgrade)
    {
        if (!CanUpgrade(upgrade))
            return false;
        Spend(UpgradeCost(upgrade));
        Workshop[upgrade]++;
        Announce($"{Upgrades.All[upgrade].Name} · level {Workshop[upgrade]}");
        GainRenown(4);
        return true;
    }

    public int ResearchCost(int toy) =>
        (int)Math.Round(Catalog.Toys[toy].Research * (1 - .15 * UpgradeLevel(Upgrades.PatternBooks)));

    public int DaysLeft(Request request) => request.DueDay - Day;

    public bool CanFill(Request request) => Stock[request.Toy] >= request.Count;

    public bool Fill(Request request)
    {
        if (!Board.Contains(request) || !CanFill(request))
            return false;

        Stock[request.Toy] -= request.Count;
        Board.Remove(request);
        Cash += request.Pay;
        Today.OtherIncome += request.Pay;
        Reputation = Math.Min(100, Reputation + 2);
        GainRenown(4 + Catalog.Toys[request.Toy].Tier * 2);
        if (request.Regular >= 0 && request.Regular < Regulars.Count)
            Regulars[request.Regular].Affinity += 4;
        Replenish();
        Announce($"{request.Giver} is delighted · +${request.Pay}");
        return true;
    }

    private void RefreshBoard()
    {
        foreach (Request late in Board.Where(request => request.DueDay < Day).ToArray())
        {
            Board.Remove(late);
            Reputation = Math.Max(15, Reputation - 3);
            if (late.Regular >= 0 && late.Regular < Regulars.Count)
                Regulars[late.Regular].Affinity = Math.Max(0, Regulars[late.Regular].Affinity - 3);
            Note($"{late.Giver}'s request went unfilled");
        }

        if (Day < 3)
            return;
        while (Board.Count < BoardSize && Roll(100) < 70)
            Board.Add(NewRequest());
    }

    private Request NewRequest()
    {
        int[] friends = Enumerable.Range(0, Regulars.Count).Where(i => Regulars[i].Bond >= 1 &&
            Board.All(request => request.Regular != i)).ToArray();
        int regular = friends.Length > 0 && Roll(100) < 55 ? friends[Roll(friends.Length)] : -1;
        ToyFamily family = regular >= 0 ? Regulars[regular].Family : Catalog.Families[Roll(Catalog.Families.Length)];
        int[] options = Enumerable.Range(0, Catalog.Toys.Length)
            .Where(toy => Unlocked[toy] && Catalog.Toys[toy].Family == family && Catalog.Toys[toy].Tier <= MaxTier)
            .ToArray();
        if (options.Length == 0)
            options = [0];

        int best = options.Max(toy => Catalog.Toys[toy].Tier);
        int[] weighted = options.Where(toy => Catalog.Toys[toy].Tier >= Math.Max(1, best - 1)).ToArray();
        int pick = weighted[Roll(weighted.Length)];
        int count = 2 + Roll(3) + (Catalog.Toys[pick].Tier == 1 ? 2 : 0);
        int due = 3 + Roll(3) - (Rank >= 3 ? 1 : 0);
        double premium = regular >= 0 ? 1.45 : 1.25;
        int pay = (int)Math.Round(Catalog.Toys[pick].Value * count * premium / 5) * 5 + 10;
        return new Request
        {
            Toy = pick, Count = count, Pay = pay, DueDay = Day + due, Regular = regular,
            Giver = regular >= 0 ? Regulars[regular].Name : Townsfolk[Roll(Townsfolk.Length)]
        };
    }
}
