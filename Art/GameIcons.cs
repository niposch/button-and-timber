using Godot;

namespace ButtonAndTimber;

internal static class GameIcons
{
    private static readonly Color[] FamilyColors =
        [ToyArt.C("99bfc0"), ToyArt.C("e1a68d"), ToyArt.C("edd28d"), ToyArt.C("aec69e")];

    public static Color FamilyColor(ToyFamily family) => FamilyColors[(int)family];

    public static void Family(CanvasItem canvas, ToyFamily family, Vector2 center, float radius)
    {
        canvas.DrawCircle(center, radius + 1, ToyArt.C("405950"), antialiased: true);
        canvas.DrawCircle(center, radius, FamilyColor(family), antialiased: true);
        ToyArt.Draw(canvas, Catalog.FamilyIcons[(int)family], center + new Vector2(0, radius * .55f), radius / 14f);
    }

    public static void Arrow(CanvasItem canvas, Tool direction, Vector2 center, float size, Color color)
    {
        Vector2[] arrow = [new(-.62f, .01f), new(0, -.61f), new(.62f, .01f),
            new(.25f, .01f), new(.25f, .55f), new(-.25f, .55f), new(-.25f, .01f)];
        for (int i = 0; i < arrow.Length; i++)
            arrow[i] = center + arrow[i].Rotated((int)direction * Mathf.Pi / 2) * size;
        canvas.DrawColoredPolygon(arrow, color);
    }
}

public partial class FamilyIcon : Control
{
    internal ToyFamily Family { get; set; }

    public override void _Draw() => GameIcons.Family(this, Family, Size / 2, Math.Min(Size.X, Size.Y) / 2 - 1);
}

public partial class DirectionIcon : Control
{
    internal Tool Direction { get; set; }

    public override void _Draw() => GameIcons.Arrow(this, Direction, Size / 2, Math.Min(Size.X, Size.Y) * .64f,
        ToyArt.C("efd28c"));
}

public partial class LockIcon : Control
{
    public override void _Draw()
    {
        Color ink = ToyArt.C("425750");
        DrawArc(new Vector2(5.5f, 5.5f), 3.3f, Mathf.Pi, Mathf.Tau, 9, ink, 2, true);
        DrawRect(new Rect2(0, 5, 11, 8), ink);
        DrawRect(new Rect2(5, 7.5f, 1.5f, 3), ToyArt.C("d8cba6"));
    }
}

public partial class BlueprintBoard : Control
{
    internal readonly List<(Vector2 From, Vector2 To, bool Ready)> Links = [];

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), ToyArt.C("203f49"));
        for (int x = 11; x < Size.X; x += 12)
            DrawLine(new Vector2(x, 1), new Vector2(x, Size.Y - 1), ToyArt.C("8fb7af26"));
        for (int y = 8; y < Size.Y; y += 12)
            DrawLine(new Vector2(1, y), new Vector2(Size.X - 1, y), ToyArt.C("8fb7af26"));
        DrawRect(new Rect2(1, 1, Size.X - 2, Size.Y - 2), ToyArt.C("759e9a"), false);
        foreach (var link in Links)
        {
            Color color = ToyArt.C(link.Ready ? "e6d598" : "93b9b7");
            float elbow = (link.From.X + link.To.X) / 2;
            Vector2[] points = [link.From, new(elbow, link.From.Y), new(elbow, link.To.Y), link.To];
            DrawPolyline(points, color, 2f, true);
            GameIcons.Arrow(this, Tool.Mallet, link.To - new Vector2(4, 0), 7, color);
        }
    }
}
