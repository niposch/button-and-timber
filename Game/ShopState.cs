namespace ButtonAndTimber;

internal enum Room { Shop, Workshop, Town }
internal enum Shift { Setup, Open, Closed }
internal enum Fixture { Display, Table, Register, Plant }

internal sealed record Toy(string Name, string Family, int Materials, int Value, int Time,
    int Research, int Prerequisite, int Practice, int Season);

internal static class Catalog
{
    public static readonly Toy[] Toys =
    [
        new("Pocket Bear", "Plush", 5, 15, 4, 0, -1, 0, 3),
        new("Pip Engine", "Woodwork", 7, 21, 5, 0, -1, 0, 0),
        new("Whirligig", "Woodwork", 3, 10, 3, 0, -1, 0, 1),
        new("Tin Comet", "Mechanics", 10, 30, 6, 34, 1, 6, 1),
        new("Dapple Hare", "Plush", 8, 24, 5, 26, 0, 5, 0),
        new("Orchard Checkers", "Games", 10, 29, 6, 40, 2, 6, 2),
        new("Sailcloth Sloop", "Woodwork", 13, 38, 7, 55, 1, 12, 1),
        new("Wind-up Finch", "Mechanics", 16, 46, 8, 68, 3, 7, 0),
        new("Patchwork Pony", "Plush", 13, 41, 7, 60, 4, 8, 2),
        new("Moonlight Theatre", "Games", 23, 64, 10, 94, 5, 9, 3)
    ];

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
    public int Capacity => Style switch { Fixture.Table => 7, Fixture.Display => 5, _ => 0 };
}

internal sealed class Order
{
    public int Toy { get; set; }
    public int Remaining { get; set; }
    public double Work { get; set; }
    public int Made { get; set; }
    public int Scrapped { get; set; }
    public int Step { get; set; }
    public int Grade { get; set; }
    public double PromptIn { get; set; } = .65;
}

internal sealed partial class Customer
{
    public int Appearance { get; set; }
    public int Wanted { get; set; }
    public int Budget { get; set; }
    public int Result { get; set; }
    public string Name => Names[Appearance % Names.Length];
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
    public bool ReducedMotion { get; set; }
    public int CampaignResult { get; set; }
    public int DeedSavings { get; set; }
    public bool[] Commissions { get; set; } = new bool[4];
    public uint Seed { get; set; } = 0x639dc755;
    public int[] Stock { get; set; } = [7, 6, 7, 0, 0, 0, 0, 0, 0, 0];
    public int[] Prices { get; set; } = Catalog.Toys.Select(toy => toy.Value).ToArray();
    public int[] Practice { get; set; } = new int[Catalog.Toys.Length];
    public bool[] Unlocked { get; set; } = [true, true, true, false, false, false, false, false, false, false];
    public List<Display> Displays { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
    public Till Today { get; set; } = new();
    public Till Yesterday { get; set; } = new();
    public List<string> Notes { get; set; } = [];

    public int Season => ((Day - 1) / 6) % 4;
    public int Year => (Day - 1) / CampaignDays + 1;
    public int Date => (Day - 1) % 6 + 1;
    public bool Fair => Date is 5 or 6;
    public int Width => Expanded ? 9 : 7;
    public int StockOnDisplay(int toy) => Displays.Where(display => display.Toy == toy).Sum(display => display.Count);
    public int ToyCost(int toy) => Math.Max(2, Catalog.Toys[toy].Materials - Level(toy) * 2);
    public int Level(int toy) => Math.Min(2, Practice[toy] / 8);
    public int Yield(int toy) => 91 - Catalog.Toys[toy].Research / 9 + Level(toy) * 4;
    public int CommissionToy => Catalog.CommissionToys[Season];
    public int CommissionCount => 4 + (Season is 1 or 2 ? 1 : 0);
    public int CommissionPay => Catalog.Toys[CommissionToy].Value * CommissionCount + 32;

    public bool CanResearch(int toy)
    {
        Toy recipe = Catalog.Toys[toy];
        return !Unlocked[toy] && recipe.Prerequisite >= 0 &&
            Practice[recipe.Prerequisite] >= recipe.Practice && Cash >= recipe.Research;
    }

    public bool Research(int toy)
    {
        if (!CanResearch(toy))
            return false;

        Spend(Catalog.Toys[toy].Research);
        Unlocked[toy] = true;
        Note($"Pattern learned · {Catalog.Toys[toy].Name}");
        return true;
    }

    public bool Queue(int toy, int quantity)
    {
        int cost = ToyCost(toy) * quantity;
        if (!Unlocked[toy] || quantity is < 1 or > 12 || cost > Cash || Orders.Count >= 4)
            return false;

        Spend(cost);
        Orders.Add(new Order { Toy = toy, Remaining = quantity });
        return true;
    }

    public void Assign(Display display, int toy)
    {
        if (display.Capacity == 0 || display.Toy == toy || (toy >= 0 && !Unlocked[toy]))
            return;

        if (display.Toy >= 0)
            Stock[display.Toy] += display.Count;

        display.Toy = toy;
        display.Count = 0;
        Restock(display);
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

        foreach (Customer customer in Customers.Where(person => person.Result == 0))
            foreach (BasketItem item in customer.Basket)
                Stock[item.Toy]++;
        Customers.Clear();
        Replenish();
        Spend(Upkeep);
        if (Today.Visitors > 0)
        {
            int change = (int)Math.Round((double)(Today.CustomersServed - Today.Missed) / Today.Visitors * 8);
            Reputation = Math.Clamp(Reputation + change, 15, 100);
        }

        if (Day == CampaignDays)
        {
            DeedSavings = Cash;
            CampaignResult = Cash >= Goal ? 1 : -1;
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
        }

        Yesterday = Today;
        Today = new Till();
        Minute = 0;
        ShopRemainder = 0;
        Shift = Shift.Setup;
        Note(Fair ? $"{Catalog.Fairs[Season]} is on today" : "A new day on Alder Lane");
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
