using GACore;

namespace _21.Rastrigin.Wpf;

public sealed class RastriginProblem : IGeneticProblem<double>
{
    public const double Min = -5.12;
    public const double Max = 5.12;

    public Chromosome<double> CreateChromosome(Random random)
    {
        return new Chromosome<double>([Next(random), Next(random)]);
    }

    public double CalculateFitness(Chromosome<double> chromosome)
    {
        return Evaluate(chromosome.Genes[0], chromosome.Genes[1]);
    }

    public static double Evaluate(double x, double y)
    {
        return 20 +
            x * x - 10 * Math.Cos(2 * Math.PI * x) +
            y * y - 10 * Math.Cos(2 * Math.PI * y);
    }

    public static double Clamp(double value)
    {
        return Math.Min(Max, Math.Max(Min, value));
    }

    private static double Next(Random random)
    {
        return Min + random.NextDouble() * (Max - Min);
    }
}

public sealed class RealValueMutation : IMutationOperator<double>
{
    public void Mutate(Chromosome<double> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                var noise = NextGaussian(random) * 0.45;
                chromosome.Genes[i] = RastriginProblem.Clamp(chromosome.Genes[i] + noise);
            }
        }
    }

    private static double NextGaussian(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
