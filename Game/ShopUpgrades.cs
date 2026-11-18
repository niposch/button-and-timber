using System.Text.Json.Serialization;

namespace ButtonAndTimber;

internal sealed record Decoration(Fixture Fixture, string Name, int BuyPrice, int Materials, int Season,
    ToyFamily? Family, Station Station);

internal static class Decorations
{
    public static readonly Decoration[] All =
    [
        new(Fixture.Plant, "Pot Plant", 18, 7, -1, null, Station.Finishing),
        new(Fixture.FlowerCrate, "Flower Crate", 42, 16, 0, ToyFamily.Plush, Station.Bench),
        new(Fixture.PennantStand, "Pennant Stand", 46, 18, 1, ToyFamily.Motion, Station.Sewing),
        new(Fixture.PumpkinCrate, "Pumpkin Crate", 44, 17, 2, ToyFamily.Games, Station.Bench),
        new(Fixture.SnowLantern, "Snow Lantern", 51, 21, 3, ToyFamily.Trains, Station.Assembly)
    ];

    public static Decoration Get(Fixture fixture) => All[(int)fixture - (int)Fixture.Plant];
}

internal sealed partial class ShopState
{
    private static readonly int[] StorageSizes = [44, 96, 192, 360];
    private static readonly int[] StorageRent = [0, 7, 17, 34];
    private static readonly int[] CartSizes = [12, 36, 90];
    private static readonly int[] CartPrices = [105, 235];
    public int StorageTier { get; set; }
    public int CartTier { get; set; }

    [JsonIgnore] public int StorageUsed => Stock.Sum();
    [JsonIgnore] public int StorageCapacity => StorageSizes[StorageTier];
    [JsonIgnore] public int DailyRent => Upkeep + StorageRent[StorageTier];
    [JsonIgnore] public bool CanRentMore => StorageTier < StorageSizes.Length - 1;
    [JsonIgnore] public int NextStorageCapacity => StorageSizes[Math.Min(StorageTier + 1, StorageSizes.Length - 1)];
    [JsonIgnore] public int NextDailyRent => Upkeep + StorageRent[Math.Min(StorageTier + 1, StorageRent.Length - 1)];
    [JsonIgnore] public int CartCapacity => CartSizes[CartTier];
    [JsonIgnore] public bool CanUpgradeCart => CartTier < CartPrices.Length;
    [JsonIgnore] public int CartUpgradePrice => CanUpgradeCart ? CartPrices[CartTier] : 0;
    [JsonIgnore] public int NextCartCapacity => CartSizes[Math.Min(CartTier + 1, CartSizes.Length - 1)];

    [JsonIgnore] public int Appeal => (PlacedDecorations().Sum(item => item.Season == Season ? 3 : 1) *
        (Style == Philosophy.Showman ? 3 : 2) + 1) / 2 + (Hired[(int)StaffRole.Dresser] ? 2 : 0);

    private IEnumerable<Decoration> PlacedDecorations() => Displays.Where(item => item.Style >= Fixture.Plant)
        .Select(item => item.Style).Distinct().Select(Decorations.Get);

    public int DecorDemand(ToyFamily family) => PlacedDecorations()
        .Where(item => item.Family == family && item.Season == Season).Sum(_ => 2);

    public int DispatchSpace(int toy) => Math.Max(0, StorageCapacity - StorageUsed) +
        Displays.Where(display => display.Toy == toy).Sum(display => display.Capacity - display.Count);

    public bool DiscardStock(int toy, int units)
    {
        if (toy < 0 || toy >= Stock.Length || units <= 0 || units > Stock[toy])
            return false;

        Stock[toy] -= units;
        return true;
    }

    public bool RentMoreStorage()
    {
        if (!CanRentMore || (StorageTier >= 2 && Rank < 3))
            return false;
        StorageTier++;
        Note($"Stockroom expanded · {StorageCapacity} spaces · ${DailyRent}/day");
        return true;
    }

    public bool UpgradeCart()
    {
        if (!CanUpgradeCart || Cash < CartUpgradePrice)
            return false;
        Spend(CartUpgradePrice);
        CartTier++;
        Note($"Cart upgraded · {CartCapacity} per trip");
        return true;
    }

    public bool MakeDecoration(Fixture kind)
    {
        return QueueProject(new Order { Toy = -1, Decoration = kind }, 1, Decorations.Get(kind).Materials);
    }
}
