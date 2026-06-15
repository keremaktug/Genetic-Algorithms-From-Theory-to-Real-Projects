namespace GACore;

public interface IMutationOperator<TGene>
{
    void Mutate(Chromosome<TGene> chromosome, double mutationRate, Random random);
}
