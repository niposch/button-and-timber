using Godot;

namespace ButtonAndTimber;

public partial class StreetView : Control
{
    internal ShopState State { get; set; } = new();
    internal Room Room { get; set; }
    internal Display? Selected { get; set; }
    internal Customer? Reading { get; set; }
    internal bool Moving { get; set; }
    internal bool ReducedMotion { get; set; }
    internal bool Paused { get; set; }
    internal Action<Display>? DisplayPicked { get; set; }
    internal Action<Customer>? CustomerPicked { get; set; }
    internal Action<Vector2I>? TilePicked { get; set; }

    private readonly List<(Rect2 Bounds, object Item)> hits = [];
    private Vector2 origin;
    private float scale;
    private float clock;

    public override void _Process(double delta)
    {
        if (!ReducedMotion && !Paused)
            clock += (float)delta;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse || Room != Room.Shop)
            return;

        if (Moving)
        {
            Vector2 point = (mouse.Position - origin) / scale;
            TilePicked?.Invoke(new Vector2I(Mathf.FloorToInt((point.X / 39 + point.Y / 19.5f) / 2),
                Mathf.FloorToInt((point.Y / 19.5f - point.X / 39) / 2)));
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
            break;
        }
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

        if (Moving)
        {
            for (int y = 1; y < 5; y++)
            for (int x = 1; x < width - 1; x++)
                if (State.CanPlace(x, y))
                    Ground(x + .05f, y + .05f, x + .95f, y + .95f, "719e777f");
        }

