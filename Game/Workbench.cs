using System.Text.Json.Serialization;

namespace ButtonAndTimber;

internal enum Tool { Saw, Mallet, Brush, Needle }
internal enum BenchCue { None, Correct, Mistake, Finished, Scrapped, LevelUp }

internal sealed partial class ShopState
{
    private static readonly Tool[] SawAndPaint = [Tool.Saw, Tool.Brush];
    private static readonly Tool[] SewAndPaint = [Tool.Needle, Tool.Brush];
    private static readonly Tool[] WoodTools = [Tool.Saw, Tool.Mallet, Tool.Brush];
    private static readonly Tool[] AssemblyTools = [Tool.Mallet, Tool.Brush, Tool.Needle, Tool.Saw];
    public uint BenchSeed { get; set; } = 0x89cbc475;
    public Order? LastBatch { get; set; }

    [JsonIgnore] public BenchCue LastBenchCue { get; private set; }
    [JsonIgnore] public Tool? Prompt => Orders.FirstOrDefault() is { PromptIn: <= 0 } order &&
        order.Step < order.Plan.Length ? order.Plan[order.Step] : null;
    [JsonIgnore] public Station? ActiveStation => Orders.FirstOrDefault() is Order order ?
        Catalog.Toys[order.Toy].Station : null;
    public bool NeedsHands(int toy) => Catalog.Toys[toy].Tier switch
    {
        1 => false, 2 => Level(toy) < 3, _ => Level(toy) < 4
    };
    public double ToolWindow(int toy) => Catalog.Toys[toy].Tier == 1 ? 2.65 : 2.35;
    public int CraftChance(Order order) => Math.Clamp(Yield(order.Toy) + order.Grade, 2, 100);

    private int BenchRoll(int max)
    {
        BenchSeed = unchecked(BenchSeed * 1664525 + 1013904223);
        return (int)((BenchSeed >> 8) % (uint)max);
    }

    private void PrepareUnit(Order order)
    {
        Toy toy = Catalog.Toys[order.Toy];
        Tool[] tools = toy.Station switch
        {
            Station.Sewing => SewAndPaint,
            Station.Assembly => AssemblyTools,
            Station.Finishing => SawAndPaint,
            _ => WoodTools
        };
        int optional = BenchRoll(100) < 45 ? 1 : 0;
        int steps = toy.Tier + optional + (order.Toy == Catalog.Engine || toy.Tier == 3 ? 1 : 0);
        if (Level(order.Toy) >= 4)
            steps = Math.Max(1, steps - 1);

        order.Plan = new Tool[steps];
        int next = BenchRoll(tools.Length);
        for (int index = 0; index < steps; index++)
        {
            order.Plan[index] = tools[next];
            if (index + 1 < steps)
                next = (next + 1 + BenchRoll(tools.Length - 1)) % tools.Length;
        }
    }

    public void AdvanceBench(double seconds, bool atBench = false)
    {
        LastBenchCue = BenchCue.None;
        while (Orders.Count > 0 && seconds > .00001)
        {
            Order order = Orders[0];
            if (order.PromptIn > 0)
            {
                double elapsed = Math.Min(order.PromptIn, seconds);
                order.PromptIn -= elapsed;
                seconds -= elapsed;
                continue;
            }

            if (!atBench && NeedsHands(order.Toy))
                break;

            double window = ToolWindow(order.Toy);
            double rate = atBench ? 1 : 1 + (Level(order.Toy) - 1) * .14;
            double progress = Math.Min(seconds * rate, window - order.Work);
            order.Work += progress;
            seconds -= progress / rate;
            if (order.Work < window - .00001)
                break;

            if (NeedsHands(order.Toy))
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

        int toy = order.Toy;
        bool made = BenchRoll(100) < CraftChance(order);
        if (made)
        {
            Stock[toy]++;
            order.Made++;
            Replenish();
        }
        else
            order.Scrapped++;
        LastBenchCue = made ? BenchCue.Finished : BenchCue.Scrapped;
        int oldLevel = Level(toy);
        Practice[toy] += made ? 2 : 1;
        if (Level(toy) > oldLevel)
        {
            if (made)
                LastBenchCue = BenchCue.LevelUp;
            Note($"{Catalog.Toys[toy].Name} reached Lv {Level(toy)}");
        }
        order.Step = 0;
        order.Grade = 0;
        order.PromptIn = .63;
        if (--order.Remaining > 0)
        {
            PrepareUnit(order);
            return;
        }

        string scrap = order.Scrapped > 0 ? $" · {order.Scrapped} scrapped" : "";
        LastBatch = order;
        Note($"Bench finished · {order.Made} {Catalog.Toys[toy].Name}{scrap}");
        Orders.RemoveAt(0);
    }
}
