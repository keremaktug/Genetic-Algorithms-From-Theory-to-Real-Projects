using GACore;

namespace _09.Sudoku.Wpf;

public sealed class SudokuRowMutation : IMutationOperator<int>
{
    private readonly IReadOnlyList<(int Start, int Length)> _rowRanges;

    public SudokuRowMutation(IReadOnlyList<(int Start, int Length)> rowRanges)
    {
        _rowRanges = rowRanges;
    }

    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        foreach (var range in _rowRanges)
        {
            if (range.Length < 2 || random.NextDouble() >= mutationRate)
            {
                continue;
            }

            var first = range.Start + random.Next(range.Length);
            var second = range.Start + random.Next(range.Length);

            while (second == first)
            {
                second = range.Start + random.Next(range.Length);
            }

            (chromosome.Genes[first], chromosome.Genes[second]) = (chromosome.Genes[second], chromosome.Genes[first]);
        }
    }
}
