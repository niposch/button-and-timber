using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private static readonly string[] DirectionKeys = ["↑", "→", "↓", "←"];
    private static readonly Vector2[] PadPositions = [new(61, 0), new(122, 61), new(61, 122), new(0, 61)];
    private Control craftPad = null!;
    private Label craftResult = null!;

    private void WorkshopPanel()
    {
        if (showWorkshopDecor)
        {
            DecorPanel(makeOnly: true);
            StockroomWarning(link: true);
            ShowQueue();
            return;
        }
        Toy recipe = Catalog.Toys[selectedToy];
        Header("Blueprints", "");
        HBoxContainer categories = Row();
        foreach (ToyFamily family in Catalog.Families)
        {
            int root = Array.FindIndex(Catalog.Toys, toy => toy.Family == family && toy.Prerequisite < 0);
            Button tab = MakeButton("", () => SelectBlueprint(root));
            tab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            tab.CustomMinimumSize = new Vector2(0, 60);
            tab.TooltipText = family.ToString();
            categories.AddChild(tab);
            SelectedStyle(tab, family == recipe.Family, false);
            Place(tab, new FamilyIcon { Family = family, MouseFilter = MouseFilterEnum.Ignore }, 22, 3, 32, 32);
            Label label = Put(tab, family.ToString(), 0, 36, 76, 20, 13, Ink);
            label.HorizontalAlignment = HorizontalAlignment.Center;
        }

        BlueprintBoard board = new() { CustomMinimumSize = new Vector2(0, 194) };
        content.AddChild(board);
        AddTree(board, recipe.Family);

        HBoxContainer selected = Row();
        In(selected, recipe.Name, 19, Ink, true, true);
        Label mastery = In(selected, "", 14, Faded);
        Label details = Line("", 14, Ink);
        Station[] route = [recipe.Station, .. recipe.FollowUp];
        Line(string.Join("  ›  ", route.Select(ShopState.StationName)), 12, Faded);

        if (state.Unlocked[selectedToy])
        {
            int toy = selectedToy;
            ProgressBar experience = Meter(8);
            experience.TooltipText = "Progress to the next level";
            content.AddChild(experience);
            HBoxContainer batch = Row();
            LineEdit units = new()
            {
                Name = "BatchQuantity", Text = quantity.ToString(), MaxLength = 3, Alignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(68, 34),
                TooltipText = $"Type a batch size from 1 to {ShopState.MaxBatch}."
            };
            units.AddThemeStyleboxOverride("normal", Box("fcf0cf", "a99667", 3));
            units.AddThemeColorOverride("font_color", Ink);
            units.AddThemeColorOverride("caret_color", Ink);
            units.TextChanged += input =>
            {
                string digits = new(input.Where(char.IsAsciiDigit).ToArray());
                if (digits != input)
                {
                    units.Text = digits;
                    units.CaretColumn = digits.Length;
                }
                if (int.TryParse(digits, out int number))
                    quantity = Math.Clamp(number, 1, ShopState.MaxBatch);
                UpdateReadouts();
            };
            units.FocusExited += () => units.Text = quantity.ToString();
            units.TextSubmitted += _ => units.ReleaseFocus();
            Button fewer = InButton(batch, "−", () => AdjustQuantity(-1), 32);
            batch.AddChild(units);
            Button more = InButton(batch, "+", () => AdjustQuantity(1), 32);
            Button make = MakeButton("", () =>
            {
                if (state.Queue(toy, quantity))
                    Changed();
            }, strong: true);
            make.CustomMinimumSize = new Vector2(146, 34);
            batch.AddChild(make);
            Sync(() =>
            {
                mastery.Text = $"Lv {state.Level(toy)}";
                details.Text = $"Make ${state.ToyCost(toy)}   Sell ${state.Prices[toy]}   ✓ {state.Yield(toy)}%";
                details.TooltipText = state.NeedsHands(toy) ? "Use the tools at the workshop for this toy." :
                    "This toy can finish while you run the shop.";
                experience.Value = state.MasteryProgress(toy) * 100;
                fewer.Disabled = quantity <= 1;
                more.Disabled = quantity >= ShopState.MaxBatch;
                make.Text = state.Orders.Count >= ShopState.MaxOrders ? "Queue full" : $"Make · ${state.ToyCost(toy) * quantity}";
                make.Disabled = state.Orders.Count >= ShopState.MaxOrders || state.Cash < state.ToyCost(toy) * quantity;
            });

            void AdjustQuantity(int by)
            {
                quantity = Math.Clamp(quantity + by, 1, ShopState.MaxBatch);
                units.Text = quantity.ToString();
                UpdateReadouts();
            }
        }
        else
        {
            int toy = selectedToy;
            Line($"Make ${recipe.Materials}   Sell ${recipe.Value}   ✓ {recipe.BaseYield}%", 14, Ink);
            Button learn = AddButton($"Learn · ${recipe.Research}", () =>
            {
                if (state.Research(toy))
                    Changed();
            }, true);
            Sync(() =>
            {
                mastery.Text = $"Tier {recipe.Tier}";
                details.Text = $"{Catalog.Toys[recipe.Prerequisite].Name} · Lv {recipe.RequiredLevel}";
                learn.Disabled = !state.CanResearch(toy);
            });
        }

        StockroomWarning(link: true);
        ShowQueue();
    }

    private void SelectBlueprint(int toy)
    {
        selectedToy = toy;
        Play(click, -21);
        Rebuild(true);
    }

    private void AddTree(BlueprintBoard board, ToyFamily family)
    {
        int[] ids = Enumerable.Range(0, Catalog.Toys.Length).Where(toy => Catalog.Toys[toy].Family == family).ToArray();
        Dictionary<int, Rect2> nodes = [];
        foreach (var tier in ids.GroupBy(toy => Catalog.Toys[toy].Tier).OrderBy(group => group.Key))
        {
            int[] items = tier.ToArray();
            for (int i = 0; i < items.Length; i++)
            {
                int toy = items[i];
                int parent = Catalog.Toys[toy].Prerequisite;
                float y = items.Length > 1 ? 17 + i * 86 : tier.Key >= 3 ? nodes[parent].Position.Y : 60;
                nodes[toy] = new Rect2(7 + (tier.Key - 1) * 81, y, 67, 74);
            }
        }

        foreach (int toy in ids)
        {
            Rect2 tile = nodes[toy];
            Toy recipe = Catalog.Toys[toy];
            if (recipe.Prerequisite >= 0)
            {
                Rect2 parent = nodes[recipe.Prerequisite];
                int linkIndex = board.Links.Count;
                board.Links.Add((new Vector2(parent.End.X, parent.GetCenter().Y),
                    new Vector2(tile.Position.X, tile.GetCenter().Y), state.Unlocked[toy] || state.MeetsPrerequisite(toy)));
                Sync(() =>
                {
                    var link = board.Links[linkIndex];
                    bool ready = state.Unlocked[toy] || state.MeetsPrerequisite(toy);
                    if (ready == link.Ready)
                        return;
                    board.Links[linkIndex] = (link.From, link.To, ready);
                    board.QueueRedraw();
                });
            }

            Button card = MakeButton("", () => SelectBlueprint(toy));
            card.TooltipText = recipe.Name;
            Place(board, card, tile.Position.X, tile.Position.Y, tile.Size.X, tile.Size.Y);
            Control art = new ToySwatch { Toy = toy, MouseFilter = MouseFilterEnum.Ignore };
            Place(card, art, 7, 3, 53, 44);
            Label stock = Put(card, "", 5, 47, 33, 24, 12, Ink);
            Label level = Put(card, "", 34, 47, 28, 24, 11, Ink);
            level.HorizontalAlignment = HorizontalAlignment.Right;
            LockIcon locked = new() { MouseFilter = MouseFilterEnum.Ignore };
            Place(card, locked, 49, 53, 11, 13);
            Sync(() =>
            {
                bool unlocked = state.Unlocked[toy];
                bool learnable = state.MeetsPrerequisite(toy);
                int look = toy == selectedToy ? 3 : unlocked ? 2 : learnable ? 1 : 0;
                if (!card.HasMeta("tile_look") || card.GetMeta("tile_look").AsInt32() != look)
                {
                    card.SetMeta("tile_look", look);
                    card.AddThemeStyleboxOverride("normal", Box(look switch
                    {
                        3 => "f2cd83", 2 => "eee0b7", 1 => "bbd1ad", _ => "b0c5bd"
                    }, look == 3 ? "fff2ba" : "7eaa9f", 5));
                    card.AddThemeStyleboxOverride("hover", Box("f7e6ab", "fff2ba", 5));
                }
                art.Modulate = unlocked || learnable ? Colors.White : new Color(.8f, .83f, .79f, .84f);
                stock.Text = unlocked ? $"×{state.Stock[toy] + state.StockOnDisplay(toy)}" : $"${recipe.Research}";
                level.Text = unlocked ? $"Lv {state.Level(toy)}" : learnable ? "+" : "";
                locked.Visible = !unlocked && !learnable;
            });
        }
    }

    private void ShowQueue()
    {
        if (state.Orders.Count > 0 || state.HeldOrders.Count > 0 || state.LastBatch != null)
            Divider();
        foreach (Order order in state.Orders)
            JobRow(order, false);
        foreach (Order order in state.HeldOrders)
            JobRow(order, true);
        if (state.Orders.Count == 0 && state.LastBatch is Order finished)
            JobRow(finished, false);

        void JobRow(Order order, bool held)
        {
            HBoxContainer row = Row();
            Control ticket = new() { CustomMinimumSize = new Vector2(217, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(ticket);
            Rectangle(ticket, 0, 0, 217, 48, order == state.Orders.FirstOrDefault() ? "d8d4a3" : "e7d2ab");
            Control picture = order.Decoration is Fixture fixture ?
                Icon(StreetView.SpriteName(fixture), FixtureCrop(fixture)) :
                new ToySwatch { Toy = order.Toy, MouseFilter = MouseFilterEnum.Ignore };
            Place(ticket, picture, 5, 7, 34, 34);
            Put(ticket, ShopState.ProjectName(order), 45, 4, 170, 20, 13, Ink);
            Label marks = Put(ticket, "", 45, 24, 171, 18, 12, Faded);
            if (order.Remaining > 0)
            {
                Button action = InButton(row, held ? "Resume" : "Set aside", () =>
                {
                    if (held ? state.Resume(order) : state.SetAside(order))
                        Changed();
                }, 89);
                action.TooltipText = held ? "Continue this batch using its existing materials." :
                    "Stop this batch and keep the unfinished work.";
                Sync(() => action.Disabled = held ? state.Orders.Count >= ShopState.MaxOrders :
                    state.HeldOrders.Count >= ShopState.MaxHeld);
            }
            Sync(() =>
            {
                string station = ShopState.StationName(ShopState.StationFor(order));
                if (order.Remaining == 0)
                    marks.Text = $"✓ {order.Made}   × {order.Scrapped}";
                else if (order.Transfer > 0 && !held)
                    marks.Text = $"Cart → {station} · ×{order.Remaining}";
                else
                    marks.Text = $"{order.Stage + 1}/{ShopState.Stages(order)} {station} · ×{order.StageLeft}";
                ticket.TooltipText = $"Finished {order.Made} · Scrapped {order.Scrapped} · Unfinished {order.Remaining}";
            });
        }
    }

    private void MakeCraftPad(Control host)
    {
        craftPad = new Control { MouseFilter = MouseFilterEnum.Ignore };
        host.AddChild(craftPad);
        AnchorTo(craftPad, LayoutPreset.BottomLeft, 28, -236, 210, -54);
        foreach (Tool tool in Tools)
        {
            int i = (int)tool;
            Button button = MakeButton("", () => Strike(tool), dark: true);
            button.TooltipText = $"{tool}  {DirectionKeys[i]} / {"WDSA"[i]}";
            Vector2 at = PadPositions[i];
            Place(craftPad, button, at.X, at.Y, 60, 60);
            Place(button, Icon(tool.ToString(), new Rect2(19, 15, 154, 112)), 5, 0, 50, 43);
            Place(button, new DirectionIcon { Direction = tool, MouseFilter = MouseFilterEnum.Ignore }, 21, 40, 18, 16);
            craftButtons.Add(button);
        }
        Rectangle(craftPad, 61, 61, 60, 60, "27433e");
        craftResult = Put(craftPad, "", 61, 62, 60, 58, 25, Light);
        craftResult.HorizontalAlignment = HorizontalAlignment.Center;
    }

    private void UpdateCraftPad()
    {
        craftPad.Visible = room == Room.Workshop && !summary && state.Orders.Count > 0;
        stage.ShowToolPrompt = craftPad.Visible && speed > 0;
        if (!craftPad.Visible)
            return;

        Tool? prompt = state.Prompt;
        for (int i = 0; i < craftButtons.Count; i++)
        {
            Button button = craftButtons[i];
            bool hot = prompt == (Tool)i && speed > 0;
            if (!button.HasMeta("tool_hot") || button.GetMeta("tool_hot").AsBool() != hot)
            {
                button.SetMeta("tool_hot", hot);
                button.AddThemeStyleboxOverride("normal", Box(hot ? "718e72" : "344f48", hot ? "fff0a8" : "819b8c", 8));
                button.AddThemeStyleboxOverride("hover", Box(hot ? "86a181" : "4b6b5c", hot ? "fff0a8" : "a7bc9e", 8));
            }
        }

        craftResult.Text = speed == 0 ? "II" : benchFeedbackLeft > 0 && prompt == null ? benchFeedback : "";
        craftResult.AddThemeColorOverride("font_color", speed == 0 ? Cream : benchFeedbackColor);
    }
}
