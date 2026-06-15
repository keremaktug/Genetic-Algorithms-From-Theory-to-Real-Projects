using GACore;
using Microsoft.ML;
using Microsoft.ML.Data;

public sealed class RandomForestHyperparameterProblem : IGeneticProblem<int>
{
    private static readonly int[] TreeOptions = [3, 5, 10, 25, 50, 100];
    private static readonly int[] LeafOptions = [2, 4, 8, 16, 32, 64];
    private static readonly int[] MinimumExampleOptions = [1, 2, 4, 8, 16, 32];

    private readonly MLContext _mlContext = new(seed: 7);
    private readonly IDataView _trainData;
    private readonly IDataView _validationData;
    private readonly Dictionary<string, double> _fitnessCache = [];

    public RandomForestHyperparameterProblem()
    {
        var allData = _mlContext.Data.LoadFromEnumerable(CreateData());
        var split = _mlContext.Data.TrainTestSplit(allData, testFraction: 0.25, seed: 7);
        _trainData = split.TrainSet;
        _validationData = split.TestSet;
    }

    public Chromosome<int> CreateChromosome(Random random)
    {
        return new Chromosome<int>(
        [
            random.Next(TreeOptions.Length),
            random.Next(LeafOptions.Length),
            random.Next(MinimumExampleOptions.Length),
            random.Next(2)
        ]);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var key = string.Join(",", chromosome.Genes);

        if (_fitnessCache.TryGetValue(key, out var cachedFitness))
        {
            return cachedFitness;
        }

        var candidate = Decode(chromosome.Genes);
        var pipeline = CreateFeaturePipeline(candidate).Append(
            _mlContext.BinaryClassification.Trainers.FastForest(
                labelColumnName: nameof(RandomForestSample.Label),
                featureColumnName: "Features",
                numberOfLeaves: candidate.NumberOfLeaves,
                numberOfTrees: candidate.NumberOfTrees,
                minimumExampleCountPerLeaf: candidate.MinimumExampleCountPerLeaf));

        var model = pipeline.Fit(_trainData);
        var predictions = model.Transform(_validationData);
        var accuracy = CalculateAccuracy(predictions);
        var fitness = 1.0 - accuracy;

        _fitnessCache[key] = fitness;
        return fitness;
    }

    public static RandomForestCandidate Decode(IReadOnlyList<int> genes)
    {
        var treeIndex = Math.Clamp(genes[0], 0, TreeOptions.Length - 1);
        var leafIndex = Math.Clamp(genes[1], 0, LeafOptions.Length - 1);
        var minimumExampleIndex = Math.Clamp(genes[2], 0, MinimumExampleOptions.Length - 1);

        return new RandomForestCandidate(
            TreeOptions[treeIndex],
            LeafOptions[leafIndex],
            MinimumExampleOptions[minimumExampleIndex],
            genes[3] == 1);
    }

    private IEstimator<ITransformer> CreateFeaturePipeline(RandomForestCandidate candidate)
    {
        var pipeline = _mlContext.Transforms.Concatenate(
            "Features",
            nameof(RandomForestSample.X1),
            nameof(RandomForestSample.X2),
            nameof(RandomForestSample.X3),
            nameof(RandomForestSample.X4),
            nameof(RandomForestSample.X5),
            nameof(RandomForestSample.X6));

        return candidate.NormalizeFeatures
            ? pipeline.Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            : pipeline;
    }

    private double CalculateAccuracy(IDataView predictions)
    {
        var rows = _mlContext.Data
            .CreateEnumerable<RandomForestPredictionRow>(predictions, reuseRowObject: false)
            .ToArray();

        return rows.Count(row => row.Label == row.PredictedLabel) / (double)rows.Length;
    }

    private static IEnumerable<RandomForestSample> CreateData()
    {
        var random = new Random(321);

        for (int i = 0; i < 650; i++)
        {
            var x1 = Next(random, -3.0f, 3.0f);
            var x2 = Next(random, -3.0f, 3.0f);
            var x3 = Next(random, -3.0f, 3.0f);
            var x4 = Next(random, -3.0f, 3.0f);
            var x5 = Next(random, -3.0f, 3.0f);
            var x6 = Next(random, -3.0f, 3.0f);

            var curvedBoundary =
                MathF.Sin(x1 * 1.4f) +
                MathF.Cos(x2 * 1.1f) +
                x3 * x4 * 0.35f -
                MathF.Abs(x5) * 0.45f +
                x6 * 0.30f;

            var noise = Next(random, -0.35f, 0.35f);

            yield return new RandomForestSample
            {
                X1 = x1,
                X2 = x2,
                X3 = x3,
                X4 = x4,
                X5 = x5,
                X6 = x6,
                Label = curvedBoundary + noise > 0.20f
            };
        }
    }

    private static float Next(Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}

public sealed class RandomForestHyperparameterMutation : IMutationOperator<int>
{
    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        if (random.NextDouble() < mutationRate)
        {
            chromosome.Genes[0] = random.Next(6);
        }

        if (random.NextDouble() < mutationRate)
        {
            chromosome.Genes[1] = random.Next(6);
        }

        if (random.NextDouble() < mutationRate)
        {
            chromosome.Genes[2] = random.Next(6);
        }

        if (random.NextDouble() < mutationRate)
        {
            chromosome.Genes[3] = random.Next(2);
        }
    }
}

public sealed record RandomForestCandidate(
    int NumberOfTrees,
    int NumberOfLeaves,
    int MinimumExampleCountPerLeaf,
    bool NormalizeFeatures);

public sealed class RandomForestSample
{
    public bool Label { get; set; }

    public float X1 { get; set; }

    public float X2 { get; set; }

    public float X3 { get; set; }

    public float X4 { get; set; }

    public float X5 { get; set; }

    public float X6 { get; set; }
}

public sealed class RandomForestPredictionRow
{
    public bool Label { get; set; }

    public bool PredictedLabel { get; set; }
}
