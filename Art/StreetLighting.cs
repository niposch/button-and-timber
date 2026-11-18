using Godot;

namespace ButtonAndTimber;

public partial class StreetView
{
    private static readonly Vector2[] FrontLamps = [new(.44f, 8.76f), new(10.15f, 8.76f)];
    private static readonly Vector2[] LaneLamps = [new(-1.17f, -.52f), new(5.79f, -.45f)];
    private static readonly Color Amber = ToyArt.C("ffc789");
    private Color sceneTint = Colors.White;
    private Color outsideTint = Colors.White;
    private float morningLight;
    private float nightLight;
    private ImageTexture? glow;

    internal void SnapAtmosphere() => UpdateAtmosphere(1);

    private void UpdateAtmosphere(float amount)
    {
        float dawn = State.Shift == Shift.Setup ? 1 :
            State.Shift == Shift.Open ? 1 - Mathf.SmoothStep(0, 105, (float)State.Minute + 32) : 0;
        float night = State.Shift == Shift.Closed ? 1 :
            State.Shift == Shift.Open ? .55f * Mathf.SmoothStep(390, ShopState.ShopHours, (float)State.Minute) : 0;
        morningLight = Mathf.Lerp(morningLight, dawn, amount);
        nightLight = Mathf.Lerp(nightLight, night, amount);
        outsideTint = Colors.White.Lerp(new Color(1.12f, .98f, .79f), morningLight)
            .Lerp(new Color(.41f, .48f, .71f), nightLight);
    }

    private Color Ambient(Color color) =>
        new(color.R * sceneTint.R, color.G * sceneTint.G, color.B * sceneTint.B, color.A);

    private Color RoomTint(Room zone)
    {
        if (zone == Room.Town)
            return outsideTint;
        bool lit = zone == Room.Shop ? State.Shift == Shift.Open : State.Orders.Count != 0;
        Color evening = lit ? new Color(.95f, .81f, .64f) : new Color(.70f, .64f, .67f);
        return Colors.White.Lerp(new Color(1.08f, .96f, .83f), morningLight).Lerp(evening, nightLight);
    }

    private void PoolOfLight(Vector2 center, float radius, float intensity, float heightRatio = .52f)
    {
        if (intensity < .008f)
            return;

        if (glow == null)
        {
            using Image image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float distance = new Vector2(x - 31.5f, y - 31.5f).Length() / 31.5f;
                float alpha = 1 - Mathf.SmoothStep(0, 1, distance);
                image.SetPixel(x, y, new Color(1, 1, 1, alpha * alpha));
            }
            glow = ImageTexture.CreateFromImage(image);
        }

        Vector2 extent = new(radius * scale, radius * heightRatio * scale);
        DrawTextureRect(glow, new Rect2(center - extent, extent * 2), false, new Color(Amber.R, Amber.G, Amber.B, intensity));
    }

    private void Lamp(Vector2 point)
    {
        Vector2 head = P(point.X, point.Y, 57);
        Box(point.X - .1f, point.Y - .1f, .2f, .2f, 0, 7, "4f5c55");
        Segment(P(point.X, point.Y, 4), head, "3c5050", 4);
        PoolOfLight(head, 34, nightLight * .62f, .94f);
        Pixel(head.X - 6 * scale, head.Y - 7 * scale, 12, 13, "384649");
        Vector2 pane = head - new Vector2(4, 5) * scale;
        DrawRect(new Rect2(pane, new Vector2(8, 8) * scale), Ambient(ToyArt.C("baba93"))
            .Lerp(ToyArt.C("ffe09b"), nightLight));
        Pixel(head.X - 5 * scale, head.Y - 8 * scale, 10, 2, "4a5e59");
    }

    private void FrontDoor()
    {
        float x = State.Door.X;
        const float y = 6;
        const float top = 59;
        bool open = State.Shift == Shift.Open;

        if (open)
        {
            Quad(P(x + .96f, y, 2), P(x + .99f, y - .76f, 2),
                P(x + .99f, y - .76f, 52), P(x + .96f, y, 52), "759e8e90");
            Segment(P(x + .99f, y - .76f, 3), P(x + .99f, y - .76f, 52), "487267", 3);
        }
        else
        {
            Quad(P(x + .05f, y, 1), P(x + .95f, y, 1), P(x + .95f, y, 53), P(x + .05f, y, 53), "658274d0");
            Quad(P(x + .08f, y + .004f, 15), P(x + .92f, y + .004f, 15),
                P(x + .92f, y + .004f, 50), P(x + .08f, y + .004f, 50), "adc7ab9e");
            Segment(P(x + .14f, y, 29), P(x + .81f, y, 42), "d7e0bb79", 2);
            Segment(P(x + .71f, y + .006f, 19), P(x + .79f, y + .006f, 19), "e4bf76", 3);
        }

        for (int side = 0; side < 2; side++)
        {
            Box(x + side - .05f, y - .045f, .11f, .14f, 0, top, "54776b");
            Segment(P(x + side + .015f, y + .096f, 2), P(x + side + .015f, y + .096f, top - 1), "93a88c", 2);
        }
        Box(x - .07f, y - .09f, 1.14f, .24f, top - 6, top + 1, "4b6b60");
        Segment(P(x - .055f, y + .16f, top - 1), P(x + 1.06f, y + .16f, top - 1), "ddbf86", 2);
        Vector2 indicator = P(x + .53f, y + .17f, top - 5);
        DrawCircle(indicator, 3.4f * scale, open ? ToyArt.C("d8cf8b") : Ambient(ToyArt.C("be8371")));
    }
}