        List<(float Depth, Action Paint)> props =
        [
            (1.0f, () => Plant(.6f, .55f, 1.35f)),
            (3.9f, () => Cabinet(.3f, 3.25f, .64f, 1.03f, 44, "a2694c", "c18a61")),
            (width - .25f, () => Counter(width - 1.92f, 1.5f)),
            (width - .72f, () => Person(width - .79f, 1.2f, 11, true, 0, 0)),
            (width + 4.48f, () => Plant(width - .48f, 5.2f, .85f)),
            (width + 6.3f, () => Sign(width - 1.5f, 7.05f)),
            (5.92f, () => Crate(.41f, 5.25f, .54f, "ac7452")),
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
            props.Add((display.X + display.Y + .85f, () => Shelf(display)));

        foreach (Customer customer in State.Customers)
        {
            Vector2 point = CustomerPosition(customer);
            props.Add((point.X + point.Y, () => DrawCustomer(customer, point)));
        }

        DrawProps(props);
        Tree(-.9f, 7.55f, .83f);
        Planter(width + 1.37f, .85f, .82f);
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

        Cabinet(.25f, 1.02f, .61f, .91f, 53, "749182", "96a793");
        Cabinet(4.74f, .2f, 1.68f, .85f, 38, "a56d4b", "bf8c63");
        for (int i = 0; i < 3; i++)
            Crate(.48f + i * .68f, .5f, .56f, i == 1 ? "a7855f" : "bb8b5d");
        Plant(6.36f, .59f, 1.2f);
        Person(3.08f, 1.94f, 27, true, State.Orders.Count > 0 ? Mathf.Sin(clock * 5) * 1.8f : 0, 0);
        Bench(2.16f, 2.69f);
        Cabinet(5.58f, 2.4f, .81f, 1.35f, 53, "768d80", "94a998");
        Box(5.71f, 2.68f, .53f, .63f, 54, 65, "6c8f95");
        Box(5.81f, 2.83f, .44f, .37f, 65, 72, "dcbd78");
        Box(.64f, 3.7f, .54f, .58f, 15, 27, "e0b674");
        Crate(.31f, 3.59f, .76f, "ab7750");
        Crate(.84f, 4.48f, .63f, "bc9060");
        Crate(4.51f, 4.8f, .62f, "ae7d54");
        Crate(5.27f, 4.6f, .68f, "b08457");
        Crate(5.79f, 4.93f, .58f, "af8459");
        Plant(.68f, 5.28f, 1.0f);
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

        Person(3.24f + Mathf.Sin(clock * .65f) * .35f, 4.3f, 19, false, 1, 0);
        Person(6.54f, 4.85f, 7, false, 0, 0);
        Person(5.34f, 2.93f, 2, true, 0, 0);
        if (State.Fair)
            Person(3.31f, 5.49f, 25, false, 0, 0);
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

    private void Cabinet(float x, float y, float w, float d, float height, string frame, string lip)
    {
        Box(x, y, w, d, 0, height, frame);
        Box(x - .035f, y - .035f, w + .07f, d + .07f, height - 5, height, lip);
        Segment(P(x, y + d, height * .52f), P(x + w, y + d, height * .52f), "644f434d", 2);
        Segment(P(x + w * .43f, y + d + .005f, height * .59f), P(x + w * .65f, y + d + .005f, height * .59f), "debe81", 2);
        Segment(P(x + w * .43f, y + d + .005f, height * .35f), P(x + w * .65f, y + d + .005f, height * .35f), "debe81", 2);
    }

    private void Shelf(Display display)
    {
        float x = display.X + .025f;
        float y = display.Y + .075f;
        int height = display.Style == Fixture.Table ? 28 : 58;
        Ground(x - .15f, y - .03f, x + 1.15f, y + 1.03f, "69583d43");
        if (display == Selected && Reading == null)
        {
            Ground(x - .10f, y - .1f, x + 1.05f, y + 1.02f, "fff0ae");
            Ground(x - .055f, y - .056f, x + .995f, y + .97f, "d6b274");
        }

        if (display.Style == Fixture.Table)
        {
            Box(x + .05f, y + .08f, .11f, .11f, 1, 26, "9e684e");
            Box(x + .79f, y + .08f, .11f, .11f, 1, 26, "9e684e");
            Box(x + .05f, y + .67f, .11f, .11f, 1, 26, "9e684e");
            Box(x + .79f, y + .67f, .11f, .11f, 1, 26, "9e684e");
            Box(x - .02f, y - .04f, 1.01f, .91f, 22, 28, "b76f59");
            Ground(x + .09f, y + .065f, x + .88f, y + .73f, "e1ad7c", 28.1f);
            foreach (int i in Enumerable.Range(0, Math.Min(display.Count, 5)))
                ToyArt.Draw(this, display.Toy, P(x + .24f + i % 3 * .235f, y + .21f + i / 3 * .36f, 29), scale * .77f);
        }
        else
        {
            string trim = display.Toy == 1 ? "7a9b91" : "c68f6e";
            Box(x, y, .92f, .14f, 0, 43, "9f724f");
            Box(x, y, .1f, .72f, 0, 30, "aa744f");
            Box(x + .83f, y, .1f, .72f, 0, 30, "a97752");
            Box(x, y, .94f, .75f, 4, 11, "bb8558");
            for (int i = 0; i < display.Count - 3; i++)
                ToyArt.Draw(this, display.Toy, P(x + .3f + i * .36f, y + .53f, 12), scale * .86f);
            Box(x - .03f, y - .035f, 1f, .83f, 27, 32, trim);
            for (int i = 0; i < Math.Min(display.Count, 3); i++)
                ToyArt.Draw(this, display.Toy, P(x + .23f + i * .265f, y + .32f + i % 2 * .23f, 33), scale * .99f);
            Segment(P(x - .018f, y + .8f, 31), P(x + .94f, y + .8f, 31), "ead2a5", 1);
        }

        Vector2 tag = P(x + .5f, y + .85f, display.Style == Fixture.Table ? 25 : 29);
        Pixel(tag.X - 6, tag.Y - 4, 13, 7, display.Count > 0 ? "ecd7a3" : "ca7359");
        Vector2 top = P(x + .47f, y + .35f, height - 6);
        hits.Add((new Rect2(top.X - 39 * scale, top.Y - 19 * scale, 78 * scale, (height + 33) * scale), display));
    }

    private void Counter(float x, float y)
    {
        Cabinet(x, y, 1.35f, .64f, 35, "63857b", "a4b39c");
        Box(x + .81f, y + .23f, .39f, .31f, 35, 42, "495957");
        Box(x + .86f, y + .23f, .25f, .14f, 42, 49, "758780");
        Ground(x + .17f, y + .2f, x + .59f, y + .48f, "efd9a8", 35.2f);
        Segment(P(x + .26f, y + .24f, 36), P(x + .5f, y + .24f, 36), "af876b", 1);
    }

    private void Bench(float x, float y)
    {
        Box(x + .12f, y + .09f, .15f, .15f, 0, 37, "8d6149");
        Box(x + 1.84f, y + .09f, .15f, .15f, 0, 37, "8d6149");
        Box(x + .12f, y + .7f, .15f, .15f, 0, 37, "8d6149");
        Box(x + 1.84f, y + .7f, .15f, .15f, 0, 37, "8d6149");
        Box(x, y, 2.15f, 1.01f, 33, 41, "ab7754");
        Ground(x + .14f, y + .2f, x + .79f, y + .77f, "73999b", 41.1f);
        for (int i = 0; i < 4; i++)
            Segment(P(x + .24f, y + .29f + i * .09f, 41.4f), P(x + .68f, y + .29f + i * .09f, 41.4f), "bbceb387", 1);
        int toy = State.Orders.Count > 0 ? State.Orders[0].Toy : 1;
        ToyArt.Draw(this, toy, P(x + 1.18f, y + .45f, 42), scale * 1.21f);
        Box(x + 1.65f, y + .35f, .27f, .38f, 41, 45, "76584b");
        if (State.Orders.Count > 0 && (int)(clock * 5) % 4 == 0)
        {
            Vector2 glint = P(x + 1.32f, y + .46f, 67);
            Pixel(glint.X, glint.Y - 3, 2, 8, "f1d68c");
            Pixel(glint.X - 3, glint.Y, 8, 2, "f1d68c");
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

    private Vector2 CustomerPosition(Customer customer)
    {
        Vector2 entry = new(State.Width - 1.12f, 5.68f);
        Vector2 aisle = new(customer.TargetX + .34f, 5.34f);
        Vector2 target = new(customer.TargetX + .44f, customer.TargetY + 1.09f);
        float elapsed = (float)customer.Age;
        if (elapsed < 5)
            return entry.Lerp(aisle, elapsed / 5);
        if (elapsed < 14)
            return aisle.Lerp(target, (elapsed - 5) / 9);
        if (elapsed < 32)
            return target;
        if (elapsed < 42)
            return target.Lerp(aisle, (elapsed - 32) / 10);
        return aisle.Lerp(entry, Mathf.Clamp((elapsed - 42) / 10, 0, 1));
    }

    private void DrawCustomer(Customer customer, Vector2 point)
    {
        bool walking = customer.Age is < 14 or >= 32;
        float step = walking ? Mathf.Sin(clock * 15 + customer.Appearance) : 0;
        Person(point.X, point.Y, customer.Appearance, false, Mathf.Abs(step) * 2, step);
        Vector2 p = P(point.X, point.Y);
        if (customer.Result != 0 || customer == Reading)
        {
            string bubble = customer.Result < 0 ? "cd987b" : "ead7a0";
            Pixel(p.X - 9 * scale, p.Y - 65 * scale, 18, 13, "4b605a");
            Pixel(p.X - 8 * scale, p.Y - 64 * scale, 16, 11, bubble);
            Pixel(p.X - 2 * scale, p.Y - 53 * scale, 4, 3, bubble);
            Pixel(p.X - 4 * scale, p.Y - 61 * scale, 2, 2, "515349");
            Pixel(p.X + 3 * scale, p.Y - 61 * scale, 2, 2, "515349");
            Pixel(p.X - 2 * scale, p.Y - (customer.Result < 0 ? 59 : 57) * scale, 5, 1, "515349");
        }
        hits.Add((new Rect2(p.X - 21 * scale, p.Y - 58 * scale, 42 * scale, 61 * scale), customer));
    }

    private void Person(float x, float y, int style, bool clerk, float bob, float step)
    {
        Ground(x - .2f, y - .15f, x + .31f, y + .27f, "4c413c50");
        Vector2 p = P(x, y, bob);
        string[] shirts = ["ab635b", "5a8e87", "c89e53", "637e9b", "97778c", "9a9667"];
        string[] skins = ["f0bc87", "d4946e", "a9674d", "e4ad87"];
        string[] hair = ["654c3b", "b87949", "4e4340", "8b533b", "ce9e60", "564641"];
        string skin = skins[style / 6 % skins.Length];
        string top = shirts[style % shirts.Length];
        string cap = hair[style / 3 % hair.Length];

        Rect(-9, -10 + step * 3, 7, 9, "4a5454");
        Rect(2, -10 - step * 3, 7, 9, "55615f");
        Rect(-10, -3 + step * 3, 8, 4, "49504a");
        Rect(2, -3 - step * 3, 9, 4, "49504a");
        Rect(-11, -24, 22, 16, top);
        Rect(-13, -20 - step, 5, 10, ToyArt.C(top).Darkened(.15f).ToHtml());
        Rect(10, -20 + step, 5, 10, top);
        Rect(-13, -12 - step, 4, 4, skin);
        Rect(11, -12 + step, 4, 4, skin);
        if (clerk)
        {
            Rect(-5, -24, 11, 15, "e0c084");
            Rect(-3, -16, 7, 4, "b79670");
        }
        Rect(-5, -28, 10, 6, skin);
        Rect(-12, -47, 25, 17, cap);
        Rect(-9, -51, 19, 9, cap);
        Rect(-8, -43, 19, 14, skin);
        Rect(-5, -30, 13, 4, skin);
        Rect(-12, -45, 7, 10, cap);
        Rect(-12, -47, 20, 6, cap);
        Rect(-2, -38, 2, 3, "4a3f3c");
        Rect(6, -39, 2, 3, "4a3f3c");
        Rect(2, -33, 5, 2, ToyArt.C(skin).Darkened(.18f).ToHtml());
        if (style % 4 == 1)
        {
            Rect(-13, -51, 26, 5, top);
            Rect(-9, -55, 18, 6, top);
            Rect(6, -48, 10, 3, ToyArt.C(top).Darkened(.12f).ToHtml());
        }

        void Rect(float dx, float dy, float w, float h, string color) =>
            Pixel(p.X + dx * scale, p.Y + dy * scale, w, h, color);
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
