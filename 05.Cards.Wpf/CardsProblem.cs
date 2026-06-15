using GACore;

namespace _05.Cards.Wpf;

public sealed class CardsProblem : IGeneticProblem<int>
{
    private static readonly int[] Cards = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    public Chromosome<int> CreateChromosome(Random random)
    {
        var genes = Cards.ToArray();

        for (int i = genes.Length - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (genes[i], genes[swapIndex]) = (genes[swapIndex], genes[i]);
        }

        return new Chromosome<int>(genes);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var sumError = Math.Abs(36 - FirstGroupSum(chromosome.Genes));
        var productError = Math.Abs(360 - SecondGroupProduct(chromosome.Genes));
        return sumError + productError;
    }

    public static int FirstGroupSum(IReadOnlyList<int> cards)
    {
        return cards.Take(5).Sum();
    }

    public static int SecondGroupProduct(IReadOnlyList<int> cards)
    {
        var product = 1;

        for (int i = 5; i < cards.Count; i++)
        {
            product *= cards[i];
        }

        return product;
    }
}
