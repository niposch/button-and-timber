using System.Text.Json.Serialization;

namespace ButtonAndTimber;

internal readonly record struct Cell(int X, int Y)
{
    public IEnumerable<Cell> Neighbors()
    {
        yield return this with { Y = Y + 1 };
        yield return this with { X = X + 1 };
        yield return this with { X = X - 1 };
        yield return this with { Y = Y - 1 };
    }
}

internal enum VisitPhase { Browse, Checkout, Leave, Approach, Depart }
internal sealed record BasketItem(int Toy, int Price);

internal sealed partial class Customer
{
    public bool WantsExact { get; set; }
    [JsonIgnore] public ToyFamily Family => Catalog.Toys[Wanted].Family;
    public Cell Position { get; set; }
    public Cell Target { get; set; }
    public Cell? DisplayCell { get; set; }
    public List<Cell> Seen { get; set; } = [];
    public List<Cell> Route { get; set; } = [];
    public List<BasketItem> Basket { get; set; } = [];
    public VisitPhase Phase { get; set; }
    public double Travel { get; set; }
    public int Wait { get; set; }
    public int Ticket { get; set; }
    [JsonIgnore] public bool Walking => Route.Count > 0;
}

internal sealed partial class ShopState
{
    public const int CurrentSchema = 4;
    public int[] Furniture { get; set; } = [2, 1, 1, 0, 0, 0, 0, 0];
    public double ShopRemainder { get; set; }
    public int LastTicket { get; set; }
    public int[] SideOrders { get; set; } = new int[4];
    [JsonIgnore] public Display? Register => Displays.Find(display => display.Style == Fixture.Register);
    [JsonIgnore] public Cell Entry => new(Width - 1, 6);
    [JsonIgnore] public Customer? ReadyToPay => Customers.FirstOrDefault(person => person.Phase == VisitPhase.Checkout &&
        !person.Walking && person.Position == CheckoutSpot && QueuePosition(person) == 0);
    public int Forecast(ToyFamily family)
    {
        ToyFamily favourite = Catalog.SeasonalDemand[(Season + (Date - 1) / 3) % Catalog.Families.Length];
        ToyFamily second = Catalog.SeasonalDemand[(Season + (Date - 1) / 2 + 1) % Catalog.Families.Length];
        int expected = family == favourite ? (Fair ? 4 : 3) : family == second ? 2 : 1;
        if (Event == DayEvent.SchoolTrip && family == ToyFamily.Games)
            expected += 3;
        return expected + DecorDemand(family);
    }

    public int Shortages(Till report, ToyFamily family) => Catalog.Toys.Select((toy, index) => (toy, index))
        .Where(entry => entry.toy.Family == family).Sum(entry => report.Requests[entry.index]);

    public bool StocksFamily(ToyFamily family) => Displays.Any(display =>
        display.Toy >= 0 && display.Count > 0 && Catalog.Toys[display.Toy].Family == family);

    public static bool Matches(Customer person, int toy) => person.Wanted == toy ||
        (!person.WantsExact && Catalog.Toys[toy].Family == person.Family);

    public event Action<int>? CheckedOut;

    public static string FixtureName(Fixture fixture) => fixture switch
    {
        Fixture.Display => "Shelf", Fixture.Table => "Table", Fixture.Register => "Register", _ => Decorations.Get(fixture).Name
    };

    public static int FurnitureCost(Fixture fixture) => fixture switch
    {
        Fixture.Display => 38, Fixture.Table => 56, Fixture.Register => 0, _ => Decorations.Get(fixture).BuyPrice
    };

