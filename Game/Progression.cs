using System.Text.Json.Serialization;

namespace ButtonAndTimber;

internal enum Archetype { Parent, Bargain, Tourist, Gifter, Collector }
internal enum Philosophy { None, Heirloom, Volume, Showman }
internal enum DayEvent { None, SchoolTrip, RivalOpens, SupplierSale, CollectorVisit, RainyDay }
internal enum StaffRole { Apprentice, Dresser, Cashier }

internal sealed record Persona(string Name, int Budget, int Markup, int MaxItems, string Blurb);

internal sealed record StaffInfo(string Name, int Fee, int Wage, int Rank, string Blurb);

internal sealed class Regular
{
    public string Name { get; set; } = "";
    public int Appearance { get; set; }
    public ToyFamily Family { get; set; }
    public Archetype Kind { get; set; }
    public int Affinity { get; set; }
    public int Visits { get; set; }
    public int LastVisitDay { get; set; }
    [JsonIgnore] public int Bond => Affinity >= 25 ? 2 : Affinity >= 10 ? 1 : 0;
    [JsonIgnore] public string BondName => Bond switch { 2 => "Devoted", 1 => "Friend", _ => "Newcomer" };
}

internal static class Progress
{
    public static readonly Persona[] Personas =
    [
        new("Parent", 100, 0, 3, "Buys a few for the kids"),
        new("Bargain hunter", 85, -30, 2, "Only buys at a fair price"),
        new("Tourist", 140, 15, 3, "Loves a souvenir"),
        new("Gift buyer", 110, 5, 2, "Shops by toy family"),
        new("Collector", 190, 35, 1, "Pays well for one rare toy")
    ];

    public static readonly StaffInfo[] Staff =
    [
        new("Apprentice", 70, 14, 1, "Keeps the bench running while you are away"),
        new("Window dresser", 55, 10, 1, "Adds appeal to the shop"),
        new("Cashier", 80, 12, 2, "Rings up shoppers much faster")
    ];

    public static readonly string[] Ranks =
        ["Stallholder", "Shopkeeper", "Craftsman", "Master maker", "Guild favourite", "Royal toymaker"];

    public static readonly int[] RankRenown = [0, 90, 260, 560, 1000, 1700];

    public static readonly string[] RankUnlocks =
    [
        "",
        "Hire staff · Collectors begin to visit",
        "Tier 3 patterns · Pick a shop style · Cashier",
        "Tier 4 patterns · Largest stockroom",
        "A bigger circle of regulars",
        "The whole of Alder Lane knows your name"
    ];

    public static readonly string[] StyleNames = ["Undecided", "Heirloom maker", "Volume merchant", "Showman"];

    public static readonly string[] StyleBlurbs =
    [
        "Pick a style once the shop is known.",
        "Rare toys fetch +25%. Tier 3 and 4 patterns turn out better.",
        "Materials cost 18% less and the shop draws a little more traffic. Shoppers haggle harder.",
        "Decor counts for half again as much. Fair days are livelier."
    ];

    public static string EventName(DayEvent kind) => kind switch
    {
        DayEvent.SchoolTrip => "School trip",
        DayEvent.RivalOpens => "Rival shop opens",
        DayEvent.SupplierSale => "Supplier sale",
        DayEvent.CollectorVisit => "Collector in town",
        DayEvent.RainyDay => "Rainy day",
        _ => ""
    };

    public static string EventBlurb(DayEvent kind) => kind switch
    {
        DayEvent.SchoolTrip => "More visitors today, and game toys sell fast.",
        DayEvent.RivalOpens => "Fewer visitors today. A good reputation softens the blow.",
        DayEvent.SupplierSale => "Materials cost a quarter less today. Stock up at the workshop.",
        DayEvent.CollectorVisit => "A wealthy collector will drop in with a long budget for one rare toy.",
        DayEvent.RainyDay => "Fewer shoppers, but those who come will pay a little more.",
        _ => ""
    };
}

internal sealed partial class Customer
{
    public Archetype Kind { get; set; }
    public int RegularId { get; set; } = -1;
    public string? Alias { get; set; }
    public int Mood { get; set; }
    [JsonIgnore] public Persona Persona => Progress.Personas[(int)Kind];
}

internal sealed partial class ShopState
{
    public int Renown { get; set; }
    public Philosophy Style { get; set; }
    public bool[] Hired { get; set; } = new bool[3];
    public List<Regular> Regulars { get; set; } = [];
    public DayEvent Event { get; set; }
    public bool EventGuestSent { get; set; }
    public int YearStartCash { get; set; }

