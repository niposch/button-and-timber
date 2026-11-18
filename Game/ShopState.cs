namespace ButtonAndTimber;

internal enum Room { Shop, Workshop, Town }
internal enum Shift { Setup, Open, Closed }
internal enum Fixture { Display, Table, Register, Plant, FlowerCrate, PennantStand, PumpkinCrate, SnowLantern }
internal enum ToyFamily { Trains, Plush, Motion, Games }
internal enum Station { Bench, Sewing, Assembly, Finishing }

internal sealed record Toy(string Name, ToyFamily Family, Station Station, int Materials, int Value,
    int Tier, int BaseYield, int Research, int Prerequisite, int RequiredLevel, params Station[] FollowUp);

internal static class Catalog
{
    public const int Engine = 1;
    public const int Whirligig = 2;
    public const int SignalCabin = 10;
    public const int TrainYard = 11;
    public const int FreightFleet = 12;
    public const int PebbleDice = 13;
    public const int MidnightExpress = 14;

    public static readonly Toy[] Toys =
    [
        new("Pocket Bear", ToyFamily.Plush, Station.Sewing, 5, 15, 1, 94, 0, -1, 0),
        new("Pip Engine", ToyFamily.Trains, Station.Bench, 7, 21, 1, 93, 0, -1, 0),
        new("Whirligig", ToyFamily.Motion, Station.Finishing, 3, 10, 1, 97, 0, -1, 0),
        new("Tin Comet", ToyFamily.Motion, Station.Assembly, 11, 32, 2, 81, 32, Whirligig, 2, Station.Finishing),
        new("Dapple Hare", ToyFamily.Plush, Station.Sewing, 8, 24, 2, 86, 26, 0, 2),
        new("Orchard Checkers", ToyFamily.Games, Station.Bench, 10, 30, 2, 83, 34, PebbleDice, 2, Station.Finishing),
        new("Sailcloth Sloop", ToyFamily.Motion, Station.Bench, 13, 38, 2, 77, 49, Whirligig, 3),
        new("Wind-up Finch", ToyFamily.Motion, Station.Assembly, 17, 55, 3, 68, 65, 3, 3, Station.Finishing),
        new("Patchwork Pony", ToyFamily.Plush, Station.Sewing, 15, 47, 3, 71, 58, 4, 3, Station.Finishing),
        new("Moonlight Theatre", ToyFamily.Games, Station.Bench, 24, 72, 3, 63, 85, 5, 3, Station.Assembly, Station.Finishing),
        new("Signal Cabin", ToyFamily.Trains, Station.Bench, 12, 37, 2, 82, 34, Engine, 2, Station.Finishing),
        new("Alder Train Yard", ToyFamily.Trains, Station.Bench, 24, 79, 3, 60, 85, SignalCabin, 3, Station.Assembly, Station.Finishing),
        new("Freight Fleet", ToyFamily.Trains, Station.Bench, 15, 44, 2, 76, 48, Engine, 3, Station.Assembly),
        new("Pebble Dice", ToyFamily.Games, Station.Finishing, 4, 13, 1, 96, 0, -1, 0),
        new("Midnight Express", ToyFamily.Trains, Station.Bench, 36, 128, 4, 54, 135, TrainYard, 4, Station.Assembly, Station.Finishing),
        new("Picnic Coach", ToyFamily.Trains, Station.Bench, 22, 66, 3, 72, 74, FreightFleet, 3, Station.Finishing),
        new("Button Kitten", ToyFamily.Plush, Station.Sewing, 10, 30, 2, 86, 34, 0, 3),
        new("Starling Teddy", ToyFamily.Plush, Station.Sewing, 31, 105, 4, 58, 110, 8, 4, Station.Assembly, Station.Finishing),
        new("Paper Glider", ToyFamily.Motion, Station.Finishing, 15, 50, 3, 77, 61, 6, 3, Station.Assembly),
        new("Starlight Carousel", ToyFamily.Motion, Station.Bench, 34, 122, 4, 55, 136, 7, 4, Station.Assembly, Station.Finishing),
        new("Marble Maze", ToyFamily.Games, Station.Bench, 18, 62, 3, 73, 67, 5, 3, Station.Assembly),
        new("Carnival Arcade", ToyFamily.Games, Station.Bench, 34, 121, 4, 56, 132, 20, 4, Station.Assembly, Station.Finishing)
    ];

