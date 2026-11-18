namespace ButtonAndTimber;

internal enum Room { Shop, Workshop, Town }
internal enum Shift { Setup, Open, Closed }
internal enum Fixture { Display, Table }

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
    public int Capacity => Style == Fixture.Table ? 7 : 5;
}

internal sealed class Order
{
    public int Toy { get; set; }
    public int Remaining { get; set; }
    public double Work { get; set; }
    public int Made { get; set; }
    public int Scrapped { get; set; }
}

internal sealed class Customer
{
    public int Appearance { get; set; }
    public int Wanted { get; set; }
    public int Budget { get; set; }
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    public double Age { get; set; }
    public int Result { get; set; }
    public string Name => Names[Appearance % Names.Length];
    private static readonly string[] Names =
        ["Elsie", "Milo", "Ada", "Nico", "June", "Ollie", "Bea", "Robin", "Kit", "Nell", "Sam", "Lou"];
}

internal sealed class Till
{
    public int Visitors { get; set; }
    public int Sold { get; set; }
    public int Revenue { get; set; }
    public int Spent { get; set; }
    public int OtherIncome { get; set; }
    public int Missed { get; set; }
    public int[] Requests { get; set; } = new int[Catalog.Toys.Length];
    public int Net => Revenue + OtherIncome - Spent;
}

internal sealed class ShopState
{
    public const int Goal = 1750;
    public const int CampaignDays = 24;
    public const int ShopHours = 540;
    public const int Upkeep = 16;
    public const int Permit = 170;
    public const int TablePrice = 75;

    public int Cash { get; set; } = 115;
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
    public List<Display> Displays { get; set; } =
    [
        new() { X = 1, Y = 2, Toy = 0, Count = 3 },
        new() { X = 4, Y = 1, Toy = 1, Count = 3 },
        new() { X = 3, Y = 4, Toy = 2, Count = 3, Style = Fixture.Table }
    ];
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

    public void AdvanceBench(double seconds)
    {
        while (Orders.Count > 0 && seconds > 0)
        {
            Order order = Orders[0];
            int toy = order.Toy;
            int workPerUnit = Catalog.Toys[toy].Time;
            double work = Math.Min(seconds, workPerUnit - order.Work);
            order.Work += work;
            seconds -= work;
            if (order.Work < workPerUnit - 0.00001)
                break;

            order.Work = 0;
            order.Remaining--;
            if (Roll(100) < Yield(toy))
            {
                Stock[toy]++;
                order.Made++;
            }
            else
            {
                order.Scrapped++;
            }

            Practice[toy]++;
            if (order.Remaining > 0)
                continue;

            string scrap = order.Scrapped > 0 ? $" · {order.Scrapped} scrapped" : "";
            Note($"Bench finished · {order.Made} {Catalog.Toys[toy].Name}{scrap}");
            Orders.RemoveAt(0);
        }
    }

    public void Assign(Display display, int toy)
    {
        if (display.Toy == toy || (toy >= 0 && !Unlocked[toy]))
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

    public void RestockAll()
    {
        int moved = Displays.Sum(Restock);
        Note(moved > 0 ? $"Shelves filled · {moved} toys" : "No spare stock for these displays");
    }

    public bool CanPlace(int x, int y) =>
        x >= 1 && x < Width - 1 && y >= 1 && y < 5 &&
        (x != Width - 2 || y != 1) &&
        !Displays.Any(display => display.X == x && display.Y == y);

    public bool Move(Display display, int x, int y)
    {
        if (Shift == Shift.Open || !CanPlace(x, y))
            return false;

        display.X = x;
        display.Y = y;
        return true;
    }

    public bool BuyTable()
    {
        if (Shift == Shift.Open || Cash < TablePrice || Displays.Count >= (Expanded ? 7 : 4))
            return false;

        for (int y = 1; y < 5; y++)
        for (int x = Width - 2; x >= 1; x--)
        {
            if (!CanPlace(x, y))
                continue;

            Spend(TablePrice);
            Displays.Add(new Display { X = x, Y = y, Style = Fixture.Table });
            return true;
        }

        return false;
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

    public void Open()
    {
        if (Shift != Shift.Setup)
            return;

        NextVisitor = 7;
        Shift = Shift.Open;
        Note("The bell is ringing. Doors are open.");
    }

    public void AdvanceShop(double minutes)
    {
        if (Shift != Shift.Open)
            return;

        double elapsed = Math.Min(minutes, ShopHours - Minute);
        Minute += elapsed;
        foreach (Customer customer in Customers)
        {
            customer.Age += elapsed;
            if (customer.Age >= 30 && customer.Result == 0)
                FinishVisit(customer);
        }

        Customers.RemoveAll(customer => customer.Age >= 52);
        if (Minute >= NextVisitor && Minute < ShopHours - 28)
        {
            Visit();
            int crowd = (Fair ? 5 : 0) + Reputation / 15 + (Expanded ? 3 : 0);
            NextVisitor = Minute + 34 - crowd + Roll(11) - 5;
        }

        if (Minute >= ShopHours)
            Close();
    }

    public void Close()
    {
        if (Shift != Shift.Open)
            return;

        foreach (Customer customer in Customers.Where(customer => customer.Result == 0))
            FinishVisit(customer);

        Customers.Clear();
        Spend(Upkeep);
        if (Today.Visitors > 0)
        {
            int change = (int)Math.Round((double)(Today.Sold - Today.Missed) / Today.Visitors * 8);
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
            Commissions = new bool[4];

        Yesterday = Today;
        Today = new Till();
        Minute = 0;
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

    private void Visit()
    {
        int appearance = Roll(48);
        List<int> weighted = [];
        for (int toy = 0; toy < Catalog.Toys.Length; toy++)
        {
            int weight = Unlocked[toy] ? 5 : Day < 3 ? 0 : 1;
            if (Catalog.Toys[toy].Season == Season && weight > 0)
                weight += Fair ? 5 : 2;
            for (int i = 0; i < weight; i++)
                weighted.Add(toy);
        }

        int wanted = weighted[Roll(weighted.Count)];
        Display target = Displays.FirstOrDefault(display => display.Toy == wanted && display.Count > 0) ??
            Displays[Roll(Displays.Count)];
        Customers.Add(new Customer
        {
            Appearance = appearance,
            Wanted = wanted,
            Budget = Catalog.Toys[wanted].Value + 2 + Roll(4 + Catalog.Toys[wanted].Value / 3),
            TargetX = target.X,
            TargetY = target.Y
        });
        Today.Visitors++;
    }

    private void FinishVisit(Customer customer)
    {
        Display? choice = Displays
            .Where(display => display.Count > 0 && Prices[display.Toy] <= customer.Budget)
            .OrderByDescending(display => display.Toy == customer.Wanted)
            .ThenBy(display => Prices[display.Toy])
            .FirstOrDefault();

        bool wanted = choice?.Toy == customer.Wanted;
        if (choice != null && (wanted || Roll(100) < (Fair ? 51 : 35)))
        {
            int price = Prices[choice.Toy];
            Cash += price;
            Today.Revenue += price;
            Today.Sold++;
            choice.Count--;
            customer.Result = 1;
            Note($"{customer.Name} bought {Catalog.Toys[choice.Toy].Name} · +${price}");
        }
        else
        {
            Today.Missed++;
            customer.Result = -1;
            Note($"{customer.Name} left looking for {Catalog.Toys[customer.Wanted].Name}");
        }

        if (!wanted)
            Today.Requests[customer.Wanted]++;
    }

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