    public void UpgradeSave()
    {
        InitializeTraffic();
        if (Year > 1 && YearStartCash == 0)
            YearStartCash = Cash;
        if (Schema >= CurrentSchema)
            return;
        if (Schema == 3)
        {
            Schema = CurrentSchema;
            return;
        }

        int count = Catalog.Toys.Length;
        if (Unlocked.Length < count || Stock.Length < count || Prices.Length < count || Practice.Length < count)
        {
            int[] stock = Stock;
            int[] experience = Practice;
            int[] prices = Prices;
            bool[] unlocked = Unlocked;
            int firstNew = prices.Length;
            Array.Resize(ref stock, count);
            Array.Resize(ref experience, count);
            Array.Resize(ref prices, count);
            Array.Resize(ref unlocked, count);
            for (int i = firstNew; i < count; i++)
                prices[i] = Catalog.Toys[i].Value;

            Stock = stock;
            Practice = experience;
            Prices = prices;
            Unlocked = unlocked;
        }
        if (!Unlocked[Catalog.PebbleDice])
        {
            Stock[Catalog.PebbleDice] = 5;
            Unlocked[Catalog.PebbleDice] = true;
        }

        int[] furniture = Furniture;
        Array.Resize(ref furniture, Enum.GetValues<Fixture>().Length);
        Furniture = furniture;
        if (Schema < 2)
            for (int i = 0; i < Practice.Length; i++)
                Practice[i] *= 2;
        ResizeRequests(Today);
        ResizeRequests(Yesterday);
        foreach (Order order in Orders)
        {
            order.Quantity = order.Made + order.Scrapped + order.Remaining;
            order.StageLeft = order.Remaining;
            order.SinglePass = true;
            if (order.Plan.Length == 0)
                PrepareWave(order);
            order.Step = Math.Min(order.Step, order.Plan.Length - 1);
            order.Work = Math.Clamp(order.Work, 0, ToolWindow(order) - .02);
        }

        if (Schema == 0)
        {
            if (Displays.Count > 0 || Shift == Shift.Open)
            {
                if (Register == null)
                    Displays.Add(new Display { X = Width - 2, Y = 1, Style = Fixture.Register });
                foreach (Fixture kind in Enum.GetValues<Fixture>())
                    Furniture[(int)kind] = Math.Max(0, Furniture[(int)kind] - Displays.Count(display => display.Style == kind));
            }

            Customers.RemoveAll(person => person.Result != 0);
            Today.CustomersServed = Today.Sold;
            foreach (Customer person in Customers)
            {
                person.Position = Entry;
                BrowseNext(person);
            }
        }

        Schema = CurrentSchema;
        Replenish();

        static void ResizeRequests(Till report)
        {
            int[] requests = report.Requests;
            Array.Resize(ref requests, Catalog.Toys.Length);
            report.Requests = requests;
        }
    }

    public bool CanPlace(Cell tile, Display? moving = null)
    {
        if (tile.X < 1 || tile.X >= Width - 1 || tile.Y is < 1 or > 4)
            return false;

        HashSet<Cell> blocked = Displays.Where(display => display != moving).Select(At).ToHashSet();
        if (!blocked.Add(tile))
            return false;

        Dictionary<Cell, Cell> walkable = Reachable(Entry, blocked);
        if (Register is Display till && till != moving)
        {
            Cell front = FrontOf(till);
            HashSet<Cell> before = Displays.Where(display => display != moving).Select(At).ToHashSet();
            if (Reachable(Entry, before).ContainsKey(front) && !walkable.ContainsKey(front))
                return false;
        }
        return blocked.All(place => place.Neighbors().Any(walkable.ContainsKey));
    }

    public Display? PlaceFixture(Fixture fixture, Cell tile, int turn = 0)
    {
        int price = FurnitureCost(fixture);
        bool owned = Furniture[(int)fixture] > 0;
        if (Shift == Shift.Open || !CanPlace(tile) || (fixture == Fixture.Register && Register != null) ||
            (!owned && Cash < price))
            return null;

        if (owned)
            Furniture[(int)fixture]--;
        else
            Spend(price);

        Display item = new() { X = tile.X, Y = tile.Y, Style = fixture, Turn = ResolveTurn(fixture, tile, turn, null) };
        Displays.Add(item);
        return item;
    }

    public bool Move(Display display, Cell tile, int? turn = null)
    {
        if (Shift == Shift.Open || !CanPlace(tile, display))
            return false;
        display.Turn = ResolveTurn(display.Style, tile, turn ?? display.Turn, display);
        display.X = tile.X;
        display.Y = tile.Y;
        return true;
    }

    private static readonly Cell[] Fronts = [new(0, 1), new(-1, 0), new(0, -1), new(1, 0)];

    /// <summary>The tile a register faces, where shoppers queue and pay.</summary>
    public static Cell FrontOf(Display display) =>
        new(display.X + Fronts[display.Turn].X, display.Y + Fronts[display.Turn].Y);

    private bool FrontWalkable(Cell tile, int turn, Display? moving)
    {
        Cell front = new(tile.X + Fronts[turn].X, tile.Y + Fronts[turn].Y);
        HashSet<Cell> blocked = Displays.Where(display => display != moving).Select(At).ToHashSet();
        blocked.Add(tile);
        return Reachable(Entry, blocked).ContainsKey(front);
    }

