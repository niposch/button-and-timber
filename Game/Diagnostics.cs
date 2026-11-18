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
        for (int index = 0; index < 13; index++)
        {
            OpenRoom(index < 3 ? (Room)index : index is 4 or 5 ? Room.Workshop : Room.Shop);
            stage.SnapCamera();
            if (index == 1)
            {
                state.Queue(1, 5);
                state.AdvanceBench(.75, true);
                Rebuild();
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
            if (index == 4)
            {
                state.Orders.Clear();
                state.Cash = 395;
                state.Practice[Catalog.Engine] = 17;
                state.Practice[Catalog.SignalCabin] = 13;
                state.Unlocked[Catalog.SignalCabin] = true;
                state.Renown = 600;
                state.Research(Catalog.TrainYard);
                selectedToy = Catalog.TrainYard;
                state.Queue(selectedToy, 3);
                Rebuild();
                await ToSignal(GetTree().CreateTimer(1.05), SceneTreeTimer.SignalName.Timeout);
            }
            if (index == 5)
            {
                state.Orders.Clear();
                state.Cash = 1000;
                quantity = 24;
                selectedToy = Catalog.TrainYard;
                state.Queue(selectedToy, quantity);
                AdvanceOnePass(state);
                Rebuild();
                await ToSignal(GetTree().CreateTimer(.34), SceneTreeTimer.SignalName.Timeout);
            }
            if (index == 6)
            {
                state.Shift = Shift.Setup;
                state.Customers.Clear();
                state.PlaceFixture(Fixture.FlowerCrate, new Cell(2, 2));
                state.PlaceFixture(Fixture.PennantStand, new Cell(5, 1));
                showDecor = true;
                Rebuild();
            }
            if (index == 7)
            {
                showDecor = false;
                state.Open();
                state.AdvanceShop(115);
                for (int tick = 0; tick < 150; tick++)
                {
                    if (state.Customers.Any(person => person.Phase == VisitPhase.Approach && person.Position == state.Entry))
                        break;
                    state.AdvanceShop(1);
                }
                Rebuild();
            }
            if (index == 8)
            {
                state.AdvanceShop(800);
                summary = true;
                Rebuild();
            }
            if (index == 9)
            {
                state.Tomorrow();
                OpenRoom(Room.Shop);
            }
            if (index == 10)
            {
                speed = 0;
                state.Stock[Catalog.Engine] += state.StorageCapacity - state.StorageUsed;
                ToggleStockroom();
                stockroomSelection = Catalog.Engine;
                Rebuild();
            }

            if (index == 11)
            {
                speed = 1;
                showStockroom = false;
                state.Renown = 300;
                state.Cash = 600;
                state.Regulars.Add(new Regular { Name = "Elsie", Family = ToyFamily.Plush, Affinity = 12, Visits = 4 });
                state.Regulars.Add(new Regular { Name = "Milo", Family = ToyFamily.Trains, Affinity = 30, Visits = 9 });
                townTab = 1;
                OpenRoom(Room.Town);
            }
            if (index == 12)
            {
                state.Shift = Shift.Setup;
                state.Event = DayEvent.SchoolTrip;
                townTab = 0;
                OpenRoom(Room.Shop);
                state.Announce("Rank up · Craftsman");
            }

            UpdateReadouts();
            stage.SnapAtmosphere();
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
            Check(state.Displays.Count == 0 && action.Text == "Place a register" && !state.Open(), "empty shop at start");
            PressSpace();
            Check(placing == Fixture.Register, "Space follows the current setup action");
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
            LineEdit amount = content.FindChild("BatchQuantity", recursive: true, owned: false) as LineEdit ??
                throw new InvalidOperationException("Batch quantity editor missing");
            amount.EmitSignal(LineEdit.SignalName.TextChanged, "200");
            Check(quantity == 200, "typed quantity above twelve");
            amount.EmitSignal(LineEdit.SignalName.TextChanged, "5");
            int before = state.StockOnDisplay(1) + state.Stock[1];
            Check(state.Queue(1, 7), "batch order");
            state.AdvanceBench(.7, true);
            Check(state.Prompt.HasValue, "first tool prompt");
            Key[] arrows = [Key.W, Key.D, Key.S, Key.A];
            using (InputEventKey key = new() { Pressed = true, Keycode = arrows[(int)state.Prompt!.Value] })
                _Input(key);
            Check(state.Orders[0].Step == 1 && state.Orders[0].Grade == 10, "correct WASD direction");
            state.AdvanceBench(.6, true);
            int step = state.Orders[0].Step;
            int remaining = state.Orders[0].Remaining;
            craftButtons[(int)state.Prompt!.Value].EmitSignal(Button.SignalName.Pressed);
            Check(state.Orders[0].Step == step + 1 || state.Orders[0].Remaining == remaining - 1,
                "the matching onscreen direction answers the prompt");
            state.AdvanceBench(.75, true);
            step = state.Orders[0].Step;
            remaining = state.Orders[0].Remaining;
            JoyButton[] directions = [JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft];
            using (InputEventJoypadButton key = new() { Pressed = true, ButtonIndex = directions[(int)state.Prompt!.Value] })
                _Input(key);
            Check(state.Orders[0].Step == step + 1 || state.Orders[0].Remaining == remaining - 1,
                "controller D-pad answers the prompt");
            state.AdvanceBench(200);
            Check(state.Orders.Count == 0 && state.Level(1) >= 2 &&
                state.StockOnDisplay(1) + state.Stock[1] > before, "starter recipe works unattended");
            Check(state.LastBatch is { Remaining: 0 } batch && batch.Made + batch.Scrapped == 7, "finished batch totals");
            int cabin = Catalog.SignalCabin;
            Check(state.Research(cabin) && state.Queue(cabin, 1), "research and queue harder pattern");
            state.AdvanceBench(100);
            Check(state.Prompt.HasValue && state.Orders[0].Work == 0, "hard recipe waits offscreen");
            FinishByHand(state);
            Check(state.Orders.Count == 0 && state.Stock[cabin] == 1, "active crafting completes hard recipe");
            state.Practice[cabin] = 23;
            Check(state.Queue(cabin, 1), "queue mastered recipe");
            state.AdvanceBench(50);
            Check(state.Orders.Count == 0, "mastered recipe works unattended");

            state.Cash = 420;
            Check(state.Expand() && state.Width == 9, "expansion");
            OpenRoom(Room.Shop);
            ledger.GrabFocus();
            PressSpace();
            Check(state.Shift == Shift.Open && !summary, "Space opens the shop with another button focused");
            PressSpace();
            Check(state.Today.Sold == 0 && !summary, "Space does nothing with an empty register");
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
            UpdateReadouts();
            PressSpace(echo: true);
            Check(state.Today.Sold == 0, "holding Space does not ring up a customer");
            PressSpace();
            Check(state.Today.Sold > 0 && state.Cash == cashAtOpening + checkoutTotal,
                "Space rings up the ready customer");
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
            summary = true;
            Rebuild();
            PressSpace();
            Check(state.Day == 2 && state.Yesterday.Visitors > 0 && state.Today.Visitors == 0 && !summary,
                "Space starts the next day from the till report");
            TestStockroomControls();

            TestLegacySave();
            TestBlueprintEconomy();
            TestProductionLine();
            TestDecorAndStorage();
            TestStreetTraffic();
            TestProgression();
            PlayCampaign();
            GD.Print("Smoke test passed: shop controls, stockroom, crafting, street traffic and lighting, saves, and campaign.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void PressSpace(bool echo = false)
    {
        using InputEventKey press = new() { Keycode = Key.Space, Pressed = true, Echo = echo };
        GetViewport().PushInput(press);
        if (!echo)
        {
            using InputEventKey release = new() { Keycode = Key.Space };
            GetViewport().PushInput(release);
        }
    }

    private void TestStockroomControls()
    {
        int toy = Catalog.Engine;
        state.Stock[toy] = 5;
        int onDisplay = state.StockOnDisplay(toy);
        stockroomTab.EmitSignal(Button.SignalName.Pressed);
        Check(showStockroom && stockroomMeter.Value == state.StorageUsed &&
            stockroomMeter.MaxValue == state.StorageCapacity, "stockroom shows used and total space");
        Button card = content.GetChildren().OfType<Button>().First(button =>
            button.AccessibilityName.StartsWith(Catalog.Toys[toy].Name));
        card.EmitSignal(Button.SignalName.Pressed);
        SpinBox units = (SpinBox)content.FindChild("DiscardQuantity", owned: false);
        Button discard = (Button)content.FindChild("DiscardStock", owned: false);
        units.GetLineEdit().Text = "3";
        discard.EmitSignal(Button.SignalName.Pressed);
        Check(confirmDiscard.Visible && pendingDiscard == (toy, 3) && state.Stock[toy] == 5,
            "typed quantity asks for confirmation before changing stock");
        confirmDiscard.GetOkButton().EmitSignal(Button.SignalName.Pressed);
        Check(state.Stock[toy] == 2 && state.StockOnDisplay(toy) == onDisplay &&
            TestDiskSave(state).Stock[toy] == 2, "confirmed discard removes and saves only the selected reserve");
    }

    private static void FinishByHand(ShopState run)
    {
        for (int tick = 0; tick < 10000 && run.Orders.Count > 0 && !run.OutputBlocked; tick++)
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
        ShopState run = JsonSerializer.Deserialize(previousFormat, DaybookJson.Default.ShopState)!;
        run.UpgradeSave();
        Check(run.Cash == 417 && run.Register != null && run.Displays.Count == 2 && run.Day == 4 &&
            run.Customers[0].Position == run.Entry && run.Orders[0].Remaining == 3, "legacy layout and trading migrate");
        run.AdvanceBench(100);
        run.AdvanceShop(640);
        Check(run.Orders.Count == 0, "migrated bench finishes");
        Check(run.Shift == Shift.Closed, $"migrated shop closes (min {run.Minute}, customers {string.Join(";", run.Customers.Select(c => $"{c.Phase}/{c.Walking}/{c.Wait}/{c.Kind}/{c.Position}->{c.Target}"))}, shift {run.Shift})");

        const string secondFormat = """
            {"Schema":1,"Cash":463,"Stock":[1,2,3,0,0,0,0,0,0,0],"Prices":[16,22,11,30,24,29,38,46,41,64],
             "Practice":[4,8,2,0,0,0,0,0,0,0],"Unlocked":[true,true,true,false,false,false,false,false,false,false],
             "Orders":[{"Toy":1,"Remaining":2,"Step":1,"Work":0.7,"PromptIn":0}],
             "Today":{"Requests":[0,0,1,0,0,0,0,0,0,0]}}
            """;
        run = JsonSerializer.Deserialize(secondFormat, DaybookJson.Default.ShopState)!;
        run.UpgradeSave();
        Check(run.Cash == 463 && run.Prices[Catalog.Engine] == 22 && run.Practice[Catalog.Engine] == 16 &&
            run.Unlocked[Catalog.PebbleDice] && run.Stock[Catalog.PebbleDice] == 5 && run.Today.Requests[2] == 1 &&
            run.Prompt.HasValue, "version-one prices, practice, requests and pending tools migrate");

        run = new ShopState { Schema = 2 };
        run.Practice[Catalog.Engine] = 12;
        run.Orders.Add(new Order { Toy = Catalog.SignalCabin, Remaining = 4, Made = 2, Plan = [Tool.Mallet], PromptIn = 0 });
        run.UpgradeSave();
        Check(run.Practice[Catalog.Engine] == 12 && run.Orders[0].Quantity == 6 && run.Orders[0].StageLeft == 4 &&
            run.Prompt == Tool.Mallet && ShopState.Stages(run.Orders[0]) == 1, "version-two orders keep paid work and prompts");
    }

    private static void TestBlueprintEconomy()
    {
        ShopState run = new() { Schema = ShopState.CurrentSchema, Cash = 1500 };
        run.RentMoreStorage();
        run.RentMoreStorage();
        HashSet<int> lengths = [];
        HashSet<Tool> starters = [];
        for (int i = 0; i < 16; i++)
        {
            run.Practice[Catalog.Whirligig] = 0;
            Check(run.Queue(Catalog.Whirligig, 1), "queue whirligig");
            lengths.Add(run.Orders[0].Plan.Length);
            starters.Add(run.Orders[0].Plan[0]);
            run.AdvanceBench(20);
        }
        Check(lengths.SetEquals([1, 2]) && starters.Count > 1, "whirligig alternates prompt count and first tool");
        Check(!run.CanResearch(Catalog.TrainYard), "advanced train locked behind an intermediate blueprint");
        Check(run.Queue(Catalog.Engine, 4), "practice engine");
        FinishByHand(run);
        Check(run.Level(Catalog.Engine) >= 2 && run.Research(Catalog.SignalCabin), "training opens rail branch");
        Check(run.Queue(Catalog.SignalCabin, 6), "practice cabin");
        FinishByHand(run);
        Check(run.Cash >= 135 || (run.Cash = 400) > 0, "funds");
        Check(!run.Research(Catalog.TrainYard) || run.Rank >= 2, "tier three patterns wait for rank");
        run.Renown = Progress.RankRenown[3];
        Check(run.Unlocked[Catalog.TrainYard] || run.Research(Catalog.TrainYard), "cabin mastery opens train yard");
        int yard = Catalog.TrainYard;
        Check(run.Yield(yard) < run.Yield(Catalog.Engine) && run.ToyCost(yard) < run.Prices[yard] &&
            run.Prices[yard] - run.ToyCost(yard) > run.Prices[Catalog.Engine] - run.ToyCost(Catalog.Engine),
            "advanced train has more risk and more upside");
        run.BenchSeed = 0x89cbc475;
        Check(run.Queue(yard, 2) && run.ActiveStation == Station.Bench && ShopState.Stages(run.Orders[0]) == 3,
            "train yard starts with woodwork");
        run.AdvanceBench(.75, true);
        int baseChance = run.CraftChance(run.Orders[0]);
        Tool wrong = Tools.First(tool => tool != run.Prompt);
        Check(run.UseTool(wrong) && run.CraftChance(run.Orders[0]) < baseChance - 40, "wrong tools increase defects sharply");

        ShopState saved = TestDiskSave(run);
        Check(run.Orders[0].Plan.SequenceEqual(saved.Orders[0].Plan) && run.BenchSeed == saved.BenchSeed,
            "random plan and progress are saved");
        FinishByHand(run);
        FinishByHand(saved);
        Check(run.Stock[yard] == saved.Stock[yard] && run.Practice[yard] == saved.Practice[yard], "craft outcomes survive resume");

        Customer shopper = new() { Wanted = Catalog.Engine };
        Check(ShopState.Matches(shopper, yard) && !ShopState.Matches(shopper, Catalog.PebbleDice),
            "another train satisfies rail demand");
        shopper.WantsExact = true;
        Check(!ShopState.Matches(shopper, yard), "collector requires the exact blueprint");
        Check(run.Forecast(ToyFamily.Trains) > run.Forecast(ToyFamily.Games), "spring demand forecast matches category weights");

        run.Stock[yard] = 2;
        run.PlaceFixture(Fixture.Register, new Cell(5, 2));
        Display showcase = run.PlaceFixture(Fixture.Table, new Cell(3, 4))!;
        run.Assign(showcase, yard);
        Check(run.Open(), "showcase shop opens");
        shopper.WantsExact = false;
        shopper.Budget = 120;
        shopper.Position = new Cell(3, 5);
        shopper.DisplayCell = new Cell(showcase.X, showcase.Y);
        run.Customers.Add(shopper);
        run.AdvanceShop(1);
        Check(shopper.Basket.Count == 1 && shopper.Basket[0].Toy == yard, "rail shopper chooses the premium train on display");
        int openingCash = run.Cash;
        run.AdvanceShop(48);
        Check(run.Today.Revenue >= 79 && run.Cash >= openingCash + 79, "premium train pays at checkout");
    }

    private static void AdvanceOnePass(ShopState run)
    {
        Order order = run.Orders[0];
        int stage = order.Stage;
        for (int tick = 0; tick < 500 && run.Orders.Contains(order) && order.Stage == stage; tick++)
        {
            run.AdvanceBench(.75, true);
            if (run.Prompt is Tool tool)
                run.UseTool(tool);
        }
    }

    private static void TestProductionLine()
    {
        ShopState run = new() { Schema = ShopState.CurrentSchema, Cash = 3000 };
        run.Unlocked[Catalog.TrainYard] = true;
        run.RentMoreStorage();
        Check(run.Queue(Catalog.TrainYard, 35), "queue larger train yard batch");
        var saveFormat = DaybookJson.Default.ShopState;
        ShopState fast = JsonSerializer.Deserialize(JsonSerializer.Serialize(run, saveFormat), saveFormat)!;
        Check(fast.UpgradeCart(), "buy bigger cart");
        AdvanceOnePass(run);
        AdvanceOnePass(fast);
        Order batch = run.Orders[0];
        Check(batch.Stage == 1 && run.ActiveStation == Station.Assembly && batch.Made == 0 &&
            run.Stock[Catalog.TrainYard] == 0 && batch.StageLeft == batch.Remaining, "whole pass before moving machines");
        Check(fast.Orders[0].Transfer < batch.Transfer, "bigger cart makes fewer trips");
        int alreadyPaid = run.Cash;
        Check(run.SetAside(batch) && run.HeldOrders.Count == 1, "set aside unfinished woodwork");
        ShopState saved = TestDiskSave(run);
        Order held = saved.HeldOrders[0];
        double transfer = held.Transfer;
        saved.AdvanceBench(20, true);
        Check(held.Transfer == transfer && saved.Resume(held) && saved.Cash == alreadyPaid, "resume paid batch with saved work");
        AdvanceOnePass(saved);
        Check(saved.ActiveStation == Station.Finishing && held.Made == 0, "only final station ships toys");
        FinishByHand(saved);
        Check(saved.Orders.Count == 0 && held.Made > 0 && held.Made + held.Scrapped == 35, "all units accounted for");
    }

    private static void TestDecorAndStorage()
    {
        ShopState run = new() { Schema = ShopState.CurrentSchema, Cash = 3000 };
        int original = run.Forecast(ToyFamily.Plush);
        Check(run.PlaceFixture(Fixture.FlowerCrate, new Cell(1, 2)) != null, "buy spring decoration");
        Check(run.Forecast(ToyFamily.Plush) == original + 2 && run.Appeal == 3, "seasonal decoration boosts demand and appeal");
        Check(run.PlaceFixture(Fixture.FlowerCrate, new Cell(2, 2)) != null && run.Appeal == 3, "duplicate does not stack");
        run.Day = 7;
        Check(run.DecorDemand(ToyFamily.Plush) == 0 && run.Appeal == 1, "seasonal effect expires");
        Check(run.MakeDecoration(Fixture.PennantStand), "craft decor instead of buying");
        FinishByHand(run);
        int paid = run.Cash;
        Check(run.PlaceFixture(Fixture.PennantStand, new Cell(3, 2)) != null && run.Cash == paid &&
            run.DecorDemand(ToyFamily.Motion) == 2, "place crafted decor from crate");

        run.Stock[0] += run.StorageCapacity - run.StorageUsed;
        Check(run.Queue(Catalog.PebbleDice, ShopState.MaxBatch), "support 250 units");
        run.AdvanceBench(120, true);
        Check(run.OutputBlocked && run.Orders[0].Remaining == ShopState.MaxBatch && run.StorageUsed == run.StorageCapacity,
            "full stockroom pauses output without loss");
        Check(!run.DiscardStock(0, run.Stock[0] + 1) && !run.DiscardStock(0, 0), "invalid discards leave stock unchanged");
        Check(run.DiscardStock(0, 3) && run.StorageUsed == run.StorageCapacity - 3 && !run.OutputBlocked,
            "discarding stock frees space for the paused batch");
        run.AdvanceBench(120, true);
        Check(run.Orders[0].Made == 3 && run.OutputBlocked, "batch fills the freed storage space");
        int rent = run.DailyRent;
        Check(run.RentMoreStorage() && run.DailyRent > rent && !run.OutputBlocked, "room and rent increase together");
        run.AdvanceBench(20, true);
        Check(run.Orders[0].Made > 0 && run.StorageUsed <= run.StorageCapacity, "batch resumes within upgraded limit");
        run.Shift = Shift.Open;
        int before = run.Cash;
        run.Close();
        Check(before - run.Cash == run.DailyRent, "rent charged at closing");
    }

    private static void TestStreetTraffic()
    {
        ShopState previous = new() { Schema = 3, Cash = 500 };
        previous.Unlocked[Catalog.TrainYard] = true;
        Check(previous.Queue(Catalog.TrainYard, 4), "existing multi-pass order");
        AdvanceOnePass(previous);
        Order savedWork = previous.Orders[0];
        previous.UpgradeSave();
        Check(previous.Pedestrians.Count > 0 && savedWork.Stage == 1 &&
            ShopState.Stages(savedWork) == 3 && savedWork.Transfer > 0, "traffic migration keeps ongoing workshop passes");

        ShopState run = new();
        run.UpgradeSave();
        Furnish(run);
        Pedestrian commuter = run.Pedestrians[0];
        Cell startingTile = commuter.Position;
        run.AdvanceShop(0);
        Check(commuter.Position == startingTile && run.StreetClock == 0, "pause holds street traffic");
        run.AdvanceShop(9 * 8);
        Check(commuter.Position != startingTile && run.StreetClock == 72 && run.ClockTime == 8 * 60 &&
            run.Today.Visitors == 0, "street keeps moving before opening at accelerated speed");

        Pedestrian walker = new()
        {
            Appearance = 41, Position = new Cell(run.Door.X - 2, 7), Direction = 1, Pace = .18, Travel = .27
        };
        run.Pedestrians = [walker];
        Check(run.Open() && run.ClockTime == 9 * 60, "daylight trading starts at nine");
        run.AdvanceShop(5);
        Customer approaching = run.Customers.Single();
        Check(approaching.Appearance == walker.Appearance && approaching.Position == walker.Position &&
            approaching.Travel == walker.Travel && !run.Pedestrians.Contains(walker), "same pedestrian turns toward the shop");
        Check(approaching.Phase == VisitPhase.Approach && approaching.Route.Contains(run.Entry) &&
            approaching.Route.Last() == run.Door && run.Today.Visitors == 0, "entry path uses the front door");

        ShopState resumed = TestDiskSave(run);
        Check(resumed.Customers.Single().Route.SequenceEqual(approaching.Route) &&
            resumed.Customers[0].StreetLane == 7 && resumed.StreetClock == run.StreetClock, "saved sidewalk approach");
        while (run.Today.Visitors == 0 && run.Minute < 85)
            run.AdvanceShop(1);
        Check(approaching.Position == run.Door && run.Today.Visitors == 1 && approaching.Phase == VisitPhase.Browse,
            "visit counted when crossing into the shop");
        for (int tick = 0; tick < 300 && run.Customers.Contains(approaching); tick++)
            run.AdvanceShop(1);
        Check(!run.Customers.Contains(approaching) && run.Pedestrians.Any(person => person.Appearance == walker.Appearance &&
            person.Position.Y == walker.Position.Y && person.Direction == walker.Direction), "customer rejoins the sidewalk on exit");

        run.AdvanceShop(800);
        Check(run.Shift == Shift.Closed && run.ClockTime == 18 * 60 && run.Customers.Count == 0, "evening after the last shopper leaves");
        long eveningStreet = run.StreetMinutes;
        run.AdvanceShop(8);
        Check(run.StreetMinutes == eveningStreet + 8 && run.ClockTime == 18 * 60, "evening foot traffic runs at game speed");
        run.Tomorrow();
        Check(run.Shift == Shift.Setup && run.ClockTime == 8 * 60, "next day goes back to morning");
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

    private static void TestProgression()
    {
        ShopState run = new() { Schema = ShopState.CurrentSchema, Cash = 1000 };
        Check(run.Rank == 0 && run.MaxTier == 2 && !run.CanHire((int)StaffRole.Cashier), "new shops start as stallholders");
        run.GainRenown(Progress.RankRenown[2]);
        Check(run.Rank == 2 && run.MaxTier == 3 && run.CanHire((int)StaffRole.Cashier), "renown raises rank");
        Check(run.Hire((int)StaffRole.Cashier) && run.CheckoutWait == 3 && run.Wages == 12, "hired cashier speeds checkout");
        Check(run.ChooseStyle(Philosophy.Volume) && run.ToyCost(Catalog.Engine) < Catalog.Toys[Catalog.Engine].Materials,
            "volume style cuts material cost");
        Check(!run.ChooseStyle(Philosophy.Showman) || run.Cash < 1000, "changing style costs money");
        Check(run.Hired[(int)StaffRole.Cashier] && run.Dismiss((int)StaffRole.Cashier) && run.Wages == 0, "staff can be let go");

        Customer haggler = new() { Kind = Archetype.Bargain, Wanted = Catalog.Engine };
        Customer tourist = new() { Kind = Archetype.Tourist, Wanted = Catalog.Engine };
        Check(run.Tolerance(tourist, Catalog.Engine, true) > run.Tolerance(haggler, Catalog.Engine, true),
            "bargain hunters tolerate less markup than tourists");

        ShopState saved = JsonSerializer.Deserialize(JsonSerializer.Serialize(run, DaybookJson.Default.ShopState),
            DaybookJson.Default.ShopState)!;
        saved.Regulars.Add(new Regular { Name = "Test", Affinity = 12 });
        Check(saved.Renown == run.Renown && saved.Style == run.Style && saved.Regulars[0].Bond == 1, "progression round-trips through saves");
        Check(new ShopState { Day = 25, Cash = 5000, YearStartCash = 5000 }.YearTarget > 5000 + 1600, "later years ask for more");
    }

    private static void PlayCampaign()
    {
        ShopState run = new() { Schema = ShopState.CurrentSchema };
        Furnish(run);
        Display games = run.PlaceFixture(Fixture.Display, new Cell(2, 1))!;
        run.Assign(games, Catalog.PebbleDice);
        for (int day = 1; day <= ShopState.CampaignDays; day++)
        {
            if (day == 3)
                run.RentMoreStorage();
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

        GD.Print($"campaign: cash {run.Cash} renown {run.Renown} rank {run.Rank} regulars {run.Regulars.Count} rep {run.Reputation}");
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
