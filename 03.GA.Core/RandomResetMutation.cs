namespace GACore;

public sealed class RandomResetMutation<TGene> : IMutationOperator<TGene>
{
    private readonly Func<Random, TGene> _createRandomGene;

    public RandomResetMutation(Func<Random, TGene> createRandomGene)
    {
        _createRandomGene = createRandomGene;
    }

    public void Mutate(Chromosome<TGene> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = _createRandomGene(random);
            }
        }
    }
}
