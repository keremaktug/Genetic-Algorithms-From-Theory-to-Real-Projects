namespace GACore;

public sealed class GeneticSolver<TGene>
{
    private readonly IGeneticProblem<TGene> _problem;
    private readonly ISelectionStrategy<TGene> _selectionStrategy;
    private readonly ICrossoverOperator<TGene> _crossoverOperator;
    private readonly IMutationOperator<TGene> _mutationOperator;
    private readonly SolverOptions _options;
    private readonly Random _random;

    private List<Chromosome<TGene>> _population = [];

    public GeneticSolver(
        IGeneticProblem<TGene> problem,
        ISelectionStrategy<TGene> selectionStrategy,
        ICrossoverOperator<TGene> crossoverOperator,
        IMutationOperator<TGene> mutationOperator,
        SolverOptions options,
        Random? random = null)
    {
        _problem = problem;
        _selectionStrategy = selectionStrategy;
        _crossoverOperator = crossoverOperator;
        _mutationOperator = mutationOperator;
        _options = options;
        _random = random ?? new Random();
    }

    public IReadOnlyList<Chromosome<TGene>> Population => _population;

    public IEnumerable<GenerationResult<TGene>> Run()
    {
        InitializePopulation();

        for (int generation = 0; generation <= _options.MaxGenerations; generation++)
        {
            var result = CreateResult(generation);
            yield return result;

            if (result.IsSolutionFound || generation == _options.MaxGenerations)
            {
                yield break;
            }

            CreateNextGeneration();
        }
    }

    private void InitializePopulation()
    {
        _population = [];

        for (int i = 0; i < _options.PopulationSize; i++)
        {
            _population.Add(_problem.CreateChromosome(_random));
        }

        EvaluateAndSort();
    }

    private void CreateNextGeneration()
    {
        var nextGeneration = new List<Chromosome<TGene>>();
        var eliteCount = Math.Max(1, (int)(_options.PopulationSize * _options.ElitismRate));

        for (int i = 0; i < eliteCount; i++)
        {
            nextGeneration.Add(_population[i].Clone());
        }

        while (nextGeneration.Count < _options.PopulationSize)
        {
            var parentA = _selectionStrategy.Select(_population, _options, _random);
            var parentB = _selectionStrategy.Select(_population, _options, _random);
            var child = _crossoverOperator.Crossover(parentA, parentB, _random);

            _mutationOperator.Mutate(child, _options.MutationRate, _random);
            nextGeneration.Add(child);
        }

        _population = nextGeneration;
        EvaluateAndSort();
    }

    private void EvaluateAndSort()
    {
        foreach (var chromosome in _population)
        {
            chromosome.Fitness = _problem.CalculateFitness(chromosome);
        }

        _population = _options.FitnessGoal == FitnessGoal.Minimize
            ? _population.OrderBy(chromosome => chromosome.Fitness).ToList()
            : _population.OrderByDescending(chromosome => chromosome.Fitness).ToList();
    }

    private GenerationResult<TGene> CreateResult(int generation)
    {
        var best = _population[0].Clone();
        var averageFitness = _population.Average(chromosome => chromosome.Fitness);

        return new GenerationResult<TGene>(
            generation,
            best,
            best.Fitness,
            averageFitness,
            IsSolutionFound(best));
    }

    private bool IsSolutionFound(Chromosome<TGene> chromosome)
    {
        if (_options.TargetFitness is null)
        {
            return false;
        }

        return Math.Abs(chromosome.Fitness - _options.TargetFitness.Value) <= _options.FitnessTolerance;
    }
}