    public static readonly ToyFamily[] Families = Enum.GetValues<ToyFamily>();
    public static readonly ToyFamily[] SeasonalDemand = [ToyFamily.Trains, ToyFamily.Motion, ToyFamily.Games, ToyFamily.Plush];
    public static readonly int[] FamilyIcons = [Engine, 0, Whirligig, PebbleDice];
    public static readonly string[] Seasons = ["Spring", "Summer", "Autumn", "Winter"];
    public static readonly string[] Fairs = ["Kite Sunday", "Harbour Fair", "Orchard Market", "Lantern Walk"];
    public static readonly int[] CommissionToys = [1, 3, 5, 0];
}

internal sealed class Display
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Toy { get; set; } = -1;
    public int Count { get; set; }
    public Fixture Style { get; set; }
    public int Turn { get; set; }
    public int Capacity => Style switch { Fixture.Table => 7, Fixture.Display => 5, _ => 0 };
}

internal sealed class Order
{
    public int Toy { get; set; }
    public Fixture? Decoration { get; set; }
    public int Remaining { get; set; }
    public int Quantity { get; set; }
    public int Stage { get; set; }
    public int StageLeft { get; set; }
    public double Transfer { get; set; }
    public bool SinglePass { get; set; }
    public double Work { get; set; }
    public int Made { get; set; }
    public int Scrapped { get; set; }
    public int Step { get; set; }
    public int Grade { get; set; }
    public double PromptIn { get; set; } = .65;
    public Tool[] Plan { get; set; } = [];
}

internal sealed partial class Customer
{
    public int Appearance { get; set; }
    public int Wanted { get; set; }
    public int Budget { get; set; }
    public int Result { get; set; }
    public string Name => Alias ?? Names[Appearance % Names.Length];
    private static readonly string[] Names =
        ["Elsie", "Milo", "Ada", "Nico", "June", "Ollie", "Bea", "Robin", "Kit", "Nell", "Sam", "Lou"];
}

internal sealed class Till
{
    public int Visitors { get; set; }
    public int Sold { get; set; }
    public int CustomersServed { get; set; }
    public int Revenue { get; set; }
    public int Spent { get; set; }
    public int OtherIncome { get; set; }
    public int Missed { get; set; }
    public int Bargains { get; set; }
    public int Gouged { get; set; }
    public int[] Requests { get; set; } = new int[Catalog.Toys.Length];
    public int Net => Revenue + OtherIncome - Spent;
}

