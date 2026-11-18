using System.Text.Json.Serialization;

namespace ButtonAndTimber;

internal enum Tool { Saw, Mallet, Brush, Needle }
internal enum BenchCue { None, Correct, Mistake, Finished, Scrapped, LevelUp }

internal sealed partial class ShopState
{
    public const int MaxBatch = 250;
    public const int MaxOrders = 4;
    public const int MaxHeld = 16;
    private static readonly Tool[] WoodTools = [Tool.Saw, Tool.Mallet];
    private static readonly Tool[] ClothTools = [Tool.Needle];
    private static readonly Tool[] MetalTools = [Tool.Mallet, Tool.Needle];
    private static readonly Tool[] DetailTools = [Tool.Brush];
    private static readonly Tool[] MovingParts = [Tool.Brush, Tool.Needle];
    private static readonly Tool[] LightWood = [Tool.Saw, Tool.Needle];
    public uint BenchSeed { get; set; } = 0x89cbc475;
    public Order? LastBatch { get; set; }
    public List<Order> HeldOrders { get; set; } = [];

    [JsonIgnore] public BenchCue LastBenchCue { get; private set; }
    [JsonIgnore] public Tool? Prompt => Orders.FirstOrDefault() is { PromptIn: <= 0, Transfer: <= 0 } order &&
        !OutputBlocked && order.Step < order.Plan.Length ? order.Plan[order.Step] : null;
    [JsonIgnore] public Station? ActiveStation => Orders.FirstOrDefault() is Order order ? StationFor(order) : null;
    [JsonIgnore] public bool OutputBlocked => Orders.FirstOrDefault() is { Decoration: null, Transfer: <= 0 } order &&
        IsFinalPass(order) && DispatchSpace(order.Toy) == 0;

    public static Station StationFor(Order order)
    {
        if (order.Decoration is Fixture fixture)
            return Decorations.Get(fixture).Station;
        Toy toy = Catalog.Toys[order.Toy];
        if (order.SinglePass)
            return order.Toy switch { 5 => Station.Finishing, 9 or Catalog.TrainYard => Station.Assembly, _ => toy.Station };
        return order.Stage == 0 ? toy.Station : toy.FollowUp[order.Stage - 1];
    }

    public static int Stages(Order order) => order.Decoration.HasValue || order.SinglePass ? 1 : 1 + Catalog.Toys[order.Toy].FollowUp.Length;
    public static bool IsFinalPass(Order order) => order.Stage + 1 >= Stages(order);
    public static string ProjectName(Order order) => order.Decoration is Fixture fixture ? Decorations.Get(fixture).Name : Catalog.Toys[order.Toy].Name;
    public static string StationName(Station station) => station switch
    {
        Station.Bench => "Woodwork", Station.Sewing => "Sewing", Station.Assembly => "Assembly", _ => "Painting"
    };

    public bool NeedsHands(Order order) => !order.Decoration.HasValue && NeedsHands(order.Toy);
    public bool NeedsHands(int toy) => Catalog.Toys[toy].Tier switch
    {
        1 => false, 2 => Level(toy) < 3, 3 => Level(toy) < 4, _ => Level(toy) < 5
    };
    private int ProjectLevel(Order order) => order.Decoration.HasValue ? 1 : Level(order.Toy);
    public double ToolWindow(Order order) => order.Decoration.HasValue || Catalog.Toys[order.Toy].Tier == 1 ? 2.65 : 2.35;

    public int CraftChance(Order order)
    {
        int nominal = order.Decoration.HasValue ? 97 : Yield(order.Toy);
        return Math.Clamp((int)Math.Round(100 * Math.Pow(nominal / 100d, 1d / Stages(order))) + order.Grade, 2, 100);
    }

    private int WaveSize(Order order)
    {
        int wave = (order.Quantity + 13) / 14;
        return Math.Min(order.StageLeft, Math.Max(1, wave));
    }

    private bool QueueProject(Order order, int quantity, int unitCost)
    {
        if (quantity is < 1 or > MaxBatch || unitCost * quantity > Cash || Orders.Count >= MaxOrders)
            return false;
        Spend(unitCost * quantity);
        order.Quantity = order.Remaining = order.StageLeft = quantity;
        PrepareWave(order);
        Orders.Add(order);
        return true;
    }

    public bool SetAside(Order order)
    {
        if (HeldOrders.Count >= MaxHeld || !Orders.Remove(order))
            return false;
        HeldOrders.Add(order);
        return true;
    }

    public bool Resume(Order order)
    {
        if (Orders.Count >= MaxOrders || !HeldOrders.Remove(order))
            return false;
        Orders.Add(order);
        return true;
    }

    private int BenchRoll(int max)
    {
        BenchSeed = unchecked(BenchSeed * 1664525 + 1013904223);
        return (int)((BenchSeed >> 8) % (uint)max);
    }

