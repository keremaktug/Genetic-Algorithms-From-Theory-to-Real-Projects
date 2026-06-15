namespace GACore;

public sealed class Chromosome<TGene>
{
    public Chromosome(IReadOnlyList<TGene> genes)
    {
        Genes = genes.ToArray();
    }

    public TGene[] Genes { get; }

    public double Fitness { get; set; }

    public Chromosome<TGene> Clone()
    {
        return new Chromosome<TGene>(Genes)
        {
            Fitness = Fitness
        };
    }

    public override string ToString()
    {
        return $"{string.Join(", ", Genes)} | Fitness: {Fitness}";
    }
}
