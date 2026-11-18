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
    internal Action<Display>? DisplayPicked { get; set; }
    internal Action<Customer>? CustomerPicked { get; set; }
    internal Action<Cell>? TilePicked { get; set; }
    internal Action? EmptyFloorPicked { get; set; }

    private readonly List<(Rect2 Bounds, object Item)> hits = [];
    private readonly List<(int Amount, float Age)> sales = [];
    private readonly Dictionary<string, Texture2D> sprites = [];
    private readonly Dictionary<Customer, Vector2> positions = [];
    private Vector2 origin;
    private float scale;
    private float clock;
    private float benchFlash;
    private bool cutCleanly;

    public override void _Process(double delta)
    {
        if (!ReducedMotion && !Paused)
            clock += (float)delta;
        benchFlash = Math.Max(0, benchFlash - (float)delta);
        if (Room == Room.Shop)
        {
            foreach (Customer person in State.Customers)
            {
                Vector2 target = CustomerPosition(person);
                positions[person] = positions.TryGetValue(person, out Vector2 previous)
                    ? previous.Lerp(target, 1 - Mathf.Exp(-(float)delta * 17)) : target;
            }
            foreach (Customer gone in positions.Keys.Where(person => !State.Customers.Contains(person)).ToArray())
                positions.Remove(gone);
        }
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

    internal void RegisterSale(int amount)
    {
        if (Room == Room.Shop)
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
        scale = Math.Min(Size.X / 825, Size.Y / (Room == Room.Town ? 555 : 493));
        origin = new Vector2(Size.X / 2 - (Room == Room.Shop && State.Expanded ? 64 : 24) * scale,
            Size.Y * (Room == Room.Town ? .26f : .294f));
        Backdrop();
        switch (Room)
        {
            case Room.Shop: Shop(); break;
            case Room.Workshop: Workshop(); break;
            case Room.Town: Town(); break;
        }
    }

    private void Backdrop()
    {
        string[] top = ["91a79a", "8ea7a1", "afa38b", "9baeb1"];
        string[] bottom = ["b8bd9a", "bad1b4", "c5b087", "b6c7c5"];
        for (int band = 0; band < 32; band++)
        {
            Color color = ToyArt.C(top[State.Season]).Lerp(ToyArt.C(bottom[State.Season]), band / 31f);
            if (State.Shift == Shift.Closed)
                color = color.Lerp(ToyArt.C("48576b"), .31f);
            DrawRect(new Rect2(0, band * Size.Y / 32, Size.X, Size.Y / 32 + 1), color);
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

        Ground(-2, 5.9f, 12, 8.3f, "afb2a0");
        Ground(-2, 6.47f, 12, 7.93f, "c8bfa9");
        for (int x = -2; x < 12; x++)
        {
            Ground(x + .02f, 6.53f, x + .98f, 7.18f, x % 2 == 0 ? "ccc4b0" : "c4baa4");
            Ground(x + .02f, 7.22f, x + .98f, 7.87f, x % 2 == 0 ? "c4baa4" : "cec5ae");
        }
    }

    private void Shop()
    {
        int width = State.Width;
        WallRoom(width, 6, false);
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
            Ground(x, .68f, x + .25f, 1.55f, "f9d98359");
            Ground(width - 2.5f + i * .33f, .68f, width - 2.25f + i * .33f, 1.55f, "f9d98359");
        }

        if (Placement.HasValue)
        {
            Cell hovered = FloorTile(GetLocalMousePosition());
            foreach (Cell tile in BuildTiles)
                Ground(tile.X + .065f, tile.Y + .065f, tile.X + .935f, tile.Y + .935f,
                    tile == hovered ? "ead99b" : "55906e8c");
        }

        List<(float Depth, Action Paint)> props =
        [
            (width + 6.3f, () => Sign(width - 1.5f, 7.05f)),
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
            Vector2 point = positions.GetValueOrDefault(customer, CustomerPosition(customer));
            props.Add((point.X + point.Y, () => DrawCustomer(customer, point)));
        }

        DrawProps(props);
        if (State.Register is Display register)
            CheckoutFeedback(register);
        Tree(-.9f, 7.55f, .83f);
        Sprite("PlantTall", width + 1.42f, .86f, 1.5f);
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

        Sprite("Cabinet", .59f, 1.44f);
        Sprite("Cabinet", 5.49f, .59f);
        for (int i = 0; i < 3; i++)
            Crate(.48f + i * .68f, .5f, .56f, i == 1 ? "a7855f" : "bb8b5d");
        Sprite("PlantTall", 6.36f, .59f, 1.5f);
        Figure(3.08f, 1.94f, 7, 1, 0);
        Bench(2.16f, 2.69f);
        Sprite("Cabinet", 6.03f, 2.94f);
        Box(.64f, 3.7f, .54f, .58f, 15, 27, "e0b674");
        Crate(.31f, 3.59f, .76f, "ab7750");
        Crate(.84f, 4.48f, .63f, "bc9060");
        Crate(4.51f, 4.8f, .62f, "ae7d54");
        Crate(5.27f, 4.6f, .68f, "b08457");
        Crate(5.79f, 4.93f, .58f, "af8459");
        Sprite("Plant", .68f, 5.28f, 1.5f);
        Tree(-1.11f, 7.45f, .84f);
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

        Figure(3.24f + Mathf.Sin(clock * .65f) * .35f, 4.3f, 3, 1, 0);
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
        float halfWidth = display.Style == Fixture.Plant ? 19 : 37;
        float height = display.Style == Fixture.Plant ? 35 : 53;
        hits.Add((new Rect2(p.X - halfWidth * scale, p.Y - height * scale, halfWidth * 2 * scale, (height + 13) * scale), display));
    }

    private void Bench(float x, float y)
    {
        Sprite("Bench", x + 1.08f, y + .49f, 3.4f);
        int toy = State.Orders.Count > 0 ? State.Orders[0].Toy : 1;
        ToyArt.Draw(this, toy, P(x + 1.18f, y + .45f, 39), scale * 1.21f);
        if (benchFlash > 0 && !ReducedMotion)
        {
            Vector2 glint = P(x + 1.18f, y + .46f, 52);
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
            Quad(P(left, near, 26), P(left + w * .15f, near, 26), P(left + w * .15f, near, 50),
                P(left, near, 50), "f2d48c");
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

    private static Vector2 CustomerPosition(Customer customer)
    {
        Vector2 point = new(customer.Position.X + .5f, customer.Position.Y + .5f);
        if (!customer.Walking)
            return point;
        Cell next = customer.Route[0];
        return point.Lerp(new Vector2(next.X + .5f, next.Y + .5f), (float)customer.Travel);
    }

    private void DrawCustomer(Customer customer, Vector2 point)
    {
        Cell facing = customer.Walking ? customer.Route[0] :
            customer.Phase == VisitPhase.Checkout && State.Register is Display register ? new Cell(register.X, register.Y) :
            customer.DisplayCell ?? customer.Position;
        int direction = facing.X > customer.Position.X ? 2 : facing.X < customer.Position.X ? 0 :
            facing.Y < customer.Position.Y ? 3 : 1;
        int frame = customer.Walking && !ReducedMotion ? 1 + (int)(clock * 12 + customer.Appearance) % 7 : 0;
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
        if (customer == Reading)
        {
            Pixel(p.X - 13 * scale, p.Y - 84 * scale, 26, 25, "47594c");
            Pixel(p.X - 12 * scale, p.Y - 83 * scale, 24, 23, "f3d9a3");
            ToyArt.Draw(this, customer.Wanted, p - new Vector2(0, 62 * scale), scale * .89f);
        }
        hits.Add((new Rect2(p.X - 17 * scale, p.Y - 57 * scale, 34 * scale, 60 * scale), customer));
    }

    private void Figure(float x, float y, int style, int direction, int frame)
    {
        Ground(x - .16f, y - .13f, x + .21f, y + .21f, "4c413c4a");
        float size = 123 * scale;
        Vector2 p = P(x, y);
        DrawTextureRectRegion(Texture($"Person{style}"),
            new Rect2(p - new Vector2(size * .5f, size * .71f), new Vector2(size, size)),
            new Rect2(frame * 112, direction * 112, 112, 112));
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
            Vector2 at = p + new Vector2(-23 + i * 9, -rise);
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
            false, tint ?? Colors.White);
    }

    internal static string SpriteName(Fixture fixture) => fixture switch
    {
        Fixture.Display => "Shelf", Fixture.Table => "Table", Fixture.Plant => "Plant", _ => "Register"
    };

    private static float SpriteSpan(Fixture fixture) => fixture == Fixture.Plant ? 1.5f : 2.9f;

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
            Math.Max(1, Mathf.Round(height * scale))), ToyArt.C(color));

    private void Segment(Vector2 a, Vector2 b, string color, float width) =>
        DrawLine(a, b, ToyArt.C(color), Math.Max(width * scale, 1));

    private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, string color) => Poly(color, a, b, c, d);

    private void Poly(string color, params Vector2[] points)
    {
        if (points.Distinct().Count() == points.Length)
            DrawColoredPolygon(points, ToyArt.C(color));
    }

    private static uint Hash(int x, int y) => unchecked((uint)((x * 87539319) ^ (y * 19349663)) * 2654435761u) >> 8;
}
