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
    private Font serif = null!;
    private ShopState state = new();
    private Room room;
    private Display? selectedDisplay;
    private int selectedToy = 1;
    private int quantity = 5;
    private int speed = 1;
    private bool moving;
    private bool placingFlip;
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
    private Label rankName = null!;
    private ProgressBar rankMeter = null!;
    private PanelContainer toast = null!;
    private Label toastText = null!;
    private double toastLeft;
    private AudioStream fanfare = null!;
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

        GetWindow().MinSize = new Vector2I(360, 480);
        serif = OS.HasFeature("web")
            ? GD.Load<Font>("res://Art/Fonts/DejaVuSerif-Bold.ttf")
            : new SystemFont { FontNames = ["Georgia", "DejaVu Serif", "Noto Serif"], FontWeight = 700 };
        Theme = MakeTheme();
        MakeInterface();
        state.CheckedOut += OnCheckout;
        state.Announced += ShowToast;
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
        if (toastLeft > 0)
        {
            toastLeft -= delta;
            toast.Modulate = new Color(1, 1, 1, (float)Math.Clamp(toastLeft / .6, 0, 1));
            toast.Visible = toastLeft > 0;
        }
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
        UpdateMusic();
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
            case Key.R: RotateFixture(); break;
            default: return;
        }

        GetViewport().SetInputAsHandled();
    }

    private BoxContainer body = null!;
    private PanelContainer side = null!;
    private Control sideFooter = null!;
    private Control demandBar = null!;
    private Control leftColumn = null!;
    private Control rankBlock = null!;

    private static PanelContainer Frame(string fill, string stroke, int corner, int padX, int padY)
    {
        StyleBoxFlat box = Box(fill, stroke, corner, padX);
        box.ContentMarginTop = box.ContentMarginBottom = padY;
        PanelContainer panel = new();
        panel.AddThemeStyleboxOverride("panel", box);
        return panel;
    }

    private static VBoxContainer Block(float width, params Control[] children)
    {
        VBoxContainer block = new() { CustomMinimumSize = new Vector2(width, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        block.AddThemeConstantOverride("separation", 0);
        foreach (Control child in children)
            block.AddChild(child);
        return block;
    }

    private static void AnchorTo(Control child, LayoutPreset preset, float left, float top, float right, float bottom)
    {
        child.SetAnchorsPreset(preset);
        child.OffsetLeft = left;
        child.OffsetTop = top;
        child.OffsetRight = right;
        child.OffsetBottom = bottom;
    }

    private void MakeInterface()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ColorRect backdrop = new() { Color = ToyArt.C("141f1d"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        MarginContainer margin = new();
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string edge in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{edge}", 12);
        VBoxContainer shell = new();
        shell.AddThemeConstantOverride("separation", 10);
        margin.AddChild(shell);

        Color muted = ToyArt.C("9db0a2");
        PanelContainer top = Frame("1b2926", "263832", 6, 16, 7);
        shell.AddChild(top);
        HBoxContainer topRow = new();
        topRow.AddThemeConstantOverride("separation", 14);
        top.AddChild(topRow);

        HFlowContainer stats = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stats.AddThemeConstantOverride("h_separation", 30);
        stats.AddThemeConstantOverride("v_separation", 4);
        topRow.AddChild(stats);
        date = TextLabel("", 18, Light, true);
        time = TextLabel("", 12, muted);
        stats.AddChild(Block(150, date, time));
        cash = TextLabel("", 21, Brass);
        stats.AddChild(Block(96, TextLabel("CASH", 10, muted), cash));
        reputation = TextLabel("", 21, Light);
        stats.AddChild(Block(96, TextLabel("REPUTATION", 10, muted), reputation));
        rankName = TextLabel("", 15, Light);
        rankMeter = Meter(5, true);
        rankMeter.CustomMinimumSize = new Vector2(120, 5);
        rankBlock = Block(130, TextLabel("RANK", 10, muted), rankName, rankMeter);
        stats.AddChild(rankBlock);
        goal = TextLabel("", 13, Cream);
        goalProgress = Meter(5, true);
        goalProgress.CustomMinimumSize = new Vector2(190, 5);
        daysLeft = TextLabel("", 11, muted);
        stats.AddChild(Block(250, goal, goalProgress, daysLeft));

        options = new MenuButton { Text = "Options", Flat = false, MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(101, 37), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        SelectedStyle(options, false, true);
        topRow.AddChild(options);
        PopupMenu menu = options.GetPopup();
        menu.AddCheckItem("Sound effects  (M)", 1);
        menu.AddCheckItem("Music", 5);
        menu.AddCheckItem("Reduce decorative motion", 2);
        menu.AddSeparator();
        menu.AddItem("Save daybook", 4);
        menu.AddItem("New campaign…", 3);
        menu.AboutToPopup += () =>
        {
            menu.SetItemChecked(0, !state.Muted);
            menu.SetItemChecked(1, !state.MusicOff);
            menu.SetItemChecked(2, state.ReducedMotion);
        };
        menu.IdPressed += id =>
        {
            switch (id)
            {
                case 1: ToggleSound(); break;
                case 5:
                    state.MusicOff = !state.MusicOff;
                    Save();
                    break;
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

        body = new BoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        shell.AddChild(body);
        VBoxContainer left = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        body.AddChild(left);
        leftColumn = left;

        HFlowContainer nav = new();
        nav.AddThemeConstantOverride("h_separation", 7);
        nav.AddThemeConstantOverride("v_separation", 6);
        left.AddChild(nav);
        Room[] rooms = [Room.Shop, Room.Workshop, Room.Town];
        for (int index = 0; index < rooms.Length; index++)
        {
            Room destination = rooms[index];
            Button tab = MakeButton(destination == Room.Town ? "Orders" : destination.ToString(),
                () => OpenRoom(destination), dark: true);
            tab.TooltipText = $"Open {destination.ToString().ToLowerInvariant()} ({index + 1})";
            tab.CustomMinimumSize = new Vector2(104, 37);
            tabs.Add(tab);
            nav.AddChild(tab);
            if (destination == Room.Workshop)
                workshopAttention = Put(tab, "✦", 82, 1, 18, 34, 14, Brass);
        }
        MakeStockroomControls();
        nav.AddChild(stockroomTab);

        foreach (int rate in Rates)
        {
            Button button = MakeButton(rate == 0 ? "II" : $"{rate}×", () =>
            {
                speed = rate;
                UpdateReadouts();
            }, dark: true);
            button.TooltipText = rate == 0 ? "Pause the shop clock and workbench" :
                $"Run the shop clock and workbench at {rate}× speed";
            button.CustomMinimumSize = new Vector2(rate == 0 ? 56 : 48, 37);
            nav.AddChild(button);
            speeds.Add(button);
        }

        Control host = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 150), ClipContents = true
        };
        left.AddChild(host);
        Panel frame = new() { MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Box("172c29", "3b4a43", 4, 0));
        host.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stage = new StreetView { ClipContents = true, State = state, MouseDefaultCursorShape = CursorShape.PointingHand };
        stage.DisplayPicked = PickDisplay;
        stage.CustomerPicked = PickCustomer;
        stage.TilePicked = PlaceDisplay;
        stage.EmptyFloorPicked = ClearSelection;
        host.AddChild(stage);
        stage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 2);
        MakeCraftPad(host);
        stageState = TextLabel("", 20, ToyArt.C("273f36"), true);
        host.AddChild(stageState);
        AnchorTo(stageState, LayoutPreset.TopLeft, 20, 16, 340, 45);
        hint = TextLabel("", 15, ToyArt.C("273f36"));
        host.AddChild(hint);
        AnchorTo(hint, LayoutPreset.BottomWide, 20, -38, -20, -8);
        toast = Frame("2e433d", "d9bd87", 8, 18, 9);
        toast.Visible = false;
        toast.MouseFilter = MouseFilterEnum.Ignore;
        toastText = TextLabel("", 17, Light, true);
        toastText.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        toast.AddChild(toastText);
        host.AddChild(toast);
        toast.SetAnchorsPreset(LayoutPreset.CenterTop);
        toast.GrowHorizontal = GrowDirection.Both;
        toast.OffsetTop = 14;

        PanelContainer demand = Frame("1b2926", "263832", 6, 14, 8);
        left.AddChild(demand);
        demandBar = demand;
        HFlowContainer demandRow = new();
        demandRow.AddThemeConstantOverride("h_separation", 9);
        demandRow.AddThemeConstantOverride("v_separation", 6);
        demand.AddChild(demandRow);
        Label caption = TextLabel("Demand", 17, Light, true);
        caption.CustomMinimumSize = new Vector2(84, 58);
        demandRow.AddChild(caption);
        foreach (ToyFamily family in Catalog.Families)
        {
            ToyFamily target = family;
            Button tile = MakeButton("", () =>
            {
                int root = Array.FindIndex(Catalog.Toys, toy => toy.Family == target && toy.Prerequisite < 0);
                selectedToy = root;
                OpenRoom(Room.Workshop);
            }, dark: true);
            tile.CustomMinimumSize = new Vector2(110, 58);
            demandRow.AddChild(tile);
            Place(tile, new FamilyIcon { Family = family, MouseFilter = MouseFilterEnum.Ignore }, 8, 7, 41, 41);
            Label heat = Put(tile, "", 54, 9, 54, 20, 14, Brass);
            Label stock = Put(tile, "", 54, 29, 54, 20, 11, Cream);
            demandTiles.Add((tile, heat, stock));
        }
        earnings = TextLabel("", 14, Cream);
        ledger = MakeButton("Till report", () =>
        {
            showStockroom = false;
            summary = !summary;
            Rebuild();
        }, dark: true);
        ledger.CustomMinimumSize = new Vector2(170, 31);
        VBoxContainer ledgerBlock = Block(170, earnings, ledger);
        ledgerBlock.AddThemeConstantOverride("separation", 4);
        demandRow.AddChild(ledgerBlock);

        side = Frame("eadbbc", "a38b67", 6, 0, 0);
        body.AddChild(side);
        VBoxContainer sideColumn = new();
        sideColumn.AddThemeConstantOverride("separation", 0);
        side.AddChild(sideColumn);
        PanelContainer head = Frame("eadbbc", "eadbbc", 5, 16, 8);
        sideColumn.AddChild(head);
        HBoxContainer headRow = new();
        head.AddChild(headRow);
        VBoxContainer titles = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        headRow.AddChild(titles);
        panelTitle = TextLabel("", 23, Ink, true);
        panelContext = TextLabel("", 12, Faded);
        titles.AddChild(panelTitle);
        titles.AddChild(panelContext);
        decorPlans = MakeButton("Decor", () =>
        {
            showWorkshopDecor = !showWorkshopDecor;
            showWorkshopTools = false;
            Rebuild();
        });
        decorPlans.CustomMinimumSize = new Vector2(72, 34);
        decorPlans.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        toolPlans = MakeButton("Tools", () =>
        {
            showWorkshopTools = !showWorkshopTools;
            showWorkshopDecor = false;
            Rebuild();
        });
        toolPlans.CustomMinimumSize = new Vector2(72, 34);
        toolPlans.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        headRow.AddChild(toolPlans);
        headRow.AddChild(decorPlans);
        sideColumn.AddChild(new ColorRect { Color = ToyArt.C("b4a17b"), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });

        MarginContainer scrollMargin = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        scrollMargin.AddThemeConstantOverride("margin_left", 14);
        scrollMargin.AddThemeConstantOverride("margin_right", 10);
        scrollMargin.AddThemeConstantOverride("margin_top", 10);
        scrollMargin.AddThemeConstantOverride("margin_bottom", 8);
        sideColumn.AddChild(scrollMargin);
        scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true
        };
        scrollMargin.AddChild(scroll);

        PanelContainer footer = Frame("e2d2ad", "d3c297", 0, 14, 9);
        sideFooter = footer;
        sideColumn.AddChild(footer);
        action = MakeButton("", AdvanceDay, strong: true);
        action.TooltipText = "Press Space";
        action.CustomMinimumSize = new Vector2(0, 38);
        footer.AddChild(action);
        spaceKey = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        spaceKey.AddThemeStyleboxOverride("panel", Box("36594d", "6c8f79", 3, 0));
        action.AddChild(spaceKey);
        AnchorTo(spaceKey, LayoutPreset.CenterRight, -58, -11, -9, 11);
        Put(spaceKey, "Space", 0, 0, 49, 21, 11, Cream).HorizontalAlignment = HorizontalAlignment.Center;

        for (int i = 0; i < audio.Length; i++)
        {
            audio[i] = new AudioStreamPlayer { Name = $"ShopSound{i + 1}" };
            AddChild(audio[i]);
        }
        bell = Chime([784, 1046.5, 1318.5], .46f);
        fanfare = Chime([523.25, 659.25, 783.99, 1046.5], .8f);
        click = Sound("select_003");
        wood = Sound("drop_002");
        warning = Sound("error_003");
        coins = Sound("glass_005");
        MakeShopAudio();
        MakeMusic();

        Resized += ApplyLayout;
        GetWindow().SizeChanged += ApplyScale;
        ApplyScale();
        ApplyLayout();
    }

    private void ApplyScale()
    {
        Window window = GetWindow();
        Vector2 pixels = window.Size;
        window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
        window.ContentScaleSize = Vector2I.Zero;
        window.ContentScaleFactor = pixels.X < pixels.Y
            ? Math.Clamp(pixels.X / 520f, .5f, 3f)
            : Math.Clamp(Math.Min(pixels.X / 1180f, pixels.Y / 720f), .75f, 2.5f);
    }

    private void ApplyLayout()
    {
        if (body == null)
            return;

        Vector2 size = Size;
        bool portrait = size.X < 780 || size.X < size.Y;
        body.Vertical = portrait;
        side.CustomMinimumSize = portrait ? new Vector2(0, 250) : new Vector2(Math.Clamp(size.X * .29f, 350, 480), 0);
        side.SizeFlagsVertical = portrait ? SizeFlags.ExpandFill : SizeFlags.Fill;
        side.SizeFlagsStretchRatio = 1.5f;
        leftColumn.SizeFlagsStretchRatio = 1;
        demandBar.Visible = !portrait || size.Y >= 820;
        rankBlock.Visible = !portrait;
        daysLeft.Visible = !portrait;
    }

    private void OpenRoom(Room newRoom)
    {
        room = newRoom;
        summary = false;
        moving = false;
        placing = null;
        showDecor = false;
        showWorkshopDecor = false;
        showWorkshopTools = false;
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
        state.Announced -= ShowToast;
        state = new ShopState { Muted = state.Muted, MusicOff = state.MusicOff, ReducedMotion = state.ReducedMotion, Schema = ShopState.CurrentSchema };
        state.CheckedOut += OnCheckout;
        state.Announced += ShowToast;
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
            selectedDisplay.Flipped = placingFlip;
        }
        else if (state.PlaceFixture(kind, cell, placingFlip) is Display item)
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
        stage.PlacementFlipped = placingFlip;
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
        placingFlip = relocate && selectedDisplay != null ? selectedDisplay.Flipped : placingFlip;
        if (!relocate)
            selectedDisplay = null;
        status = "";
        Rebuild();
    }

    private void RotateFixture()
    {
        if (placing.HasValue)
            placingFlip = !placingFlip;
        else if (selectedDisplay != null && state.Shift != Shift.Open && !summary && room == Room.Shop)
        {
            selectedDisplay.Flipped = !selectedDisplay.Flipped;
            Play(wood, -16, 1.2f);
            Save();
        }
        else
            return;
        Rebuild(true);
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
        bool known = state.RegularLevel(customer) >= 0 && customer.RegularId >= 0;
        Line(known ? $"{customer.Persona.Name} · regular" : customer.Persona.Name, 15, Ink, true);
        Paragraph(customer.Persona.Blurb);
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
        if (state.Shift == Shift.Closed && state.LastDayOfYear)
        {
            bool met = state.CampaignResult == 1;
            if (state.Year == 1)
            {
                Line(met ? "The shop is yours!" : "The deed is out of reach", 19, Ink, true);
                Paragraph($"You saved ${state.DeedSavings:N0} of the ${state.YearTarget:N0} needed for the deed. The shop opens for a second year with a bigger target.");
            }
            else
            {
                Line(met ? $"Year {state.Year} complete" : $"A lean year {state.Year}", 19, Ink, true);
                Paragraph($"You made ${state.DeedSavings - state.YearStartCash:N0} against a target of ${state.YearTarget - state.YearStartCash:N0}. Next year asks for more.");
            }
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
        Stat("Fair deals", () => today.Bargains.ToString(), previous.Bargains.ToString());
        Stat("Overcharged", () => today.Gouged.ToString(), previous.Gouged.ToString());
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
        reputation.Text = $"{state.Reputation}";
        goal.Text = state.Year == 1
            ? $"Shop deed   ${state.Cash:N0} / ${state.YearTarget:N0}"
            : $"Year {state.Year} profit   ${state.Cash - state.YearStartCash:N0} / ${state.YearTarget - state.YearStartCash:N0}";
        goalProgress.Value = Math.Clamp(state.YearProgress * 100, 0, 100);
        int remaining = ShopState.CampaignDays - state.YearDay + 1;
        daysLeft.Text = remaining == 1 ? "Final day of the year" : $"{remaining} days left this year";
        rankName.Text = Progress.Ranks[state.Rank];
        rankMeter.Value = state.RankProgressFraction * 100;
        rankMeter.TooltipText = $"Renown {state.Renown:N0}";

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
            tile.Heat.Text = heat >= 4 ? "Hot" : heat >= 2 ? "Steady" : "Quiet";
            tile.Stock.Text = stock ? "on shelf" : "no stock";
            tile.Stock.AddThemeColorOverride("font_color", stock ? ToyArt.C("b9cdb4") : ToyArt.C("e9a58f"));
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
            _ when state.LastDayOfYear => $"Begin year {state.Year + 1}",
            _ => "Start next day"
        };
        action.Disabled = (state.Shift == Shift.Setup && placing.HasValue) ||
            (state.Shift == Shift.Open && room == Room.Shop && state.ReadyToPay == null);
        action.Visible = summary || room == Room.Shop || state.Shift == Shift.Closed;
        sideFooter.Visible = action.Visible;
        spaceKey.Visible = !action.Disabled;
        ledger.Text = summary ? "Close report" : "Till report";
        ledger.TooltipText = state.Notes.FirstOrDefault() ?? "";
        decorPlans.Visible = room == Room.Workshop && !summary && !showStockroom;
        decorPlans.Text = showWorkshopDecor ? "Toys" : "Decor";
        toolPlans.Visible = decorPlans.Visible;
        toolPlans.Text = showWorkshopTools ? "Toys" : "Tools";

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
        string fill = selected ? dark ? "d9bd87" : "e5ca93" : dark ? "2b3d37" : "f2e6c8";
        string stroke = selected ? dark ? "eee0b1" : "b58d58" : dark ? "3d524a" : "dccba3";
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
        if (OS.HasFeature("web"))
        {
            theme.DefaultFont = new FontVariation
            {
                BaseFont = ThemeDB.FallbackFont,
                Fallbacks = [GD.Load<Font>("res://Art/Fonts/DejaVuSans.ttf")]
            };
        }
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

    public override void _ExitTree()
    {
        state.CheckedOut -= OnCheckout;
        state.Announced -= ShowToast;
    }

    private void ShowToast(string text)
    {
        if (toast == null)
            return;
        toastText.Text = text;
        toast.Visible = true;
        toast.Modulate = Colors.White;
        toastLeft = 3.4;
        if (text.StartsWith("Rank up") || text.Contains("deed") || text.Contains("target met"))
            Play(fanfare, -9);
        else
            Play(coins, -17, 1.2f);
    }

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
