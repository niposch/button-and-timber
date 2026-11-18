using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private static readonly Tool[] Tools = Enum.GetValues<Tool>();
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

            Header("Shop floor", "");
            foreach (Display itemOnFloor in state.Displays)
                DisplayRow(itemOnFloor);
            Divider();
            RegisterStatus();
            return;
        }

        if (showDecor)
        {
            DecorPanel();
            return;
        }

        Header("Shop layout", "");
        if (placing.HasValue)
            AddButton("Cancel", CancelPlacement);

        HBoxContainer row = Row();
        foreach (Fixture kind in new[] { Fixture.Display, Fixture.Table, Fixture.Register })
        {
            int owned = state.Furniture[(int)kind];
            bool installed = kind == Fixture.Register && state.Register != null;
            string sub = installed ? "✓" : owned > 0 ? $"×{owned}" : $"${ShopState.FurnitureCost(kind)}";
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
        Button decorations = MakeButton("", () =>
        {
            showDecor = true;
            placing = null;
            Rebuild();
        });
        decorations.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        decorations.CustomMinimumSize = new Vector2(0, 94);
        row.AddChild(decorations);
        Place(decorations, Icon("FlowerCrate", new Rect2(30, 19, 132, 112)), 11, 1, 54, 49);
        Put(decorations, "Decor", 0, 52, 76, 27, 14, Ink).HorizontalAlignment = HorizontalAlignment.Center;

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
    }

    private void FixturePanel(Display item, bool trading)
    {
        bool isRegister = item.Style == Fixture.Register;
        string name = ShopState.FixtureName(item.Style);
        if (trading)
        {
            Header(name, "");
            Button back = AddButton("‹  Shop floor", ClearSelection);
            back.Alignment = HorizontalAlignment.Left;
        }
        else
            Line(item.Capacity > 0 ? "Choose a toy" : name, 17, Ink, true);

        if (isRegister)
            RegisterStatus();
        else if (item.Capacity > 0)
        {
            for (int i = 0; i < Catalog.Toys.Length; i++)
            {
                int toy = i;
                if (!state.Unlocked[toy])
                    continue;
                var (card, caption) = ToyRow(toy, () =>
                {
                    if (state.Assign(item, toy))
                        Changed();
                    else
                        Changed("Stockroom full");
                }, item.Toy == toy);
                AddFamilyBadge(card, Catalog.Toys[toy].Family);
                Sync(() => caption.Text = $"{Catalog.Toys[toy].Name}   ×{state.Stock[toy]}");
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
                    inventory.Text = $"{item.Count}/{item.Capacity} on display";
                    priceText.Text = $"${state.Prices[toy]}";
                    lower.Disabled = state.Prices[toy] <= 1;
                    raise.Disabled = state.Prices[toy] >= Catalog.Toys[toy].Value * 3;
                });
                if (state.Shift != Shift.Open)
                    AddButton("Empty", () => Changed(state.Assign(item, -1) ? "" : "Stockroom full"));
            }
        }

        if (state.Shift == Shift.Open)
            return;
        HBoxContainer arrange = Row();
        InButton(arrange, "Move", () => BeginPlacement(item.Style, true));
        InButton(arrange, "Pack away", () =>
        {
            if (!state.Pack(item))
            {
                Changed("Stockroom full");
                return;
            }
            selectedDisplay = null;
            Changed();
        });
    }

    private void DisplayRow(Display item)
    {
        var (button, caption) = ToyRow(item.Toy, () => PickDisplay(item));
        if (item.Toy < 0)
        {
            Place(button, Icon(StreetView.SpriteName(item.Style), FixtureCrop(item.Style)), 7, 4, 32, 31);
        }
        else
            AddFamilyBadge(button, Catalog.Toys[item.Toy].Family);
        Sync(() =>
        {
            caption.Text = item.Capacity == 0 ? ShopState.FixtureName(item.Style) : item.Toy < 0 ?
                $"{ShopState.FixtureName(item.Style)}  +":
                $"{Catalog.Toys[item.Toy].Name}   {item.Count}/{item.Capacity}   ·   ${state.Prices[item.Toy]}";
            caption.AddThemeColorOverride("font_color", item.Toy >= 0 && item.Count == 0 ? Clay : Ink);
        });
    }

    private void RegisterStatus()
    {
        if (state.Shift != Shift.Open)
            return;
        Label line = Line("", 14, Faded);
        Sync(() =>
        {
            int waiting = state.Customers.Count(person => person.Phase == VisitPhase.Checkout);
            line.Text = waiting == 0 ? "" : $"Checkout  ×{waiting}";
        });
    }

    private void AddFamilyBadge(Button card, ToyFamily family)
    {
        Place(card, new FamilyIcon { Family = family, MouseFilter = MouseFilterEnum.Ignore }, 281, 6, 29, 29);
    }

    private void TownPanel()
    {
        Header("Town orders", "");
        Upgrades();
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
            var (card, caption) = ToyRow(toy);
            caption.Text = $"{count} × {Catalog.Toys[toy].Name}";
            AddFamilyBadge(card, Catalog.Toys[toy].Family);
            HBoxContainer row = Row();
            Label available = In(row, "", 13, Faded, true);
            Button send = InButton(row, "", () => { if (deliver()) Changed(); }, 127);
            Sync(() =>
            {
                available.Text = rep > state.Reputation ? $"Reputation {rep}" : $"{state.Stock[toy]}/{count}";
                send.Text = filled() ? "Delivered" : $"Deliver · ${pay}";
                send.Disabled = filled() || rep > state.Reputation || state.Stock[toy] < count;
            });
        }
    }

    private void DecorPanel(bool makeOnly = false)
    {
        Header("Decor", placing.HasValue ? "Pick a tile" : Catalog.Seasons[state.Season]);
        if (!makeOnly)
            AddButton(placing.HasValue ? "Cancel" : "‹  Layout", () =>
        {
            if (placing.HasValue)
                CancelPlacement();
            else
            {
                showDecor = false;
                Rebuild();
            }
        });
        foreach (Decoration style in Decorations.All.OrderByDescending(style => style.Season == state.Season))
        {
            bool seasonal = style.Season >= 0;
            bool inSeason = style.Season == state.Season;
            var (card, caption) = ToyRow(-1);
            Place(card, Icon(StreetView.SpriteName(style.Fixture), FixtureCrop(style.Fixture)), 5, 3, 36, 32);
            caption.Text = style.Name;
            if (style.Family is ToyFamily family)
                AddFamilyBadge(card, family);
            string when = seasonal ? Catalog.Seasons[style.Season] : "All year";
            string perk = seasonal ? $" · +2 {style.Family} in season" : " · +1 appeal";
            Label note = Line(when + perk, 12, inSeason ? Ink : Faded);
            note.TooltipText = "More appeal brings visitors sooner. Each style counts once.";
            HBoxContainer buttons = Row();
            Button? build = makeOnly ? null : InButton(buttons, "", () => BeginPlacement(style.Fixture, false));
            Button craft = InButton(buttons, $"Make ${style.Materials}", () =>
            {
                if (state.MakeDecoration(style.Fixture))
                    Changed($"{style.Name} sent to workshop");
            });
            Sync(() =>
            {
                int owned = state.Furniture[(int)style.Fixture];
                if (build != null)
                {
                    build.Text = owned > 0 ? $"Place ×{owned}" : $"Buy ${style.BuyPrice}";
                    build.Disabled = state.Shift == Shift.Open || (owned == 0 && state.Cash < style.BuyPrice);
                }
                craft.Disabled = state.Orders.Count >= ShopState.MaxOrders || state.Cash < style.Materials;
            });
        }
    }

    private void Upgrades()
    {
        Label capacity = Line("", 14, Ink);
        HBoxContainer first = Row();
        Button storage = InButton(first, "", () =>
        {
            if (state.RentMoreStorage())
                Changed();
        });
        Button cart = InButton(first, "", () =>
        {
            if (state.UpgradeCart())
                Changed();
        });
        storage.TooltipText = "Rent a bigger stockroom. Rent comes out of the till at closing.";
        cart.TooltipText = "Carry more workpieces per trip between machines.";
        Sync(() =>
        {
            capacity.Text = $"Stockroom {state.StorageUsed}/{state.StorageCapacity}   ·   Cart ×{state.CartCapacity}";
            storage.Text = state.CanRentMore ? $"{state.NextStorageCapacity} · ${state.NextDailyRent}/day" : "Stockroom max";
            cart.Text = state.CanUpgradeCart ? $"Cart ×{state.NextCartCapacity} · ${state.CartUpgradePrice}" : "Cart max";
            storage.Disabled = !state.CanRentMore;
            cart.Disabled = !state.CanUpgradeCart || state.Cash < state.CartUpgradePrice;
        });
        Divider();
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

    private static Rect2 FixtureCrop(Fixture fixture) => fixture == Fixture.Plant
        ? new Rect2(35, 10, 65, 70) : new Rect2(39, 9, 114, 112);

    private void Strike(Tool tool)
    {
        if (summary || speed == 0 || state.Prompt is not Tool expected)
            return;
        int orders = state.Orders.Count;
        if (!state.UseTool(tool))
            return;
        ShowBenchCue(tool == expected ? state.LastBenchCue : BenchCue.Mistake, guided: true);
        if (orders != state.Orders.Count)
        {
            Save();
            Rebuild(true);
        }
        else
            UpdateReadouts();
    }

    private void ShowBenchCue(BenchCue cue, bool guided = false)
    {
        (benchFeedback, benchFeedbackColor) = cue switch
        {
            BenchCue.Correct => ("✓", ToyArt.C("b8d5a1")),
            BenchCue.Finished => ("+1", ToyArt.C("fbdd91")),
            BenchCue.LevelUp => ("★", ToyArt.C("fbdd91")),
            BenchCue.Scrapped => ("×", ToyArt.C("efaa91")),
            _ => ("×", ToyArt.C("efaa91"))
        };
        benchFeedbackLeft = .83;
        stage.BenchStroke(cue is BenchCue.Correct or BenchCue.Finished or BenchCue.LevelUp);
        switch (cue)
        {
            case BenchCue.Correct:
            case BenchCue.Finished:
            case BenchCue.LevelUp:
                if (guided)
                    PlayCraftSuccess(cue);
                else
                    Play(wood, -25);
                break;
            case BenchCue.Scrapped:
            case BenchCue.Mistake:
                craftStreak = 0;
                Play(warning, -18);
                break;
        }
    }
}
