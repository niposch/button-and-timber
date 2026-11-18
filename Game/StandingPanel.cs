using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private int townTab;

    private void TownTabs()
    {
        HBoxContainer row = Row();
        string[] names = ["Orders", "Standing"];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            Button tab = InButton(row, names[i], () =>
            {
                townTab = index;
                Rebuild();
            });
            SelectedStyle(tab, townTab == i, false);
        }
    }

    private void StandingPanel()
    {
        Line(Progress.Ranks[state.Rank], 21, Ink, true);
        ProgressBar renown = Meter(9);
        renown.Value = state.RankProgressFraction * 100;
        content.AddChild(renown);
        Paragraph(state.TopRank
            ? $"Renown {state.Renown:N0} · the highest rank on Alder Lane."
            : $"Renown {state.Renown:N0} of {Progress.RankRenown[state.Rank + 1]:N0} for {Progress.Ranks[state.Rank + 1]}. " +
              $"Next: {Progress.RankUnlocks[state.Rank + 1]}.");
        Paragraph("Earn renown from sales, regulars, orders, new patterns and levelling up toys.");
        Divider();

        Section("Shop style");
        if (state.Rank < 2)
            Paragraph($"Reach {Progress.Ranks[2]} to choose how your shop trades.");
        else
        {
            HBoxContainer styles = Row();
            foreach (Philosophy style in new[] { Philosophy.Heirloom, Philosophy.Volume, Philosophy.Showman })
            {
                Philosophy choice = style;
                Button pick = InButton(styles, Progress.StyleNames[(int)style].Split(' ')[0], () =>
                {
                    if (state.ChooseStyle(choice))
                        Changed();
                });
                pick.TooltipText = Progress.StyleBlurbs[(int)style];
                SelectedStyle(pick, state.Style == style, false);
                pick.Disabled = state.Style != style && !state.CanChooseStyle(style);
            }
            Paragraph(Progress.StyleBlurbs[(int)state.Style]);
            if (state.Style != Philosophy.None)
                Paragraph($"Changing style costs ${state.StyleCost} while the shop is closed.");
        }
        Divider();

        Section("Staff");
        for (int i = 0; i < Progress.Staff.Length; i++)
        {
            int role = i;
            StaffInfo info = Progress.Staff[i];
            HBoxContainer row = Row();
            In(row, $"{info.Name} · ${info.Wage}/day", 14, Ink, true);
            Button action = InButton(row, "", () =>
            {
                if (state.Hired[role] ? state.Dismiss(role) : state.Hire(role))
                    Changed();
            }, 116);
            action.Text = state.Hired[i] ? "Let go" : state.Rank < info.Rank ? Progress.Ranks[info.Rank] : $"Hire · ${info.Fee}";
            action.Disabled = !state.Hired[i] && !state.CanHire(i);
            Paragraph(info.Blurb);
        }
        if (state.Wages > 0)
            Paragraph($"Wages are paid at closing: ${state.Wages}/day.");
        Divider();

        Section($"Regulars  {state.Regulars.Count}/{state.RegularCap}");
        if (state.Regulars.Count == 0)
            Paragraph("Happy shoppers sometimes come back. Fair prices and a well-stocked shelf make regulars.");
        foreach (Regular regular in state.Regulars.OrderByDescending(regular => regular.Affinity))
        {
            HBoxContainer row = Row();
            FamilyIcon icon = new() { Family = regular.Family, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(26, 26) };
            row.AddChild(icon);
            In(row, regular.Name, 14, Ink, true);
            In(row, $"{regular.BondName}  {new string('♥', regular.Bond + 1)}", 13, Faded);
        }
    }

    private void Briefing()
    {
        if (state.Day <= 2 && state.Rank == 0 && state.Today.Sold == 0)
        {
            bool register = state.Register != null;
            bool shelf = state.Displays.Any(display => display.Capacity > 0);
            bool stocked = state.Displays.Any(display => display.Count > 0);
            Line("Opening your first day", 17, Ink, true);
            Paragraph($"{(register ? "✓" : "○")} Place a register\n" +
                $"{(shelf ? "✓" : "○")} Place a shelf\n" +
                $"{(stocked ? "✓" : "○")} Stock it with a toy\n" +
                "○ Open the shop and ring up your first sale\n" +
                "Make more toys in the Workshop. Match what shoppers ask for to what's on your shelves.");
            Divider();
        }
        if (state.Event != DayEvent.None)
        {
            Line(Progress.EventName(state.Event), 17, Ink, true);
            Paragraph(Progress.EventBlurb(state.Event));
        }
        if (state.Fair)
            Line($"{Catalog.Fairs[state.Season]} today", 15, Ink, true);
        ToyFamily hot = Catalog.Families.OrderByDescending(state.Forecast).First();
        Line($"Hot today: {hot}", 14, Ink);
        string[] expected = state.Regulars.Where(regular => regular.LastVisitDay != state.Day).Take(3)
            .Select(regular => regular.Name).ToArray();
        if (expected.Length > 0)
            Paragraph($"Regulars may drop in: {string.Join(", ", expected)}.");
        Divider();
    }
}

