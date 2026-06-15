using GACore;
using Microsoft.ML;
using Microsoft.ML.Data;

public sealed class FeatureSelectionProblem : IGeneticProblem<int>
{
    public static readonly string[] FeatureNames =
    [
        nameof(FeatureSample.SignalA),
        nameof(FeatureSample.SignalB),
        nameof(FeatureSample.SignalC),
        nameof(FeatureSample.SignalD),
        nameof(FeatureSample.NoiseA),
        nameof(FeatureSample.NoiseB),
        nameof(FeatureSample.NoiseC),
        nameof(FeatureSample.NoiseD),
        nameof(FeatureSample.NoiseE),
        nameof(FeatureSample.NoiseF)
    ];

    private readonly MLContext _mlContext = new(seed: 11);
    private readonly IDataView _trainData;
    private readonly IDataView _validationData;
    private readonly Dictionary<string, FeatureSelectionEvaluation> _cache = [];

    public FeatureSelectionProblem()
    {
        var data = _mlContext.Data.LoadFromEnumerable(CreateData());
        var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.25, seed: 11);
        _trainData = split.TrainSet;
        _validationData = split.TestSet;
    }

    public Chromosome<int> CreateChromosome(Random random)
    {
        var genes = new int[FeatureNames.Length];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = random.NextDouble() < 0.55 ? 1 : 0;
        }

        if (!genes.Any(gene => gene == 1))
        {
            genes[random.Next(genes.Length)] = 1;
        }

        return new Chromosome<int>(genes);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var candidate = EvaluateCandidate(chromosome.Genes);
        var featurePenalty = candidate.SelectedFeatureCount * 0.006;
        return 1.0 - candidate.Accuracy + featurePenalty;
    }

    public FeatureSelectionEvaluation EvaluateCandidate(IReadOnlyList<int> genes)
    {
        var key = string.Join("", genes);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var selectedFeatures = FeatureNames
            .Where((_, index) => genes.Count > index && genes[index] == 1)
            .ToArray();

        if (selectedFeatures.Length == 0)
        {
            selectedFeatures = [FeatureNames[0]];
        }

        var pipeline = _mlContext.Transforms
            .Concatenate("Features", selectedFeatures)
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(FeatureSample.Label),
                featureColumnName: "Features",
                l2Regularization: 0.01f,
                maximumNumberOfIterations: 60));

        var model = pipeline.Fit(_trainData);
        var predictions = model.Transform(_validationData);
        var accuracy = CalculateAccuracy(predictions);
        var result = new FeatureSelectionEvaluation(
            accuracy,
            selectedFeatures.Length,
            string.Join(", ", selectedFeatures));

        _cache[key] = result;
        return result;
    }

    private double CalculateAccuracy(IDataView predictions)
    {
        var rows = _mlContext.Data
            .CreateEnumerable<FeaturePredictionRow>(predictions, reuseRowObject: false)
            .ToArray();

        return rows.Count(row => row.Label == row.PredictedLabel) / (double)rows.Length;
    }

    private static IEnumerable<FeatureSample> CreateData()
    {
        var random = new Random(202);

        for (int i = 0; i < 700; i++)
        {
            var signalA = Next(random, -2.5f, 2.5f);
            var signalB = Next(random, -2.5f, 2.5f);
            var signalC = Next(random, -2.5f, 2.5f);
            var signalD = Next(random, -2.5f, 2.5f);
            var noiseA = Next(random, -3.0f, 3.0f);
            var noiseB = Next(random, -3.0f, 3.0f);
            var noiseC = Next(random, -3.0f, 3.0f);
            var noiseD = Next(random, -3.0f, 3.0f);
            var noiseE = Next(random, -3.0f, 3.0f);
            var noiseF = Next(random, -3.0f, 3.0f);

            var score =
                signalA * 1.7f -
                signalB * 1.3f +
                signalC * 0.9f +
                MathF.Sin(signalD * 1.4f) * 0.8f +
                Next(random, -0.65f, 0.65f);

            yield return new FeatureSample
            {
                SignalA = signalA,
                SignalB = signalB,
                SignalC = signalC,
                SignalD = signalD,
                NoiseA = noiseA,
                NoiseB = noiseB,
                NoiseC = noiseC,
                NoiseD = noiseD,
                NoiseE = noiseE,
                NoiseF = noiseF,
                Label = score > 0
            };
        }
    }

    private static float Next(Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}

public sealed class FeatureSelectionMutation : IMutationOperator<int>
{
    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = chromosome.Genes[i] == 1 ? 0 : 1;
            }
        }

        if (!chromosome.Genes.Any(gene => gene == 1))
        {
            chromosome.Genes[random.Next(chromosome.Genes.Length)] = 1;
        }
    }
}

public sealed record FeatureSelectionEvaluation(double Accuracy, int SelectedFeatureCount, string SelectedFeatureText);

public sealed class FeatureSample
{
    public bool Label { get; set; }

    public float SignalA { get; set; }

    public float SignalB { get; set; }

    public float SignalC { get; set; }

    public float SignalD { get; set; }

    public float NoiseA { get; set; }

    public float NoiseB { get; set; }

    public float NoiseC { get; set; }

    public float NoiseD { get; set; }

    public float NoiseE { get; set; }

    public float NoiseF { get; set; }
}

public sealed class FeaturePredictionRow
{
    public bool Label { get; set; }

    public bool PredictedLabel { get; set; }
}
