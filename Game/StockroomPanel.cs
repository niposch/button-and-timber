using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private bool showStockroom;
    private int stockroomSelection = -1;
    private Button stockroomTab = null!;
    private Label stockroomTitle = null!;
    private Label stockroomCount = null!;
    private ProgressBar stockroomMeter = null!;
    private ConfirmationDialog confirmDiscard = null!;
    private (int Toy, int Units) pendingDiscard;

    private void MakeStockroomControls()
    {
        stockroomTab = MakeButton("", ToggleStockroom, dark: true);
        Place(this, stockroomTab, 433, 105, 206, 37);
        stockroomTitle = Put(stockroomTab, "Stockroom", 11, 1, 99, 22, 14, Light);
        stockroomCount = Put(stockroomTab, "", 105, 1, 89, 22, 15, Brass);
        stockroomCount.HorizontalAlignment = HorizontalAlignment.Right;
        stockroomMeter = Meter(7, dark: true);
        Place(stockroomTab, stockroomMeter, 11, 24, 183, 7);

        confirmDiscard = new ConfirmationDialog
        {
            Title = "Discard toys?",
            OkButtonText = "Discard",
            CancelButtonText = "Keep toys"
        };
        confirmDiscard.Confirmed += () =>
        {
            var (toy, units) = pendingDiscard;
            if (state.DiscardStock(toy, units))
            {
                Changed($"Discarded {units} × {Catalog.Toys[toy].Name}");
            }
            else
            {
                status = "Stock changed. Choose a new amount.";
                UpdateReadouts();
            }
        };
        AddChild(confirmDiscard);
    }

    private void UpdateStockroomMeter()
    {
        int used = state.StorageUsed;
        int capacity = state.StorageCapacity;
        bool full = used >= capacity;
        stockroomCount.Text = $"{used} / {capacity}";
        stockroomTab.AccessibilityName = $"Stockroom, {used} of {capacity} spaces used";
        stockroomMeter.MaxValue = capacity;
        stockroomMeter.Value = used;
        stockroomTab.TooltipText = full ? "Stockroom full" : $"{capacity - used} spaces free";
        SelectedStyle(stockroomTab, showStockroom, true);

        int look = (showStockroom ? 2 : 0) + (full ? 1 : 0);
        if (stockroomTab.HasMeta("meter_look") && stockroomTab.GetMeta("meter_look").AsInt32() == look)
            return;

        stockroomTab.SetMeta("meter_look", look);
        stockroomTitle.AddThemeColorOverride("font_color", showStockroom ? Ink : Light);
        stockroomCount.AddThemeColorOverride("font_color", full
            ? ToyArt.C(showStockroom ? "923d31" : "ffb79b") : showStockroom ? Ink : Brass);
        string fill = full ? "d97960" : "d0ac72";
        stockroomMeter.AddThemeStyleboxOverride("fill", Box(fill, fill, 3, 0));
    }

    private void ToggleStockroom()
    {
        showStockroom = !showStockroom;
        if (showStockroom)
        {
            stockroomSelection = -1;
            summary = false;
            reading = null;
            selectedDisplay = null;
            placing = null;
            moving = false;
        }
        status = "";
        Rebuild();
    }

    private void StockroomPanel()
    {
        Header("Stockroom", "");
        StockroomWarning();
        HBoxContainer lease = Row();
        Label rent = In(lease, "", 14, Faded, stretch: true);
        Button expand = InButton(lease, "", () =>
        {
            if (state.RentMoreStorage())
                Changed();
        }, 165);
        expand.TooltipText = "Rent a bigger stockroom. The new rent applies at closing.";
        Label empty = Line("No toys in reserve", 15, Faded);
        Sync(() =>
        {
            rent.Text = $"Rent ${state.DailyRent}/day";
            expand.Text = state.CanRentMore ? $"Rent {state.NextStorageCapacity} · ${state.NextDailyRent}/day" : "Max capacity";
            expand.Disabled = !state.CanRentMore;
            empty.Visible = state.StorageUsed == 0;
        });

        for (int id = 0; id < Catalog.Toys.Length; id++)
        {
            int toy = id;
            bool selected = stockroomSelection == toy;
            var (card, caption) = ToyRow(toy, () =>
            {
                stockroomSelection = toy;
                Rebuild(true);
            }, selected);
            caption.Text = Catalog.Toys[toy].Name;
            caption.OffsetRight = -57;
            Label count = Put(card, "", 258, 0, 50, 41, 15, Ink, true);
            count.HorizontalAlignment = HorizontalAlignment.Right;
            Sync(() =>
            {
                card.Visible = state.Stock[toy] > 0;
                card.AccessibilityName = $"{Catalog.Toys[toy].Name}, {state.Stock[toy]} in reserve";
                count.Text = $"×{state.Stock[toy]}";
            });
            if (selected)
                StockroomDiscardRow(toy);
        }
    }

    private void StockroomDiscardRow(int toy)
    {
        HBoxContainer row = Row();
        In(row, "Quantity", 14, Faded, stretch: true);
        SpinBox units = new()
        {
            Name = "DiscardQuantity", MinValue = 1, Value = 1, Step = 1, SelectAllOnFocus = true,
            Alignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(85, 33)
        };
        LineEdit input = units.GetLineEdit();
        input.AccessibilityName = "Quantity to discard";
        input.AddThemeStyleboxOverride("normal", Box("fcf0cf", "a99667", 3));
        input.AddThemeColorOverride("font_color", Ink);
        input.AddThemeColorOverride("caret_color", Ink);
        units.AddThemeConstantOverride("buttons_width", 22);
        foreach (string direction in new[] { "up", "down" })
        {
            units.AddThemeColorOverride($"{direction}_icon_modulate", Ink);
            units.AddThemeColorOverride($"{direction}_hover_icon_modulate", Ink);
            units.AddThemeColorOverride($"{direction}_pressed_icon_modulate", Clay);
            units.AddThemeColorOverride($"{direction}_disabled_icon_modulate", new Color(Faded, .5f));
        }
        row.AddChild(units);
        Button discard = InButton(row, "Discard…", () =>
        {
            units.Apply();
            pendingDiscard = (toy, (int)units.Value);
            confirmDiscard.DialogText = $"{pendingDiscard.Units} × {Catalog.Toys[toy].Name}\nThis cannot be undone.";
            confirmDiscard.PopupCentered(new Vector2I(380, 136));
        }, 100);
        discard.Name = "DiscardStock";
        Sync(() =>
        {
            row.Visible = state.Stock[toy] > 0;
            units.MaxValue = Math.Max(1, state.Stock[toy]);
        });
    }

    private void StockroomWarning(bool link = false)
    {
        const string warning = "Stockroom full · Batch paused";
        Control notice = link ? AddButton(warning, ToggleStockroom) : Line(warning, 14, ToyArt.C("923d31"));
        Sync(() => notice.Visible = state.OutputBlocked);
    }
}
