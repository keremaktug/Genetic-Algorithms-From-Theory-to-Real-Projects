using GACore;

namespace _04.Phrase.WithCore.Wpf;

public sealed class PhraseProblem : IGeneticProblem<char>
{
    private readonly string _targetPhrase;
    private readonly string _alphabet;

    public PhraseProblem(string targetPhrase, string alphabet)
    {
        _targetPhrase = targetPhrase;
        _alphabet = alphabet;
    }

    public Chromosome<char> CreateChromosome(Random random)
    {
        var genes = new char[_targetPhrase.Length];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = _alphabet[random.Next(_alphabet.Length)];
        }

        return new Chromosome<char>(genes);
    }

    public double CalculateFitness(Chromosome<char> chromosome)
    {
        var fitness = 0;

        for (int i = 0; i < _targetPhrase.Length; i++)
        {
            fitness += Math.Abs(_targetPhrase[i] - chromosome.Genes[i]);
        }

        return fitness;
    }
}