    public event Action<string>? Announced;
    public event Action<int>? RankedUp;

    [JsonIgnore] public int Rank => Math.Max(0, Array.FindLastIndex(Progress.RankRenown, need => Renown >= need));
    [JsonIgnore] public bool TopRank => Rank >= Progress.RankRenown.Length - 1;
    [JsonIgnore] public int MaxTier => Rank < 2 ? 2 : Rank == 2 ? 3 : 4;
    [JsonIgnore] public int RegularCap => Math.Min(12, 2 + Rank * 2);
    [JsonIgnore] public int Wages => Enumerable.Range(0, Hired.Length).Where(i => Hired[i]).Sum(i => Progress.Staff[i].Wage);
    [JsonIgnore] public int YearDay => (Day - 1) % CampaignDays + 1;
    [JsonIgnore] public bool LastDayOfYear => YearDay == CampaignDays;
    [JsonIgnore] public int CheckoutWait => Hired[(int)StaffRole.Cashier] ? 3 : 7;

    [JsonIgnore] public int YearTarget => Year == 1 ? Goal :
        YearStartCash + (int)(Math.Round(1600 * Math.Pow(1.4, Year - 1) / 50) * 50);

    public double YearProgress => Year == 1 ? Cash / (double)Goal :
        (Cash - YearStartCash) / (double)Math.Max(1, YearTarget - YearStartCash);

    public double RankProgressFraction
    {
        get
        {
            if (TopRank)
                return 1;
            int from = Progress.RankRenown[Rank];
            return (Renown - from) / (double)(Progress.RankRenown[Rank + 1] - from);
        }
    }

    public void GainRenown(int amount)
    {
        if (amount <= 0)
            return;
        int before = Rank;
        Renown += amount;
        if (Rank > before)
        {
            Announce($"Rank up · {Progress.Ranks[Rank]}");
            Note(Progress.RankUnlocks[Rank]);
            RankedUp?.Invoke(Rank);
        }
    }

    public void Announce(string text)
    {
        Note(text);
        Announced?.Invoke(text);
    }

    public bool CanHire(int role) => !Hired[role] && Rank >= Progress.Staff[role].Rank &&
        Cash >= Progress.Staff[role].Fee;

    public bool Hire(int role)
    {
        if (!CanHire(role))
            return false;
        Spend(Progress.Staff[role].Fee);
        Hired[role] = true;
        Announce($"{Progress.Staff[role].Name} hired · ${Progress.Staff[role].Wage}/day");
        return true;
    }

    public bool Dismiss(int role)
    {
        if (!Hired[role])
            return false;
        Hired[role] = false;
        Note($"{Progress.Staff[role].Name} let go");
        return true;
    }

    public bool CanChooseStyle(Philosophy style) => style != Philosophy.None && style != Style && Rank >= 2 &&
        Shift != Shift.Open && Cash >= StyleCost;

    [JsonIgnore] public int StyleCost => Style == Philosophy.None ? 0 : 200;

    public bool ChooseStyle(Philosophy style)
    {
        if (!CanChooseStyle(style))
            return false;
        Spend(StyleCost);
        Style = style;
        Announce($"The shop is now a {Progress.StyleNames[(int)style].ToLowerInvariant()}");
        return true;
    }

    public int RegularLevel(Customer person) => person.RegularId >= 0 && person.RegularId < Regulars.Count
        ? Regulars[person.RegularId].Bond : 0;

    public int Tolerance(Customer person, int toy, bool wanted)
    {
        int tolerance = (wanted ? 145 : 112) + person.Persona.Markup;
        int tier = Catalog.Toys[toy].Tier;
        if (Style == Philosophy.Heirloom && tier >= 3)
            tolerance += 25;
        if (Style == Philosophy.Volume)
            tolerance -= 5;
        if (Event == DayEvent.RainyDay)
            tolerance += 8;
        return tolerance + RegularLevel(person) * 5;
    }

    /// <summary>Cheap shelves draw a crowd and greedy ones drive people past the door.</summary>
    public int PriceMood()
    {
        int total = 0;
        int count = 0;
        foreach (Display display in Displays)
        {
            if (display.Toy < 0 || display.Count == 0)
                continue;
            total += Catalog.Toys[display.Toy].Value * 100 / Math.Max(1, Prices[display.Toy]);
            count++;
        }
        return count == 0 ? 0 : Math.Clamp((total / count - 100) / 12, -3, 3);
    }

