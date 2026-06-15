namespace GACore;

public sealed class SwapMutation<TGene> : IMutationOperator<TGene>
{
    public void Mutate(Chromosome<TGene> chromosome, double mutationRate, Random random)
    {
        if (chromosome.Genes.Length < 2 || random.NextDouble() >= mutationRate)
        {
            return;
        }

        var first = random.Next(chromosome.Genes.Length);
        var second = random.Next(chromosome.Genes.Length);

        while (second == first)
        {
            second = random.Next(chromosome.Genes.Length);
        }

        (chromosome.Genes[first], chromosome.Genes[second]) = (chromosome.Genes[second], chromosome.Genes[first]);
    }
}
