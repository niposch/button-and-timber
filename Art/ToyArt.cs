using Godot;

namespace ButtonAndTimber;

internal static class ToyArt
{
    public static Color C(string hex) => new(hex);

    public static void Draw(CanvasItem canvas, int toy, Vector2 feet, float scale)
    {
        Rect(-8, -2, 16, 3, "49393275");
        switch (toy)
        {
            case 0:
                Rect(-6, -19, 4, 5, "99543b");
                Rect(3, -19, 4, 5, "99543b");
                Rect(-7, -17, 14, 9, "b8764b");
                Rect(-5, -16, 9, 6, "d6955a");
                Rect(-5, -9, 10, 8, "ad6846");
                Rect(-8, -8, 4, 5, "c68456");
                Rect(4, -8, 4, 5, "985436");
                Rect(-7, -4, 5, 4, "d99b68");
                Rect(3, -4, 5, 4, "d99b68");
                Rect(-3, -12, 7, 3, "f2c785");
                Rect(-4, -15, 2, 2, "4b342c");
                Rect(3, -15, 2, 2, "4b342c");
                Rect(0, -12, 2, 2, "754538");
                Rect(-4, -8, 9, 2, "a74550");
                break;
            case 1:
                Rect(-10, -8, 18, 6, "90443b");
                Rect(-9, -10, 16, 4, "d87a4f");
                Rect(-7, -15, 7, 7, "327475");
                Rect(-9, -16, 11, 2, "275258");
                Rect(-5, -13, 3, 3, "f2c680");
                Rect(1, -11, 7, 5, "e4a54f");
                Rect(5, -15, 3, 6, "935348");
                Rect(-8, -4, 5, 4, "3c414a");
                Rect(3, -4, 5, 4, "3c414a");
                Rect(-6, -3, 2, 2, "aab6a5");
                Rect(5, -3, 2, 2, "aab6a5");
                break;
            case 2:
                Rect(-1, -18, 3, 19, "8d4b33");
                Rect(-5, -1, 10, 2, "b87845");
                Poly("a54b58", (-8, -17), (1, -14), (0, -8), (-8, -10));
                Poly("e9ad53", (1, -14), (8, -19), (8, -11), (0, -8));
                Poly("70a09a", (0, -8), (8, -11), (6, -5), (0, -4));
                Rect(-1, -12, 3, 3, "fff0b6");
                break;
            case 3:
                Poly("b85a4c", (-7, -1), (-4, -9), (-4, -16), (0, -22), (5, -16), (5, -9), (8, -1));
                Poly("ecd8a9", (-3, -7), (-3, -15), (0, -20), (4, -15), (4, -7));
                Rect(-1, -14, 4, 4, "4a6872");
                Rect(-2, -7, 5, 4, "9a4946");
                Rect(-2, -3, 5, 3, "eaa151");
                break;
            case 4:
                Rect(-5, -22, 3, 9, "b78e83");
                Rect(2, -21, 3, 8, "c2a095");
                Rect(-4, -21, 1, 6, "e7b9a3");
                Rect(-6, -15, 12, 7, "ddd2b6");
                Rect(-5, -9, 10, 8, "c8bda0");
                Rect(-7, -3, 6, 3, "efe2bd");
                Rect(2, -3, 5, 3, "efe2bd");
                Rect(-4, -13, 2, 2, "4e453b");
                Rect(3, -13, 2, 2, "4e453b");
                Rect(0, -11, 2, 2, "b77f85");
                Rect(-4, -8, 8, 2, "769280");
                break;
            case 5:
                Rect(-9, -8, 18, 8, "7f5041");
                Rect(-9, -13, 18, 9, "e5b975");
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 6; x++)
                    if ((x + y) % 2 == 0)
                        Rect(-9 + x * 3, -12 + y * 4, 3, 4, "9d6250");
                Rect(-5, -15, 4, 4, "557f70");
                Rect(3, -11, 4, 4, "c34e4f");
                break;
            case 6:
                Poly("dab679", (-11, -6), (10, -8), (5, -1), (-6, -1));
                Poly("a76548", (-11, -6), (10, -8), (8, -5), (-9, -3));
                Rect(0, -23, 2, 17, "72503a");
                Poly("efd9a4", (-1, -20), (-1, -9), (-9, -8));
                Poly("61878e", (2, -19), (9, -10), (2, -9));
                Rect(2, -23, 5, 2, "be6d56");
                break;
            case 7:
                Poly("447b78", (-10, -9), (-6, -10), (-2, -16), (6, -17), (8, -12), (8, -5), (2, -1), (-5, -3));
                Poly("a3ab76", (-8, -9), (3, -12), (5, -7), (-3, -5));
                Rect(4, -15, 2, 2, "293d3b");
                Rect(8, -12, 4, 3, "d99753");
                Rect(-5, -16, 2, 7, "dcbb77");
                Rect(-7, -17, 6, 2, "f4da8c");
                break;
            case 8:
                Rect(-9, -10, 15, 6, "d59f76");
                Rect(2, -16, 6, 9, "b96e60");
                Rect(4, -18, 3, 4, "ac7864");
                Rect(6, -13, 4, 4, "edbb89");
                Rect(6, -15, 2, 2, "53433f");
                Rect(-7, -6, 3, 5, "9a6454");
                Rect(3, -6, 3, 5, "936654");
                Rect(-9, -13, 3, 6, "8a5b53");
                Rect(-5, -11, 5, 5, "7f9b8d");
                Rect(-9, -2, 17, 2, "72483e");
                break;
            default:
                Rect(-10, -17, 20, 17, "884735");
                Rect(-8, -14, 16, 11, "43566f");
                Rect(-9, -14, 4, 12, "bd5c65");
                Rect(5, -14, 4, 12, "a95159");
                Rect(-10, -17, 20, 3, "eab16c");
                Rect(-3, -12, 4, 4, "f3d78e");
                Rect(-10, -3, 20, 3, "c88058");
                break;
        }

        void Rect(float x, float y, float width, float height, string color) =>
            canvas.DrawRect(new Rect2(feet + new Vector2(x, y) * scale, new Vector2(width, height) * scale), C(color));

        void Poly(string color, params (int X, int Y)[] points) =>
            canvas.DrawColoredPolygon(points.Select(p => feet + new Vector2(p.X, p.Y) * scale).ToArray(), C(color));
    }
}

public partial class ToySwatch : Control
{
    internal int Toy { get; set; }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), ToyArt.C("e7d6ad"));
        DrawRect(new Rect2(1, 1, Size.X - 2, Size.Y - 2), ToyArt.C("c4ab7f"), false);
        if (Toy >= 0)
            ToyArt.Draw(this, Toy, Size * new Vector2(.5f, .8f), Size.Y / 30);
    }
}
