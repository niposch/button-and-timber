using Godot;

namespace ButtonAndTimber;

public partial class StreetView : Control
{
    internal ShopState State { get; set; } = new();
    internal Room Room { get; set; }
    internal Display? Selected { get; set; }
    internal Customer? Reading { get; set; }
    internal Fixture? Placement { get; set; }
    internal HashSet<Cell> BuildTiles { get; set; } = [];
    internal bool ReducedMotion { get; set; }
    internal bool Paused { get; set; }
    internal bool ShowToolPrompt { get; set; }
    internal Action<Display>? DisplayPicked { get; set; }
    internal Action<Customer>? CustomerPicked { get; set; }
    internal Action<Cell>? TilePicked { get; set; }
    internal Action? EmptyFloorPicked { get; set; }

    private readonly List<(Rect2 Bounds, object Item)> hits = [];
    private readonly List<(int Amount, float Age)> sales = [];
    private readonly Dictionary<string, Texture2D> sprites = [];
    private readonly Dictionary<string, Vector2[]> feetBySprite = [];
    private Vector2 origin;
    private Vector2 camera;
    private static readonly Vector2[] RoomOffsets = [Vector2.Zero, new(.8f, -9.15f), new(12.7f, 11.3f)];
    private float scale;
    private float clock;
    private float benchFlash;
    private bool cutCleanly;
    private Station workerStation;
    private Vector2 workerPosition = new(2.50f, 2.21f);
    private readonly Queue<Vector2> workerPath = new();
    private int workerFacing = 3;
    private static readonly (Vector2 Machine, Vector2 Operator, int Facing, float Height, float Span)[] WorkshopStations =
    [
        (new(2.50f, 1.34f), new(2.50f, 2.21f), 3, 39, 3.4f),
        (new(1.18f, 3.47f), new(2.07f, 3.47f), 0, 35, 2.55f),
        (new(5.44f, 1.27f), new(5.44f, 2.13f), 3, 37, 2.55f),
        (new(5.53f, 3.53f), new(4.65f, 3.53f), 2, 33, 2.55f)
    ];

    public override void _Ready() => SnapAtmosphere();

    public override void _Process(double delta)
    {
        UpdateAtmosphere(1 - Mathf.Exp(-(float)delta * 4.9f));
        if (!ReducedMotion && !Paused)
            clock += (float)delta;
        Vector2 destination = RoomOffsets[(int)Room];
        camera = ReducedMotion ? destination : camera.Lerp(destination, 1 - Mathf.Exp(-(float)delta * 6.5f));
        benchFlash = Math.Max(0, benchFlash - (float)delta);
        if (!Paused)
            UpdateWorker((float)delta);
        if (State.Shift != Shift.Open)
            sales.Clear();
        for (int i = sales.Count - 1; i >= 0; i--)
        {
            var sale = sales[i];
            sale.Age += (float)delta;
            if (sale.Age >= 1.36f)
                sales.RemoveAt(i);
            else
                sales[i] = sale;
        }
        QueueRedraw();
    }

    internal void SnapCamera() => camera = RoomOffsets[(int)Room];

    internal void RegisterSale(int amount)
    {
        if (Room != Room.Shop)
            return;
        if (sales.Count > 0 && sales[^1].Age < .20f)
        {
            sales[^1] = (sales[^1].Amount + amount, 0);
            return;
        }
        if (sales.Count == 3)
            sales.RemoveAt(0);
        sales.Add((amount, 0));
    }

    internal void BenchStroke(bool clean)
    {
        cutCleanly = clean;
        benchFlash = .38f;
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse || Room != Room.Shop)
            return;

        if (Placement.HasValue)
        {
            TilePicked?.Invoke(FloorTile(mouse.Position));
            return;
        }

