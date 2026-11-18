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

internal enum VisitPhase { Browse, Checkout, Leave }
internal sealed record BasketItem(int Toy, int Price);

internal sealed partial class Customer
{
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
    public const int CurrentSchema = 1;
    public int[] Furniture { get; set; } = [2, 1, 1, 0];
    public double ShopRemainder { get; set; }
    public int LastTicket { get; set; }
    public int[] SideOrders { get; set; } = new int[4];
    [JsonIgnore] public Display? Register => Displays.Find(display => display.Style == Fixture.Register);
    [JsonIgnore] public Cell Entry => new(Width - 1, 6);
    [JsonIgnore] public Customer? ReadyToPay => Customers.FirstOrDefault(person => person.Phase == VisitPhase.Checkout &&
        !person.Walking && person.Position == CheckoutSpot && QueuePosition(person) == 0);
    [JsonIgnore] public int FeaturedToy => Enumerable.Range(0, Catalog.Toys.Length)
        .Where(toy => Unlocked[toy] && Catalog.Toys[toy].Season == Season)
        .DefaultIfEmpty(Season == 2 ? 1 : 0).Max();

    public event Action<int>? CheckedOut;

    public static string FixtureName(Fixture fixture) => fixture switch
    {
        Fixture.Display => "Shelf", Fixture.Table => "Table", Fixture.Plant => "Plant", _ => "Register"
    };

    public static int FurnitureCost(Fixture fixture) => fixture switch
    {
        Fixture.Display => 38, Fixture.Table => 56, Fixture.Plant => 18, _ => 0
    };

    public void UpgradeSave()
    {
        if (Schema >= CurrentSchema)
            return;

        if (Displays.Count > 0 || Shift == Shift.Open)
        {
            if (Register == null)
                Displays.Add(new Display { X = Width - 2, Y = 1, Style = Fixture.Register });
            foreach (Fixture kind in Enum.GetValues<Fixture>())
                Furniture[(int)kind] = Math.Max(0, Furniture[(int)kind] - Displays.Count(display => display.Style == kind));
        }

        Customers.RemoveAll(person => person.Result != 0);
        Today.CustomersServed = Today.Sold;
        foreach (Order order in Orders)
        {
            int steps = StepCount(order.Toy);
            double scaled = Math.Clamp(order.Work / Catalog.Toys[order.Toy].Time * steps, 0, steps - .01);
            order.Step = (int)scaled;
            order.Work = (scaled - order.Step) * ToolWindow(order.Toy);
        }
        foreach (Customer person in Customers)
        {
            person.Position = Entry;
            BrowseNext(person);
        }
        Schema = CurrentSchema;
        Replenish();
    }

    public bool CanPlace(Cell tile, Display? moving = null)
    {
        if (tile.X < 1 || tile.X >= Width - 1 || tile.Y is < 1 or > 4)
            return false;

        HashSet<Cell> blocked = Displays.Where(display => display != moving).Select(At).ToHashSet();
        if (!blocked.Add(tile))
            return false;

        Dictionary<Cell, Cell> walkable = Reachable(Entry, blocked);
        return blocked.All(place => place.Neighbors().Any(walkable.ContainsKey));
    }

    public Display? PlaceFixture(Fixture fixture, Cell tile)
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

        Display item = new() { X = tile.X, Y = tile.Y, Style = fixture };
        Displays.Add(item);
        return item;
    }

    public bool Move(Display display, Cell tile)
    {
        if (Shift == Shift.Open || !CanPlace(tile, display))
            return false;
        display.X = tile.X;
        display.Y = tile.Y;
        return true;
    }

    public bool Pack(Display display)
    {
        if (Shift == Shift.Open || !Displays.Remove(display))
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
            0 => [0, 4], 1 => [2, 6], 2 => [1, 8], _ => [3, 9]
        };
        int toy = toys[index];
        int count = 3 + index;
        return (index == 0 ? "Library playgroup" : "Window commission", toy, count,
            Catalog.Toys[toy].Value * count + (index == 0 ? 18 : 45));
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
        Note($"{request.Name} delivered · +${request.Pay}");
        return true;
    }

    public void AdvanceShop(double minutes)
    {
        if (Shift != Shift.Open)
            return;

        ShopRemainder += minutes;
        while (ShopRemainder >= 1 && Shift == Shift.Open)
        {
            ShopRemainder--;
            Minute = Math.Min(Minute + 1, ShopHours);
            foreach (Customer person in Customers)
                AdvanceVisit(person);

            Customers.RemoveAll(person => person.Phase == VisitPhase.Leave && !person.Walking);
            if (Minute >= NextVisitor && Minute < ShopHours - 45)
            {
                Visit();
                int crowd = (Fair ? 4 : 0) + Reputation / 20 + (Expanded ? 2 : 0);
                NextVisitor = Minute + 26 - crowd + Roll(7) - 3;
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

    private void Visit()
    {
        List<int> weights = [];
        for (int toy = 0; toy < Catalog.Toys.Length; toy++)
        {
            int weight = Unlocked[toy] ? 5 : Day < 3 ? 0 : 1;
            if (toy == FeaturedToy)
                weight += Fair ? 8 : 4;
            for (int i = 0; i < weight; i++)
                weights.Add(toy);
        }

        int wanted = weights[Roll(weights.Count)];
        int value = Catalog.Toys[wanted].Value;
        Customer person = new() { Appearance = Roll(48), Wanted = wanted,
            Budget = value + 5 + Roll(16 + value / 2), Position = Entry };
        Customers.Add(person);
        Today.Visitors++;
        BrowseNext(person);
    }

    private void BrowseNext(Customer person)
    {
        var paths = Paths(person.Position);
        Cell[] shelves = Displays.Where(display => display.Capacity > 0 && !person.Seen.Contains(At(display)))
            .OrderByDescending(display => display.Toy == person.Wanted && display.Count > 0)
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
            person.Travel += .36;
            if (person.Travel >= 1)
            {
                person.Position = person.Route[0];
                person.Route.RemoveAt(0);
                person.Travel = person.Walking ? person.Travel - 1 : 0;
            }
            return;
        }

        switch (person.Phase)
        {
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
                bool wanted = toy == person.Wanted;
                int left = person.Budget - person.Basket.Sum(item => item.Price);
                if (price <= left && price * 100 <= Catalog.Toys[toy].Value * (wanted ? 145 : 112) &&
                    person.Basket.All(item => item.Toy != toy) && (wanted || Roll(100) < (Fair ? 58 : 42)))
                {
                    display.Count--;
                    person.Basket.Add(new BasketItem(toy, price));
                    Restock(display);
                }
            }
        }

        if (person.Seen.Count >= 3 || person.Basket.Count >= 2 ||
            (person.Basket.Count > 0 && Roll(100) < 53) || Minute >= ShopHours)
            DoneBrowsing(person);
        else
            BrowseNext(person);
    }

    private void DoneBrowsing(Customer person)
    {
        if (person.Basket.All(item => item.Toy != person.Wanted))
            Today.Requests[person.Wanted]++;
        if (person.Basket.Count == 0)
        {
            Today.Missed++;
            person.Result = -1;
            Note($"{person.Name} left looking for {Catalog.Toys[person.Wanted].Name}");
            Exit(person);
            return;
        }

        person.Phase = VisitPhase.Checkout;
        person.Wait = 7;
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