    private int CrowdBonus()
    {
        int crowd = (Fair ? 4 : 0) + Reputation / 20 + (Expanded ? 2 : 0) + Math.Min(9, Appeal) + PriceMood();
        crowd += Event switch
        {
            DayEvent.SchoolTrip => 6,
            DayEvent.RivalOpens => Reputation >= 70 ? -2 : -5,
            DayEvent.RainyDay => -3,
            _ => 0
        };
        if (Style == Philosophy.Volume)
            crowd += 2;
        if (Style == Philosophy.Showman && Fair)
            crowd += 3;
        return crowd;
    }

    private void RollEvent()
    {
        EventGuestSent = false;
        Event = DayEvent.None;
        if (Day <= 2)
            return;

        int roll = Roll(100);
        Event = roll switch
        {
            < 12 => DayEvent.SchoolTrip,
            < 22 => DayEvent.RivalOpens,
            < 34 => DayEvent.SupplierSale,
            < 44 when Rank >= 1 => DayEvent.CollectorVisit,
            < 56 => DayEvent.RainyDay,
            _ => DayEvent.None
        };
        if (Event != DayEvent.None)
            Note($"{Progress.EventName(Event)} · {Progress.EventBlurb(Event)}");
    }

    private Archetype PickArchetype()
    {
        int collector = Rank >= 1 && Reputation >= 58 ? 8 + (Fair ? 6 : 0) : 0;
        int[] weights = [38, 20, 12 + (Fair ? 14 : 0), 16, collector];
        int roll = Roll(weights.Sum());
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0)
                return (Archetype)i;
        }
        return Archetype.Parent;
    }

    private int PickRegular()
    {
        if (Regulars.Count == 0 || Roll(100) >= 22 + (Fair ? 10 : 0))
            return -1;
        int[] open = Enumerable.Range(0, Regulars.Count).Where(i => Regulars[i].LastVisitDay != Day).ToArray();
        return open.Length == 0 ? -1 : open[Roll(open.Length)];
    }

    private void RecruitRegular(Customer person)
    {
        if (person.RegularId >= 0 || Regulars.Count >= RegularCap || person.Mood < 0 ||
            Regulars.Any(regular => regular.Appearance == person.Appearance))
            return;
        if (Roll(100) >= 14 + (Fair ? 8 : 0) + Rank * 2)
            return;

        Regulars.Add(new Regular { Name = person.Name, Appearance = person.Appearance, Family = person.Family,
            Kind = person.Kind == Archetype.Collector ? Archetype.Gifter : person.Kind, Affinity = 4, Visits = 1, LastVisitDay = Day });
        person.RegularId = Regulars.Count - 1;
        person.Alias = person.Name;
        Announce($"{person.Name} is now a regular");
        GainRenown(4);
    }

    private void ChargeVisit(Customer person, int total)
    {
        int bargains = 0;
        int gouged = 0;
        foreach (BasketItem item in person.Basket)
        {
            int ratio = item.Price * 100 / Math.Max(1, Catalog.Toys[item.Toy].Value);
            if (ratio <= 100)
                bargains++;
            else if (ratio > 130)
                gouged++;
        }
        Today.Bargains += bargains;
        Today.Gouged += gouged;
        person.Mood = gouged > 0 ? -1 : bargains == person.Basket.Count ? 1 : 0;
        GainRenown(Math.Max(1, total / 10));

        if (person.RegularId >= 0 && person.RegularId < Regulars.Count)
        {
            Regular regular = Regulars[person.RegularId];
            int bondBefore = regular.Bond;
            regular.Affinity = Math.Max(0, regular.Affinity + 3 + person.Mood * 2);
            regular.Visits++;
            if (regular.Bond > bondBefore)
            {
                Announce($"{regular.Name} is now a {regular.BondName.ToLowerInvariant()}");
                GainRenown(6);
            }
            if (regular.Bond == 2 && person.Mood >= 0)
            {
                int tip = Math.Max(2, total / 6);
                Cash += tip;
                Today.OtherIncome += tip;
                Note($"{regular.Name} left a ${tip} tip");
            }
        }
        else
            RecruitRegular(person);
    }

    private void LoseRegular(Customer person)
    {
        if (person.RegularId < 0 || person.RegularId >= Regulars.Count)
            return;
        Regulars[person.RegularId].Affinity = Math.Max(0, Regulars[person.RegularId].Affinity - 2);
    }
}