public partial class ShopGame
{
    private Button toolPlans = null!;
    private bool showWorkshopTools;

    private Label Section(string text)
    {
        content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore });
        Label label = TextLabel(text.ToUpperInvariant(), 11, Faded);
        content.AddChild(label);
        return label;
    }

    private void RequestBoard()
    {
        Section("Requests");
        if (state.Board.Count == 0)
            Paragraph("New requests arrive each morning. Regulars ask for their favourites and pay a premium.");
        foreach (Request request in state.Board.ToArray())
        {
            Request ask = request;
            var (card, caption) = ToyRow(ask.Toy);
            caption.Text = $"{ask.Count} × {Catalog.Toys[ask.Toy].Name}";
            AddFamilyBadge(card, Catalog.Toys[ask.Toy].Family);
            HBoxContainer row = Row();
            Label who = In(row, "", 13, Faded, true);
            Button deliver = InButton(row, $"Deliver · ${ask.Pay}", () =>
            {
                if (state.Fill(ask))
                    Changed();
            }, 127);
            Sync(() =>
            {
                int left = state.DaysLeft(ask);
                string due = left <= 0 ? "due today" : left == 1 ? "due tomorrow" : $"{left} days left";
                who.Text = $"{ask.Giver} · {due} · {Math.Min(state.Stock[ask.Toy], ask.Count)}/{ask.Count}";
                who.AddThemeColorOverride("font_color", left <= 0 ? Clay : Faded);
                deliver.Disabled = !state.CanFill(ask);
            });
        }
    }

    private void ToolsPanel()
    {
        Header("Tools", "");
        AddButton("‹  Blueprints", () =>
        {
            showWorkshopTools = false;
            Rebuild();
        });
        for (int i = 0; i < Upgrades.All.Length; i++)
        {
            int index = i;
            WorkshopUpgrade info = Upgrades.All[i];
            Section(info.Name);
            Paragraph(info.Blurb);
            HBoxContainer row = Row();
            string pips = new string('●', state.UpgradeLevel(i)) + new string('○', info.Cost.Length - state.UpgradeLevel(i));
            In(row, pips, 16, Ink, true);
            Button buy = InButton(row, "", () =>
            {
                if (state.BuyUpgrade(index))
                    Changed();
            }, 150);
            buy.Text = state.UpgradeMaxed(i) ? "Fully upgraded" :
                state.Rank < state.UpgradeRank(i) ? Progress.Ranks[state.UpgradeRank(i)] : $"Upgrade · ${state.UpgradeCost(i)}";
            buy.Disabled = !state.CanUpgrade(i);
        }
    }
}
