namespace GACore;

public interface IGeneticProblem<TGene>
{
    Chromosome<TGene> CreateChromosome(Random random);

    double CalculateFitness(Chromosome<TGene> chromosome);
}
