namespace GACore;

public sealed class GenerationResult<TGene>
{
    public GenerationResult(
        int generation,
        Chromosome<TGene> bestChromosome,
        double bestFitness,
        double averageFitness,
        bool isSolutionFound)
    {
        Generation = generation;
        BestChromosome = bestChromosome;
        BestFitness = bestFitness;
        AverageFitness = averageFitness;
        IsSolutionFound = isSolutionFound;
    }

    public int Generation { get; }

    public Chromosome<TGene> BestChromosome { get; }

    public double BestFitness { get; }

    public double AverageFitness { get; }

    public bool IsSolutionFound { get; }
}