        for (int i = hits.Count - 1; i >= 0; i--)
        {
            if (!hits[i].Bounds.HasPoint(mouse.Position))
                continue;

            if (hits[i].Item is Display display)
                DisplayPicked?.Invoke(display);
            else if (hits[i].Item is Customer customer)
                CustomerPicked?.Invoke(customer);
            return;
        }
        EmptyFloorPicked?.Invoke();
    }

    public override void _Draw()
    {
        if (Size.X < 1 || Size.Y < 1)
            return;

        hits.Clear();
        scale = Math.Min(Size.X / 902, Size.Y / 541);
        float offsetX = 24 + (State.Expanded ? 39 * Mathf.Clamp(1 - camera.Length() / 5, 0, 1) : 0);
        origin = new Vector2(Size.X / 2 - offsetX * scale, Size.Y * .292f) - Project(camera);
        sceneTint = outsideTint;
        Backdrop();
        Vector2 streetOrigin = origin;
        if (Near(Room.Shop) || Near(Room.Workshop))
            StreetGround();
        PaintRoom(Room.Workshop, Workshop);
        origin = streetOrigin;
        sceneTint = outsideTint;
        if (Near(Room.Shop) || Near(Room.Workshop))
            StreetLife(back: true);
        PaintRoom(Room.Shop, Shop);
        PaintRoom(Room.Town, Town);
        origin = streetOrigin;
        sceneTint = outsideTint;
        if (Near(Room.Shop) || Near(Room.Workshop))
            StreetLife(back: false);

        bool Near(Room zone)
        {
            Vector2 bounds = Project(RoomOffsets[(int)zone] - camera);
            return Math.Abs(bounds.X) < Size.X * .86f && Math.Abs(bounds.Y) < Size.Y * .90f;
        }

        void PaintRoom(Room zone, Action paint)
        {
            if (!Near(zone))
                return;
            origin = streetOrigin + Project(RoomOffsets[(int)zone]);
            sceneTint = RoomTint(zone);
            paint();
        }
    }

    private Vector2 Project(Vector2 point) => new Vector2(39 * (point.X - point.Y), 19.5f * (point.X + point.Y)) * scale;

    private void Backdrop()
    {
        string[] top = ["91a79a", "8ea7a1", "afa38b", "9baeb1"];
        string[] bottom = ["b8bd9a", "bad1b4", "c5b087", "b6c7c5"];
        for (int band = 0; band < 32; band++)
        {
            Color color = ToyArt.C(top[State.Season]).Lerp(ToyArt.C(bottom[State.Season]), band / 31f);
            DrawRect(new Rect2(0, band * Size.Y / 32, Size.X, Size.Y / 32 + 1), Ambient(color));
        }

        for (int x = -13; x < 22; x++)
        for (int y = -13; y < 22; y++)
        {
            uint grain = Hash(x, y);
            if (grain % 4 != 0)
                continue;

            Vector2 point = P(x + (grain % 7) * .126f, y + .23f);
            Pixel(point.X, point.Y, 2 + grain % 4, 1, "485e472e");
            Pixel(point.X - 2, point.Y - 3, 2, 1, "ecedb73a");
        }

    }

    private void StreetGround()
    {
        string terrain = State.Season switch { 1 => "a2ad83", 2 => "aca181", 3 => "a3b3aa", _ => "98a88a" };
        Ground(-12, -16, 21, 13, terrain);
        Ground(-12, 8.62f, 21, 10.97f, "777f77");
        Ground(-12, 8.6f, 21, 8.77f, "ddd1b4");
        Ground(-12, 10.9f, 21, 11.05f, "acaa92");
        Ground(-12, 11.05f, 21, 12.55f, "beb59b");
        Walkway(6.16f, 8.59f);
        Walkway(-3.05f, -.16f);
        Ground(-1.8f, -11, -.15f, 8.1f, "b8b498");
        Ground(9.62f, -11, 10.88f, 8.12f, "b9b49a");
        for (int x = -11; x < 21; x += 2)
        {
            Ground(x + .04f, 9.73f, x + .69f, 9.80f, "b1ae94");
            Ground(x + .08f, 11.95f, x + .82f, 12.52f, "b5ae96");
        }
        for (int i = 0; i < 6; i++)
            Ground(10.10f + .37f * i, 8.79f, 10.28f + .37f * i, 10.82f, "dad2b7");
        foreach (Vector2 lamp in FrontLamps.Concat(LaneLamps))
            PoolOfLight(P(lamp.X, lamp.Y), 83, nightLight * .48f);

        for (int x = -8; x < 18; x++)
        for (int y = 0; y < 6; y++)
        {
            if (x is >= -2 and <= 10 || Hash(x, y) % 4 == 1)
                continue;
            Vector2 tuft = P(x + .17f + Hash(x, y) % 6 * .112f, y + .31f, 1);
            Pixel(tuft.X, tuft.Y, 4, 2, "687e5e70");
            Pixel(tuft.X + 3, tuft.Y - 3, 2, 3, "758c63");
        }
        House(-5.45f, -5.15f, 2.75f, 2.35f, "c8a882", "a26756");
        House(11.77f, -5.58f, 2.76f, 2.54f, "dad1a7", "628780");
        House(-5.15f, -8.0f, 2.70f, 2.10f, "d2b18f", "8f6c59");
        Garden(-3.72f, .94f);
        Garden(10.71f, 3.62f);
        Garden(-2.72f, -7.1f);
        Tree(-2.10f, 3.41f, .99f);
        Tree(-2.41f, -2.85f, .89f);
        Tree(11.42f, .55f, 1.1f);
        Tree(10.32f, -6.8f, .93f);
        Sprite("PlantTall", 8.91f, .73f, 1.5f);
        Sprite("FlowerCrate", -.72f, -1.24f, 1.68f);

        void Walkway(float start, float end)
        {
            Ground(-12, start, 21, end, "b3ad93");
            for (int x = -12; x < 21; x++)
            for (float y = start; y < end - .2f; y += .57f)
            {
                string color = Hash(x, (int)(y * 50)) % 3 == 0 ? "cfc4aa" : "c2b69d";
                Ground(x + .024f, y + .018f, x + .976f, Math.Min(end - .02f, y + .55f), color);
            }
        }

        void Garden(float x, float y)
        {
            Ground(x - .09f, y - .12f, x + 1.13f, y + .94f, "728b68");
            for (int i = 0; i < 4; i++)
            {
                Ground(x + i * .27f, y, x + .19f + i * .27f, y + .79f, "7b7353");
                Vector2 bloom = P(x + .1f + i * .27f, y + .34f, 3);
                DrawCircle(bloom, 3, ToyArt.C(i % 2 == 0 ? "d6a27b" : "e8c78e"));
            }
        }
    }

    private void StreetLife(bool back)
    {
        List<(float Depth, Action Paint)> people = [];
        foreach (Pedestrian person in State.Pedestrians)
        {
            if (back != (person.Position.Y < 0))
                continue;
            float x = person.Position.X + .5f + person.Direction * (float)(person.Travel + State.ShopRemainder * person.Pace);
            float y = person.Position.Y + .5f;
            people.Add((x + y, () => Figure(x, y, person.Appearance % 6, person.Direction > 0 ? 2 : 0,
                WalkFrame(person.Appearance))));
        }
        foreach (Vector2 lamp in back ? LaneLamps : FrontLamps)
            people.Add((lamp.X + lamp.Y, () => Lamp(lamp)));
        DrawProps(people);
        if (back)
            return;
        Tree(-1.17f, 8.49f, .85f);
        Tree(10.05f, 7.95f, .89f);
        Sign(8.2f, 7.95f);
    }

    private void Shop()
    {
        int width = State.Width;
        WallRoom(width, 6, false);
        Box(width - 1.65f, 6f, 1.44f, .54f, -9, 0, "ae9777");
        Segment(P(width - 1.58f, 6.50f), P(width - .27f, 6.50f), "d3c098", 2);
        Window(1.05f, 2.84f, false);
        Window(width - 2.6f, width - .86f, false);
        Window(.76f, 2.59f, true);
        Picture(width - 3.45f, 80, "daaa62");
        Ground(width - 2.5f, 4.32f, width - .25f, 5.78f, "934f49");
        Ground(width - 2.39f, 4.42f, width - .36f, 5.69f, "c98266");
        Ground(width - 2.27f, 4.53f, width - .48f, 5.6f, "ac6255");
        for (int i = 0; i < 5; i++)
            Ground(width - 2.17f + i * .31f, 4.92f, width - 2.02f + i * .31f, 5.17f, "e0ab80");

        for (int i = 0; i < 4; i++)
        {
            float x = .86f + i * .33f;
            string warmth = morningLight > .4f ? "ffe4ad9e" : "f9d98348";
            Ground(x, .68f, x + .25f, 1.94f, warmth);
            Ground(width - 2.5f + i * .33f, .68f, width - 2.25f + i * .33f, 1.94f, warmth);
        }
        if (State.Shift == Shift.Open)
            PoolOfLight(P(width - .53f, 5.66f), 60, nightLight * .34f);

        if (Placement.HasValue)
        {
            Cell hovered = FloorTile(GetLocalMousePosition());
            foreach (Cell tile in BuildTiles)
                Ground(tile.X + .065f, tile.Y + .065f, tile.X + .935f, tile.Y + .935f,
                    tile == hovered ? "ead99b" : "55906e8c");
        }

        List<(float Depth, Action Paint)> props =
        [
            (width + 5.46f, FrontDoor),
            (width + 4.9f, () => Sign(width - 2.05f, 6.82f)),
            (1.5f, () => Pinboard(3.18f, 3.85f))
        ];

        if (State.Fair)
        {
            for (int i = 0; i < width - 1; i++)
            {
                float x = .5f + i;
                Segment(P(x, .01f, 115 - i % 3 * 2), P(x + 1, .01f, 115 - (i + 1) % 3 * 2), "4a5b53", 1);
                Poly(i % 2 == 0 ? "b46658" : "e0bc73", P(x + .2f, .02f, 114), P(x + .62f, .02f, 112), P(x + .42f, .02f, 101));
            }
        }

        foreach (Display display in State.Displays)
        {
            props.Add((display.X + display.Y + 1f, () => Shelf(display)));
            if (display.Style == Fixture.Register)
                props.Add((display.X + display.Y + .57f,
                    () => Figure(display.X + .62f, display.Y + .05f, 6, 2, 0)));
        }

        Cell tileUnderPointer = FloorTile(GetLocalMousePosition());
        if (Placement is Fixture ghost && BuildTiles.Contains(tileUnderPointer))
        {
            Cell spot = tileUnderPointer;
            props.Add((spot.X + spot.Y + 1f, () => Sprite(SpriteName(ghost), spot.X + .5f, spot.Y + .5f, SpriteSpan(ghost),
                tint: new Color(1, 1, 1, .70f))));
        }

        foreach (Customer customer in State.Customers)
        {
            Vector2 point = CustomerPosition(customer);
            props.Add((point.X + point.Y, () => DrawCustomer(customer, point)));
        }

        DrawProps(props);
        if (State.Shift == Shift.Open && State.Register is Display register)
            CheckoutFeedback(register);
    }

    private void Workshop()
    {
        WallRoom(7, 6, true);
        Window(.87f, 2.7f, true);
        Window(4.75f, 6.22f, false);
        Pinboard(1.35f, 3.95f);
        Ground(1.3f, 2.14f, 5.35f, 4.2f, "6b8980");
        Ground(1.41f, 2.25f, 5.24f, 4.08f, "79988b");
        for (int i = 0; i < 9; i++)
            Ground(1.45f + i * .41f, 2.41f, 1.61f + i * .41f, 3.93f, "b6b29254");

        List<(float Depth, Action Paint)> furnishings =
        [
            (2, () => Sprite("Cabinet", .59f, 1.44f)),
            (2.2f, () => Crate(1.63f, .42f, .47f, "bb8b5d")),
            (7, () => Sprite("PlantTall", 6.36f, .59f, 1.5f)),
            (6.6f, () => Crate(1.04f, 5.05f, .62f, "ac7852")),
            (10, () => Crate(4.72f, 4.95f, .62f, "ae7d54")),
            (10.8f, () => Crate(5.44f, 4.95f, .68f, "b08457")),
            (6, () => Sprite("Plant", .61f, 5.38f, 1.5f))
        ];
        foreach (Station station in Enum.GetValues<Station>())
        {
            Vector2 point = WorkshopStations[(int)station].Machine;
            furnishings.Add((point.X + point.Y, () => DrawStation(station)));
        }
        furnishings.Add((workerPosition.X + workerPosition.Y, DrawWorker));
        if (State.Orders.FirstOrDefault() is { Transfer: > 0 })
        {
            Vector2 cart = workerPosition + (workerFacing switch
            {
                0 => new Vector2(-.57f, .18f), 2 => new Vector2(.57f, -.14f),
                3 => new Vector2(.14f, -.57f), _ => new Vector2(-.14f, .57f)
            });
            furnishings.Add((cart.X + cart.Y, () => Sprite($"Cart{State.CartTier}", cart.X, cart.Y, 2.1f)));
        }
        DrawProps(furnishings);
        DrawToolBubble();
    }

    private void UpdateWorker(float elapsed)
    {
        if (State.ActiveStation is Station next && next != workerStation)
        {
            workerStation = next;
            Vector2 destination = WorkshopStations[(int)next].Operator;
            workerPath.Clear();
            workerPath.Enqueue(new Vector2(workerPosition.X, 2.57f));
            workerPath.Enqueue(new Vector2(destination.X, 2.57f));
            workerPath.Enqueue(destination);
        }

        if (!workerPath.TryPeek(out Vector2 waypoint))
            return;
        Vector2 direction = waypoint - workerPosition;
        workerPosition = workerPosition.MoveToward(waypoint, elapsed * 6.3f);
        if (direction.LengthSquared() > .006f)
            workerFacing = Math.Abs(direction.X) > Math.Abs(direction.Y)
                ? (direction.X > 0 ? 2 : 0) : (direction.Y > 0 ? 1 : 3);
        if (workerPosition.DistanceSquaredTo(waypoint) < .001f)
            workerPath.Dequeue();
    }

    private void DrawWorker()
    {
        bool walking = workerPath.Count > 0;
        bool working = !walking && State.ActiveStation.HasValue && benchFlash > 0;
        int frame = walking && !ReducedMotion ? 1 + (int)(clock * 15) % 7 : 0;
        if (working && !ReducedMotion)
            frame = 1 + Math.Clamp((int)((1 - benchFlash / .38f) * 7), 0, 6);
        Figure(workerPosition.X, workerPosition.Y, 7,
            walking ? workerFacing : WorkshopStations[(int)workerStation].Facing, frame, working ? "MechanicWork" : null);
    }

    private void DrawToolBubble()
    {
        if (!ShowToolPrompt || State.Prompt is not Tool prompt || State.Orders.FirstOrDefault() is not Order order)
            return;

        Vector2 station = WorkshopStations[(int)ShopState.StationFor(order)].Machine;
        Vector2 at = P(station.X, station.Y, 95) + new Vector2(24, 0);
        DrawCircle(at + new Vector2(0, 3), 35, ToyArt.C("304a4180"), antialiased: true);
        DrawCircle(at, 35, ToyArt.C("4a6254"), antialiased: true);
        DrawCircle(at, 32, ToyArt.C("fff0c9"), antialiased: true);
        DrawCircle(at + new Vector2(-16, 34), 5, ToyArt.C("f8e6b4"), antialiased: true);
        DrawTextureRectRegion(Texture(prompt.ToString()), new Rect2(at + new Vector2(-27, -25), new Vector2(54, 44)),
            new Rect2(19, 15, 154, 112));
        float fraction = (float)Math.Clamp(1 - order.Work / State.ToolWindow(order), 0, 1);
        DrawRect(new Rect2(at.X - 19, at.Y + 21, 38, 3), ToyArt.C("b8ac87"));
        DrawRect(new Rect2(at.X - 19, at.Y + 21, 38 * fraction, 3), ToyArt.C("486f61"));
    }

    private void Town()
    {
        for (int x = -1; x < 10; x++)
        for (int y = 1; y < 7; y++)
            Ground(x + .018f, y + .018f, x + .982f, y + .982f,
                new[] { "c6b69d", "cdbca0", "baaf9a", "d1c0a6" }[Hash(x, y) % 4]);

        Ground(3.88f, 3.2f, 6.12f, 5.45f, "9a9d83");
        Tree(.35f, .7f, 1.27f);
        House(1.35f, 1.05f, 2.49f, 1.81f, "897f6a", "a16055");
        House(4.74f, .37f, 3.18f, 2.47f, "dad0a2", "4c7f79");
        Tree(8.45f, .91f, 1.1f);
        Stall(.5f, 4.13f, "ab6556", "e5c18c");
        Stall(6.86f, 3.68f, "527d73", "d8c88f");
        Box(4.1f, 3.43f, 1.83f, 1.79f, 0, 16, "8b978b");
        Box(4.18f, 3.51f, 1.67f, 1.63f, 16, 19, "bac3ac");
        Ground(4.34f, 3.67f, 5.69f, 4.99f, "7daba8", 20);
        Box(4.8f, 4.13f, .37f, .4f, 20, 45, "a1ab96");
        Box(4.73f, 4.06f, .53f, .55f, 44, 51, "c3c5a8");
        for (int i = 0; i < 5; i++)
        {
            float t = (clock * 1.35f + i * .2f) % 1f;
            Vector2 drop = P(5f + t * .32f, 4.32f + t * (i - 1) * .12f, 51 + 11 * Mathf.Sin(t * Mathf.Pi) - 28 * t);
            Pixel(drop.X, drop.Y, 2, 3, "d7e2c4");
        }

        float stroll = (float)(State.StreetClock % 1000) * .118f;
        Figure(3.24f + Mathf.Sin(stroll) * .35f, 4.3f, 3, Mathf.Cos(stroll) >= 0 ? 2 : 0, WalkFrame(3));
        Figure(6.54f, 4.85f, 5, 2, 0);
        Figure(5.34f, 2.93f, 2, 2, 0);
        if (State.Fair)
            Figure(3.31f, 5.49f, 0, 3, 0);
        Tree(1.15f, 7.03f, 1.23f);
        Planter(6.02f, 6.09f, .92f);
    }

    private void WallRoom(int width, int length, bool workshop)
    {
        Ground(-.2f, -.22f, width + .5f, length + .86f, "4f62514a", -13);
        Box(0, 0, width, length, -15, -2, "745c4b");
        for (int y = 0; y < length * 2; y++)
        for (int x = 0; x <= width; x++)
        {
            float shift = y % 2 * .5f;
            float left = Math.Clamp(x - shift + .013f, 0, width);
            float right = Math.Clamp(x + 1 - shift - .013f, 0, width);
            if (right <= left)
                continue;
            string[] wood = workshop ? ["bd966e", "c8a47a", "c49c70", "b58c67", "cdae84"]
                : ["d8ab73", "d0a46d", "e0b786", "d7ab7b", "d3a377"];
            uint hash = Hash(x, y);
            Ground(left, y * .5f + .018f, right, y * .5f + .497f, wood[hash % (uint)wood.Length]);
            if (right - left > .58f)
            {
                float grain = y * .5f + .13f + hash % 7 * .032f;
                Segment(P(left + .15f, grain), P(right - .21f, grain), "885f4530", 1);
                Segment(P(left + .3f, grain + .085f), P(right - .12f, grain + .085f), "ffe3ab36", 1);
            }
        }

        string leftWall = workshop ? "8d9f8b" : "e2c691";
        string rearWall = workshop ? "a1af99" : "f1d7a1";
        Quad(P(0, length, 0), P(0, 0, 0), P(0, 0, 109), P(0, length, 109), leftWall);
        Quad(P(0, 0, 0), P(width, 0, 0), P(width, 0, 109), P(0, 0, 109), rearWall);
        for (int plank = 0; plank < width * 3; plank++)
        {
            float x = plank / 3f;
            Segment(P(x, 0, 28), P(x, 0, 108), workshop ? "788f8040" : "b9936551", 1);
        }
        for (int plank = 0; plank < length * 3; plank++)
        {
            float y = plank / 3f;
            Segment(P(0, y, 28), P(0, y, 108), workshop ? "667d6a50" : "aa855350", 1);
        }
        Quad(P(0, length), P(0, 0), P(0, 0, 29), P(0, length, 29), "638377");
        Quad(P(0, 0), P(width, 0), P(width, 0, 29), P(0, 0, 29), "78978a");
        Segment(P(0, length, 29), P(0, 0, 29), "aac0a3", 3);
        Segment(P(0, 0, 29), P(width, 0, 29), "adbfa4", 3);
        Segment(P(0, length, 108), P(0, 0, 108), "e8c89a", 5);
        Segment(P(0, 0, 108), P(width, 0, 108), "f4daac", 5);
        Segment(P(0, 0, 3), P(width, 0, 3), "6e6750", 3);
        Segment(P(0, length, 3), P(0, 0, 3), "6e6750", 3);
        Segment(P(0, length, 0), P(width, length, 0), "ebc492", 2);
        Segment(P(width, length, 0), P(width, 0, 0), "edca9b", 2);
    }

    private void Window(float start, float end, bool side)
    {
        Face(start - .09f, end + .09f, 43, 97, "ac8962");
        Face(start, end, 47, 94, "52716d");
        Face(start + .061f, end - .062f, 51, 90, "b5cdad");
        Face(start + .061f, end - .062f, 51, 63, "8caa89");
        float segment = (end - start) / 6;
        for (int i = 0; i < 6; i++)
        {
            float x = start + segment * i;
            Face(x + .08f, x + segment * .94f, 52, 61 + Hash(i, side ? 1 : 2) % 14, i % 2 == 0 ? "83a181" : "98b095");
        }

        float middle = (start + end) * .5f;
        Face(middle - .045f, middle + .04f, 48, 94, "ded1a4");
        Face(start, end, 68, 71, "e0d4a8");
        Face(start - .1f, end + .11f, 43, 47, "f3d9a3");

        Vector2 WallPoint(float horizontal, float height) =>
            side ? P(0, horizontal, height) : P(horizontal, 0, height);

        void Face(float a, float b, float low, float high, string color) =>
            Quad(WallPoint(a, low), WallPoint(b, low), WallPoint(b, high), WallPoint(a, high), color);
    }

    private void Picture(float x, float height, string color)
    {
        Quad(P(x, .02f, height - 19), P(x + .56f, .02f, height - 19),
            P(x + .56f, .02f, height + 6), P(x, .02f, height + 6), "925f49");
        Quad(P(x + .065f, .03f, height - 15), P(x + .495f, .03f, height - 15),
            P(x + .495f, .03f, height + 2), P(x + .065f, .03f, height + 2), color);
        Segment(P(x + .14f, .04f, height - 10), P(x + .41f, .04f, height - 5), "997353", 2);
    }

    private void Pinboard(float start, float end)
    {
        Quad(P(start, .02f, 44), P(end, .02f, 44), P(end, .02f, 92), P(start, .02f, 92), "9d714f");
        Quad(P(start + .09f, .026f, 49), P(end - .09f, .026f, 49), P(end - .09f, .026f, 87), P(start + .09f, .026f, 87), "bd9464");
        for (float x = start + .16f; x < end - .34f; x += .43f)
        {
            Quad(P(x, .032f, 57), P(x + .31f, .032f, 58), P(x + .31f, .032f, 79), P(x, .032f, 78), "e7d7a9");
            Segment(P(x + .065f, .04f, 67), P(x + .25f, .04f, 68), "759392", 1);
            Segment(P(x + .065f, .04f, 62), P(x + .27f, .04f, 63), "9f967b", 1);
        }
    }

    private void Shelf(Display display)
    {
        int x = display.X;
        int y = display.Y;
        Ground(x + .04f, y + .04f, x + .98f, y + .99f, "67553d48");
        if (display == Selected && Reading == null)
        {
            Ground(x, y, x + 1, y + 1, "fff0ae");
            Ground(x + .045f, y + .045f, x + .955f, y + .955f, "d6b274");
        }

        Sprite(SpriteName(display.Style), x + .5f, y + .5f, SpriteSpan(display.Style));
        float surface = display.Style == Fixture.Table ? 31 : 35;
        for (int i = 0; i < Math.Min(display.Count, display.Style == Fixture.Table ? 6 : 4); i++)
            ToyArt.Draw(this, display.Toy, P(x + .24f + i % 3 * .24f, y + .38f + i / 3 * .25f, surface), scale * .83f);

        Vector2 p = P(x + .5f, y + .5f);
        if (display.Toy >= 0)
            GameIcons.Family(this, Catalog.Toys[display.Toy].Family, P(x + .77f, y + .8f, 21), 8);
        float halfWidth = display.Style == Fixture.Plant ? 19 : 37;
        float height = display.Style == Fixture.Plant ? 35 : 53;
        hits.Add((new Rect2(p.X - halfWidth * scale, p.Y - height * scale, halfWidth * 2 * scale, (height + 13) * scale), display));
    }

    private void DrawStation(Station station)
    {
        var table = WorkshopStations[(int)station];
        Vector2 spot = table.Machine;
        bool active = State.ActiveStation == station;
        if (active)
            Ground(spot.X - .39f, spot.Y - .7f, spot.X + .4f, spot.Y + .8f, "f3ce775e");
        Sprite(station.ToString(), spot.X, spot.Y, table.Span);
        if (!active)
            return;

        Order order = State.Orders[0];
        if (order.Decoration is Fixture decoration)
        {
            Vector2 item = P(spot.X + .19f, spot.Y + .24f, table.Height);
            DrawTextureRect(Texture(SpriteName(decoration)), new Rect2(item - new Vector2(38, 38), new Vector2(76, 76)), false);
        }
        else if (order.Stage + 1 == ShopState.Stages(order))
            ToyArt.Draw(this, order.Toy, P(spot.X + .19f, spot.Y + .24f, table.Height), scale * 1.17f);
        else
            Box(spot.X + .04f, spot.Y + .09f, .31f, .29f, table.Height, table.Height + 11, "c59562");
        if (benchFlash > 0 && !ReducedMotion)
        {
            Vector2 glint = P(spot.X + .25f, spot.Y + .29f, table.Height + 13);
            float progress = 1 - benchFlash / .38f;
            Color color = ToyArt.C(cutCleanly ? "ffdf92" : "c76957");
            color.A = 1 - progress;
            for (int i = 0; i < 5; i++)
            {
                Vector2 offset = Vector2.FromAngle(-.37f - i * .54f) * ((7 + 28 * progress) * scale);
                DrawCircle(glint + offset, (2.3f - progress) * scale, color);
            }
        }
    }

    private void Crate(float x, float y, float size, string color)
    {
        Box(x, y, size, size * .85f, 0, 18 + size * 7, color);
        Segment(P(x + .08f, y + size * .85f, 6), P(x + size - .07f, y + size * .85f, 19), "e0aa745d", 2);
        Segment(P(x + .11f, y + size * .85f, 14), P(x + size - .11f, y + size * .85f, 14), "76583d80", 1);
    }

    private void Plant(float x, float y, float size)
    {
        Box(x - .13f, y - .12f, .32f, .34f, 0, 12 * size, "986650");
        Vector2 p = P(x, y, 13 * size);
        Segment(p, p + new Vector2(3, -26) * size * scale, "496c58", 3);
        Leaf(-6, -14, 13, "638266");
        Leaf(9, -24, 12, "79966c");
        Leaf(-4, -28, 11, "a1a875");
        Leaf(5, -10, 14, "4e7a62");

        void Leaf(float dx, float dy, float width, string color)
        {
            Vector2 q = p + new Vector2(dx, dy) * size * scale;
            Poly(color, q + new Vector2(-width, -4) * scale * size, q + new Vector2(-2, -8) * scale * size,
                q + new Vector2(width * .54f, -5) * scale * size, q + new Vector2(6, 1) * scale * size,
                q + new Vector2(-3, 3) * scale * size);
        }
    }

    private void Tree(float x, float y, float big)
    {
        Ground(x - .25f, y - .22f, x + .82f, y + .36f, "5f715536");
        Vector2 p = P(x, y);
        Pixel(p.X - 4 * big * scale, p.Y - 48 * big * scale, 8 * big, 49 * big, "8d6d51");
        Pixel(p.X - 3 * big * scale, p.Y - 42 * big * scale, 2 * big, 42 * big, "b38c62");
        string[] light = ["a8b883", "9fb182", "d1ae66", "b6c8bd"];
        string[] shade = ["749b76", "73956f", "ab8052", "879e94"];
        Leaf(-20, -48, 27, shade[State.Season]);
        Leaf(19, -46, 26, shade[State.Season]);
        Leaf(0, -66, 33, light[State.Season]);
        Leaf(-10, -77, 22, light[State.Season]);
        Leaf(16, -68, 21, light[State.Season]);
        for (int i = 0; i < 13; i++)
        {
            float dx = (int)(Hash(i, 19) % 54) - 27;
            float dy = 55 + Hash(i, 3) % 34;
            Pixel(p.X + dx * big * scale, p.Y - dy * big * scale, 3 * big, 2 * big, "e8d9a371");
        }

        void Leaf(float dx, float dy, float radius, string color)
        {
            Vector2 center = p + new Vector2(dx, dy) * big * scale;
            Vector2 D(float a, float b) => center + new Vector2(a, b) * (radius * big * scale);
            Poly(color, D(-1, .13f), D(-.85f, -.34f), D(-.42f, -.64f), D(.4f, -.61f),
                D(.91f, -.22f), D(1, .22f), D(.61f, .54f), D(-.61f, .57f));
        }
    }

    private void Planter(float x, float y, float size)
    {
        Box(x - .28f, y - .36f, .75f, .76f, 0, 8, "92765b");
        for (int i = 0; i < 3; i++)
            Plant(x - .16f + i * .18f, y - .07f + i * .09f, size * .64f);
    }

    private void Sign(float x, float y)
    {
        Box(x, y, .73f, .45f, 0, 4, "816b54");
        Box(x + .09f, y + .1f, .53f, .16f, 4, 35, "81543e");
        Quad(P(x + .14f, y + .263f, 10), P(x + .57f, y + .263f, 10),
            P(x + .57f, y + .263f, 32), P(x + .14f, y + .263f, 32), "374f4a");
        Segment(P(x + .21f, y + .27f, 20), P(x + .49f, y + .27f, 20), "ead7a1", 2);
        Segment(P(x + .28f, y + .27f, 15), P(x + .45f, y + .27f, 15), "c7b386", 1);
    }

    private void House(float x, float y, float w, float d, string walls, string roof)
    {
        Box(x, y, w, d, 0, 75, walls);
        float near = y + d + .008f;
        Quad(P(x + w * .39f, near), P(x + w * .63f, near), P(x + w * .63f, near, 48),
            P(x + w * .39f, near, 48), "425a52");
        for (int i = 0; i < 2; i++)
        {
            float left = x + w * (.12f + i * .63f);
            Vector2[] glass = [P(left, near, 26), P(left + w * .15f, near, 26), P(left + w * .15f, near, 50),
                P(left, near, 50)];
            DrawColoredPolygon(glass, Ambient(ToyArt.C("f2d48c")).Lerp(ToyArt.C("ffd489"), nightLight * .92f));
            Segment(P(left + w * .075f, near + .005f, 27), P(left + w * .075f, near + .005f, 50), "8e775d", 2);
        }

        Color darker = ToyArt.C(roof).Darkened(.2f);
        Poly(darker.ToHtml(), P(x + w, y - .14f, 72), P(x + w, y + d * .5f, 108), P(x + w, y + d + .16f, 72));
        Quad(P(x - .14f, y + d * .5f, 108), P(x + w + .13f, y + d * .5f, 108),
            P(x + w + .13f, y - .14f, 72), P(x - .14f, y - .14f, 72), darker.ToHtml());
        Quad(P(x - .14f, y + d * .5f, 108), P(x + w + .13f, y + d * .5f, 108),
            P(x + w + .13f, y + d + .16f, 72), P(x - .14f, y + d + .16f, 72), roof);
        for (int i = 1; i < 8; i++)
        {
            float slope = i / 8f;
            Segment(P(x - .14f, y + d * .5f + (d * .5f + .16f) * slope, 108 - 36 * slope),
                P(x + w + .13f, y + d * .5f + (d * .5f + .16f) * slope, 108 - 36 * slope), "332e3344", 1);
        }
        Box(x + .34f, y + .64f, .34f, .31f, 96, 121, "a88870");
    }

    private void Stall(float x, float y, string shade, string cream)
    {
        Box(x, y, 1.55f, .8f, 1, 30, "ae7b56");
        Box(x - .1f, y - .07f, 1.75f, .99f, 26, 32, "cb9c69");
        Box(x + .05f, y + .07f, .1f, .1f, 32, 70, "82634b");
        Box(x + 1.44f, y + .07f, .1f, .1f, 32, 70, "82634b");
        ToyArt.Draw(this, 0, P(x + .34f, y + .51f, 33), scale);
        ToyArt.Draw(this, 1, P(x + .86f, y + .51f, 33), scale);
        ToyArt.Draw(this, 2, P(x + 1.21f, y + .61f, 33), scale);
        for (int i = 0; i < 7; i++)
        {
            float left = x - .14f + i * .26f;
            string stripe = i % 2 == 0 ? shade : cream;
            Quad(P(left, y - .09f, 74), P(left + .26f, y - .09f, 74),
                P(left + .26f, y + 1.04f, 56), P(left, y + 1.04f, 56), stripe);
            Quad(P(left, y + 1.04f, 56), P(left + .26f, y + 1.04f, 56),
                P(left + .26f, y + 1.04f, 50), P(left, y + 1.04f, 50), ToyArt.C(stripe).Darkened(.1f).ToHtml());
        }
    }

    private Vector2 CustomerPosition(Customer customer)
    {
        Vector2 point = new(customer.Position.X + .5f, customer.Position.Y + .5f);
        if (!customer.Walking)
            return point;
        Cell next = customer.Route[0];
        double pace = customer.Phase is VisitPhase.Approach or VisitPhase.Depart
            ? (customer.StreetPace > 0 ? customer.StreetPace : .17) : .36;
        float travel = (float)Math.Min(1, customer.Travel + State.ShopRemainder * pace);
        return point.Lerp(new Vector2(next.X + .5f, next.Y + .5f), travel);
    }

    private int WalkFrame(int appearance) => ReducedMotion ? 0 : 1 + (int)((State.StreetClock * 1.16 + appearance * 2.7) % 7);

    private void DrawCustomer(Customer customer, Vector2 point)
    {
        Cell facing = customer.Walking ? customer.Route[0] :
            customer.Phase == VisitPhase.Checkout && State.Register is Display register ? new Cell(register.X, register.Y) :
            customer.DisplayCell ?? customer.Position;
        int direction = facing.X > customer.Position.X ? 2 : facing.X < customer.Position.X ? 0 :
            facing.Y < customer.Position.Y ? 3 : 1;
        Color roomLight = sceneTint;
        bool inside = point.Y < 6;
        if (!inside)
            sceneTint = outsideTint;
        int frame = customer.Walking ? WalkFrame(customer.Appearance) : 0;
        Figure(point.X, point.Y, customer.Appearance % 6, direction, frame);
        Vector2 p = P(point.X, point.Y);
        if (customer.Basket.Count > 0)
        {
            float x = p.X + (direction is 0 or 1 ? -16 : 7) * scale;
            float y = p.Y - 21 * scale;
            Pixel(x + 2 * scale, y - 3 * scale, 6, 4, "956549");
            Pixel(x, y, 10, 10, customer.Result == 1 ? "b68659" : "e6c089");
            Pixel(x + 7 * scale, y, 3, 10, "a77956");
        }
        if (inside && (customer == Reading || (customer.Phase == VisitPhase.Browse && !customer.Walking)))
        {
            Vector2 mark = p - new Vector2(0, 73 * scale);
            GameIcons.Family(this, customer.Family, mark, 13 * scale);
            if (customer.WantsExact)
            {
                DrawCircle(mark + new Vector2(17, 2), 11, ToyArt.C("f5e2b5"), antialiased: true);
                ToyArt.Draw(this, customer.Wanted, mark + new Vector2(17, 10), .68f);
            }
        }
        if (inside && customer.Phase == VisitPhase.Leave && customer.Result != 0)
            DrawEmote(p - new Vector2(0, 70 * scale), customer);
        if (inside)
            hits.Add((new Rect2(p.X - 17 * scale, p.Y - 57 * scale, 34 * scale, 60 * scale), customer));
        sceneTint = roomLight;
    }

    private void DrawEmote(Vector2 at, Customer customer)
    {
        bool happy = customer.Result == 1 && customer.Mood > 0;
        bool angry = customer.Mood < 0;
        Color fill = ToyArt.C(happy ? "f7d7d2" : angry ? "f3c9a8" : "f5e2b5");
        float radius = 12 * scale;
        DrawCircle(at, radius + 1.5f, ToyArt.C("3a5d4f"), antialiased: true);
        DrawCircle(at, radius, fill, antialiased: true);
        if (happy)
        {
            Color heart = ToyArt.C("c95d62");
            DrawCircle(at + new Vector2(-3.4f, -2) * scale, 3.8f * scale, heart, antialiased: true);
            DrawCircle(at + new Vector2(3.4f, -2) * scale, 3.8f * scale, heart, antialiased: true);
            DrawColoredPolygon([at + new Vector2(-7.2f, -.6f) * scale, at + new Vector2(7.2f, -.6f) * scale,
                at + new Vector2(0, 8) * scale], heart);
            return;
        }

        string glyph = customer.Result == 1 ? (angry ? "!" : "✓") : angry ? "?" : "…";
        Font font = ThemeDB.FallbackFont;
        int size = (int)(17 * scale);
        DrawString(font, at + new Vector2(-4.5f * scale, 6 * scale), glyph, fontSize: size, modulate: ToyArt.C("3a5d4f"));
    }

    private void Figure(float x, float y, int style, int direction, int frame, string? actionSprite = null)
    {
        string name = actionSprite ?? $"Person{style}";
        Texture2D texture = Texture(name);
        if (!feetBySprite.TryGetValue(name, out Vector2[]? feet))
            feetBySprite[name] = feet = MeasureFeet(texture);
        float size = 123 * scale;
        Vector2 p = P(x, y);
        Vector2 contact = feet[direction * 8 + frame] * (size / 112);
        Vector2 center = p + new Vector2(contact.X - size / 2, -.05f);
        Vector2[] shadow = new Vector2[16];
        for (int i = 0; i < shadow.Length; i++)
            shadow[i] = center + Vector2.FromAngle(i * Mathf.Tau / shadow.Length) * new Vector2(10, 3.5f) * scale;
        DrawColoredPolygon(shadow, Ambient(ToyArt.C("453c356d")));
        DrawTextureRectRegion(texture,
            new Rect2(p - new Vector2(size * .5f, contact.Y - .7f * scale), new Vector2(size, size)),
            new Rect2(frame * 112, direction * 112, 112, 112), Ambient(Colors.White));
    }

    private static Vector2[] MeasureFeet(Texture2D texture)
    {
        using Image bitmap = texture.GetImage();
        if (bitmap.IsCompressed())
            bitmap.Decompress();
        bitmap.Convert(Image.Format.Rgba8);
        byte[] pixels = bitmap.GetData();
        int width = bitmap.GetWidth();
        Vector2[] result = new Vector2[32];
        for (int direction = 0; direction < 4; direction++)
        for (int frame = 0; frame < 8; frame++)
        {
            Vector2 contact = new(56, 80);
            for (int y = 108; y >= 66; y--)
            {
                int sum = 0;
                int count = 0;
                for (int x = 15; x < 97; x++)
                {
                    int offset = ((direction * 112 + y) * width + frame * 112 + x) * 4 + 3;
                    if (pixels[offset] < 145)
                        continue;
                    sum += x;
                    count++;
                }
                if (count < 2)
                    continue;
                contact = new Vector2((float)sum / count, y + .76f);
                break;
            }
            result[direction * 8 + frame] = contact;
        }
        return result;
    }

    private void CheckoutFeedback(Display register)
    {
        Vector2 p = P(register.X + .5f, register.Y + .5f, 54);
        if (State.ReadyToPay != null)
        {
            DrawCircle(p - new Vector2(0, 5), 9, ToyArt.C("3a5d4f"));
            DrawCircle(p - new Vector2(0, 5), 6.5f, ToyArt.C("efd094"));
        }
        for (int i = 0; i < sales.Count; i++)
        {
            var sale = sales[i];
            float life = sale.Age / 1.36f;
            float alpha = 1 - Mathf.SmoothStep(.57f, 1f, life);
            float rise = ReducedMotion ? 16 : 9 + 46 * Mathf.Ease(life, .65f);
            Vector2 at = p + new Vector2(-23, -rise - (sales.Count - i - 1) * 24);
            string text = $"+${sale.Amount}";
            Color shadow = ToyArt.C("304f3c");
            Color gold = ToyArt.C("fff1b3");
            shadow.A = gold.A = alpha;
            Font font = ThemeDB.FallbackFont;
            DrawString(font, at + new Vector2(1, 2), text, fontSize: 21, modulate: shadow);
            DrawString(font, at, text, fontSize: 21, modulate: gold);
            if (ReducedMotion || life > .48f)
                continue;
            for (int spark = 0; spark < 4; spark++)
            {
                Vector2 d = Vector2.FromAngle(-.25f - spark * .92f) * (7 + life * 48);
                DrawCircle(p + d, (1 - life) * 2, gold);
            }
        }
    }

    private void Sprite(string name, float x, float y, float span = 2.9f, Color? tint = null)
    {
        float size = span * 55.15f * scale;
        Vector2 p = P(x, y);
        DrawTextureRect(Texture(name), new Rect2(p - new Vector2(size / 2, size / 2), new Vector2(size, size)),
            false, Ambient(tint ?? Colors.White));
    }

    internal static string SpriteName(Fixture fixture) => fixture switch
    {
        Fixture.Display => "Shelf", Fixture.Table => "Table", Fixture.Register => "Register", _ => fixture.ToString()
    };

    private static float SpriteSpan(Fixture fixture) => fixture == Fixture.Plant ? 1.5f : fixture > Fixture.Plant ? 2.35f : 2.9f;

    private Texture2D Texture(string name)
    {
        if (!sprites.TryGetValue(name, out Texture2D? texture))
            sprites[name] = texture = GD.Load<Texture2D>($"res://Art/Furniture/{name}.png");
        return texture;
    }

    private Cell FloorTile(Vector2 mouse)
    {
        Vector2 point = (mouse - origin) / scale;
        return new Cell(Mathf.FloorToInt((point.X / 39 + point.Y / 19.5f) / 2),
            Mathf.FloorToInt((point.Y / 19.5f - point.X / 39) / 2));
    }

    private void DrawProps(List<(float Depth, Action Paint)> props)
    {
        foreach (var prop in props.OrderBy(item => item.Depth))
            prop.Paint();
    }

    private Vector2 P(float x, float y, float z = 0) =>
        (origin + new Vector2(39 * (x - y), 19.5f * (x + y) - z) * scale).Round();

    private void Ground(float x, float y, float right, float bottom, string color, float z = 0) =>
        Quad(P(x, y, z), P(right, y, z), P(right, bottom, z), P(x, bottom, z), color);

    private void Box(float x, float y, float w, float d, float bottom, float top, string hex)
    {
        Color color = ToyArt.C(hex);
        Quad(P(x, y + d, bottom), P(x + w, y + d, bottom), P(x + w, y + d, top), P(x, y + d, top), color.Darkened(.13f).ToHtml());
        Quad(P(x + w, y, bottom), P(x + w, y + d, bottom), P(x + w, y + d, top), P(x + w, y, top), color.Darkened(.27f).ToHtml());
        Ground(x, y, x + w, y + d, color.Lightened(.13f).ToHtml(), top);
    }

    private void Pixel(float x, float y, float width, float height, string color) =>
        DrawRect(new Rect2(Mathf.Round(x), Mathf.Round(y), Math.Max(1, Mathf.Round(width * scale)),
            Math.Max(1, Mathf.Round(height * scale))), Ambient(ToyArt.C(color)));

    private void Segment(Vector2 a, Vector2 b, string color, float width) =>
        DrawLine(a, b, Ambient(ToyArt.C(color)), Math.Max(width * scale, 1));

    private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, string color) => Poly(color, a, b, c, d);

    private void Poly(string color, params Vector2[] points)
    {
        if (points.Distinct().Count() == points.Length)
            DrawColoredPolygon(points, Ambient(ToyArt.C(color)));
    }

    private static uint Hash(int x, int y) => unchecked((uint)((x * 87539319) ^ (y * 19349663)) * 2654435761u) >> 8;
}
