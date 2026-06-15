using GACore;
using Microsoft.ML;
using Microsoft.ML.Data;

public sealed class MlHyperparameterProblem : IGeneticProblem<int>
{
    private static readonly string[] TrainerOptions = ["SDCA Logistic", "Averaged Perceptron"];
    private static readonly int[] IterationOptions = [5, 10, 20, 40, 80, 120];
    private static readonly double[] L2Options = [0.0001, 0.001, 0.01, 0.05, 0.10, 0.30];

    private readonly MLContext _mlContext = new(seed: 1);
    private readonly IDataView _trainData;
    private readonly IDataView _validationData;
    private readonly Dictionary<string, double> _fitnessCache = [];

    public MlHyperparameterProblem()
    {
        var allData = _mlContext.Data.LoadFromEnumerable(CreateData());
        var split = _mlContext.Data.TrainTestSplit(allData, testFraction: 0.25, seed: 1);
        _trainData = split.TrainSet;
        _validationData = split.TestSet;
    }

    public Chromosome<int> CreateChromosome(Random random)
    {
        return new Chromosome<int>(
        [
            random.Next(TrainerOptions.Length),
            random.Next(IterationOptions.Length),
            random.Next(L2Options.Length),
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

        var pipeline = candidate.NormalizeFeatures
            ? _mlContext.Transforms.Concatenate("Features", nameof(SampleData.X1), nameof(SampleData.X2), nameof(SampleData.X3), nameof(SampleData.X4))
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(CreateTrainer(candidate))
            : _mlContext.Transforms.Concatenate("Features", nameof(SampleData.X1), nameof(SampleData.X2), nameof(SampleData.X3), nameof(SampleData.X4))
                .Append(CreateTrainer(candidate));

        var model = pipeline.Fit(_trainData);
        var predictions = model.Transform(_validationData);
        var accuracy = CalculateAccuracy(predictions);
        var fitness = 1.0 - accuracy;

        _fitnessCache[key] = fitness;
        return fitness;
    }

    private double CalculateAccuracy(IDataView predictions)
    {
        var rows = _mlContext.Data
            .CreateEnumerable<PredictionRow>(predictions, reuseRowObject: false)
            .ToArray();

        return rows.Count(row => row.Label == row.PredictedLabel) / (double)rows.Length;
    }

    public static HyperparameterCandidate Decode(IReadOnlyList<int> genes)
    {
        var trainerIndex = Math.Clamp(genes[0], 0, TrainerOptions.Length - 1);
        var iterationIndex = Math.Clamp(genes[1], 0, IterationOptions.Length - 1);
        var l2Index = Math.Clamp(genes[2], 0, L2Options.Length - 1);

        return new HyperparameterCandidate(
            TrainerOptions[trainerIndex],
            IterationOptions[iterationIndex],
            L2Options[l2Index],
            genes[3] == 1);
    }

    private IEstimator<ITransformer> CreateTrainer(HyperparameterCandidate candidate)
    {
        if (candidate.TrainerName == "Averaged Perceptron")
        {
            return _mlContext.BinaryClassification.Trainers.AveragedPerceptron(
                labelColumnName: nameof(SampleData.Label),
                featureColumnName: "Features",
                numberOfIterations: candidate.NumberOfIterations);
        }

        return _mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
            labelColumnName: nameof(SampleData.Label),
            featureColumnName: "Features",
            l2Regularization: (float)candidate.L2Regularization,
            maximumNumberOfIterations: candidate.NumberOfIterations);
    }

    private static IEnumerable<SampleData> CreateData()
    {
        var random = new Random(123);

        for (int i = 0; i < 420; i++)
        {
            var x1 = Next(random, -2.0f, 2.0f);
            var x2 = Next(random, -2.0f, 2.0f);
            var x3 = Next(random, -2.0f, 2.0f);
            var x4 = Next(random, -2.0f, 2.0f);
            var score = x1 * 1.8f - x2 * 1.2f + x3 * 0.8f + MathF.Sin(x4 * 1.7f);
            var noise = Next(random, -0.55f, 0.55f);

            yield return new SampleData
            {
                X1 = x1,
                X2 = x2,
                X3 = x3,
                X4 = x4,
                Label = score + noise > 0
            };
        }
    }

    private static float Next(Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}

public sealed class HyperparameterMutation : IMutationOperator<int>
{
    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        if (random.NextDouble() < mutationRate)
        {
            chromosome.Genes[0] = random.Next(2);
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

public sealed record HyperparameterCandidate(string TrainerName, int NumberOfIterations, double L2Regularization, bool NormalizeFeatures);

public sealed class SampleData
{
    public bool Label { get; set; }

    public float X1 { get; set; }

    public float X2 { get; set; }

    public float X3 { get; set; }

    public float X4 { get; set; }
}

public sealed class PredictionRow
{
    public bool Label { get; set; }

    public bool PredictedLabel { get; set; }
}
