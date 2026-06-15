using GACore;

namespace _08.TSP.Wpf;

public sealed class TspProblem : IGeneticProblem<City>
{
    private readonly IReadOnlyList<City> _cities;

    public TspProblem(IReadOnlyList<City> cities)
    {
        _cities = cities;
    }

    public Chromosome<City> CreateChromosome(Random random)
    {
        var route = _cities.ToArray();

        for (int i = route.Length - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (route[i], route[swapIndex]) = (route[swapIndex], route[i]);
        }

        return new Chromosome<City>(route);
    }

    public double CalculateFitness(Chromosome<City> chromosome)
    {
        var totalDistance = 0.0;

        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            var current = chromosome.Genes[i];
            var next = chromosome.Genes[(i + 1) % chromosome.Genes.Length];
            totalDistance += Distance(current, next);
        }

        return totalDistance;
    }

    public static IReadOnlyList<City> CreateCircularCities(int count)
    {
        var cities = new List<City>();
        const double radius = 140;

        for (int i = 0; i < count; i++)
        {
            var angle = Math.PI * 2 * i / count;
            cities.Add(new City(i + 1, Math.Cos(angle) * radius, Math.Sin(angle) * radius));
        }

        return cities;
    }

    public static IReadOnlyList<City> CreateRandomCities(int count, Random random)
    {
        var cities = new List<City>();

        for (int i = 0; i < count; i++)
        {
            cities.Add(new City(i + 1, random.NextDouble() * 300 - 150, random.NextDouble() * 300 - 150));
        }

        return cities;
    }

    private static double Distance(City left, City right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed record City(int Id, double X, double Y);
