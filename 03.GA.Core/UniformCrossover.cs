namespace GACore;

public sealed class UniformCrossover<TGene> : ICrossoverOperator<TGene>
{
    public Chromosome<TGene> Crossover(Chromosome<TGene> parentA, Chromosome<TGene> parentB, Random random)
    {
        if (parentA.Genes.Length != parentB.Genes.Length)
        {
            throw new ArgumentException("Parent chromosomes must have the same length.");
        }

        var childGenes = new TGene[parentA.Genes.Length];

        for (int i = 0; i < childGenes.Length; i++)
        {
            childGenes[i] = random.Next(2) == 0 ? parentA.Genes[i] : parentB.Genes[i];
        }

        return new Chromosome<TGene>(childGenes);
    }
}
