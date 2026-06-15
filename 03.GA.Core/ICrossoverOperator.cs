namespace GACore;

public interface ICrossoverOperator<TGene>
{
    Chromosome<TGene> Crossover(Chromosome<TGene> parentA, Chromosome<TGene> parentB, Random random);
}
