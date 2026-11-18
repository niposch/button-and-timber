using System.Text.Json;
using Godot;

namespace ButtonAndTimber;

public partial class ShopGame
{
    private async void Capture(string destination)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string prefix = Path.GetFullPath(destination);
        for (int index = 0; index < 4; index++)
        {
            if (index < 3)
                OpenRoom((Room)index);
            else
            {
                OpenRoom(Room.Shop);
                state.Open();
                for (int tick = 0; tick < 66; tick++)
                    state.AdvanceShop(1);
                UpdateReadouts();
            }

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Error result = GetViewport().GetTexture().GetImage().SavePng($"{prefix}-{index}.png");
            if (result != Error.Ok)
            {
                GD.PushError($"Screenshot failed: {result}");
                GetTree().Quit(1);
                return;
            }
        }
        GetTree().Quit();
    }

    private void SmokeTest()
    {
        try
        {
            Check(stage.Room == Room.Shop && action.Text == "Open shop", "starting scene");
            PickDisplay(state.Displays[0]);
            int originalPrice = state.Prices[0];
            Price(0, 1);
            Check(state.Prices[0] == originalPrice + 1, "retail price");
            Check(state.Restock(state.Displays[0]) == 2, "reserve transfer");
            moving = true;
            PlaceDisplay(new Vector2I(2, 2));
            Check(!moving && state.Displays[0].X == 2, "fixture relocation");
            Check(!state.Move(state.Displays[0], 4, 1), "occupied tile");

            OpenRoom(Room.Workshop);
            Check(state.Queue(1, 7), "batch order");
            Assist();
            Check(state.Orders[0].Work > 0, "manual bench work");
            state.AdvanceBench(100);
            Check(state.Orders.Count == 0 && state.Practice[1] == 7 && state.Stock[1] > 6, "batch output");
            Check(state.Research(3) && state.Unlocked[3], "pattern unlock");
            Check(state.Deliver() && !state.Deliver(), "seasonal commission");
            state.Cash = 400;
            Check(state.Expand() && state.Width == 9 && state.BuyTable(), "expansion");
            state.Assign(state.Displays[^1], 1);
            Check(state.Displays[^1].Count > 0, "new fixture merchandising");

            OpenRoom(Room.Shop);
            AdvanceDay();
            for (int minute = 0; minute < 80; minute++)
                state.AdvanceShop(1);
            Check(state.Shift == Shift.Open && state.Today.Visitors > 0 && state.Today.Sold > 0, "customer simulation");
            Check(!state.Move(state.Displays[0], 3, 2), "floor editing during trading");
            PickCustomer(state.Customers[0]);
            Check(panelTitle.Text == state.Customers[0].Name, "shopper inspection");
            string save = JsonSerializer.Serialize(state);
            ShopState resumed = JsonSerializer.Deserialize<ShopState>(save)!;
            Check(resumed.Cash == state.Cash && resumed.Seed == state.Seed &&
                resumed.Customers.Count == state.Customers.Count && resumed.Displays.Count == 4, "save round trip");
            for (int minute = 0; minute < ShopState.ShopHours; minute++)
            {
                state.AdvanceShop(1);
                resumed.AdvanceShop(1);
            }
            Check(state.Shift == Shift.Closed && state.Today.Revenue == resumed.Today.Revenue &&
                state.Cash == resumed.Cash && state.Seed == resumed.Seed, "deterministic resume and close");
            AdvanceDay();
            Check(state.Day == 2 && state.Yesterday.Visitors > 0 && state.Today.Visitors == 0, "daily report rollover");

            PlayCampaign();
            GD.Print("Smoke test passed: shop, workshop, research, town, save, and 24-day campaign.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void PlayCampaign()
    {
        ShopState campaign = new();
        for (int day = 1; day <= ShopState.CampaignDays; day++)
        {
            if (campaign.Cash >= 330 && !campaign.Expanded)
                campaign.Expand();

            foreach (Display display in campaign.Displays)
                if (campaign.Stock[display.Toy] < 12)
                    campaign.Queue(display.Toy, 10);

            campaign.AdvanceBench(500);
            campaign.Deliver();
            campaign.RestockAll();
            campaign.Open();
            for (int minute = 0; minute < ShopState.ShopHours; minute++)
            {
                if (minute % 50 == 0)
                    campaign.RestockAll();
                campaign.AdvanceShop(1);
            }
            if (day < ShopState.CampaignDays)
                campaign.Tomorrow();
        }

        Check(campaign.CampaignResult == 1, $"deed target is attainable (finished with ${campaign.Cash})");
        campaign.Tomorrow();
        Check(campaign.Year == 2 && campaign.Date == 1 && !campaign.Commissions[0], "free play rollover");
        ShopState missed = new() { Day = ShopState.CampaignDays, Cash = 1 };
        missed.Open();
        missed.Close();
        Check(missed.CampaignResult == -1, "missed deed outcome");
    }

    private static void Check(bool condition, string feature)
    {
        if (!condition)
            throw new InvalidOperationException($"Smoke test failed: {feature}");
    }
}
