namespace GACore;

public sealed class OnePointCrossover<TGene> : ICrossoverOperator<TGene>
{
    public Chromosome<TGene> Crossover(Chromosome<TGene> parentA, Chromosome<TGene> parentB, Random random)
    {
        EnsureEqualLength(parentA, parentB);

        var length = parentA.Genes.Length;
        var crossoverPoint = random.Next(1, length);
        var childGenes = new TGene[length];

        for (int i = 0; i < length; i++)
        {
            childGenes[i] = i < crossoverPoint ? parentA.Genes[i] : parentB.Genes[i];
        }

        return new Chromosome<TGene>(childGenes);
    }

    private static void EnsureEqualLength(Chromosome<TGene> parentA, Chromosome<TGene> parentB)
    {
        if (parentA.Genes.Length != parentB.Genes.Length)
        {
            throw new ArgumentException("Parent chromosomes must have the same length.");
        }

        if (parentA.Genes.Length < 2)
        {
            throw new ArgumentException("Chromosomes must contain at least two genes.");
        }
    }
}
