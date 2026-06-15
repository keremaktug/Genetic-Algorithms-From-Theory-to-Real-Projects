namespace GACore;

public sealed class InversionMutation<TGene> : IMutationOperator<TGene>
{
    public void Mutate(Chromosome<TGene> chromosome, double mutationRate, Random random)
    {
        if (chromosome.Genes.Length < 2 || random.NextDouble() >= mutationRate)
        {
            return;
        }

        var start = random.Next(chromosome.Genes.Length);
        var end = random.Next(chromosome.Genes.Length);

        if (start > end)
        {
            (start, end) = (end, start);
        }

        Array.Reverse(chromosome.Genes, start, end - start + 1);
    }
}
