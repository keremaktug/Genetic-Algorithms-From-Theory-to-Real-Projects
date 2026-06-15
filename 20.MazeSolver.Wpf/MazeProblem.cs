using GACore;

namespace _20.MazeSolver.Wpf;

public sealed class MazeProblem : IGeneticProblem<int>
{
    private static readonly string[] Layout =
    [
        "#################",
        "#S....#.........#",
        "###.#.#.#######.#",
        "#...#.#.....#...#",
        "#.###.#####.#.###",
        "#.....#.....#...#",
        "#.#####.#######.#",
        "#.#.....#.......#",
        "#.#.#####.#####.#",
        "#.........#....E#",
        "#################"
    ];

    private readonly int _moveCount;

    public MazeProblem(int moveCount)
    {
        _moveCount = moveCount;
        Start = Find('S');
        Exit = Find('E');
    }

    public int Width => Layout[0].Length;

    public int Height => Layout.Length;

    public MazePoint Start { get; }

    public MazePoint Exit { get; }

    public Chromosome<int> CreateChromosome(Random random)
    {
        var genes = new int[_moveCount];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = random.Next(4);
        }

        return new Chromosome<int>(genes);
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var evaluation = Evaluate(chromosome.Genes);
        return evaluation.ReachedExit
            ? evaluation.UsedMoves
            : evaluation.DistanceToExit * 20 + evaluation.Collisions * 4 + evaluation.UsedMoves * 0.10;
    }

    public MazeEvaluation Evaluate(IReadOnlyList<int> moves)
    {
        var position = Start;
        var path = new List<MazePoint> { position };
        var collisions = 0;
        var usedMoves = 0;
        var reachedExit = false;

        foreach (var move in moves)
        {
            usedMoves++;
            var next = ApplyMove(position, move);

            if (IsWall(next.X, next.Y))
            {
                collisions++;
            }
            else
            {
                position = next;
                path.Add(position);
            }

            if (position == Exit)
            {
                reachedExit = true;
                break;
            }
        }

        var distance = Math.Abs(position.X - Exit.X) + Math.Abs(position.Y - Exit.Y);
        return new MazeEvaluation(path, position, distance, collisions, usedMoves, reachedExit);
    }

    public bool IsWall(int x, int y)
    {
        if (x < 0 || y < 0 || y >= Height || x >= Width)
        {
            return true;
        }

        return Layout[y][x] == '#';
    }

    private MazePoint Find(char marker)
    {
        for (int y = 0; y < Layout.Length; y++)
        {
            var x = Layout[y].IndexOf(marker);

            if (x >= 0)
            {
                return new MazePoint(x, y);
            }
        }

        throw new InvalidOperationException($"Maze marker '{marker}' was not found.");
    }

    private static MazePoint ApplyMove(MazePoint point, int move)
    {
        return move switch
        {
            0 => point with { Y = point.Y - 1 },
            1 => point with { X = point.X + 1 },
            2 => point with { Y = point.Y + 1 },
            3 => point with { X = point.X - 1 },
            _ => point
        };
    }
}

public sealed record MazePoint(int X, int Y);

public sealed record MazeEvaluation(
    IReadOnlyList<MazePoint> Path,
    MazePoint EndPosition,
    int DistanceToExit,
    int Collisions,
    int UsedMoves,
    bool ReachedExit);
