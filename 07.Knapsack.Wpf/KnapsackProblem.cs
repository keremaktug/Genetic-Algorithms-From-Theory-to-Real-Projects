using GACore;

namespace _07.Knapsack.Wpf;

public sealed class KnapsackProblem : IGeneticProblem<int>
{
    public const int Capacity = 35;

    public static readonly KnapsackItem[] Items =
    [
        new("Laptop", 9, 150),
        new("Headphones", 2, 35),
        new("Camera", 7, 95),
        new("Jacket", 5, 60),
        new("Water", 3, 40),
        new("Book", 4, 45),
        new("Tent", 10, 120),
        new("Food", 6, 80),
        new("Flashlight", 1, 30),
        new("First aid", 2, 50),
        new("Shoes", 6, 70),
        new("Power bank", 3, 55)
    ];

    public Chromosome<int> CreateChromosome(Random random)
    {
        var genes = new int[Items.Length];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = random.Next(2);
        }

        return new Chromosome<int>(genes);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var weight = TotalWeight(chromosome.Genes);

        if (weight > Capacity)
        {
            return 0;
        }

        return TotalValue(chromosome.Genes);
    }

    public static int TotalWeight(IReadOnlyList<int> genes)
    {
        var weight = 0;

        for (int i = 0; i < genes.Count; i++)
        {
            if (genes[i] == 1)
            {
                weight += Items[i].Weight;
            }
        }

        return weight;
    }

    public static int TotalValue(IReadOnlyList<int> genes)
    {
        var value = 0;

        for (int i = 0; i < genes.Count; i++)
        {
            if (genes[i] == 1)
            {
                value += Items[i].Value;
            }
        }

        return value;
    }
}

public sealed record KnapsackItem(string Name, int Weight, int Value);
