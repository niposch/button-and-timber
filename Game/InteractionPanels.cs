using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private static readonly Tool[] Tools = Enum.GetValues<Tool>();
    private static readonly string[] ToolKeys = ["A", "S", "D", "F"];
    private Control craftStrip = null!;
    private Label craftPrompt = null!;
    private Label craftBatch = null!;
    private ProgressBar craftTime = null!;
    private readonly List<Button> craftButtons = [];

    private void ShopPanel()
    {
        if (state.Shift == Shift.Open)
        {
            if (selectedDisplay is Display openDisplay)
            {
                FixturePanel(openDisplay, true);
                return;
            }

            Header("On the floor", "Select a customer or display in the shop.");
            var (_, demand) = ToyRow(state.FeaturedToy);
            demand.Text = $"In demand today · {Catalog.Toys[state.FeaturedToy].Name}";
            foreach (Display itemOnFloor in state.Displays)
                DisplayRow(itemOnFloor);
            Divider();
            RegisterStatus();
            return;
        }

        Header("Shop layout", placing.HasValue ? "Click a highlighted tile to place the fixture." :
            "Choose a fixture, then place it on the floor.");
        if (placing is Fixture pending)
            AddButton($"Cancel placing {ShopState.FixtureName(pending).ToLowerInvariant()}  ·  Esc", CancelPlacement);

        HBoxContainer row = Row();
        foreach (Fixture kind in Enum.GetValues<Fixture>())
        {
            int owned = state.Furniture[(int)kind];
            bool installed = kind == Fixture.Register && state.Register != null;
            string sub = installed ? "Placed" : owned > 0 ? $"{owned} in crate" : $"${ShopState.FurnitureCost(kind)}";
            Button furniture = MakeButton("", () => BeginPlacement(kind, false));
            furniture.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            furniture.CustomMinimumSize = new Vector2(0, 94);
            furniture.Disabled = installed || (owned == 0 && state.Cash < ShopState.FurnitureCost(kind));
            SelectedStyle(furniture, placing == kind && !moving, false);
            row.AddChild(furniture);
            Rect2 crop = kind == Fixture.Plant ? new Rect2(36, 12, 60, 62) : new Rect2(40, 15, 112, 112);
            Place(furniture, Icon(StreetView.SpriteName(kind), crop), 11, 1, 54, 49);
            Put(furniture, ShopState.FixtureName(kind), 0, 51, 76, 22, 14, Ink).HorizontalAlignment = HorizontalAlignment.Center;
            Put(furniture, sub, 0, 71, 76, 18, 12, Faded).HorizontalAlignment = HorizontalAlignment.Center;
        }

        if (!placing.HasValue && selectedDisplay is Display item)
        {
            Divider();
            FixturePanel(item, false);
        }
        else if (state.Displays.Count > 0)
        {
            Divider();
            foreach (Display display in state.Displays)
                DisplayRow(display);
        }
        else
            Paragraph("Your crate has a register, two shelves, and a table. Place the register and stock at least one display to open.");
    }

    private void FixturePanel(Display item, bool trading)
    {
        bool isRegister = item.Style == Fixture.Register;
        string name = ShopState.FixtureName(item.Style);
        if (trading)
        {
            Header(name, isRegister ? "Shoppers bring their baskets here to pay." :
                item.Capacity > 0 ? "Choose what goes on this display." : "");
            Button back = AddButton("‹  Shop floor", ClearSelection);
            back.Alignment = HorizontalAlignment.Left;
        }
        else
            Line(item.Capacity > 0 ? $"{name} assortment" : name, 17, Ink, true);

        if (isRegister)
            RegisterStatus();
        else if (item.Capacity > 0)
        {
            for (int i = 0; i < Catalog.Toys.Length; i++)
            {
                int toy = i;
                if (!state.Unlocked[toy])
                    continue;
                var (_, caption) = ToyRow(toy, () =>
                {
                    state.Assign(item, toy);
                    Changed();
                }, item.Toy == toy);
                Sync(() => caption.Text = $"{Catalog.Toys[toy].Name}   ·   {state.Stock[toy]} in reserve");
            }
            if (item.Toy >= 0)
            {
                int toy = item.Toy;
                Label inventory = Line("", 13, Faded);
                HBoxContainer price = Row();
                Label priceText = In(price, "", 16, Ink, true, true);
                Button lower = InButton(price, "−", () => Price(toy, -1), 40);
                Button raise = InButton(price, "+", () => Price(toy, 1), 40);
                Sync(() =>
                {
                    inventory.Text = $"{item.Count}/{item.Capacity} on display · Restocks from reserve";
                    priceText.Text = $"Price  ${state.Prices[toy]}";
                    lower.Disabled = state.Prices[toy] <= 1;
                    raise.Disabled = state.Prices[toy] >= Catalog.Toys[toy].Value * 3;
                });
                if (state.Shift != Shift.Open)
                    AddButton("Clear this display", () => { state.Assign(item, -1); Changed(); });
            }
        }

        if (state.Shift == Shift.Open)
            return;
        HBoxContainer arrange = Row();
        InButton(arrange, "Move", () => BeginPlacement(item.Style, true));
        InButton(arrange, "Pack away", () =>
        {
            if (!state.Pack(item))
                return;
            selectedDisplay = null;
            Changed();
        });
    }

    private void DisplayRow(Display item)
    {
        var (button, caption) = ToyRow(item.Toy, () => PickDisplay(item));
        if (item.Toy < 0)
        {
            Rect2 crop = item.Style == Fixture.Plant ? new Rect2(36, 12, 60, 62) : new Rect2(40, 15, 112, 112);
            Place(button, Icon(StreetView.SpriteName(item.Style), crop), 7, 4, 32, 31);
        }
        Sync(() =>
        {
            caption.Text = item.Capacity == 0 ? ShopState.FixtureName(item.Style) : item.Toy < 0 ?
                $"{ShopState.FixtureName(item.Style)} · Choose a toy" :
                $"{Catalog.Toys[item.Toy].Name}   {item.Count}/{item.Capacity}   ·   ${state.Prices[item.Toy]}";
            caption.AddThemeColorOverride("font_color", item.Toy >= 0 && item.Count == 0 ? Clay : Ink);
        });
    }

    private void RegisterStatus()
    {
        if (state.Shift != Shift.Open)
            return;
        Label line = Line("", 14, Faded);
        Button ring = AddButton("Ring up customer", () => state.RingUp(), true);
        Sync(() =>
        {
            int waiting = state.Customers.Count(person => person.Phase == VisitPhase.Checkout);
            line.Text = waiting == 0 ? "The register is clear." : waiting == 1 ? "1 shopper checking out" : $"{waiting} shoppers checking out";
            ring.Disabled = state.ReadyToPay == null;
        });
    }

    private void WorkshopPanel()
    {
        Header("Pattern book", "Compare stock, material cost, and bench skill.");
        Control columns = new() { CustomMinimumSize = new Vector2(0, 20) };
        content.AddChild(columns);
        Put(columns, "Toy", 39, 0, 132, 20, 12, Faded);
        Put(columns, "Stock", 177, 0, 42, 20, 12, Faded);
        Put(columns, "Cost", 226, 0, 36, 20, 12, Faded);
        Put(columns, "Skill", 274, 0, 41, 20, 12, Faded);

        VBoxContainer patterns = new();
        patterns.AddThemeConstantOverride("separation", 3);
        content.AddChild(patterns);
        for (int i = 0; i < Catalog.Toys.Length; i++)
            PatternRow(patterns, i);

        Toy toy = Catalog.Toys[selectedToy];
        if (!state.Unlocked[selectedToy])
        {
            string prerequisite = Catalog.Toys[toy.Prerequisite].Name;
            Paragraph($"Practice with {prerequisite}   {Math.Min(state.Practice[toy.Prerequisite], toy.Practice)}/{toy.Practice}");
            Button research = AddButton($"Learn {toy.Name} · ${toy.Research}", () =>
            {
                if (state.Research(selectedToy))
                    Changed();
            }, true);
            Sync(() => research.Disabled = !state.CanResearch(selectedToy));
            return;
        }

        Label details = Line("", 13, Faded);
        HBoxContainer batch = Row();
        Label units = In(batch, "", 14, Ink, true, true);
        Button fewer = InButton(batch, "−", () => { quantity--; UpdateReadouts(); }, 32);
        Button more = InButton(batch, "+", () => { quantity++; UpdateReadouts(); }, 32);
        Button make = InButton(batch, "", () =>
        {
            if (state.Queue(selectedToy, quantity))
                Changed($"Queued {quantity} × {toy.Name}");
        }, 152);
        make.AddThemeStyleboxOverride("normal", Box("426d5c", "315649", 4));
        make.AddThemeStyleboxOverride("hover", Box("54816a", "315649", 4));
        make.AddThemeColorOverride("font_color", Light);
        make.AddThemeColorOverride("font_hover_color", Light);
        Sync(() =>
        {
            string mode = state.NeedsHands(selectedToy) ? "Needs supervision" : "Can run unattended";
            details.Text = $"{mode} · Base yield {state.Yield(selectedToy)}%";
            units.Text = $"Batch {quantity}";
            fewer.Disabled = quantity <= 1;
            more.Disabled = quantity >= 12;
            make.Text = state.Orders.Count >= 4 ? "Queue full" : $"Make · ${state.ToyCost(selectedToy) * quantity}";
            make.Disabled = state.Orders.Count >= 4 || state.Cash < state.ToyCost(selectedToy) * quantity;
        });
    }

    private void PatternRow(VBoxContainer patterns, int toy)
    {
        Button row = MakeButton("", () => { selectedToy = toy; Rebuild(true); });
        row.CustomMinimumSize = new Vector2(0, 29);
        row.TooltipText = state.Unlocked[toy] ? Catalog.Toys[toy].Name : $"{Catalog.Toys[toy].Name} · Locked pattern";
        patterns.AddChild(row);
        SelectedStyle(row, selectedToy == toy, false);
        Place(row, new ToySwatch { Toy = toy, MouseFilter = MouseFilterEnum.Ignore }, 6, 3, 26, 23);
        bool unlocked = state.Unlocked[toy];
        Put(row, Catalog.Toys[toy].Name, 39, 0, 134, 29, 13, unlocked ? Ink : Faded);
        Label stock = Put(row, "", 180, 0, 44, 29, 13, Ink);
        Label cost = Put(row, "", 226, 0, 40, 29, 13, Faded);
        Label skill = Put(row, "", 273, 0, 44, 29, 12, Ink);
        Sync(() =>
        {
            stock.Text = unlocked ? (state.Stock[toy] + state.StockOnDisplay(toy)).ToString() : "—";
            cost.Text = $"${state.ToyCost(toy)}";
            skill.Text = unlocked ? $"Lv {state.Level(toy) + 1}" : "Locked";
        });
    }

    private void TownPanel()
    {
        Header("Town orders", state.Fair ? $"{Catalog.Fairs[state.Season]} is on today." : $"Next up · {Catalog.Fairs[state.Season]}");
        Contract(Catalog.Fairs[state.Season], state.CommissionToy, state.CommissionCount, state.CommissionPay,
            () => state.Commissions[state.Season], () => state.Deliver());
        for (int index = 0; index < 2; index++)
        {
            int extra = index;
            var order = state.ExtraOrder(extra);
            Contract(order.Name, order.Toy, order.Count, order.Pay,
                () => (state.SideOrders[state.Season] & (1 << extra)) != 0,
                () => state.DeliverExtra(extra), extra == 1 ? 58 : 0);
        }
        Divider();
        Button permit = AddButton(state.Expanded ? "Adjoining room opened" : $"Open adjoining room · ${ShopState.Permit}", () =>
        {
            if (state.Expand())
                Changed();
        });
        permit.TooltipText = "Adds two columns of floor space. Build while the shop is closed.";
        Sync(() => permit.Disabled = state.Expanded || state.Cash < ShopState.Permit || state.Shift == Shift.Open);

        void Contract(string name, int toy, int count, int pay, Func<bool> filled, Func<bool> deliver, int rep = 0)
        {
            Line(name, 17, Ink, true);
            var (_, caption) = ToyRow(toy);
            caption.Text = $"{count} × {Catalog.Toys[toy].Name}";
            HBoxContainer row = Row();
            Label available = In(row, "", 13, Faded, true);
            Button send = InButton(row, "", () => { if (deliver()) Changed(); }, 127);
            Sync(() =>
            {
                available.Text = rep > state.Reputation ? $"Needs reputation {rep}" : $"{state.Stock[toy]}/{count} in reserve";
                send.Text = filled() ? "Delivered" : $"Deliver · ${pay}";
                send.Disabled = filled() || rep > state.Reputation || state.Stock[toy] < count;
            });
        }
    }

    private static TextureRect Icon(string name, Rect2 crop)
    {
        return new TextureRect
        {
            Texture = new AtlasTexture
            {
                Atlas = GD.Load<Texture2D>($"res://Art/Furniture/{name}.png"), Region = crop
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
    }

    private void MakeBenchStrip()
    {
        craftStrip = new Control { MouseFilter = MouseFilterEnum.Ignore };
        Place(this, craftStrip, 155, 521, 600, 126);
        Rectangle(craftStrip, 0, 0, 600, 126, "355b51");
        Rectangle(craftStrip, 2, 2, 596, 122, "263e38");
        craftPrompt = Put(craftStrip, "", 15, 8, 375, 30, 23, Brass, true);
        craftBatch = Put(craftStrip, "", 332, 9, 250, 27, 14, Cream);
        craftBatch.HorizontalAlignment = HorizontalAlignment.Right;
        craftTime = Meter(5, true);
        Place(craftStrip, craftTime, 15, 41, 570, 5);
        foreach (Tool tool in Tools)
        {
            int i = (int)tool;
            Button button = MakeButton("", () => Strike(tool), dark: true);
            button.TooltipText = $"Use {tool.ToString().ToLowerInvariant()} ({ToolKeys[i]})";
            Place(craftStrip, button, 15 + i * 144, 53, 137, 61);
            Place(button, Icon(tool.ToString(), new Rect2(19, 15, 154, 112)), 5, 3, 58, 55);
            Put(button, tool.ToString(), 66, 11, 70, 24, 15, Light);
            Put(button, ToolKeys[i], 67, 34, 19, 16, 13, Brass);
            craftButtons.Add(button);
        }
    }

    private void UpdateBenchStrip()
    {
        craftStrip.Visible = room == Room.Workshop && !summary && state.Orders.Count > 0;
        if (!craftStrip.Visible)
            return;

        Order order = state.Orders[0];
        Tool? prompt = state.Prompt;
        craftBatch.Text = $"{Catalog.Toys[order.Toy].Name} · {order.Remaining} left" +
            (state.Orders.Count > 1 ? $"  /  +{state.Orders.Count - 1} queued" : "");
        craftPrompt.Text = speed == 0 ? "Bench paused" : benchFeedbackLeft > 0 && prompt == null ? benchFeedback :
            prompt is Tool tool ? $"Use the {tool.ToString().ToLowerInvariant()}" : "Get ready";
        craftPrompt.AddThemeColorOverride("font_color",
            benchFeedbackLeft > 0 && prompt == null && speed > 0 ? benchFeedbackColor : Brass);
        craftTime.Value = prompt.HasValue ? 100 * (1 - order.Work / state.ToolWindow(order.Toy)) : 0;
        foreach (Button button in craftButtons)
            button.Disabled = speed == 0 || prompt == null;
    }

    private void Strike(Tool tool)
    {
        if (summary || speed == 0 || state.Prompt is not Tool expected)
            return;
        int orders = state.Orders.Count;
        if (!state.UseTool(tool))
            return;
        ShowBenchCue(tool == expected ? state.LastBenchCue : BenchCue.Mistake);
        if (orders != state.Orders.Count)
        {
            Save();
            Rebuild(true);
        }
        else
            UpdateReadouts();
    }

    private void ShowBenchCue(BenchCue cue)
    {
        (benchFeedback, benchFeedbackColor) = cue switch
        {
            BenchCue.Correct => ("Clean stroke", ToyArt.C("b8d5a1")),
            BenchCue.Finished => ("Toy finished", ToyArt.C("d8dda3")),
            BenchCue.Scrapped => ("Toy scrapped", ToyArt.C("efaa91")),
            _ => ("Wrong tool · yield down", ToyArt.C("efaa91"))
        };
        benchFeedbackLeft = .83;
        stage.BenchStroke(cue is BenchCue.Correct or BenchCue.Finished);
        switch (cue)
        {
            case BenchCue.Correct: Play(wood, -17, 1.1f); break;
            case BenchCue.Finished: Play(success, -15); break;
            case BenchCue.Scrapped:
            case BenchCue.Mistake: Play(warning, -18); break;
        }
    }
}
