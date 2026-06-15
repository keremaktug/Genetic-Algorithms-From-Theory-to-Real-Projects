namespace GACore;

public sealed class MultisetOrderCrossover<TGene> : ICrossoverOperator<TGene>
    where TGene : notnull
{
    public Chromosome<TGene> Crossover(Chromosome<TGene> parentA, Chromosome<TGene> parentB, Random random)
    {
        if (parentA.Genes.Length != parentB.Genes.Length)
        {
            throw new ArgumentException("Parent chromosomes must have the same length.");
        }

        var length = parentA.Genes.Length;
        if (length < 2)
        {
            throw new ArgumentException("Chromosomes must contain at least two genes.");
        }

        var start = random.Next(length);
        var end = random.Next(length);

        if (start > end)
        {
            (start, end) = (end, start);
        }

        var remainingCounts = CountGenes(parentA.Genes);
        var child = new TGene?[length];

        for (int i = start; i <= end; i++)
        {
            child[i] = parentA.Genes[i];
            remainingCounts[parentA.Genes[i]]--;
        }

        var childIndex = (end + 1) % length;

        for (int offset = 0; offset < length; offset++)
        {
            var gene = parentB.Genes[(end + 1 + offset) % length];

            if (remainingCounts[gene] <= 0) continue;

            while (child[childIndex] is not null)
            {
                childIndex = (childIndex + 1) % length;
            }

            child[childIndex] = gene;
            remainingCounts[gene]--;
        }

        return new Chromosome<TGene>(child.Select(gene => gene!).ToArray());
    }

    private static Dictionary<TGene, int> CountGenes(IEnumerable<TGene> genes)
    {
        var counts = new Dictionary<TGene, int>();

        foreach (var gene in genes)
        {
            counts.TryGetValue(gene, out var count);
            counts[gene] = count + 1;
        }

        return counts;
    }
}
