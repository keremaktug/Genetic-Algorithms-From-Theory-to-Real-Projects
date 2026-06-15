using GACore;

namespace _12.AnalogRC.Wpf;

public sealed class RcProblem : IGeneticProblem<int>
{
    public static readonly double[] Resistors =
    [
        100, 120, 150, 180, 220, 270, 330, 390, 470, 560, 680, 820,
        1000, 1200, 1500, 1800, 2200, 2700, 3300, 3900, 4700, 5600, 6800, 8200,
        10000, 12000, 15000, 18000, 22000, 27000, 33000, 39000, 47000, 56000, 68000, 82000,
        100000, 120000, 150000, 180000, 220000, 270000, 330000, 390000, 470000, 560000, 680000, 820000
    ];

    public static readonly double[] Capacitors =
    [
        1e-9, 1.2e-9, 1.5e-9, 1.8e-9, 2.2e-9, 2.7e-9, 3.3e-9, 3.9e-9, 4.7e-9, 5.6e-9, 6.8e-9, 8.2e-9,
        10e-9, 12e-9, 15e-9, 18e-9, 22e-9, 27e-9, 33e-9, 39e-9, 47e-9, 56e-9, 68e-9, 82e-9,
        100e-9, 120e-9, 150e-9, 180e-9, 220e-9, 270e-9, 330e-9, 390e-9, 470e-9, 560e-9, 680e-9, 820e-9,
        1e-6, 1.2e-6, 1.5e-6, 1.8e-6, 2.2e-6, 2.7e-6, 3.3e-6, 3.9e-6, 4.7e-6
    ];

    public RcProblem(double targetFrequency)
    {
        TargetFrequency = targetFrequency;
    }

    public double TargetFrequency { get; }

    public Chromosome<int> CreateChromosome(Random random)
    {
        return new Chromosome<int>([random.Next(Resistors.Length), random.Next(Capacitors.Length)]);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var design = Decode(chromosome.Genes);
        return Math.Abs(TargetFrequency - design.CutoffFrequency);
    }

    public RcDesign Decode(IReadOnlyList<int> genes)
    {
        var resistorIndex = Math.Clamp(genes[0], 0, Resistors.Length - 1);
        var capacitorIndex = Math.Clamp(genes[1], 0, Capacitors.Length - 1);
        var resistance = Resistors[resistorIndex];
        var capacitance = Capacitors[capacitorIndex];
        var cutoff = 1.0 / (2.0 * Math.PI * resistance * capacitance);
        return new RcDesign(resistance, capacitance, cutoff);
    }

    public static string FormatResistance(double value)
    {
        return value >= 1000 ? $"{value / 1000:0.##} kΩ" : $"{value:0} Ω";
    }

    public static string FormatCapacitance(double value)
    {
        if (value >= 1e-6) return $"{value / 1e-6:0.##} µF";
        return $"{value / 1e-9:0.##} nF";
    }
}

public sealed class RcMutation : IMutationOperator<int>
{
    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() >= mutationRate) continue;
            chromosome.Genes[i] = i == 0 ? random.Next(RcProblem.Resistors.Length) : random.Next(RcProblem.Capacitors.Length);
        }
    }
}

public sealed record RcDesign(double Resistance, double Capacitance, double CutoffFrequency);