internal sealed partial class ShopState
{
    public const int Goal = 1750;
    public const int CampaignDays = 24;
    public const int ShopHours = 540;
    public const int Upkeep = 16;
    public const int Permit = 170;
    public int Schema { get; set; }
    public int Cash { get; set; } = 150;
    public int Day { get; set; } = 1;
    public int Reputation { get; set; } = 52;
    public Shift Shift { get; set; }
    public double Minute { get; set; }
    public double NextVisitor { get; set; }
    public bool Expanded { get; set; }
    public bool Muted { get; set; }
    public bool MusicOff { get; set; }
    public bool ReducedMotion { get; set; }
    public int CampaignResult { get; set; }
    public int DeedSavings { get; set; }
    public bool[] Commissions { get; set; } = new bool[4];
    public uint Seed { get; set; } = 0x639dc755;
    public int[] Stock { get; set; } = NewStock();
    public int[] Prices { get; set; } = Catalog.Toys.Select(toy => toy.Value).ToArray();
    public int[] Practice { get; set; } = new int[Catalog.Toys.Length];
    public bool[] Unlocked { get; set; } = Catalog.Toys.Select(toy => toy.Prerequisite < 0).ToArray();
    public List<Display> Displays { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
    public Till Today { get; set; } = new();
    public Till Yesterday { get; set; } = new();
    public List<string> Notes { get; set; } = [];

    private static int[] NewStock()
    {
        int[] stock = new int[Catalog.Toys.Length];
        stock[0] = 7;
        stock[Catalog.Engine] = 6;
        stock[Catalog.Whirligig] = 7;
        stock[Catalog.PebbleDice] = 6;
        return stock;
    }

    public int Season => ((Day - 1) / 6) % 4;
    public int Year => (Day - 1) / CampaignDays + 1;
    public int Date => (Day - 1) % 6 + 1;
    public bool Fair => Date is 5 or 6;
    public int Width => Expanded ? 9 : 7;
    public int StockOnDisplay(int toy) => Displays.Where(display => display.Toy == toy).Sum(display => display.Count);
    private static readonly int[] LevelExperience = [0, 4, 12, 23, 38];
    public int ToyCost(int toy) => Math.Max(2, (int)Math.Round(Catalog.Toys[toy].Materials * (1 - (Level(toy) - 1) * .09) *
        (Style == Philosophy.Volume ? .82 : 1) * (Event == DayEvent.SupplierSale ? .75 : 1)));
    public int Level(int toy) => Array.FindLastIndex(LevelExperience, xp => Practice[toy] >= xp) + 1;
    public int Yield(int toy) => Math.Min(99, Catalog.Toys[toy].BaseYield + (Level(toy) - 1) * 8 + UpgradeLevel(Upgrades.SharpTools) * 3 +
        (Style == Philosophy.Heirloom && Catalog.Toys[toy].Tier >= 3 ? 4 : 0));
    public double MasteryProgress(int toy)
    {
        int level = Level(toy);
        return level == LevelExperience.Length ? 1 :
            (Practice[toy] - LevelExperience[level - 1]) / (double)(LevelExperience[level] - LevelExperience[level - 1]);
    }
    public int CommissionToy => Catalog.CommissionToys[Season];
    public int CommissionCount => 4 + (Season is 1 or 2 ? 1 : 0);
    public int CommissionPay => Catalog.Toys[CommissionToy].Value * CommissionCount + 32;

    public bool CanResearch(int toy)
    {
        Toy recipe = Catalog.Toys[toy];
        return MeetsPrerequisite(toy) && recipe.Tier <= MaxTier && Cash >= ResearchCost(toy);
    }

    public bool MeetsPrerequisite(int toy)
    {
        Toy recipe = Catalog.Toys[toy];
        return !Unlocked[toy] && recipe.Prerequisite >= 0 && Unlocked[recipe.Prerequisite] &&
            Level(recipe.Prerequisite) >= recipe.RequiredLevel;
    }

    public bool Research(int toy)
    {
        if (!CanResearch(toy))
            return false;

        Spend(ResearchCost(toy));
        Unlocked[toy] = true;
        Announce($"Pattern learned · {Catalog.Toys[toy].Name}");
        GainRenown(5 + Catalog.Toys[toy].Tier * 2);
        return true;
    }

    public bool Queue(int toy, int quantity)
    {
        if (!Unlocked[toy])
            return false;
        return QueueProject(new Order { Toy = toy }, quantity, ToyCost(toy));
    }

    public bool Assign(Display display, int toy)
    {
        if (display.Capacity == 0 || (toy >= 0 && !Unlocked[toy]))
            return false;
        if (display.Toy == toy)
            return true;
        int incoming = toy < 0 ? 0 : Math.Min(display.Capacity, Stock[toy]);
        if (StorageUsed + display.Count - incoming > StorageCapacity && display.Count > incoming)
            return false;

        if (display.Toy >= 0)
            Stock[display.Toy] += display.Count;

        display.Toy = toy;
        display.Count = 0;
        Restock(display);
        return true;
    }

    public int Restock(Display display)
    {
        if (display.Toy < 0)
            return 0;

        int transfer = Math.Min(display.Capacity - display.Count, Stock[display.Toy]);
        Stock[display.Toy] -= transfer;
        display.Count += transfer;
        return transfer;
    }

    public void Replenish()
    {
        foreach (Display display in Displays)
            Restock(display);
    }

    public bool Expand()
    {
        if (Expanded || Shift == Shift.Open || Cash < Permit)
            return false;

        Spend(Permit);
        Expanded = true;
        Note("The adjoining room is ready to furnish");
        return true;
    }

    public bool Deliver()
    {
        int toy = CommissionToy;
        if (Commissions[Season] || Stock[toy] < CommissionCount)
            return false;

        Stock[toy] -= CommissionCount;
        Cash += CommissionPay;
        Today.OtherIncome += CommissionPay;
        Reputation = Math.Min(100, Reputation + 6);
        GainRenown(12);
        Commissions[Season] = true;
        Note($"Town order delivered · ${CommissionPay}");
        return true;
    }

    public bool Open()
    {
        if (Shift != Shift.Setup || Register == null || !Displays.Any(display => display.Count > 0))
            return false;

        NextVisitor = 5;
        Shift = Shift.Open;
        Note("The bell is ringing. Doors are open.");
        return true;
    }

    public void Close()
    {
        if (Shift != Shift.Open)
            return;

        foreach (Customer customer in Customers)
        {
            if (customer.Result == 0)
                foreach (BasketItem item in customer.Basket)
                    Stock[item.Toy]++;
            if (customer.StreetDirection != 0)
                RejoinSidewalk(customer);
        }
        Customers.Clear();
        Replenish();
        Spend(DailyRent);
        if (Wages > 0)
            Spend(Wages);
        if (Today.Visitors > 0)
        {
            int change = (int)Math.Round((double)(Today.CustomersServed - Today.Missed) / Today.Visitors * 8);
            int pricing = Math.Clamp((Today.Bargains - 2 * Today.Gouged) * 3 / Math.Max(1, Today.CustomersServed), -4, 3);
            Reputation = Math.Clamp(Reputation + change + pricing + Math.Min(2, Appeal / 3), 15, 100);
        }
        GainRenown(Reputation / 12 + Today.CustomersServed / 2);

        if (LastDayOfYear)
        {
            DeedSavings = Cash;
            CampaignResult = Cash >= YearTarget ? 1 : -1;
            if (CampaignResult == 1)
            {
                Announce(Year == 1 ? "The shop deed is yours" : $"Year {Year} target met");
                GainRenown(40 + Year * 10);
            }
        }

        Shift = Shift.Closed;
        Note($"Till closed · {Today.Sold} sold · net {Signed(Today.Net)}");
    }

    public void Tomorrow()
    {
        if (Shift != Shift.Closed)
            return;

        Day++;
        if (Date == 1 && Season == 0)
        {
            Commissions = new bool[4];
            SideOrders = new int[4];
            YearStartCash = Cash;
        }

        Yesterday = Today;
        Today = new Till();
        Minute = 0;
        Shift = Shift.Setup;
        Note(Fair ? $"{Catalog.Fairs[Season]} is on today" : "A new day on Alder Lane");
        RollEvent();
        RefreshBoard();
        foreach (Regular regular in Regulars)
            if (regular.LastVisitDay < Day - 6)
                regular.Affinity = Math.Max(0, regular.Affinity - 1);
    }

    public void Note(string text)
    {
        Notes.Insert(0, text);
        if (Notes.Count > 24)
            Notes.RemoveAt(24);
    }

    public static string Signed(int amount) => amount < 0 ? $"−${-amount}" : $"+${amount}";

    private int Roll(int max)
    {
        Seed = unchecked(Seed * 1664525 + 1013904223);
        return (int)((Seed >> 8) % (uint)max);
    }

    private void Spend(int amount)
    {
        Cash -= amount;
        Today.Spent += amount;
    }
}
