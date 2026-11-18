using System.Text.Json;
using Godot;

namespace ButtonAndTimber;

public partial class ShopGame : Control
{
    private static readonly Color Ink = ToyArt.C("343f36");
    private static readonly Color Clay = ToyArt.C("86624e");
    private static readonly Color Faded = ToyArt.C("726451");
    private static readonly Color Cream = ToyArt.C("f1e3bc");
    private static readonly Color Brass = ToyArt.C("e6be7f");
    private static readonly Color Light = ToyArt.C("f9ecd0");
    private static readonly int[] Rates = [0, 1, 3, 8];
    private const string SaveFile = "user://alder-lane.json";

    private readonly List<Action> refresh = [];
    private readonly SystemFont serif = new()
    {
        FontNames = ["Georgia", "DejaVu Serif", "Noto Serif"],
        FontWeight = 700
    };

    private ShopState state = new();
    private Room room;
    private int selectedDisplay;
    private int selectedToy = 1;
    private int quantity = 5;
    private int speed = 1;
    private bool moving;
    private bool summary;
    private bool sandboxRun;
    private string status = "";
    private Customer? reading;
    private double stepClock;
    private double saveClock;
    private double assistCooldown;
    private StreetView stage = null!;
    private VBoxContainer content = null!;
    private ScrollContainer scroll = null!;
    private Label panelTitle = null!;
    private Label panelContext = null!;
    private Label date = null!;
    private Label time = null!;
    private Label cash = null!;
    private Label reputation = null!;
    private Label goal = null!;
    private ProgressBar goalProgress = null!;
    private Label daysLeft = null!;
    private Label stageTitle = null!;
    private Label stageState = null!;
    private Label hint = null!;
    private Label log1 = null!;
    private Label log2 = null!;
    private Label earnings = null!;
    private Button action = null!;
    private MenuButton options = null!;
    private ConfirmationDialog newCampaign = null!;
    private Button ledger = null!;
    private readonly List<Button> tabs = [];
    private readonly List<Button> speeds = [];
    private AudioStreamPlayer audio = null!;
    private AudioStreamWav bell = null!;
    private AudioStreamWav click = null!;

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        sandboxRun = args.Contains("--smoke-test") || args.Any(arg => arg.StartsWith("--capture="));
        state = sandboxRun ? new ShopState() : Load();
        AddFirstNote();

        GetWindow().MinSize = new Vector2I(960, 600);
        Theme = MakeTheme();
        MakeInterface();
        OpenRoom(Room.Shop);
        if (state.Shift == Shift.Closed)
        {
            summary = true;
            Rebuild();
        }

