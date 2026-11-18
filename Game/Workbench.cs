using System.Text.Json.Serialization;

namespace ButtonAndTimber;

internal enum Tool { Saw, Mallet, Brush, Needle }
internal enum BenchCue { None, Correct, Mistake, Finished, Scrapped }

internal sealed partial class ShopState
{
    private static readonly Tool[][] Recipes =
    [
        [Tool.Needle, Tool.Brush],
        [Tool.Saw, Tool.Mallet, Tool.Brush],
        [Tool.Saw, Tool.Brush],
        [Tool.Mallet, Tool.Brush, Tool.Mallet],
        [Tool.Needle, Tool.Needle, Tool.Brush],
        [Tool.Saw, Tool.Brush, Tool.Mallet],
        [Tool.Saw, Tool.Needle, Tool.Brush],
        [Tool.Mallet, Tool.Needle, Tool.Mallet, Tool.Brush],
        [Tool.Needle, Tool.Brush, Tool.Needle],
        [Tool.Saw, Tool.Mallet, Tool.Needle, Tool.Brush]
    ];

    [JsonIgnore] public BenchCue LastBenchCue { get; private set; }
    [JsonIgnore] public Tool? Prompt => Orders.FirstOrDefault() is { PromptIn: <= 0 } order
        ? Recipes[order.Toy][order.Step] : null;
    public int StepCount(int toy) => Recipes[toy].Length;
    public bool NeedsHands(int toy) => Catalog.Toys[toy].Research >= 34 && Level(toy) < 2;
    public double ToolWindow(int toy) => NeedsHands(toy) ? 2.9 : 2.45;
    public int CraftChance(Order order) => Math.Clamp(Yield(order.Toy) + order.Grade, 2, 100);

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
            double progress = Math.Min(seconds, window - order.Work);
            order.Work += progress;
            seconds -= progress;
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
        order.Grade += correct ? 7 : -52;
        NextStep(order);
        if (LastBenchCue == BenchCue.None)
            LastBenchCue = correct ? BenchCue.Correct : BenchCue.Mistake;
        return true;
    }

    private void NextStep(Order order)
    {
        order.Work = 0;
        order.PromptIn = .53;
        if (++order.Step < StepCount(order.Toy))
            return;

        int toy = order.Toy;
        bool made = Roll(100) < CraftChance(order);
        if (made)
        {
            Stock[toy]++;
            order.Made++;
            Replenish();
        }
        else
            order.Scrapped++;
        LastBenchCue = made ? BenchCue.Finished : BenchCue.Scrapped;
        Practice[toy]++;
        order.Step = 0;
        order.Grade = 0;
        order.PromptIn = .85;
        if (--order.Remaining > 0)
            return;

        string scrap = order.Scrapped > 0 ? $" · {order.Scrapped} scrapped" : "";
        Note($"Bench finished · {order.Made} {Catalog.Toys[toy].Name}{scrap}");
        Orders.RemoveAt(0);
    }
}
