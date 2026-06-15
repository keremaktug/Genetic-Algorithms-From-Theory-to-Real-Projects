using GACore;

var problem = new RandomForestHyperparameterProblem();
var options = new SolverOptions
{
    PopulationSize = 28,
    MaxGenerations = 24,
    ElitismRate = 0.15,
    MutationRate = 0.22,
    FitnessGoal = FitnessGoal.Minimize,
    TargetFitness = 0,
    FitnessTolerance = 0.02,
    TournamentSize = 3
};

var solver = new GeneticSolver<int>(
    problem,
    new TournamentSelection<int>(),
    new UniformCrossover<int>(),
    new RandomForestHyperparameterMutation(),
    options,
    new Random(42));

Console.WriteLine("ML.NET Random Forest Hyperparameter Optimization with a Genetic Algorithm");
Console.WriteLine("Trainer = FastForest binary classifier");
Console.WriteLine("Fitness = 1 - validation accuracy");
Console.WriteLine();

GenerationResult<int>? finalResult = null;

foreach (var result in solver.Run())
{
    finalResult = result;
    var candidate = RandomForestHyperparameterProblem.Decode(result.BestChromosome.Genes);
    var accuracy = 1.0 - result.BestFitness;

    Console.WriteLine(
        $"Generation {result.Generation,2} | Error {result.BestFitness:F4} | Accuracy {accuracy:P2} | " +
        $"Trees {candidate.NumberOfTrees,3} | Leaves {candidate.NumberOfLeaves,3} | " +
        $"MinExamples {candidate.MinimumExampleCountPerLeaf,2} | Normalize {candidate.NormalizeFeatures}");

    if (result.IsSolutionFound)
    {
        Console.WriteLine();
        Console.WriteLine("Target accuracy reached.");
        break;
    }
}

if (finalResult is not null)
{
    var best = RandomForestHyperparameterProblem.Decode(finalResult.BestChromosome.Genes);
    Console.WriteLine();
    Console.WriteLine("Best configuration");
    Console.WriteLine($"NumberOfTrees               : {best.NumberOfTrees}");
    Console.WriteLine($"NumberOfLeaves              : {best.NumberOfLeaves}");
    Console.WriteLine($"MinimumExampleCountPerLeaf  : {best.MinimumExampleCountPerLeaf}");
    Console.WriteLine($"NormalizeFeatures           : {best.NormalizeFeatures}");
    Console.WriteLine($"ValidationAccuracy          : {1.0 - finalResult.BestFitness:P2}");
}