    /// <summary>Registers must face a tile shoppers can reach, so pick the nearest turn that does.</summary>
    public int ResolveTurn(Fixture fixture, Cell tile, int turn, Display? moving)
    {
        turn = (turn % 4 + 4) % 4;
        if (fixture != Fixture.Register)
            return turn;
        for (int i = 0; i < 4; i++)
            if (FrontWalkable(tile, (turn + i) % 4, moving))
                return (turn + i) % 4;
        return turn;
    }

    public bool TurnFixture(Display display, int step)
    {
        if (Shift == Shift.Open)
            return false;
        for (int i = 1; i <= 4; i++)
        {
            int turn = ((display.Turn + step * i) % 4 + 4) % 4;
            if (display.Style == Fixture.Register && !FrontWalkable(At(display), turn, display))
                continue;
            if (turn == display.Turn)
                return false;
            display.Turn = turn;
            return true;
        }
        return false;
    }

    public bool Pack(Display display)
    {
        if (Shift == Shift.Open || (display.Count > 0 && StorageUsed + display.Count > StorageCapacity) ||
            !Displays.Remove(display))
            return false;
        if (display.Toy >= 0)
            Stock[display.Toy] += display.Count;
        Furniture[(int)display.Style]++;
        Replenish();
        return true;
    }

    public (string Name, int Toy, int Count, int Pay) ExtraOrder(int index)
    {
        int[] toys = Season switch
        {
            0 => [0, Catalog.TrainYard], 1 => [2, 7], 2 => [1, 9], _ => [3, 8]
        };
        int toy = toys[index];
        int count = index == 0 ? 3 : 2;
        return (index == 0 ? "Library playgroup" : "Showcase order", toy, count,
            Catalog.Toys[toy].Value * count + (index == 0 ? 18 : 50));
    }

    public bool CanDeliverExtra(int index)
    {
        var request = ExtraOrder(index);
        return (SideOrders[Season] & (1 << index)) == 0 && (index == 0 || Reputation >= 58) &&
            Stock[request.Toy] >= request.Count;
    }

    public bool DeliverExtra(int index)
    {
        if (!CanDeliverExtra(index))
            return false;
        var request = ExtraOrder(index);
        Stock[request.Toy] -= request.Count;
        SideOrders[Season] |= 1 << index;
        Cash += request.Pay;
        Today.OtherIncome += request.Pay;
        Reputation = Math.Min(100, Reputation + 4 + index * 3);
        GainRenown(5 + index * 4);
        Note($"{request.Name} delivered · +${request.Pay}");
        return true;
    }

    public void AdvanceShop(double minutes)
    {
        InitializeTraffic();
        ShopRemainder += minutes;
        while (ShopRemainder >= 1)
        {
            ShopRemainder--;
            TickStreet();
            if (Shift != Shift.Open)
                continue;
            Minute = Math.Min(Minute + 1, ShopHours);
            foreach (Customer person in Customers)
                AdvanceVisit(person);

            Customers.RemoveAll(person => person.Phase == VisitPhase.Depart && !person.Walking);
            if (Minute >= NextVisitor && Minute < ShopHours - 45 && FindPassingShopper() is Pedestrian passing)
            {
                Visit(passing);
                NextVisitor = Minute + Math.Max(7, 26 - CrowdBonus() + Roll(7) - 3);
            }
            if (Minute >= ShopHours && Customers.Count == 0)
                Close();
        }
    }

    public bool RingUp()
    {
        if (ReadyToPay is not Customer person)
            return false;

        int total = person.Basket.Sum(item => item.Price);
        Cash += total;
        Today.Revenue += total;
        ChargeVisit(person, total);
        Today.Sold += person.Basket.Count;
        Today.CustomersServed++;
        person.Result = 1;
        Note($"{person.Name} bought {string.Join(" & ", person.Basket.Select(item => Catalog.Toys[item.Toy].Name))} · +${total}");
        Exit(person);
        CheckedOut?.Invoke(total);
        return true;
    }

    private static Cell At(Display display) => new(display.X, display.Y);
    private bool Walkable(Cell cell, HashSet<Cell> blocked) =>
        cell == Entry || (cell.X >= 0 && cell.X < Width && cell.Y is >= 1 and <= 5 && !blocked.Contains(cell));