    private void PrepareWave(Order order)
    {
        Toy? toy = order.Decoration.HasValue ? null : Catalog.Toys[order.Toy];
        Tool[] tools = StationFor(order) switch
        {
            Station.Sewing => ClothTools,
            Station.Assembly => toy?.Family == ToyFamily.Motion ? MetalTools : WoodTools,
            Station.Finishing => toy?.Family is ToyFamily.Motion or ToyFamily.Games ? MovingParts : DetailTools,
            _ => toy?.Family == ToyFamily.Motion ? LightWood : WoodTools
        };
        int optional = BenchRoll(100) < 45 ? 1 : 0;
        int steps = 1 + optional + (order.Toy == Catalog.Engine || toy?.Tier == 4 ? 1 : 0);
        if (ProjectLevel(order) >= 4)
            steps = Math.Max(1, steps - 1);

        order.Plan = new Tool[steps];
        int next = BenchRoll(tools.Length);
        for (int index = 0; index < steps; index++)
        {
            order.Plan[index] = tools[next];
            if (index + 1 < steps && tools.Length > 1)
                next = (next + 1 + BenchRoll(tools.Length - 1)) % tools.Length;
        }
    }

    public void AdvanceBench(double seconds, bool atBench = false)
    {
        LastBenchCue = BenchCue.None;
        while (Orders.Count > 0 && seconds > .00001)
        {
            Order order = Orders[0];
            if (order.Transfer > 0)
            {
                double elapsed = Math.Min(seconds, order.Transfer);
                order.Transfer -= elapsed;
                seconds -= elapsed;
                continue;
            }
            if (OutputBlocked)
                return;
            if (order.PromptIn > 0)
            {
                double elapsed = Math.Min(order.PromptIn, seconds);
                order.PromptIn -= elapsed;
                seconds -= elapsed;
                continue;
            }
            if (!atBench && NeedsHands(order) && !Hired[(int)StaffRole.Apprentice])
                return;

            double window = ToolWindow(order);
            double rate = atBench ? 1 : 1 + (ProjectLevel(order) - 1) * .14;
            double progress = Math.Min(seconds * rate, window - order.Work);
            order.Work += progress;
            seconds -= progress / rate;
            if (order.Work < window - .00001)
                return;

            if (NeedsHands(order))
                order.Grade -= 14;
            NextStep(order);
        }
    }

    public bool UseTool(Tool tool)
    {
        LastBenchCue = BenchCue.None;
        if (Prompt is not Tool expected)
            return false;

        Order order = Orders[0];
        bool correct = tool == expected;
        order.Grade += correct ? 10 : -46;
        NextStep(order);
        if (LastBenchCue == BenchCue.None)
            LastBenchCue = correct ? BenchCue.Correct : BenchCue.Mistake;
        return true;
    }

    private void NextStep(Order order)
    {
        order.Work = 0;
        order.PromptIn = .44;
        if (++order.Step < order.Plan.Length)
            return;

        bool final = IsFinalPass(order);
        int attempts = WaveSize(order);
        if (final && !order.Decoration.HasValue)
            attempts = Math.Min(attempts, DispatchSpace(order.Toy));
        int passed = 0;
        int chance = CraftChance(order);
        for (int i = 0; i < attempts; i++)
            if (BenchRoll(100) < chance)
                passed++;

        int scrap = attempts - passed;
        order.StageLeft -= attempts;
        order.Remaining -= scrap;
        order.Scrapped += scrap;
        if (final)
        {
            order.Remaining -= passed;
            order.Made += passed;
            if (order.Decoration is Fixture fixture)
                Furniture[(int)fixture] += passed;
            else
            {
                Stock[order.Toy] += passed;
                Replenish();
            }
        }

        LastBenchCue = scrap > 0 && passed == 0 ? BenchCue.Scrapped : final ? BenchCue.Finished : BenchCue.Correct;
        if (!order.Decoration.HasValue)
        {
            int oldLevel = Level(order.Toy);
            Practice[order.Toy] += scrap + (final ? passed * 2 : 0);
            if (Level(order.Toy) > oldLevel)
            {
                if (passed > 0)
                    LastBenchCue = BenchCue.LevelUp;
                Note($"{ProjectName(order)} reached Lv {Level(order.Toy)}");
                GainRenown(3 + Catalog.Toys[order.Toy].Tier);
            }
        }

        if (order.Remaining == 0)
        {
            LastBatch = order;
            Orders.RemoveAt(0);
            Note($"Bench finished · {order.Made} {ProjectName(order)} · {order.Scrapped} scrapped");
            return;
        }

        if (order.StageLeft == 0)
        {
            order.Stage++;
            order.StageLeft = order.Remaining;
            order.Transfer = .8 + Math.Ceiling((double)order.Remaining / CartCapacity) * .62;
        }
        order.Step = 0;
        order.Grade = 0;
        order.PromptIn = .56;
        PrepareWave(order);
    }
}
