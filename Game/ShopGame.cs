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
    private Display? selectedDisplay;
    private int selectedToy = 1;
    private int quantity = 5;
    private int speed = 1;
    private bool moving;
    private bool showDecor;
    private bool showWorkshopDecor;
    private Fixture? placing;
    private bool summary;
    private bool sandboxRun;
    private string status = "";
    private Customer? reading;
    private double stepClock;
    private double saveClock;
    private double benchFeedbackLeft;
    private string benchFeedback = "";
    private Color benchFeedbackColor = Cream;
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
    private Label stageState = null!;
    private Label hint = null!;
    private Label earnings = null!;
    private Button action = null!;
    private Panel spaceKey = null!;
    private MenuButton options = null!;
    private ConfirmationDialog newCampaign = null!;
    private Button ledger = null!;
    private Label workshopAttention = null!;
    private Button decorPlans = null!;
    private readonly List<Button> tabs = [];
    private readonly List<Button> speeds = [];
    private readonly List<(Button Button, Label Heat, Label Stock)> demandTiles = [];
    private readonly AudioStreamPlayer[] audio = new AudioStreamPlayer[5];
    private int nextAudio;
    private AudioStream bell = null!;
    private AudioStream click = null!;
    private AudioStream wood = null!;
    private AudioStream warning = null!;
    private AudioStream coins = null!;

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        sandboxRun = args.Contains("--smoke-test") || args.Any(arg => arg.StartsWith("--capture="));
        state = sandboxRun ? new ShopState() : Load();
        state.UpgradeSave();
        AddFirstNote();

        GetWindow().MinSize = new Vector2I(960, 600);
        Theme = MakeTheme();
        MakeInterface();
        state.CheckedOut += OnCheckout;
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
        benchFeedbackLeft = Math.Max(0, benchFeedbackLeft - delta);
        if (stepClock < .05)
            return;

        double elapsed = Math.Min(stepClock, .25);
        stepClock = 0;
        Shift before = state.Shift;
        int visitorsBefore = state.Today.Visitors;
        int ordersBefore = state.Orders.Count;
        bool atBench = room == Room.Workshop && !summary && !newCampaign.Visible;
        Tool? previous = state.Prompt;
        if (speed > 0)
        {
            state.AdvanceBench(elapsed * (atBench ? 1 : speed), atBench);
            state.AdvanceShop(elapsed * 5.5 * speed);
            PlayShopMovement(elapsed, visitorsBefore);
            if (atBench && previous == null && state.Prompt.HasValue)
                Play(click, -18, 1.17f);
            if (atBench && state.LastBenchCue != BenchCue.None)
                ShowBenchCue(state.LastBenchCue);
        }

        if (state.Shift != before)
        {
            Play(bell, -10);
            summary = room == Room.Shop && !showStockroom;
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
            if (room == Room.Workshop && !showStockroom)
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

    public override void _Input(InputEvent input)
    {
        if (newCampaign == null || newCampaign.Visible || confirmDiscard.Visible || options.GetPopup().Visible ||
            GetViewport().GuiGetFocusOwner() is LineEdit)
            return;

        if (input is InputEventKey { Keycode: Key.Space } space && action.IsVisibleInTree())
        {
            GetViewport().SetInputAsHandled();
            if (space.Pressed && !space.Echo && !action.Disabled)
                AdvanceDay();
            return;
        }

        if (room != Room.Workshop || summary || state.Orders.Count == 0)
            return;

        Tool? tool = input switch
        {
            InputEventKey { Pressed: true, Echo: false } key => key.Keycode switch
            {
                Key.Up or Key.W => Tool.Saw, Key.Right or Key.D => Tool.Mallet,
                Key.Down or Key.S => Tool.Brush, Key.Left or Key.A => Tool.Needle, _ => null
            },
            InputEventJoypadButton { Pressed: true } pad => pad.ButtonIndex switch
            {
                JoyButton.DpadUp => Tool.Saw, JoyButton.DpadRight => Tool.Mallet,
                JoyButton.DpadDown => Tool.Brush, JoyButton.DpadLeft => Tool.Needle, _ => null
            },
            _ => null
        };
        if (tool is Tool selected)
        {
            Strike(selected);
            GetViewport().SetInputAsHandled();
        }
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
            case Key.Escape when placing.HasValue: CancelPlacement(); break;
            case Key.Escape when showStockroom: ToggleStockroom(); break;
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
        options = new MenuButton { Text = "Options", Flat = false, MouseDefaultCursorShape = CursorShape.PointingHand };
        SelectedStyle(options, false, true);
        Place(this, options, 1145, 31, 101, 37);
        PopupMenu menu = options.GetPopup();
        menu.AddCheckItem("Sound effects  (M)", 1);
        menu.AddCheckItem("Reduce decorative motion", 2);
        menu.AddSeparator();
        menu.AddItem("Save daybook", 4);
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
                case 4:
                    status = Save() ? "Daybook saved." : "Couldn’t save the daybook.";
                    UpdateReadouts();
                    break;
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
            Button tab = MakeButton(destination == Room.Town ? "Orders" : destination.ToString(),
                () => OpenRoom(destination), dark: true);
            tab.TooltipText = $"Open {destination.ToString().ToLowerInvariant()} ({index + 1})";
            tabs.Add(tab);
            Place(this, tab, 16 + index * 139, 105, 132, 37);
            if (destination == Room.Workshop)
                workshopAttention = Put(tab, "✦", 109, 1, 18, 34, 14, Brass);
        }
        MakeStockroomControls();

        int speedX = 662;
        foreach (int rate in Rates)
        {
            int width = rate == 0 ? 62 : 49;
            Button button = MakeButton(rate == 0 ? "II" : $"{rate}×", () =>
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
        stage.EmptyFloorPicked = ClearSelection;
        Place(this, stage, 20, 153, 869, 527);
        MakeCraftPad();
        stageState = Put(this, "", 39, 171, 272, 29, 20, ToyArt.C("273f36"), true);
        hint = Put(this, "", 39, 641, 720, 31, 15, ToyArt.C("273f36"));

        Rectangle(this, 16, 695, 877, 90, "476157");
        Rectangle(this, 18, 697, 873, 86, "31483f");
        Put(this, "Today’s demand", 32, 710, 160, 31, 18, Light, true);
        foreach (ToyFamily family in Catalog.Families)
        {
            ToyFamily target = family;
            Button tile = MakeButton("", () =>
            {
                int root = Array.FindIndex(Catalog.Toys, toy => toy.Family == target && toy.Prerequisite < 0);
                selectedToy = root;
                OpenRoom(Room.Workshop);
            }, dark: true);
            Place(this, tile, 196 + (int)family * 111, 711, 102, 58);
            Place(tile, new FamilyIcon { Family = family, MouseFilter = MouseFilterEnum.Ignore }, 8, 7, 41, 41);
            Label heat = Put(tile, "", 55, 7, 44, 20, 12, Brass);
            Label stock = Put(tile, "", 58, 28, 43, 20, 12, Cream);
            demandTiles.Add((tile, heat, stock));
        }
        Rectangle(this, 663, 709, 1, 59, "667968");
        earnings = Put(this, "", 678, 707, 198, 26, 14, Cream);
        ledger = MakeButton("Till report", () =>
        {
            showStockroom = false;
            summary = !summary;
            Rebuild();
        }, dark: true);
        Place(this, ledger, 677, 741, 194, 31);

        Rectangle(this, 905, 105, 359, 680, "a38b67");
        Rectangle(this, 907, 107, 355, 676, "eadbbc");
        Rectangle(this, 909, 109, 351, 57, "f2e5c7");
        Rectangle(this, 910, 165, 349, 1, "b4a17b");
        panelTitle = Put(this, "", 925, 111, 319, 33, 25, Ink, true);
        panelContext = Put(this, "", 926, 144, 316, 17, 12, Faded);
        decorPlans = MakeButton("Decor", () =>
        {
            showWorkshopDecor = !showWorkshopDecor;
            Rebuild();
        });
        Place(this, decorPlans, 1166, 120, 77, 34);

        scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true
        };
        Place(this, scroll, 924, 178, 325, 539);
        Rectangle(this, 908, 727, 353, 54, "e2d2ad");
        Rectangle(this, 910, 727, 349, 1, "b4a17b");
        action = MakeButton("", AdvanceDay, strong: true);
        action.TooltipText = "Press Space";
        Place(this, action, 923, 736, 322, 36);
        spaceKey = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        spaceKey.AddThemeStyleboxOverride("panel", Box("36594d", "6c8f79", 3, 0));
        Place(action, spaceKey, 264, 7, 49, 22);
        Put(spaceKey, "Space", 0, 0, 49, 21, 11, Cream).HorizontalAlignment = HorizontalAlignment.Center;

        for (int i = 0; i < audio.Length; i++)
        {
            audio[i] = new AudioStreamPlayer { Name = $"ShopSound{i + 1}" };
            AddChild(audio[i]);
        }
        bell = Chime([784, 1046.5, 1318.5], .46f);
        click = Sound("select_003");
        wood = Sound("drop_002");
        warning = Sound("error_003");
        coins = Sound("glass_005");
        MakeShopAudio();
        return;

        void Rule(int x) => Rectangle(this, x, 29, 1, 42, "526455");
    }

    private void OpenRoom(Room newRoom)
    {
        room = newRoom;
        summary = false;
        moving = false;
        placing = null;
        showDecor = false;
        showWorkshopDecor = false;
        showStockroom = false;
        reading = null;
        status = "";
        Rebuild();
    }

    private void AddFirstNote()
    {
        if (state.Notes.Count == 0)
            state.Note("Your first fixtures and a box of toys are ready to unpack.");
    }

    private void StartCampaign()
    {
        state.CheckedOut -= OnCheckout;
        state = new ShopState { Muted = state.Muted, ReducedMotion = state.ReducedMotion, Schema = ShopState.CurrentSchema };
        state.CheckedOut += OnCheckout;
        stage.State = state;
        selectedDisplay = null;
        selectedToy = 1;
        quantity = 5;
        benchFeedbackLeft = 0;
        speed = 1;
        AddFirstNote();
        OpenRoom(Room.Shop);
        Save();
    }

    private void PickDisplay(Display display)
    {
        if (display.Style == Fixture.Register && state.Shift == Shift.Open && state.RingUp())
            return;
        placing = null;
        moving = false;
        showDecor = false;
        selectedDisplay = display;
        reading = null;
        summary = false;
        showStockroom = false;
        status = "";
        Rebuild();
    }

    private void PickCustomer(Customer customer)
    {
        reading = customer;
        summary = false;
        showStockroom = false;
        status = "";
        Rebuild();
    }

    private void PlaceDisplay(Cell cell)
    {
        if (placing is not Fixture kind)
            return;

        if (moving && selectedDisplay != null)
        {
            if (!state.Move(selectedDisplay, cell))
                return;
        }
        else if (state.PlaceFixture(kind, cell) is Display item)
            selectedDisplay = item;
        else
            return;

        placing = null;
        moving = false;
        Play(wood, -10);
        Save();
        Rebuild();
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
        stage.Placement = placing;
        stage.BuildTiles = placing.HasValue ? ValidBuildTiles() : [];
        stage.Selected = selectedDisplay;
        stage.Reading = reading;
        stage.ReducedMotion = state.ReducedMotion;

        if (summary)
            TillPanel();
        else if (showStockroom)
            StockroomPanel();
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

    private HashSet<Cell> ValidBuildTiles()
    {
        HashSet<Cell> tiles = [];
        for (int x = 1; x < state.Width - 1; x++)
        for (int y = 1; y < 5; y++)
        {
            Cell tile = new(x, y);
            if (state.CanPlace(tile, moving ? selectedDisplay : null))
                tiles.Add(tile);
        }
        return tiles;
    }

    private void ClearSelection()
    {
        if (selectedDisplay == null && reading == null && !summary)
            return;
        selectedDisplay = null;
        reading = null;
        summary = false;
        Rebuild();
    }

    private void BeginPlacement(Fixture kind, bool relocate)
    {
        showStockroom = false;
        moving = relocate;
        placing = kind;
        if (!relocate)
            selectedDisplay = null;
        status = "";
        Rebuild();
    }

    private void CancelPlacement()
    {
        moving = false;
        placing = null;
        status = "";
        Rebuild();
    }

    private void CustomerPanel(Customer customer)
    {
        Header(customer.Name, "");
        var (card, caption) = ToyRow(customer.WantsExact ? customer.Wanted : Catalog.FamilyIcons[(int)customer.Family]);
        AddFamilyBadge(card, customer.Family);
        caption.Text = customer.WantsExact ? Catalog.Toys[customer.Wanted].Name : customer.Family.ToString();
        Label mood = Line("", 14, Faded);
        Sync(() =>
        {
            mood.Text = customer.Result switch
            {
                1 => "Paid", -1 => "Leaving", _ when customer.Phase == VisitPhase.Checkout => "Checkout",
                _ => $"${customer.Budget} budget"
            };
        });
        AddButton("‹  Shop floor", ClearSelection);
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
        Line("Unmet demand", 17, Ink, true);
        HBoxContainer shortages = Row();
        foreach (ToyFamily family in Catalog.Families)
        {
            Control figure = new() { CustomMinimumSize = new Vector2(68, 37), TooltipText = family.ToString() };
            shortages.AddChild(figure);
            Place(figure, new FamilyIcon { Family = family, MouseFilter = MouseFilterEnum.Ignore }, 0, 2, 32, 32);
            Label count = Put(figure, "", 36, 2, 30, 32, 15, Ink);
            Sync(() => count.Text = state.Shortages(today.Visitors > 0 ? today : previous, family).ToString());
        }
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
        if (state.Shift == Shift.Setup && placing.HasValue)
            return;
        status = "";
        reading = null;
        moving = false;
        placing = null;
        switch (state.Shift)
        {
            case Shift.Setup:
                if (state.Register == null)
                {
                    BeginPlacement(Fixture.Register, false);
                    return;
                }
                if (!state.Displays.Any(display => display.Count > 0))
                {
                    if (state.Displays.Find(display => display.Capacity > 0) is Display display)
                        PickDisplay(display);
                    else
                        BeginPlacement(Fixture.Display, false);
                    return;
                }
                if (speed == 0)
                    speed = 1;
                if (!state.Open())
                    return;
                Play(bell, -10);
                selectedDisplay = null;
                if (room == Room.Shop)
                    summary = false;
                break;
            case Shift.Open:
                if (room == Room.Shop)
                    state.RingUp();
                else
                    OpenRoom(Room.Shop);
                return;
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

    private void Changed(string message = "")
    {
        status = message;
        Play(click, -19);
        Save();
        Rebuild(true);
    }

    private void UpdateReadouts()
    {
        date.Text = $"{Catalog.Seasons[state.Season]} {state.Date:00}";
        int minutes = state.ClockTime;
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

        stageState.Text = room switch
        {
            Room.Workshop => "",
            Room.Town => state.Fair ? Catalog.Fairs[state.Season] : "",
            _ => state.Shift switch
            {
                Shift.Setup => "Before opening",
                Shift.Open when state.Minute >= ShopState.ShopHours => "Closing",
                Shift.Open => "Open",
                _ => "Closed for the day"
            }
        };
        Color sceneText = state.Shift == Shift.Closed || state.Minute > 500 ? Light : ToyArt.C("273f36");
        stageState.AddThemeColorOverride("font_color", sceneText);
        hint.AddThemeColorOverride("font_color", sceneText);
        hint.Text = status;
        foreach (ToyFamily family in Catalog.Families)
        {
            var tile = demandTiles[(int)family];
            int heat = state.Forecast(family);
            bool stock = state.StocksFamily(family);
            tile.Heat.Text = new string('•', heat);
            tile.Stock.Text = stock ? "✓" : "—";
            tile.Button.TooltipText = $"{family} · demand {heat}/6 · {(stock ? "on display" : "none on display")}";
        }
        earnings.Text = $"{state.Today.Sold} sold   ·   ${state.Today.Revenue}";
        action.Text = state.Shift switch
        {
            Shift.Setup when placing.HasValue => "Pick a tile",
            Shift.Setup when state.Register == null => "Place a register",
            Shift.Setup when !state.Displays.Any(display => display.Capacity > 0) => "Place a shelf",
            Shift.Setup when !state.Displays.Any(display => display.Count > 0) => "Stock a display",
            Shift.Setup => "Open shop",
            Shift.Open when room != Room.Shop => "Back to shop",
            Shift.Open when state.ReadyToPay is Customer next => $"Ring up · ${next.Basket.Sum(item => item.Price)}",
            Shift.Open => "Shop open",
            _ when state.Day == ShopState.CampaignDays => "Continue in free play",
            _ => "Start next day"
        };
        action.Disabled = (state.Shift == Shift.Setup && placing.HasValue) ||
            (state.Shift == Shift.Open && room == Room.Shop && state.ReadyToPay == null);
        action.Visible = summary || room == Room.Shop || state.Shift == Shift.Closed;
        spaceKey.Visible = !action.Disabled;
        ledger.Text = summary ? "Close report" : "Till report";
        ledger.TooltipText = state.Notes.FirstOrDefault() ?? "";
        decorPlans.Visible = room == Room.Workshop && !summary && !showStockroom;
        decorPlans.Text = showWorkshopDecor ? "Toys" : "Decor";

        for (int i = 0; i < tabs.Count; i++)
            SelectedStyle(tabs[i], i == (int)room, true);
        workshopAttention.Visible = room != Room.Workshop && state.Prompt.HasValue &&
            state.Orders.FirstOrDefault() is Order pending && state.NeedsHands(pending);
        tabs[(int)Room.Workshop].TooltipText = workshopAttention.Visible ? "An order is waiting for a tool (2)" : "Workshop (2)";
        for (int i = 0; i < speeds.Count; i++)
            SelectedStyle(speeds[i], speed == Rates[i], true);
        stage.Paused = speed == 0;
        UpdateCraftPad();
        UpdateStockroomMeter();

        foreach (Action read in refresh)
            read();
    }

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
            foreach (AudioStreamPlayer voice in audio)
                voice.Stop();
        else
            Play(click, -15);
        Save();
    }

    public override void _ExitTree() => state.CheckedOut -= OnCheckout;

    private void OnCheckout(int amount)
    {
        stage.RegisterSale(amount);
        if (room == Room.Shop)
        {
            Play(drawer, -19);
            Play(coins, -15, amount >= 28 ? 1.18f : .99f);
        }
    }

    private static AudioStream Sound(string name) => GD.Load<AudioStream>($"res://Art/Audio/{name}.ogg");

    private void Play(AudioStream soundEffect, float volume, float pitch = 1)
    {
        if (state.Muted || sandboxRun)
            return;
        AudioStreamPlayer voice = audio[nextAudio];
        nextAudio = (nextAudio + 1) % audio.Length;
        voice.Stream = soundEffect;
        voice.VolumeDb = volume;
        voice.PitchScale = pitch;
        voice.Play();
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

    private static ShopState Load() => LoadDaybook(ProjectSettings.GlobalizePath(SaveFile));

    private static ShopState LoadDaybook(string path, bool reportReadErrors = true)
    {
        foreach (string location in new[] { path, path + ".bak" })
        {
            if (!File.Exists(location))
                continue;

            try
            {
                ShopState? saved = JsonSerializer.Deserialize(File.ReadAllText(location), DaybookJson.Default.ShopState);
                if (saved != null)
                    return saved;
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
            {
                if (reportReadErrors)
                    GD.PushWarning($"Daybook could not be read: {error.Message}");
            }
        }
        return new ShopState();
    }

    private bool Save()
    {
        if (sandboxRun || stage == null)
            return false;

        saveClock = 0;
        return WriteDaybook(ProjectSettings.GlobalizePath(SaveFile), state);
    }

    private static bool WriteDaybook(string path, ShopState progress)
    {
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(progress, DaybookJson.Default.ShopState));
            if (File.Exists(path))
                File.Replace(temp, path, path + ".bak", true);
            else
                File.Move(temp, path);
            if (OS.HasFeature("web"))
                JavaScriptBridge.ForceFsSync();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Daybook could not be saved: {error.Message}");
            return false;
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
