using GACore;

namespace _17.VehicleRouting.Wpf;

public sealed class VehicleRoutingProblem : IGeneticProblem<Customer>
{
    private readonly int _vehicleCount;
    private readonly int _vehicleCapacity;

    public VehicleRoutingProblem(IReadOnlyList<Customer> customers, Depot depot, int vehicleCount, int vehicleCapacity)
    {
        Customers = customers;
        Depot = depot;
        _vehicleCount = vehicleCount;
        _vehicleCapacity = vehicleCapacity;
    }

    public IReadOnlyList<Customer> Customers { get; }

    public Depot Depot { get; }

    public Chromosome<Customer> CreateChromosome(Random random)
    {
        var route = Customers.ToArray();

        for (int i = route.Length - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (route[i], route[swapIndex]) = (route[swapIndex], route[i]);
        }

        return new Chromosome<Customer>(route);
    }

    public double CalculateFitness(Chromosome<Customer> chromosome)
    {
        var evaluation = Evaluate(chromosome.Genes);
        return evaluation.Distance + evaluation.CapacityPenalty * 120 + evaluation.UnservedPenalty * 250;
    }

    public RouteEvaluation Evaluate(IReadOnlyList<Customer> order)
    {
        var routes = SplitRoutes(order);
        var distance = routes.Sum(route => CalculateRouteDistance(route.Customers));
        var capacityPenalty = routes.Sum(route => Math.Max(0, route.Load - _vehicleCapacity));
        var unservedPenalty = Math.Max(0, routes.Count - _vehicleCount) * 10;

        return new RouteEvaluation(routes, distance, capacityPenalty, unservedPenalty);
    }

    public IReadOnlyList<VehicleRoute> SplitRoutes(IReadOnlyList<Customer> order)
    {
        var routes = new List<VehicleRoute>();
        var current = new List<Customer>();
        var load = 0;

        foreach (var customer in order)
        {
            if (current.Count > 0 && load + customer.Demand > _vehicleCapacity)
            {
                routes.Add(new VehicleRoute(routes.Count + 1, current.ToArray(), load));
                current.Clear();
                load = 0;
            }

            current.Add(customer);
            load += customer.Demand;
        }

        if (current.Count > 0)
        {
            routes.Add(new VehicleRoute(routes.Count + 1, current.ToArray(), load));
        }

        return routes;
    }

    private double CalculateRouteDistance(IReadOnlyList<Customer> route)
    {
        if (route.Count == 0) return 0;

        var total = Distance(Depot.X, Depot.Y, route[0].X, route[0].Y);

        for (int i = 0; i < route.Count - 1; i++)
        {
            total += Distance(route[i].X, route[i].Y, route[i + 1].X, route[i + 1].Y);
        }

        total += Distance(route[^1].X, route[^1].Y, Depot.X, Depot.Y);
        return total;
    }

    public static IReadOnlyList<Customer> CreateCustomers(int count, Random random)
    {
        var customers = new List<Customer>();

        for (int i = 0; i < count; i++)
        {
            customers.Add(new Customer(
                i + 1,
                random.NextDouble() * 320 - 160,
                random.NextDouble() * 260 - 130,
                random.Next(2, 9)));
        }

        return customers;
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed record Customer(int Id, double X, double Y, int Demand);

public sealed record Depot(double X, double Y);

public sealed record VehicleRoute(int VehicleId, IReadOnlyList<Customer> Customers, int Load);

public sealed record RouteEvaluation(IReadOnlyList<VehicleRoute> Routes, double Distance, int CapacityPenalty, int UnservedPenalty);
