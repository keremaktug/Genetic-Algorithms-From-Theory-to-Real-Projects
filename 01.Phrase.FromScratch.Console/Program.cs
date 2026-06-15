const string TargetPhrase = "Those who live in glass houses should not throw stones";
const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,.";

const int PopulationSize = 800;
const int MaxGenerations = 1000;
const double ElitismRate = 0.10;
const double MutationRate = 0.03;

var random = new Random();

var population = CreateInitialPopulation();
EvaluateAndSort(population);

Console.WriteLine("Genetic Algorithm - Phrase Evolution From Scratch");
Console.WriteLine($"Target : {TargetPhrase}");
Console.WriteLine();

for (int generation = 0; generation <= MaxGenerations; generation++)
{
    var best = population[0];
    var averageFitness = population.Average(chromosome => chromosome.Fitness);

    if (generation % 20 == 0 || best.Fitness == 0)
    {
        Console.WriteLine(
            $"Generation {generation,4} | Best Fitness {best.Fitness,4} | Average {averageFitness,8:F2} | {new string(best.Genes)}");
    }

    if (best.Fitness == 0)
    {
        Console.WriteLine();
        Console.WriteLine("Solution found.");
        break;
    }

    population = CreateNextGeneration(population);
    EvaluateAndSort(population);
}

List<(char[] Genes, int Fitness)> CreateInitialPopulation()
{
    var result = new List<(char[] Genes, int Fitness)>();

    for (int i = 0; i < PopulationSize; i++)
    {
        result.Add((CreateRandomChromosome(), 0));
    }

    return result;
}

char[] CreateRandomChromosome()
{
    var genes = new char[TargetPhrase.Length];

    for (int i = 0; i < genes.Length; i++)
    {
        genes[i] = CreateRandomGene();
    }

    return genes;
}

char CreateRandomGene()
{
    var index = random.Next(Alphabet.Length);
    return Alphabet[index];
}

void EvaluateAndSort(List<(char[] Genes, int Fitness)> currentPopulation)
{
    for (int i = 0; i < currentPopulation.Count; i++)
    {
        var genes = currentPopulation[i].Genes;
        var fitness = CalculateFitness(genes);
        currentPopulation[i] = (genes, fitness);
    }

    currentPopulation.Sort((left, right) => left.Fitness.CompareTo(right.Fitness));
}

int CalculateFitness(char[] genes)
{
    var fitness = 0;

    for (int i = 0; i < TargetPhrase.Length; i++)
    {
        fitness += Math.Abs(TargetPhrase[i] - genes[i]);
    }

    return fitness;
}

List<(char[] Genes, int Fitness)> CreateNextGeneration(List<(char[] Genes, int Fitness)> currentPopulation)
{
    var nextGeneration = new List<(char[] Genes, int Fitness)>();
    var eliteCount = Math.Max(1, (int)(PopulationSize * ElitismRate));

    for (int i = 0; i < eliteCount; i++)
    {
        nextGeneration.Add((Clone(currentPopulation[i].Genes), 0));
    }

    while (nextGeneration.Count < PopulationSize)
    {
        var parentA = SelectParent(currentPopulation);
        var parentB = SelectParent(currentPopulation);

        var child = Crossover(parentA.Genes, parentB.Genes);
        Mutate(child);

        nextGeneration.Add((child, 0));
    }

    return nextGeneration;
}

(char[] Genes, int Fitness) SelectParent(List<(char[] Genes, int Fitness)> currentPopulation)
{
    var selectionPoolSize = currentPopulation.Count / 2;
    var index = random.Next(selectionPoolSize);
    return currentPopulation[index];
}

char[] Crossover(char[] parentA, char[] parentB)
{
    var child = new char[TargetPhrase.Length];
    var crossoverPoint = random.Next(1, TargetPhrase.Length);

    for (int i = 0; i < TargetPhrase.Length; i++)
    {
        child[i] = i < crossoverPoint ? parentA[i] : parentB[i];
    }

    return child;
}

void Mutate(char[] genes)
{
    for (int i = 0; i < genes.Length; i++)
    {
        if (random.NextDouble() < MutationRate)
        {
            genes[i] = CreateRandomGene();
        }
    }
}

char[] Clone(char[] genes)
{
    var clone = new char[genes.Length];
    Array.Copy(genes, clone, genes.Length);
    return clone;
}
