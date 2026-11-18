using System.Text.Json;
using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private async void Capture(string destination)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string prefix = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(prefix)!);
        for (int index = 0; index < 4; index++)
        {
            OpenRoom(index < 3 ? (Room)index : Room.Shop);
            if (index == 1)
            {
                state.Queue(1, 5);
                state.AdvanceBench(.75, true);
            }
            if (index == 3)
            {
                Furnish(state);
                state.Open();
                for (int tick = 0; tick < 140 && state.ReadyToPay == null; tick++)
                    state.AdvanceShop(1);
                state.RingUp();
                Rebuild();
            }

            UpdateReadouts();
            stage.QueueRedraw();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Error result = GetViewport().GetTexture().GetImage().SavePng($"{prefix}-{index}.png");
            if (result != Error.Ok)
            {
                GD.PushError($"Screenshot failed: {result}");
                GetTree().Quit(1);
                return;
            }
        }
        GetTree().Quit();
    }

    private void SmokeTest()
    {
        try
        {
            Check(state.Displays.Count == 0 && action.Disabled && !state.Open(), "empty shop at start");
            BeginPlacement(Fixture.Register, false);
            PlaceDisplay(new Cell(5, 2));
            Check(state.Register == selectedDisplay && state.Furniture[(int)Fixture.Register] == 0, "place register");
            BeginPlacement(Fixture.Display, false);
            PlaceDisplay(new Cell(1, 2));
            Display display = selectedDisplay!;
            state.Assign(display, 0);
            Check(display.Count == display.Capacity && !state.CanPlace(new Cell(1, 2)), "assign and fill automatically");
            BeginPlacement(Fixture.Display, true);
            PlaceDisplay(new Cell(2, 2));
            Check(display.X == 2 && placing == null, "move display");
            Check(state.Pack(display) && state.Stock[0] == 7, "packing restores shelf and stock");
            Furnish(state);

            OpenRoom(Room.Workshop);
            int before = state.StockOnDisplay(1) + state.Stock[1];
            Check(state.Queue(1, 7), "batch order");
            state.AdvanceBench(.7, true);
            Check(state.Prompt == Tool.Saw, "first tool prompt");
            using (InputEventKey key = new() { Pressed = true, Keycode = Key.A })
                _UnhandledKeyInput(key);
            Check(state.Orders[0].Step == 1 && state.Orders[0].Grade == 7, "correct stroke");
            state.AdvanceBench(.6, true);
            int originalChance = state.CraftChance(state.Orders[0]);
            craftButtons[(int)Tool.Needle].EmitSignal(Button.SignalName.Pressed);
            Check(state.Orders[0].Step == 2 && state.CraftChance(state.Orders[0]) < originalChance - 40,
                "bad stroke sharply lowers yield");
            state.AdvanceBench(200);
            Check(state.Orders.Count == 0 && state.Practice[1] == 7 &&
                state.StockOnDisplay(1) + state.Stock[1] > before, "starter recipe works unattended");
            Check(state.Research(3) && state.Queue(3, 1), "research and queue harder pattern");
            state.AdvanceBench(100);
            Check(state.Prompt == Tool.Mallet && state.Orders[0].Work == 0, "hard recipe waits offscreen");
            FinishByHand(state);
            Check(state.Orders.Count == 0 && state.Stock[3] == 1, "active crafting completes hard recipe");
            state.Practice[3] = 16;
            Check(state.Queue(3, 1), "queue mastered recipe");
            state.AdvanceBench(50);
            Check(state.Orders.Count == 0, "mastered recipe works unattended");

            state.Cash = 420;
            Check(state.Expand() && state.Width == 9, "expansion");
            OpenRoom(Room.Shop);
            AdvanceDay();
            int cashAtOpening = state.Cash;
            int beforeSale = state.Displays.Sum(item => item.Count) + state.Stock.Sum();
            int checkoutTotal = 0;
            state.CheckedOut += amount => checkoutTotal += amount;
            for (int tick = 0; tick < 200 && state.ReadyToPay == null; tick++)
            {
                state.AdvanceShop(1);
                CheckRoutes(state);
            }
            Check(state.ReadyToPay is Customer && state.Cash == cashAtOpening && state.Today.Sold == 0,
                "baskets reserve stock before checkout");
            Check(state.Displays.Sum(item => item.Count) + state.Stock.Sum() < beforeSale, "basket removed stock");
            Check(state.RingUp() && state.Today.Sold > 0 && state.Cash == cashAtOpening + checkoutTotal,
                "payment happens at the register");
            Check(!state.Move(state.Displays[0], new Cell(3, 2)), "layout protected during trading");

            for (int tick = 0; tick < 45 && state.ReadyToPay == null; tick++)
                state.AdvanceShop(1);
            Check(state.Customers.Any(person => person.Phase != VisitPhase.Leave), "active visit for save");
            ShopState resumed = TestDiskSave(state);
            for (int tick = 0; tick < 680; tick++)
            {
                state.AdvanceShop(1);
                resumed.AdvanceShop(1);
            }
            Check(state.Shift == Shift.Closed && state.Today.Revenue == resumed.Today.Revenue &&
                state.Cash == resumed.Cash && state.Seed == resumed.Seed, "checkout and closing resume deterministically");
            AdvanceDay();
            Check(state.Day == 2 && state.Yesterday.Visitors > 0 && state.Today.Visitors == 0, "daily report rollover");

            TestLegacySave();
            PlayCampaign();
            GD.Print("Smoke test passed: furnishing, tool prompts, supervised and mastered recipes, shopping routes, checkout, save migration, and campaign.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void FinishByHand(ShopState run)
    {
        while (run.Orders.Count > 0)
        {
            run.AdvanceBench(1, true);
            if (run.Prompt is Tool tool)
                run.UseTool(tool);
        }
    }

    private static void CheckRoutes(ShopState run)
    {
        var occupied = run.Displays.Select(display => new Cell(display.X, display.Y)).ToHashSet();
        foreach (Customer person in run.Customers)
        {
            Cell previous = person.Position;
            Check(!occupied.Contains(previous), "walkable shopper position");
            foreach (Cell step in person.Route)
            {
                Check(!occupied.Contains(step) && previous.Neighbors().Contains(step), "route takes adjacent aisle cells");
                previous = step;
            }
        }
    }

    private static ShopState TestDiskSave(ShopState progress)
    {
        string path = Path.Combine(Path.GetTempPath(), $"button-timber-smoke-{Guid.NewGuid():N}.json");
        try
        {
            Check(WriteDaybook(path, progress) && WriteDaybook(path, progress), "write current daybook and backup");
            ShopState loaded = LoadDaybook(path);
            Check(loaded.Cash == progress.Cash && loaded.Seed == progress.Seed &&
                loaded.Customers.Count == progress.Customers.Count && loaded.Displays.Count == progress.Displays.Count,
                "save round trip");
            Check(File.Exists(path + ".bak"), "previous daybook is backed up");
            File.WriteAllText(path, "{broken");
            Check(LoadDaybook(path, reportReadErrors: false).Cash == progress.Cash, "backup recovery after interrupted write");
            return loaded;
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".bak");
            File.Delete(path + ".tmp");
        }
    }

    private static void TestLegacySave()
    {
        const string previousFormat = """
            {"Cash":417,"Day":4,"Shift":1,"Displays":[{"X":1,"Y":2,"Toy":0,"Count":3}],
             "Customers":[{"Appearance":5,"Wanted":0,"Budget":28,"Age":19,"TargetX":1,"TargetY":2}],
             "Orders":[{"Toy":1,"Remaining":3,"Work":3.1}],"Today":{"Visitors":6,"Sold":4,"Revenue":60}}
            """;
        ShopState run = JsonSerializer.Deserialize<ShopState>(previousFormat)!;
        run.UpgradeSave();
        Check(run.Cash == 417 && run.Register != null && run.Displays.Count == 2 && run.Day == 4 &&
            run.Customers[0].Position == run.Entry && run.Orders[0].Remaining == 3, "legacy layout and trading migrate");
        run.AdvanceBench(100);
        run.AdvanceShop(550);
        Check(run.Orders.Count == 0 && run.Shift == Shift.Closed, "migrated game stays playable");
    }

    private static void Furnish(ShopState run)
    {
        if (run.Register == null)
            Check(run.PlaceFixture(Fixture.Register, new Cell(5, 2)) != null, "register for campaign");
        foreach (var (type, x, y, toy) in new[] { (Fixture.Display, 1, 2, 0), (Fixture.Display, 4, 1, 1), (Fixture.Table, 3, 4, 2) })
        {
            Display? display = run.PlaceFixture(type, new Cell(x, y));
            Check(display != null, "display for campaign");
            run.Assign(display!, toy);
        }
    }

    private static void PlayCampaign()
    {
        ShopState run = new() { Schema = ShopState.CurrentSchema };
        Furnish(run);
        for (int day = 1; day <= ShopState.CampaignDays; day++)
        {
            if (run.Cash >= 330)
                run.Expand();
            foreach (Display item in run.Displays.Where(item => item.Toy >= 0))
                if (run.Stock[item.Toy] < 12)
                    run.Queue(item.Toy, 12);
            run.AdvanceBench(500);
            run.Deliver();
            run.DeliverExtra(0);
            Check(run.Open(), $"open on day {day}");
            run.AdvanceShop(800);
            Check(run.Shift == Shift.Closed, $"all customers out on day {day}");
            if (day < ShopState.CampaignDays)
                run.Tomorrow();
        }

        Check(run.CampaignResult == 1, $"deed is attainable (finished with ${run.Cash})");
        run.Tomorrow();
        Check(run.Year == 2 && run.SideOrders[0] == 0, "free play orders renew");
        ShopState missed = new() { Day = ShopState.CampaignDays, Cash = 1 };
        Furnish(missed);
        missed.Open();
        missed.Close();
        Check(missed.CampaignResult == -1, "missed deed outcome");
    }

    private static void Check(bool condition, string feature)
    {
        if (!condition)
            throw new InvalidOperationException($"Smoke test failed: {feature}");
    }
}
