using GACore;

namespace _13.AnalogOpAmp.Wpf;

public sealed class OpAmpProblem : IGeneticProblem<int>
{
    public static readonly double[] Resistors =
    [
        100, 120, 150, 180, 220, 270, 330, 390, 470, 560, 680, 820,
        1000, 1200, 1500, 1800, 2200, 2700, 3300, 3900, 4700, 5600, 6800, 8200,
        10000, 12000, 15000, 18000, 22000, 27000, 33000, 39000, 47000, 56000, 68000, 82000,
        100000, 120000, 150000, 180000, 220000, 270000, 330000, 390000, 470000, 560000, 680000, 820000
    ];

    public OpAmpProblem(double targetGain)
    {
        TargetGain = targetGain;
    }

    public double TargetGain { get; }

    public Chromosome<int> CreateChromosome(Random random)
    {
        return new Chromosome<int>([random.Next(Resistors.Length), random.Next(Resistors.Length)]);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var design = Decode(chromosome.Genes);
        var gainError = Math.Abs(TargetGain - design.Gain);
        var resistancePenalty = (design.Rg + design.Rf) / 2_000_000.0;
        return gainError + resistancePenalty;
    }

    public OpAmpDesign Decode(IReadOnlyList<int> genes)
    {
        var rgIndex = Math.Clamp(genes[0], 0, Resistors.Length - 1);
        var rfIndex = Math.Clamp(genes[1], 0, Resistors.Length - 1);
        var rg = Resistors[rgIndex];
        var rf = Resistors[rfIndex];
        return new OpAmpDesign(rg, rf, 1.0 + rf / rg);
    }

    public static string FormatResistance(double value)
    {
        return value >= 1000 ? $"{value / 1000:0.##} kΩ" : $"{value:0} Ω";
    }
}

public sealed class OpAmpMutation : IMutationOperator<int>
{
    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() >= mutationRate) continue;
            chromosome.Genes[i] = random.Next(OpAmpProblem.Resistors.Length);
        }
    }
}

public sealed record OpAmpDesign(double Rg, double Rf, double Gain);
