using System.Text.Json.Serialization;

namespace ButtonAndTimber;

internal sealed class Pedestrian
{
    public int Appearance { get; set; }
    public Cell Position { get; set; }
    public int Direction { get; set; }
    public double Pace { get; set; }
    public double Travel { get; set; }
}

internal sealed partial class Customer
{
    public int StreetDirection { get; set; }
    public int StreetLane { get; set; }
    public double StreetPace { get; set; }
}

internal sealed partial class ShopState
{
    private const int StreetWest = -12;
    private const int StreetEast = 22;
    public List<Pedestrian> Pedestrians { get; set; } = [];
    public long StreetMinutes { get; set; }

    [JsonIgnore] public double StreetClock => StreetMinutes + ShopRemainder;
    [JsonIgnore] public int ClockTime => Shift switch
    {
        Shift.Setup => 8 * 60, Shift.Closed => 9 * 60 + ShopHours, _ => 9 * 60 + (int)Minute
    };
    [JsonIgnore] public Cell Door => new(Entry.X, 5);

    private void InitializeTraffic()
    {
        if (Pedestrians.Count > 0 || Customers.Any(person => person.StreetDirection != 0))
            return;
        int[] starts = [-9, -5, -2, 2, 5, 9, 12, 17, 21, -6, 0, 7, 13];
        for (int i = 0; i < starts.Length; i++)
        {
            Pedestrians.Add(new Pedestrian
            {
                Appearance = (i * 7 + 1) % 48,
                Position = new Cell(starts[i], i < 9 ? 7 + i % 2 : -3 + i % 2),
                Direction = i % 2 == 0 ? 1 : -1,
                Pace = .14 + i % 4 * .019,
                Travel = (i % 3) * .23
            });
        }
    }

    private void TickStreet()
    {
        StreetMinutes++;
        foreach (Pedestrian person in Pedestrians)
        {
            person.Travel += person.Pace;
            if (person.Travel < 1)
                continue;
            int x = person.Position.X + person.Direction;
            person.Position = person.Position with { X = x > StreetEast ? StreetWest : x < StreetWest ? StreetEast : x };
            person.Travel -= 1;
        }
    }

    private Pedestrian? FindPassingShopper() => Pedestrians
        .Where(person => person.Position.Y >= 7 && (Door.X - person.Position.X) * person.Direction > 0 &&
            Math.Abs(Door.X - person.Position.X) <= 7)
        .OrderBy(person => Math.Abs(Door.X - person.Position.X) - person.Travel)
        .FirstOrDefault();

    private void LeadInside(Customer customer, Pedestrian pedestrian)
    {
        Pedestrians.Remove(pedestrian);
        customer.Position = pedestrian.Position;
        customer.Travel = pedestrian.Travel;
        customer.StreetDirection = pedestrian.Direction;
        customer.StreetLane = pedestrian.Position.Y;
        customer.StreetPace = pedestrian.Pace;
        customer.Phase = VisitPhase.Approach;
        for (int x = pedestrian.Position.X; x != Door.X;)
        {
            x += pedestrian.Direction;
            customer.Route.Add(new Cell(x, pedestrian.Position.Y));
        }
        for (int y = pedestrian.Position.Y - 1; y >= Door.Y; y--)
            customer.Route.Add(new Cell(Door.X, y));
        customer.Target = Door;
    }

    private void LeadOutside(Customer customer)
    {
        customer.Phase = VisitPhase.Depart;
        int lane = Math.Clamp(customer.StreetLane, 7, 8);
        for (int y = Entry.Y + 1; y <= lane; y++)
            customer.Route.Add(new Cell(Entry.X, y));
        customer.Target = new Cell(Entry.X, lane);
    }

    private void RejoinSidewalk(Customer customer)
    {
        Pedestrians.Add(new Pedestrian
        {
            Appearance = customer.Appearance,
            Position = new Cell(Entry.X, Math.Clamp(customer.StreetLane, 7, 8)),
            Direction = customer.StreetDirection != 0 ? customer.StreetDirection : (customer.Appearance % 2 == 0 ? 1 : -1),
            Pace = customer.StreetPace > 0 ? customer.StreetPace : .17
        });
    }
}
