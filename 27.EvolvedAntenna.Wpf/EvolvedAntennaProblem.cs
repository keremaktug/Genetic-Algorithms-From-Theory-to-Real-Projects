using GACore;

namespace _27.EvolvedAntenna.Wpf;

public sealed class EvolvedAntennaProblem : IGeneticProblem<double>
{
    public const int ElementCount = 4;
    public const int GeneCount = 1 + ElementCount * 2;
    public const double MinFrequencyMhz = 2030;
    public const double MaxFrequencyMhz = 2300;

    private static readonly double[] FrequenciesMhz = [2030, 2075, 2120, 2210, 2255, 2300];

    public Chromosome<double> CreateChromosome(Random random)
    {
        var genes = new double[GeneCount];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = random.NextDouble();
        }

        return new Chromosome<double>(genes);
    }

    public double CalculateFitness(Chromosome<double> chromosome)
    {
        return Evaluate(chromosome).Fitness;
    }

    public static AntennaEvaluation Evaluate(Chromosome<double> chromosome)
    {
        return Evaluate(Decode(chromosome.Genes));
    }

    public static AntennaDesign Decode(IReadOnlyList<double> genes)
    {
        var height = Lerp(3.0, 4.0, Clamp01(genes[0]));
        var elements = new AntennaElement[ElementCount];
        var previousSize = Lerp(0.30, 1.15, Clamp01(genes[2]));

        for (int i = 0; i < ElementCount; i++)
        {
            var spacingGene = Clamp01(genes[1 + i * 2]);
            var sizeGene = Clamp01(genes[2 + i * 2]);
            var spacing = Lerp(0.16, 0.78, spacingGene);
            var size = i == 0
                ? previousSize
                : Lerp(previousSize * 0.78, previousSize * 1.22, sizeGene);

            size = Math.Clamp(size, 0.15, 1.45);
            elements[i] = new AntennaElement(spacing, size);
            previousSize = size;
        }

        return new AntennaDesign(height, elements);
    }

    public static AntennaEvaluation Evaluate(AntennaDesign design)
    {
        var boresightGain = GainAtAngle(design, 0);
        var gain20 = GainAtAngle(design, 20);
        var sideLobeMax = Enumerable.Range(30, 61).Select(angle => GainAtAngle(design, angle)).Max();
        var smoothness = PatternSmoothness(design);
        var vswrValues = FrequenciesMhz.Select(frequency => Vswr(design, frequency)).ToArray();
        var maxVswr = vswrValues.Max();
        var footprint = design.HeightLambda + design.Elements.Sum(element => element.SizeLambda * 0.16 + element.SpacingLambda * 0.08);

        var boresightPenalty = Math.Pow(Math.Max(0, 15.25 - boresightGain), 2.0) * 2.0;
        var gain20Penalty = Math.Pow(Math.Max(0, 10.25 - gain20), 2.0) * 2.4;
        var sideLobePenalty = Math.Pow(Math.Max(0, sideLobeMax - 5.0), 2.0) * 1.7;
        var vswrPenalty = vswrValues.Sum(v => Math.Pow(Math.Max(0, v - 1.5), 2.0) * 8.0 + Math.Pow(Math.Max(0, v - 3.0), 2.0) * 28.0);
        var smoothnessPenalty = smoothness * 0.55;
        var sizePenalty = Math.Max(0, footprint - 4.7) * 3.0;

        var fitness = boresightPenalty + gain20Penalty + sideLobePenalty + vswrPenalty + smoothnessPenalty + sizePenalty;

        return new AntennaEvaluation(
            design,
            fitness,
            boresightGain,
            gain20,
            sideLobeMax,
            maxVswr,
            smoothness,
            footprint,
            vswrValues);
    }

    public static double GainAtAngle(AntennaDesign design, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var directional = Math.Pow(Math.Cos(radians), 2.0);
        var aperture = design.Elements
            .Select((element, index) => element.SizeLambda * (1.0 - index * 0.08))
            .Sum();
        var spacingMean = design.Elements.Average(element => element.SpacingLambda);
        var sizeVariance = design.Elements.Select(element => Math.Pow(element.SizeLambda - design.Elements.Average(e => e.SizeLambda), 2)).Average();
        var resonance = ResonanceScore(design);
        var arrayShape = Math.Cos(Math.PI * spacingMean * Math.Sin(radians));
        var ripple = Math.Abs(Math.Sin(angleDegrees * 0.16 + aperture * 1.7)) * (0.7 + sizeVariance * 2.4);

        var gain = 5.5 + aperture * 3.1 + resonance * 4.2 + directional * 4.0 + arrayShape * 1.4 - ripple;

        if (angleDegrees > 30)
        {
            gain -= (angleDegrees - 30) * 0.18 + Math.Max(0, spacingMean - 0.46) * 5.5;
        }

        return gain;
    }

    private static double PatternSmoothness(AntennaDesign design)
    {
        var gains = Enumerable.Range(0, 21).Select(i => GainAtAngle(design, i)).ToArray();
        var deltas = gains.Zip(gains.Skip(1), (a, b) => Math.Abs(a - b)).ToArray();
        return deltas.Average() * 10.0;
    }

    private static double Vswr(AntennaDesign design, double frequencyMhz)
    {
        var normalizedFrequency = (frequencyMhz - MinFrequencyMhz) / (MaxFrequencyMhz - MinFrequencyMhz);
        var targetSize = 0.68 + normalizedFrequency * 0.24;
        var meanSize = design.Elements.Average(element => element.SizeLambda);
        var spacingSpread = design.Elements.Max(element => element.SpacingLambda) - design.Elements.Min(element => element.SpacingLambda);
        var heightPenalty = Math.Abs(design.HeightLambda - 3.55);
        var mismatch = Math.Abs(meanSize - targetSize) + spacingSpread * 0.38 + heightPenalty * 0.12;

        return 1.02 + mismatch * 2.3;
    }

    private static double ResonanceScore(AntennaDesign design)
    {
        var meanSize = design.Elements.Average(element => element.SizeLambda);
        var sizeScore = 1.0 - Math.Min(1.0, Math.Abs(meanSize - 0.78) / 0.55);
        var heightScore = 1.0 - Math.Min(1.0, Math.Abs(design.HeightLambda - 3.55) / 0.75);
        return Math.Max(0, sizeScore * 0.65 + heightScore * 0.35);
    }

    public static double Clamp01(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    private static double Lerp(double min, double max, double t)
    {
        return min + (max - min) * t;
    }
}

public sealed class AntennaMutation : IMutationOperator<double>
{
    public void Mutate(Chromosome<double> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = EvolvedAntennaProblem.Clamp01(chromosome.Genes[i] + NextGaussian(random) * 0.08);
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

public sealed record AntennaDesign(double HeightLambda, AntennaElement[] Elements);

public sealed record AntennaElement(double SpacingLambda, double SizeLambda);

public sealed record AntennaEvaluation(
    AntennaDesign Design,
    double Fitness,
    double BoresightGain,
    double Gain20,
    double SideLobeMax,
    double MaxVswr,
    double Smoothness,
    double Footprint,
    double[] VswrValues);
