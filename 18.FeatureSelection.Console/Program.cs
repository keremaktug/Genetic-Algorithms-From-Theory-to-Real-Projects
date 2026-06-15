using GACore;

var problem = new FeatureSelectionProblem();
var options = new SolverOptions
{
    PopulationSize = 36,
    MaxGenerations = 30,
    ElitismRate = 0.16,
    MutationRate = 0.10,
    FitnessGoal = FitnessGoal.Minimize,
    TargetFitness = null,
    TournamentSize = 4
};

var solver = new GeneticSolver<int>(
    problem,
    new TournamentSelection<int>(),
    new UniformCrossover<int>(),
    new FeatureSelectionMutation(),
    options,
    new Random(42));

Console.WriteLine("ML.NET Feature Selection with a Genetic Algorithm");
Console.WriteLine("Gene = 1 means feature is selected, 0 means feature is ignored");
Console.WriteLine("Fitness = validation error + small penalty for selected feature count");
Console.WriteLine();

GenerationResult<int>? finalResult = null;

foreach (var result in solver.Run())
{
    finalResult = result;
    var candidate = problem.EvaluateCandidate(result.BestChromosome.Genes);

    Console.WriteLine(
        $"Generation {result.Generation,2} | Fitness {result.BestFitness:F4} | Accuracy {candidate.Accuracy:P2} | " +
        $"Features {candidate.SelectedFeatureCount,2}/{FeatureSelectionProblem.FeatureNames.Length} | {candidate.SelectedFeatureText}");
}

if (finalResult is not null)
{
    var best = problem.EvaluateCandidate(finalResult.BestChromosome.Genes);

    Console.WriteLine();
    Console.WriteLine("Best feature subset");
    Console.WriteLine($"Chromosome       : {string.Join("", finalResult.BestChromosome.Genes)}");
    Console.WriteLine($"Selected features: {best.SelectedFeatureText}");
    Console.WriteLine($"Feature count    : {best.SelectedFeatureCount}");
    Console.WriteLine($"Validation score : {best.Accuracy:P2}");
    Console.WriteLine($"Fitness          : {finalResult.BestFitness:F4}");
}
