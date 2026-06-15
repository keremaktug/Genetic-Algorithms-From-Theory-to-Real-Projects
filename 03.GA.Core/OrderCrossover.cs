namespace GACore;

public sealed class OrderCrossover<TGene> : ICrossoverOperator<TGene>
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

        var child = new TGene[length];
        var used = new HashSet<TGene>();

        for (int i = start; i <= end; i++)
        {
            child[i] = parentA.Genes[i];
            used.Add(parentA.Genes[i]);
        }

        var childIndex = (end + 1) % length;

        for (int offset = 0; offset < length; offset++)
        {
            var parentIndex = (end + 1 + offset) % length;
            var gene = parentB.Genes[parentIndex];

            if (used.Contains(gene)) continue;

            child[childIndex] = gene;
            used.Add(gene);
            childIndex = (childIndex + 1) % length;
        }

        return new Chromosome<TGene>(child);
    }
}
