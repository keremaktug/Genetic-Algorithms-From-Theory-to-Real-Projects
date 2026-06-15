using GACore;

var problem = new MlHyperparameterProblem();
var options = new SolverOptions
{
    PopulationSize = 24,
    MaxGenerations = 18,
    ElitismRate = 0.15,
    MutationRate = 0.20,
    FitnessGoal = FitnessGoal.Minimize,
    TargetFitness = 0,
    FitnessTolerance = 0.025,
    TournamentSize = 3
};

var solver = new GeneticSolver<int>(
    problem,
    new TournamentSelection<int>(),
    new UniformCrossover<int>(),
    new HyperparameterMutation(),
    options,
    new Random(42));

Console.WriteLine("ML.NET Hyperparameter Optimization with a Genetic Algorithm");
Console.WriteLine("Fitness = 1 - validation accuracy");
Console.WriteLine();

GenerationResult<int>? finalResult = null;

foreach (var result in solver.Run())
{
    finalResult = result;
    var candidate = MlHyperparameterProblem.Decode(result.BestChromosome.Genes);
    var accuracy = 1.0 - result.BestFitness;

    Console.WriteLine(
        $"Generation {result.Generation,2} | Error {result.BestFitness:F4} | Accuracy {accuracy:P2} | " +
        $"Trainer {candidate.TrainerName,-18} | Iterations {candidate.NumberOfIterations,4} | L2 {candidate.L2Regularization:F4} | Normalize {candidate.NormalizeFeatures}");

    if (result.IsSolutionFound)
    {
        Console.WriteLine();
        Console.WriteLine("Target accuracy reached.");
        break;
    }
}

if (finalResult is not null)
{
    var best = MlHyperparameterProblem.Decode(finalResult.BestChromosome.Genes);
    Console.WriteLine();
    Console.WriteLine("Best configuration");
    Console.WriteLine($"Trainer            : {best.TrainerName}");
    Console.WriteLine($"NumberOfIterations : {best.NumberOfIterations}");
    Console.WriteLine($"L2Regularization   : {best.L2Regularization}");
    Console.WriteLine($"NormalizeFeatures  : {best.NormalizeFeatures}");
    Console.WriteLine($"ValidationAccuracy : {1.0 - finalResult.BestFitness:P2}");
}