        if (args.Contains("--smoke-test"))
            CallDeferred(MethodName.SmokeTest);
        else if (args.FirstOrDefault(arg => arg.StartsWith("--capture=")) is string capture)
            Capture(capture["--capture=".Length..]);
    }

    public override void _Process(double delta)
    {
        stepClock += delta;
        assistCooldown = Math.Max(0, assistCooldown - delta);
        if (stepClock < .05)
            return;

        double elapsed = Math.Min(stepClock, .25);
        stepClock = 0;
        Shift before = state.Shift;
        int ordersBefore = state.Orders.Count;
        int sold = state.Today.Sold;
        state.AdvanceBench(elapsed * speed);
        state.AdvanceShop(elapsed * 4 * speed);

        if (state.Today.Sold > sold && room == Room.Shop && speed < 8)
            Play(click, -15);

        if (state.Shift != before)
        {
            Play(bell, -10);
            summary = true;
            status = "";
            Save();
            Rebuild();
        }
        else if (reading != null && !state.Customers.Contains(reading))
        {
            reading = null;
            Rebuild();
        }
        else if (state.Orders.Count != ordersBefore)
        {
            Save();
            if (room == Room.Workshop)
                Rebuild(true);
        }

        saveClock += elapsed;
        if (saveClock >= 5 && (state.Shift == Shift.Open || state.Orders.Count > 0 || ordersBefore > 0))
            Save();

        UpdateReadouts();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
            Save();
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.Key1: OpenRoom(Room.Shop); break;
            case Key.Key2: OpenRoom(Room.Workshop); break;
            case Key.Key3: OpenRoom(Room.Town); break;
            case Key.Space when room == Room.Workshop: Assist(); break;
            case Key.Escape when moving:
                moving = false;
                status = "";
                Rebuild();
                break;
            case Key.M: ToggleSound(); break;
            default: return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void MakeInterface()
    {
        Rectangle(this, 0, 0, 1280, 800, "233330");
        Rectangle(this, 14, 12, 1252, 80, "43584c");
        Rectangle(this, 16, 14, 1248, 76, "2e433d");
        Rectangle(this, 16, 88, 1248, 2, "6a7358");
        TextureRect icon = new()
        {
            Texture = GD.Load<Texture2D>("res://Art/Icon.svg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Place(this, icon, 28, 26, 49, 48);
        Put(this, "Button & Timber", 85, 22, 316, 54, 30, Light, true);
        Rule(400);
        Rule(559);
        Rule(688);
        Rule(831);

        date = Put(this, "", 422, 23, 136, 28, 19, Light, true);
        time = Put(this, "", 423, 52, 130, 23, 13, Brass);
        cash = Put(this, "", 580, 24, 103, 32, 25, Brass, true);
        Put(this, "Cash", 582, 55, 96, 22, 13, Light);
        reputation = Put(this, "", 711, 27, 116, 28, 21, Light, true);
        Put(this, "Reputation", 712, 55, 110, 22, 13, Light);
        goal = Put(this, "", 851, 24, 288, 23, 15, Cream);
        goalProgress = Meter(9, true);
        Place(this, goalProgress, 851, 52, 190, 9);
        daysLeft = Put(this, "", 851, 63, 267, 18, 12, ToyArt.C("c1c8a7"));
        options = new MenuButton { Text = "Options", MouseDefaultCursorShape = CursorShape.PointingHand };
        SelectedStyle(options, false, true);
        Place(this, options, 1145, 31, 101, 37);
        PopupMenu menu = options.GetPopup();
        menu.AddCheckItem("Sound effects  (M)", 1);
        menu.AddCheckItem("Reduce decorative motion", 2);
        menu.AddSeparator();
        menu.AddItem("New campaign…", 3);
        menu.AboutToPopup += () =>
        {
            menu.SetItemChecked(0, !state.Muted);
            menu.SetItemChecked(1, state.ReducedMotion);
        };
        menu.IdPressed += id =>
        {
            switch (id)
            {
                case 1: ToggleSound(); break;
                case 2:
                    state.ReducedMotion = !state.ReducedMotion;
                    stage.ReducedMotion = state.ReducedMotion;
                    Save();
                    break;
                case 3: newCampaign.PopupCentered(new Vector2I(420, 144)); break;
            }
        };
        newCampaign = new ConfirmationDialog
        {
            Title = "Start a new campaign?",
            DialogText = "This replaces your current daybook and shop.",
            OkButtonText = "Start new campaign",
            CancelButtonText = "Keep playing"
        };
        newCampaign.Confirmed += StartCampaign;
        AddChild(newCampaign);

        Room[] rooms = [Room.Shop, Room.Workshop, Room.Town];
        for (int index = 0; index < rooms.Length; index++)
        {
            Room destination = rooms[index];
            Button tab = MakeButton(destination.ToString(), () => OpenRoom(destination), dark: true);
            tab.TooltipText = $"Open {destination.ToString().ToLowerInvariant()} ({index + 1})";
            tabs.Add(tab);
            Place(this, tab, 16 + index * 139, 105, 132, 37);
        }

        Put(this, "Speed", 609, 105, 51, 37, 13, Cream);
        int speedX = 662;
        foreach (int rate in Rates)
        {
            int width = rate == 0 ? 62 : 49;
            Button button = MakeButton(rate == 0 ? "Pause" : $"{rate}×", () =>
            {
                speed = rate;
                UpdateReadouts();
            }, dark: true);
            button.TooltipText = rate == 0 ? "Pause the shop clock and workbench" :
                $"Run the shop clock and workbench at {rate}× speed";
            Place(this, button, speedX, 105, width, 37);
            speedX += width + 7;
            speeds.Add(button);
        }

        Rectangle(this, 16, 149, 877, 535, "b3ac7e");
        Rectangle(this, 18, 151, 873, 531, "172c29");
        stage = new StreetView { ClipContents = true, State = state, MouseDefaultCursorShape = CursorShape.PointingHand };
        stage.DisplayPicked = PickDisplay;
        stage.CustomerPicked = PickCustomer;
        stage.TilePicked = PlaceDisplay;
        Place(this, stage, 20, 153, 869, 527);
        stageTitle = Put(this, "", 39, 164, 298, 36, 26, ToyArt.C("273f36"), true);
        stageState = Put(this, "", 41, 200, 270, 23, 14, ToyArt.C("344a3e"));
        Rectangle(this, 20, 650, 869, 30, "324c43");
        hint = Put(this, "", 34, 650, 839, 30, 13, Cream);

        Rectangle(this, 16, 695, 877, 90, "476157");
        Rectangle(this, 18, 697, 873, 86, "31483f");
        Put(this, "Daybook", 31, 703, 104, 24, 17, Light, true);
        log1 = Put(this, "", 32, 730, 623, 20, 14, Cream);
        log2 = Put(this, "", 32, 751, 623, 20, 13, ToyArt.C("b6c0a2"));
        Rectangle(this, 663, 709, 1, 59, "667968");
        earnings = Put(this, "", 678, 707, 198, 26, 14, Cream);
        ledger = MakeButton("Till report", () =>
        {
            summary = !summary;
            Rebuild();
        }, dark: true);
        Place(this, ledger, 677, 741, 194, 31);

        Rectangle(this, 905, 105, 359, 680, "a38b67");
        Rectangle(this, 907, 107, 355, 676, "eadbbc");
        Rectangle(this, 909, 109, 351, 67, "f2e5c7");
        Rectangle(this, 910, 175, 349, 1, "b4a17b");
        panelTitle = Put(this, "", 925, 112, 319, 39, 26, Ink, true);
        panelContext = Put(this, "", 926, 150, 316, 19, 13, Faded);

        scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true
        };
        Place(this, scroll, 924, 189, 325, 528);
        Rectangle(this, 908, 727, 353, 54, "e2d2ad");
        Rectangle(this, 910, 727, 349, 1, "b4a17b");
        action = MakeButton("", AdvanceDay, strong: true);
        Place(this, action, 923, 736, 322, 36);

        audio = new AudioStreamPlayer { Name = "ShopSounds" };
        AddChild(audio);
        bell = Chime([784, 1046.5, 1318.5], .46f);
        click = Chime([1046.5, 1567.98], .12f);
        return;

        void Rule(int x) => Rectangle(this, x, 29, 1, 42, "526455");
    }

    private void OpenRoom(Room newRoom)
    {
        room = newRoom;
        summary = false;
        moving = false;
        reading = null;
        status = "";
        Rebuild();
    }

    private void AddFirstNote()
    {
        if (state.Notes.Count == 0)
            state.Note("Three displays are stocked for your first morning.");
    }

    private void StartCampaign()
    {
        state = new ShopState { Muted = state.Muted, ReducedMotion = state.ReducedMotion };
        stage.State = state;
        selectedDisplay = 0;
        selectedToy = 1;
        quantity = 5;
        assistCooldown = 0;
        speed = 1;
        AddFirstNote();
        OpenRoom(Room.Shop);
        Save();
    }

    private void PickDisplay(Display display)
    {
        selectedDisplay = state.Displays.IndexOf(display);
        reading = null;
        summary = false;
        status = "";
        Rebuild();
    }

    private void PickCustomer(Customer customer)
    {
        reading = customer;
        summary = false;
        status = "";
        Rebuild();
    }

    private void PlaceDisplay(Vector2I cell)
    {
        if (!state.Move(state.Displays[selectedDisplay], cell.X, cell.Y))
            return;

        moving = false;
        Changed("Display moved");
    }

    private void Rebuild(bool keepScroll = false)
    {
        int previousScroll = keepScroll ? scroll.ScrollVertical : 0;
        refresh.Clear();
        if (content != null)
        {
            scroll.RemoveChild(content);
            content.QueueFree();
        }

        content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
        stage.Room = room;
        stage.MouseDefaultCursorShape = room == Room.Shop ? CursorShape.PointingHand : CursorShape.Arrow;
        stage.Moving = moving;
        stage.Selected = state.Displays[selectedDisplay];
        stage.Reading = reading;
        stage.ReducedMotion = state.ReducedMotion;

        if (summary)
            TillPanel();
        else if (room == Room.Workshop)
            WorkshopPanel();
        else if (room == Room.Town)
            TownPanel();
        else if (reading != null)
            CustomerPanel(reading);
        else
            ShopPanel();

        if (previousScroll > 0)
            scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, previousScroll);
        UpdateReadouts();
    }

    private void ShopPanel()
    {
        Header("Displays", "Select a display to change its stock or price.");
        for (int index = 0; index < state.Displays.Count; index++)
        {
            int number = index;
            Display item = state.Displays[number];
            var (button, caption) = ToyRow(item.Toy, () => PickDisplay(item), index == selectedDisplay);
            Sync(() =>
            {
                string name = item.Toy < 0 ? "Empty" : Catalog.Toys[item.Toy].Name;
                string fixture = item.Style == Fixture.Table ? "Table" : "Shelf";
                string price = item.Toy >= 0 ? $" · ${state.Prices[item.Toy]}" : "";
                caption.Text = $"{fixture} {number + 1}   {name}   {item.Count}/{item.Capacity}{price}";
                caption.AddThemeColorOverride("font_color", item.Count == 0 ? Clay : Ink);
                button.TooltipText = $"Manage {fixture.ToLowerInvariant()} {number + 1}";
            });
        }

        Display display = state.Displays[selectedDisplay];
        Divider();
        Line("Toy on display", 15, Ink, true);
        OptionButton selection = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 33) };
        selection.AddItem("Empty display", 0);
        for (int i = 0; i < Catalog.Toys.Length; i++)
        {
            if (state.Unlocked[i])
                selection.AddItem(Catalog.Toys[i].Name, i + 1);
        }
        selection.Select(selection.GetItemIndex(display.Toy + 1));
        selection.ItemSelected += index =>
        {
            state.Assign(display, selection.GetItemId((int)index) - 1);
            Changed();
        };
        content.AddChild(selection);

        if (display.Toy >= 0)
        {
            int toy = display.Toy;
            HBoxContainer price = Row();
            Label priceText = In(price, "", 15, Ink, true, true);
            Button lowerPrice = InButton(price, "−", () => Price(toy, -1), 43);
            Button raisePrice = InButton(price, "+", () => Price(toy, 1), 43);
            lowerPrice.TooltipText = "Lower retail price by $1";
            raisePrice.TooltipText = "Raise retail price by $1";
            Label backroom = Line("", 13, Faded);
            Sync(() =>
            {
                priceText.Text = $"Price ${state.Prices[toy]}";
                backroom.Text = $"{state.Stock[toy]} in back room · Typical price ${Catalog.Toys[toy].Value}";
                lowerPrice.Disabled = state.Prices[toy] <= 1;
                raisePrice.Disabled = state.Prices[toy] >= Catalog.Toys[toy].Value * 3;
            });
        }

        HBoxContainer stock = Row();
        Button fill = InButton(stock, "Fill display", () =>
        {
            int moved = state.Restock(display);
            Changed(moved == 1 ? "1 toy moved to the display" : $"{moved} toys moved to the display");
        });
        Sync(() => fill.Disabled = display.Toy < 0 || state.Stock[display.Toy] == 0 || display.Count >= display.Capacity);
        InButton(stock, "Fill all", () =>
        {
            state.RestockAll();
            Changed();
        });

        HBoxContainer furniture = Row();
        Button move = InButton(furniture, moving ? "Cancel move" : "Move display", () =>
        {
            moving = !moving;
            Rebuild(true);
        });
        move.Disabled = state.Shift == Shift.Open;
        move.TooltipText = "Rearrange furniture while the shop is closed";
        Button buy = InButton(furniture, $"Buy table · ${ShopState.TablePrice}", () =>
        {
            if (!state.BuyTable())
                return;
            selectedDisplay = state.Displays.Count - 1;
            Changed("New table installed");
        });
        Sync(() => buy.Disabled = state.Shift == Shift.Open || state.Cash < ShopState.TablePrice ||
            state.Displays.Count >= (state.Expanded ? 7 : 4));
    }

    private void CustomerPanel(Customer customer)
    {
        Header(customer.Name, "Customer");
        var (_, caption) = ToyRow(customer.Wanted);
        caption.Text = Catalog.Toys[customer.Wanted].Name;
        caption.AddThemeColorOverride("font_color", Ink);
        Label mood = Line("", 19, Ink, true);
        Paragraph($"Budget ${customer.Budget} · Listed at ${state.Prices[customer.Wanted]}");
        Label stock = Line("", 14, Faded);
        Sync(() =>
        {
            mood.Text = customer.Result switch { 1 => "Bought a toy", -1 => "Left empty-handed", _ => "Looking around" };
            stock.Text = $"{state.StockOnDisplay(customer.Wanted)} on display · {state.Stock[customer.Wanted]} in back room";
        });
        AddButton("Back to displays", () =>
        {
            reading = null;
            Rebuild();
        });
    }

    private void WorkshopPanel()
    {
        Header("Workbench", "Make stock for the shop.");
        if (state.Orders.Count > 0)
        {
            Order current = state.Orders[0];
            Toy product = Catalog.Toys[current.Toy];
            Label order = Line("", 15, Ink, true);
            ProgressBar progress = Meter(8);
            content.AddChild(progress);
            Button assist = AddButton("Work the bench  ·  Space", Assist);
            assist.TooltipText = "Add a stroke of work to the active order";
            Sync(() =>
            {
                order.Text = $"{product.Name} · {current.Remaining} left";
                progress.Value = current.Work / product.Time * 100;
                assist.Disabled = assistCooldown > 0;
            });

            if (state.Orders.Count > 1)
                Paragraph("Queued  " + string.Join("  ·  ", state.Orders.Skip(1)
                    .Select(item => $"{item.Remaining} × {Catalog.Toys[item.Toy].Name}")));
            Divider();
        }

        Toy toy = Catalog.Toys[selectedToy];
        OptionButton patterns = new() { CustomMinimumSize = new Vector2(0, 35) };
        for (int i = 0; i < Catalog.Toys.Length; i++)
            patterns.AddItem(state.Unlocked[i] ? Catalog.Toys[i].Name : $"{Catalog.Toys[i].Name} (locked)", i);
        patterns.Select(selectedToy);
        patterns.ItemSelected += index =>
        {
            selectedToy = (int)index;
            Rebuild(true);
        };
        content.AddChild(patterns);
        var (_, caption) = ToyRow(selectedToy);
        caption.Text = toy.Family;
        caption.AddThemeColorOverride("font_color", Ink);

        if (!state.Unlocked[selectedToy])
        {
            Paragraph($"Practice with {toy.Practice} {Catalog.Toys[toy.Prerequisite].Name} at the bench to open this pattern.");
            Label practice = Line("", 14, Faded);
            Button research = AddButton($"Learn pattern · ${toy.Research}", () =>
            {
                if (state.Research(selectedToy))
                    Changed();
            }, true);
            Sync(() =>
            {
                practice.Text = $"Practice  {Math.Min(state.Practice[toy.Prerequisite], toy.Practice)} / {toy.Practice}";
                research.Disabled = !state.CanResearch(selectedToy);
            });
            return;
        }

        Label details = Line("", 14, Faded);
        Label reserve = Line("", 14, Faded);
        HBoxContainer batch = Row();
        Label units = In(batch, "", 15, Ink, true, true);
        Button fewer = InButton(batch, "−", () => { quantity--; UpdateReadouts(); }, 43);
        Button more = InButton(batch, "+", () => { quantity++; UpdateReadouts(); }, 43);
        Button make = AddButton("", () =>
        {
            if (state.Queue(selectedToy, quantity))
                Changed($"Order placed · {quantity} × {toy.Name}");
        }, true);
        Sync(() =>
        {
            details.Text = $"Materials ${state.ToyCost(selectedToy)} each · {state.Yield(selectedToy)}% yield";
            reserve.Text = $"{state.Stock[selectedToy]} in back room · Practice {state.Practice[selectedToy]}";
            units.Text = $"Batch of {quantity}";
            fewer.Disabled = quantity <= 1;
            more.Disabled = quantity >= 12;
            make.Text = state.Orders.Count >= 4 ? "Queue full" : $"Queue order · ${state.ToyCost(selectedToy) * quantity}";
            make.Disabled = state.Orders.Count >= 4 || state.Cash < state.ToyCost(selectedToy) * quantity;
        });
    }

    private void TownPanel()
    {
        Header("Alder Lane", Catalog.Fairs[state.Season]);
        string eventDate = StateFairText();
        Paragraph(eventDate);
        int toy = state.CommissionToy;
        var (_, caption) = ToyRow(toy);
        caption.Text = $"{state.CommissionCount} × {Catalog.Toys[toy].Name}";
        caption.AddThemeColorOverride("font_color", Ink);
        Label available = Line("", 13, Faded);
        Button deliver = AddButton("", () =>
        {
            if (state.Deliver())
                Changed();
        }, true);
        Sync(() =>
        {
            available.Text = $"{state.Stock[toy]} in back room";
            deliver.Text = state.Commissions[state.Season] ? "Order delivered" : $"Deliver town order · ${state.CommissionPay}";
            deliver.Disabled = state.Commissions[state.Season] || state.Stock[toy] < state.CommissionCount;
        });
        Divider();
        Line("The adjoining room", 18, Ink, true);
        Paragraph(state.Expanded ? "You can furnish up to seven displays." : "Open up two more aisles for up to seven displays.");
        Button permit = AddButton(state.Expanded ? "Room opened" : $"Buy permit · ${ShopState.Permit}", () =>
        {
            if (state.Expand())
                Changed();
        });
        Sync(() => permit.Disabled = state.Expanded || state.Cash < ShopState.Permit || state.Shift == Shift.Open);
    }

    private void TillPanel()
    {
        Header("Till report", $"{Catalog.Seasons[state.Season]}, day {state.Date}");
        if (state.Shift == Shift.Closed && state.Day == ShopState.CampaignDays)
        {
            Line(state.CampaignResult == 1 ? "The shop is yours!" : "The deed is out of reach", 19, Ink, true);
            Paragraph($"You saved ${state.DeedSavings:N0} of the ${ShopState.Goal:N0} needed for the deed. The shop stays open in free play.");
            Divider();
        }

        Till today = state.Today;
        Till previous = state.Yesterday;
        HBoxContainer columns = Row();
        In(columns, "", 13, Faded, true);
        In(columns, "Today", 13, Faded).CustomMinimumSize = new Vector2(75, 0);
        In(columns, "Previous", 13, Faded).CustomMinimumSize = new Vector2(70, 0);
        Stat("Visitors", () => today.Visitors.ToString(), previous.Visitors.ToString());
        Stat("Toys sold", () => today.Sold.ToString(), previous.Sold.ToString());
        Stat("Sales", () => $"${today.Revenue}", $"${previous.Revenue}");
        Stat("Town orders", () => $"${today.OtherIncome}", $"${previous.OtherIncome}");
        Stat("Expenses", () => $"${today.Spent}", $"${previous.Spent}");
        Divider();
        Stat("Net cash", () => ShopState.Signed(today.Net), ShopState.Signed(previous.Net));
        Divider();
        Line("Unmet requests", 17, Ink, true);
        Label demand = Paragraph("");
        Sync(() =>
        {
            Till source = today.Visitors > 0 ? today : previous;
            var wants = Catalog.Toys
                .Select((toy, i) => (toy.Name, Count: source.Requests[i]))
                .Where(entry => entry.Count > 0)
                .OrderByDescending(entry => entry.Count)
                .Take(4);
            string results = string.Join("\n", wants.Select(entry => $"{entry.Count}  {entry.Name}"));
            demand.Text = results.Length > 0 ? results : "No unmet requests yet.";
        });
        AddButton("Return to " + room.ToString().ToLowerInvariant(), () =>
        {
            summary = false;
            Rebuild();
        });
        return;

        void Stat(string key, Func<string> current, string prior)
        {
            HBoxContainer row = Row();
            In(row, key, 14, Ink, true);
            Label value = In(row, "", 14, Ink);
            value.CustomMinimumSize = new Vector2(75, 0);
            In(row, state.Day > 1 ? prior : "—", 14, Faded).CustomMinimumSize = new Vector2(70, 0);
            Sync(() => value.Text = current());
        }
    }

    private void AdvanceDay()
    {
        status = "";
        reading = null;
        moving = false;
        switch (state.Shift)
        {
            case Shift.Setup:
                if (speed == 0)
                    speed = 1;
                state.Open();
                Play(bell, -10);
                if (room == Room.Shop)
                    summary = false;
                break;
            case Shift.Open:
                speed = speed == 8 ? 1 : 8;
                break;
            case Shift.Closed:
                state.Tomorrow();
                speed = 1;
                summary = false;
                break;
        }
        Save();
        Rebuild();
    }

    private void Price(int toy, int change)
    {
        state.Prices[toy] = Math.Clamp(state.Prices[toy] + change, 1, Catalog.Toys[toy].Value * 3);
        Save();
        UpdateReadouts();
    }

    private void Assist()
    {
        if (state.Orders.Count == 0 || assistCooldown > 0)
            return;

        assistCooldown = .34;
        int orders = state.Orders.Count;
        state.AdvanceBench(1.35);
        Play(click, -18);
        if (state.Orders.Count != orders)
        {
            Save();
            Rebuild(true);
        }
    }

    private void Changed(string message = "")
    {
        status = message;
        Play(click, -16);
        Save();
        Rebuild(true);
    }

    private void UpdateReadouts()
    {
        date.Text = $"{Catalog.Seasons[state.Season]} {state.Date:00}";
        double elapsed = state.Shift == Shift.Closed ? ShopState.ShopHours : state.Minute;
        int minutes = 9 * 60 + (int)elapsed;
        int hour = minutes / 60;
        time.Text = $"{(hour + 11) % 12 + 1}:{minutes % 60:00} {(hour >= 12 ? "PM" : "AM")}  ·  Year {state.Year}";
        cash.Text = state.Cash < 0 ? $"−${-state.Cash:N0}" : $"${state.Cash:N0}";
        reputation.Text = $"{state.Reputation} / 100";
        goal.Text = state.Day > ShopState.CampaignDays
            ? "Free play" : $"Shop deed   ${state.Cash:N0} / ${ShopState.Goal:N0}";
        goalProgress.Value = Math.Clamp(state.Cash * 100d / ShopState.Goal, 0, 100);
        goalProgress.Visible = state.Day <= ShopState.CampaignDays;
        int remaining = ShopState.CampaignDays - state.Day + 1;
        daysLeft.Text = state.Day > ShopState.CampaignDays ? "The shop is open for another year." :
            remaining == 1 ? "Final day for the deed" : $"{remaining} days to earn the deed";

        stageTitle.Text = room switch
        {
            Room.Workshop => "The workshop",
            Room.Town => "Market square",
            _ => "The storefront"
        };
        stageState.Text = room switch
        {
            Room.Workshop => state.Orders.Count > 0 ? "In production" : "Bench idle",
            Room.Town => state.Fair ? Catalog.Fairs[state.Season] : "Alder Lane",
            _ => state.Shift switch { Shift.Setup => "Before opening", Shift.Open => "Doors open", _ => "Closed for the evening" }
        };
        hint.Text = moving ? "Choose a green floor tile to move the display. Esc cancels." :
            status.Length > 0 ? status :
            room switch
            {
                Room.Workshop => "Finished toys go to the back room. Fill your displays from there.",
                Room.Town => "Town orders use stock from the back room.",
                _ => state.Shift switch
                {
                    Shift.Setup => "Stock your displays, then open the doors when you’re ready.",
                    Shift.Open => "Click a shopper to see what they want. Click a display to refill it.",
                    _ => "Check unmet requests in the till report before making tomorrow’s stock."
                }
            };
        log1.Text = state.Notes.FirstOrDefault() ?? "";
        log2.Text = state.Notes.Skip(1).FirstOrDefault() ?? "";
        earnings.Text = $"Today   {state.Today.Sold} sold  ·  ${state.Today.Revenue}";
        action.Text = state.Shift switch
        {
            Shift.Setup => "Open shop",
            Shift.Open => speed == 8 ? "Return to 1×" : "Fast-forward to closing",
            _ when state.Day == ShopState.CampaignDays => "Continue in free play",
            _ => "Start next day"
        };
        ledger.Text = summary ? "Close till report" : "Till report";

        for (int i = 0; i < tabs.Count; i++)
            SelectedStyle(tabs[i], i == (int)room, true);
        for (int i = 0; i < speeds.Count; i++)
            SelectedStyle(speeds[i], speed == Rates[i], true);
        stage.Paused = speed == 0;

        foreach (Action read in refresh)
            read();
    }

    private string StateFairText() => state.Fair ? "More shoppers are in town today." : "Market days are the 5th and 6th.";

    private void Header(string title, string detail)
    {
        panelTitle.Text = title;
        panelContext.Text = detail;
    }

    private void Sync(Action read) => refresh.Add(read);

    private static void Place(Control parent, Control child, float x, float y, float w, float h)
    {
        parent.AddChild(child);
        child.Position = new Vector2(x, y);
        child.Size = new Vector2(w, h);
    }

    private static void Rectangle(Control parent, float x, float y, float w, float h, string color) =>
        Place(parent, new ColorRect { Color = ToyArt.C(color), MouseFilter = MouseFilterEnum.Ignore }, x, y, w, h);

    private Label Put(Control parent, string text, float x, float y, float w, float h, int size, Color color, bool display = false)
    {
        Label label = TextLabel(text, size, color, display);
        Place(parent, label, x, y, w, h);
        return label;
    }

    private Label TextLabel(string text, int size, Color color, bool display = false) => new Label
    {
        Text = text,
        MouseFilter = MouseFilterEnum.Ignore,
        VerticalAlignment = VerticalAlignment.Center,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
    }.WithTextStyle(size, color, display ? serif : null);

    private Label Line(string text, int size, Color color, bool display = false)
    {
        Label label = TextLabel(text, size, color, display);
        content.AddChild(label);
        return label;
    }

    private Label Paragraph(string text)
    {
        Label label = Line(text, 14, Faded);
        label.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private Label In(HBoxContainer row, string text, int size, Color color, bool stretch = false, bool display = false)
    {
        Label label = TextLabel(text, size, color, display);
        if (stretch)
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        return label;
    }

    private HBoxContainer Row()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 7);
        content.AddChild(row);
        return row;
    }

    private void Divider()
    {
        ColorRect divider = new() { Color = ToyArt.C("b9a279"), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore };
        content.AddChild(divider);
    }

    private (Button Button, Label Caption) ToyRow(int toy, Action? callback = null, bool active = false)
    {
        Button tile = MakeButton("", callback ?? (() => { }));
        tile.CustomMinimumSize = new Vector2(0, 41);
        tile.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(tile);
        SelectedStyle(tile, active, false);
        if (callback == null)
        {
            tile.FocusMode = FocusModeEnum.None;
            tile.MouseFilter = MouseFilterEnum.Ignore;
        }

        ToySwatch swatch = new() { Toy = toy, MouseFilter = MouseFilterEnum.Ignore };
        Place(tile, swatch, 6, 5, 34, 31);
        Label caption = TextLabel("", 13, Ink);
        tile.AddChild(caption);
        caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        caption.OffsetLeft = 49;
        caption.OffsetRight = -6;
        return (tile, caption);
    }

    private Button AddButton(string caption, Action callback, bool strong = false)
    {
        Button button = MakeButton(caption, callback, strong);
        content.AddChild(button);
        return button;
    }

    private Button InButton(HBoxContainer row, string caption, Action callback, int width = 0)
    {
        Button button = MakeButton(caption, callback);
        button.CustomMinimumSize = new Vector2(width, 32);
        if (width == 0)
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(button);
        return button;
    }

    private Button MakeButton(string caption, Action callback, bool strong = false, bool dark = false)
    {
        Button button = new()
        {
            Text = caption,
            CustomMinimumSize = new Vector2(0, 33),
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.Pressed += callback;
        button.AddThemeFontSizeOverride("font_size", 14);
        if (strong)
        {
            button.AddThemeStyleboxOverride("normal", Box("426d5c", "315649", 4));
            button.AddThemeStyleboxOverride("hover", Box("54816a", "315649", 4));
            button.AddThemeStyleboxOverride("pressed", Box("345749", "315649", 4));
            button.AddThemeColorOverride("font_color", Light);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", Light);
        }
        else if (dark)
            SelectedStyle(button, false, true);
        return button;
    }

    private static void SelectedStyle(Button button, bool selected, bool dark)
    {
        if (button.HasMeta("selected") && button.GetMeta("selected").AsBool() == selected)
            return;

        button.SetMeta("selected", selected);
        string fill = selected ? dark ? "d9bd87" : "e5ca93" : dark ? "364e45" : "efe1bf";
        string stroke = selected ? dark ? "eee0b1" : "9e784d" : dark ? "587064" : "beab83";
        string hover = selected ? "ecd2a0" : dark ? "456154" : "f5e8ca";
        string pressed = selected ? "c19c66" : dark ? "2b4139" : "dfc692";
        Color text = selected || !dark ? Ink : Light;
        button.AddThemeStyleboxOverride("normal", Box(fill, stroke, 4));
        button.AddThemeStyleboxOverride("hover", Box(hover, stroke, 4));
        button.AddThemeStyleboxOverride("pressed", Box(pressed, stroke, 4));
        button.AddThemeColorOverride("font_color", text);
        button.AddThemeColorOverride("font_hover_color", text);
        button.AddThemeColorOverride("font_pressed_color", text);
    }

    private static ProgressBar Meter(int height, bool dark = false)
    {
        ProgressBar meter = new() { ShowPercentage = false, CustomMinimumSize = new Vector2(0, height), MouseFilter = MouseFilterEnum.Ignore };
        meter.AddThemeStyleboxOverride("background", Box(dark ? "23342e" : "d2be96", dark ? "6f7e67" : "ac9979", 3, 0));
        meter.AddThemeStyleboxOverride("fill", Box(dark ? "d0ac72" : "67907b", dark ? "d0ac72" : "5a806e", 3, 0));
        return meter;
    }

    private static StyleBoxFlat Box(string fill, string stroke, int corner, int padding = 10) => new()
    {
        BgColor = ToyArt.C(fill),
        BorderColor = ToyArt.C(stroke),
        BorderWidthBottom = 1,
        BorderWidthTop = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusTopLeft = corner,
        CornerRadiusTopRight = corner,
        CornerRadiusBottomLeft = corner,
        CornerRadiusBottomRight = corner,
        ContentMarginLeft = padding,
        ContentMarginRight = padding,
        ContentMarginTop = 1,
        ContentMarginBottom = 1
    };

    private static Theme MakeTheme()
    {
        Theme theme = new() { DefaultFontSize = 14 };
        foreach (string type in new[] { "Button", "OptionButton", "CheckButton" })
        {
            theme.SetStylebox("normal", type, Box("efe1bf", "beab83", 4));
            theme.SetStylebox("hover", type, Box("f5e8ca", "b7a179", 4));
            theme.SetStylebox("pressed", type, Box("dfc692", "a88656", 4));
            theme.SetStylebox("disabled", type, Box("dfd1b2", "c5b796", 4));
            StyleBoxFlat focus = Box("ffffff00", "65877c", 4, 0);
            focus.BorderWidthTop = focus.BorderWidthBottom = focus.BorderWidthLeft = focus.BorderWidthRight = 2;
            theme.SetStylebox("focus", type, focus);
            theme.SetColor("font_color", type, Ink);
            theme.SetColor("font_hover_color", type, Ink);
            theme.SetColor("font_pressed_color", type, Ink);
            theme.SetColor("font_focus_color", type, Ink);
            theme.SetColor("font_disabled_color", type, ToyArt.C("887a63"));
        }

        theme.SetColor("icon_normal_color", "OptionButton", Ink);
        theme.SetColor("icon_hover_color", "OptionButton", Ink);
        theme.SetStylebox("panel", "PopupMenu", Box("f3e6c8", "a38b67", 4));
        theme.SetStylebox("hover", "PopupMenu", Box("deca9e", "deca9e", 2));
        theme.SetColor("font_color", "PopupMenu", Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Ink);
        theme.SetStylebox("scroll", "VScrollBar", Box("d8c7a4", "d8c7a4", 2, 4));
        theme.SetStylebox("grabber", "VScrollBar", Box("a8936c", "a8936c", 2, 4));
        return theme;
    }

    private void ToggleSound()
    {
        state.Muted = !state.Muted;
        if (state.Muted)
            audio.Stop();
        else
            Play(click, -15);
        Save();
    }

    private void Play(AudioStreamWav soundEffect, float volume)
    {
        if (state.Muted || sandboxRun)
            return;
        audio.Stream = soundEffect;
        audio.VolumeDb = volume;
        audio.Play();
    }

    private static AudioStreamWav Chime(double[] tones, float length)
    {
        const int rate = 22050;
        byte[] bytes = new byte[(int)(rate * length) * 2];
        for (int i = 0; i < bytes.Length / 2; i++)
        {
            double t = i / (double)rate;
            double signal = 0;
            for (int j = 0; j < tones.Length; j++)
            {
                double local = t - j * length / (tones.Length + 1);
                if (local >= 0)
                    signal += Math.Sin(Math.Tau * tones[j] * local) * Math.Exp(-local * 14 / length) * Math.Min(local * 350, 1);
            }
            short sample = (short)Math.Clamp(signal * 14000, short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)sample;
            bytes[i * 2 + 1] = (byte)(sample >> 8);
        }
        return new AudioStreamWav { Data = bytes, MixRate = rate, Format = AudioStreamWav.FormatEnum.Format16Bits };
    }

    private static ShopState Load()
    {
        foreach (string location in new[] { SaveFile, SaveFile + ".bak" })
        {
            string path = ProjectSettings.GlobalizePath(location);
            if (!File.Exists(path))
                continue;

            try
            {
                ShopState? saved = JsonSerializer.Deserialize<ShopState>(File.ReadAllText(path));
                if (saved != null)
                    return saved;
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
            {
                GD.PushWarning($"Daybook could not be read: {error.Message}");
            }
        }
        return new ShopState();
    }

    private void Save()
    {
        if (sandboxRun || stage == null)
            return;

        saveClock = 0;
        string path = ProjectSettings.GlobalizePath(SaveFile);
        string temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(state));
            if (File.Exists(path))
                File.Replace(temp, path, path + ".bak", true);
            else
                File.Move(temp, path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Daybook could not be saved: {error.Message}");
        }
    }
}

internal static class LabelStyle
{
    public static Label WithTextStyle(this Label label, int size, Color color, Font? font)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        if (font != null)
            label.AddThemeFontOverride("font", font);
        return label;
    }
}