    private Dictionary<Cell, Cell> Reachable(Cell start, HashSet<Cell> blocked)
    {
        Dictionary<Cell, Cell> parents = new() { [start] = start };
        Queue<Cell> frontier = new();
        frontier.Enqueue(start);
        while (frontier.TryDequeue(out Cell cell))
        {
            foreach (Cell next in cell.Neighbors())
                if (Walkable(next, blocked) && parents.TryAdd(next, cell))
                    frontier.Enqueue(next);
        }
        return parents;
    }

    private Dictionary<Cell, Cell> Paths(Cell start) => Reachable(start, Displays.Select(At).ToHashSet());

    private static List<Cell> Route(Dictionary<Cell, Cell> parents, Cell goal)
    {
        List<Cell> path = [];
        if (!parents.TryGetValue(goal, out Cell previous))
            return path;
        while (goal != previous)
        {
            path.Add(goal);
            goal = previous;
            previous = parents[goal];
        }
        path.Reverse();
        return path;
    }

    private void WalkTo(Customer person, Cell target, Dictionary<Cell, Cell>? paths = null)
    {
        person.Route = Route(paths ?? Paths(person.Position), target);
        person.Target = target;
        person.Travel = 0;
    }

    private void Visit(Pedestrian pedestrian)
    {
        int regularIndex = PickRegular();
        Regular? regular = regularIndex >= 0 ? Regulars[regularIndex] : null;
        bool collectorEvent = Event == DayEvent.CollectorVisit && !EventGuestSent && Minute >= 90 && Rank >= 1;
        Archetype kind = collectorEvent ? Archetype.Collector : regular?.Kind ?? PickArchetype();

        ToyFamily family = Catalog.Families[0];
        if (regular != null)
            family = regular.Family;
        else
        {
            int roll = Roll(Catalog.Families.Sum(category => 4 + Forecast(category) * 3));
            foreach (ToyFamily category in Catalog.Families)
            {
                roll -= 4 + Forecast(category) * 3;
                if (roll >= 0)
                    continue;
                family = category;
                break;
            }
        }

        int patron = Roll(100);
        int rare = Reputation >= 66 ? (Fair ? 18 : 10) : 4;
        int tier = patron < rare ? 4 : patron < (Fair ? 37 : 25) ? 3 : patron < 58 ? 2 : 1;
        if (kind == Archetype.Collector)
            tier = 4;
        int[] candidates = Enumerable.Range(0, Catalog.Toys.Length)
            .Where(toy => Unlocked[toy] && Catalog.Toys[toy].Family == family && Catalog.Toys[toy].Tier <= tier)
            .OrderByDescending(toy => Catalog.Toys[toy].Tier).ToArray();
        int wanted = candidates[tier == 1 || kind == Archetype.Collector || Roll(100) < 65 ? 0 : Roll(candidates.Length)];
        bool exact = candidates.Length > 1 && (kind == Archetype.Collector || Roll(100) < (Fair ? 24 : 14));
        int value = Catalog.Toys[wanted].Value;
        int budget = value + 5 + Roll(17 + value / 2);
        budget = budget * Progress.Personas[(int)kind].Budget / 100;
        if (collectorEvent)
            budget = budget * 5 / 2;
        Customer person = new() { Appearance = regular?.Appearance ?? pedestrian.Appearance, Wanted = wanted, WantsExact = exact,
            Kind = kind, RegularId = regularIndex, Alias = regular?.Name };
        if (regular != null)
        {
            regular.LastVisitDay = Day;
            budget = budget * (100 + regular.Bond * 10) / 100;
        }
        person.Budget = Math.Max(value, budget);
        if (collectorEvent)
        {
            EventGuestSent = true;
            Note("The collector has arrived");
        }
        LeadInside(person, pedestrian);
        Customers.Add(person);
    }

    private void BrowseNext(Customer person)
    {
        var paths = Paths(person.Position);
        int left = person.Budget - person.Basket.Sum(item => item.Price);
        Cell[] shelves = Displays.Where(display => display.Capacity > 0 && !person.Seen.Contains(At(display)))
            .OrderByDescending(display => display.Count > 0 && Prices[display.Toy] <= left && Matches(person, display.Toy))
            .ThenByDescending(display => display.Toy == person.Wanted && display.Count > 0 && Prices[display.Toy] <= left)
            .ThenByDescending(display => display.Count > 0)
            .ThenBy(display => Math.Abs(display.X - person.Position.X) + Math.Abs(display.Y - person.Position.Y))
            .Select(At).ToArray();
        foreach (Cell shelf in shelves)
        {
            Cell? stop = shelf.Neighbors().Where(paths.ContainsKey)
                .OrderBy(tile => Route(paths, tile).Count).Select(tile => (Cell?)tile).FirstOrDefault();
            if (stop == null)
                continue;
            person.DisplayCell = shelf;
            person.Wait = 4 + Roll(4);
            WalkTo(person, stop.Value, paths);
            return;
        }
        DoneBrowsing(person);
    }

