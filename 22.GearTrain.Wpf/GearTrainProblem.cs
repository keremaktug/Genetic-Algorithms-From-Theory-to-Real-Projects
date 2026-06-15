using GACore;

namespace _22.GearTrain.Wpf;

public sealed class GearTrainProblem : IGeneticProblem<int>
{
    private readonly double _targetRatio;
    private readonly int _minTeeth;
    private readonly int _maxTeeth;

    public GearTrainProblem(double targetRatio, int minTeeth, int maxTeeth)
    {
        _targetRatio = targetRatio;
        _minTeeth = minTeeth;
        _maxTeeth = maxTeeth;
    }

    public Chromosome<int> CreateChromosome(Random random)
    {
        return new Chromosome<int>(
        [
            random.Next(_minTeeth, _maxTeeth + 1),
            random.Next(_minTeeth, _maxTeeth + 1),
            random.Next(_minTeeth, _maxTeeth + 1),
            random.Next(_minTeeth, _maxTeeth + 1)
        ]);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var ratio = CalculateRatio(chromosome.Genes);
        return Math.Abs(_targetRatio - ratio) / _targetRatio * 100.0;
    }

    public static double CalculateRatio(IReadOnlyList<int> gears)
    {
        return gears[1] * gears[3] / (double)(gears[0] * gears[2]);
    }
}

public sealed class GearMutation : IMutationOperator<int>
{
    private readonly int _minTeeth;
    private readonly int _maxTeeth;

    public GearMutation(int minTeeth, int maxTeeth)
    {
        _minTeeth = minTeeth;
        _maxTeeth = maxTeeth;
    }

    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                var localStep = random.Next(-4, 5);
                chromosome.Genes[i] = Math.Clamp(chromosome.Genes[i] + localStep, _minTeeth, _maxTeeth);

                if (random.NextDouble() < 0.18)
                {
                    chromosome.Genes[i] = random.Next(_minTeeth, _maxTeeth + 1);
                }
            }
        }
    }
}