    private void AdvanceVisit(Customer person)
    {
        if (person.Walking)
        {
            person.Travel += person.Phase is VisitPhase.Approach or VisitPhase.Depart
                ? (person.StreetPace > 0 ? person.StreetPace : .17) : .36;
            if (person.Travel >= 1)
            {
                person.Position = person.Route[0];
                person.Route.RemoveAt(0);
                person.Travel = person.Walking ? person.Travel - 1 : 0;
            }
            if (person.Walking || person.Phase is VisitPhase.Browse or VisitPhase.Checkout)
                return;
        }

        switch (person.Phase)
        {
            case VisitPhase.Approach:
                person.Phase = VisitPhase.Browse;
                Today.Visitors++;
                BrowseNext(person);
                break;
            case VisitPhase.Leave:
                LeadOutside(person);
                break;
            case VisitPhase.Depart:
                RejoinSidewalk(person);
                break;
            case VisitPhase.Browse:
                if (--person.Wait <= 0)
                    TryPickToy(person);
                break;
            case VisitPhase.Checkout:
                int rank = QueuePosition(person);
                Cell slot = LineSpot(rank);
                if (person.Position != slot)
                    WalkTo(person, slot);
                else if (rank == 0 && --person.Wait <= 0)
                    RingUp();
                break;
        }
    }

    private void TryPickToy(Customer person)
    {
        if (person.DisplayCell is Cell shelf)
        {
            Display? display = Displays.Find(item => At(item) == shelf);
            person.Seen.Add(shelf);
            if (display is { Toy: >= 0, Count: > 0 })
            {
                int toy = display.Toy;
                int price = Prices[toy];
                bool wanted = Matches(person, toy);
                int left = person.Budget - person.Basket.Sum(item => item.Price);
                if (price <= left && price * 100 <= Catalog.Toys[toy].Value * Tolerance(person, toy, wanted) &&
                    person.Basket.All(item => item.Toy != toy) && (wanted || Roll(100) < (Fair ? 58 : 42)))
                {
                    display.Count--;
                    person.Basket.Add(new BasketItem(toy, price));
                    Restock(display);
                }
            }
        }

        if (person.Seen.Count >= 3 || person.Basket.Count >= person.Persona.MaxItems ||
            (person.Basket.Count > 0 && Roll(100) < 53) || Minute >= ShopHours)
            DoneBrowsing(person);
        else
            BrowseNext(person);
    }

    private void DoneBrowsing(Customer person)
    {
        if (person.Basket.All(item => !Matches(person, item.Toy)))
            Today.Requests[person.Wanted]++;
        if (person.Basket.Count == 0)
        {
            Today.Missed++;
            person.Result = -1;
            person.Mood = -1;
            LoseRegular(person);
            Note($"{person.Name} left looking for {Catalog.Toys[person.Wanted].Name}");
            Exit(person);
            return;
        }

        person.Phase = VisitPhase.Checkout;
        person.Wait = CheckoutWait;
        person.Ticket = ++LastTicket;
        WalkTo(person, LineSpot(QueuePosition(person)));
    }

    private void Exit(Customer person)
    {
        person.Phase = VisitPhase.Leave;
        WalkTo(person, Entry);
    }

    private int QueuePosition(Customer person) => Customers.Count(other =>
        other.Phase == VisitPhase.Checkout && other.Ticket < person.Ticket);

    private Cell CheckoutSpot
    {
        get
        {
            if (Register is not Display register)
                return Entry;
            var paths = Paths(Entry);
            if (paths.ContainsKey(FrontOf(register)))
                return FrontOf(register);
            return At(register).Neighbors().Where(paths.ContainsKey).DefaultIfEmpty(Entry).First();
        }
    }

    private Cell LineSpot(int rank)
    {
        if (rank == 0)
            return CheckoutSpot;
        List<Cell> path = Route(Paths(Entry), CheckoutSpot);
        return rank < path.Count ? path[path.Count - 1 - rank] : Entry;
    }
}
