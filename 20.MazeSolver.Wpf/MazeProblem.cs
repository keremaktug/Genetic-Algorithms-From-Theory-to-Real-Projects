using GACore;

namespace _20.MazeSolver.Wpf;

public sealed class MazeProblem : IGeneticProblem<int>
{
    private static readonly string[] Layout =
    [
        "#########################################",
        "#S....#...#...#.............#.......#...#",
        "#####.#.#.#.#.#.######..###.#.#####.#.###",
        "#...#...#.#.#.#.....#...#.#...#...#.#...#",
        "#.#.#####.###.#####.#.###.#####.###.#.#.#",
        "#.#.....#.#...#...#.....#...........#.#.#",
        "###.#.###.#.#...#.#.###.#####.####.####.#",
        "#...#.#...#.#.#.#.#...#.....#.#.........#",
        "#.#.###.###.###.#.###.#####.#.###..##.#.#",
        "#.#.#...#...#...#...#...#.#.#...#...#.#.#",
        "#.#.#.###.###.###.#####.#.#.###.###.#.#.#",
        "#.#.#.#...#...#.#.....#...#...#.....#...#",
        "#.###.###.#.###.#####.#.#####.#######.###",
        "#...#...#.#.........#.........#.....#...#",
        "#.#.###.#.#######.#######.#####.#.#.#.#.#",
        "#.#...#.#.......#.#.....#.#.....#.#.#.#.#",
        "#.#.###.###.#####.#.#.#.#.#.#####.#.#.#.#",
        "#.#...#...#.......#.#...#.#.......#.#...#",
        "#.###.###.#.#######.#.###.###.###.#####.#",
        "#...#.....#.......#.#...#...#...#.#.....#",
        "###.###############.###.###.###.#.#.#####",
        "#.#...#.............#.#.....#...#.#.....#",
        "#.###.#.#.#####.##.##.##.####.###.#####.#",
        "#.#...#.#.#.....#.............#...#.....#",
        "#.#.#####.###.#.#.#######.#####.###.#####",
        "#.#.....#...#.#.#.....#...#.......#.#...#",
        "#.#####.###.#.#.#####.#.#########.#.#.###",
        "#.#...#.#...#.#.....#...........#.#.....#",
        "#.#.#.#.#.###.#.#.#.#.#.#.###.#.###.###.#",
        "#...#.....#...#...#...#.......#........E#",
        "#########################################"
    ];

    private readonly int _moveCount;
    private readonly int[,] _distanceToExit;

    public MazeProblem(int moveCount)
    {
        _moveCount = moveCount;
        Start = Find('S');
        Exit = Find('E');
        _distanceToExit = BuildDistanceMap();
    }

    public int Width => Layout[0].Length;

    public int Height => Layout.Length;

    public MazePoint Start { get; }

    public MazePoint Exit { get; }

    public Chromosome<int> CreateChromosome(Random random)
    {
        if (random.NextDouble() < 0.35)
        {
            return CreateGuidedChromosome(random);
        }

        var genes = new int[_moveCount];

        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = random.Next(4);
        }

        return new Chromosome<int>(genes);
    }

    private Chromosome<int> CreateGuidedChromosome(Random random)
    {
        var genes = new int[_moveCount];
        var position = Start;

        for (int i = 0; i < genes.Length; i++)
        {
            var candidates = Enumerable.Range(0, 4)
                .Select(move => new
                {
                    Move = move,
                    Next = ApplyMove(position, move)
                })
                .Where(candidate => !IsWall(candidate.Next.X, candidate.Next.Y))
                .Select(candidate => new
                {
                    candidate.Move,
                    candidate.Next,
                    Distance = GetDistanceToExit(candidate.Next)
                })
                .OrderBy(candidate => candidate.Distance)
                .ToArray();

            if (candidates.Length == 0)
            {
                genes[i] = random.Next(4);
                continue;
            }

            var choice = random.NextDouble() switch
            {
                < 0.68 => candidates[0],
                < 0.90 => candidates[Math.Min(1, candidates.Length - 1)],
                _ => candidates[random.Next(candidates.Length)]
            };

            genes[i] = choice.Move;
            position = choice.Next;

            if (position == Exit)
            {
                FillRemainingMoves(genes, i + 1, random);
                break;
            }
        }

        return new Chromosome<int>(genes);
    }

    private static void FillRemainingMoves(int[] genes, int start, Random random)
    {
        for (int i = start; i < genes.Length; i++)
        {
            genes[i] = random.Next(4);
        }
    }

    public double CalculateFitness(Chromosome<int> chromosome)
    {
        var evaluation = Evaluate(chromosome.Genes);
        return evaluation.ReachedExit
            ? evaluation.UsedMoves + evaluation.Collisions * 2
            : evaluation.DistanceToExit * 12 + evaluation.Collisions * 6 + evaluation.UsedMoves * 0.06;
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

        var distance = GetDistanceToExit(position);
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

    private int GetDistanceToExit(MazePoint point)
    {
        var distance = _distanceToExit[point.Y, point.X];
        return distance >= 0
            ? distance
            : Math.Abs(point.X - Exit.X) + Math.Abs(point.Y - Exit.Y) + Width + Height;
    }

    private int[,] BuildDistanceMap()
    {
        var distances = new int[Height, Width];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                distances[y, x] = -1;
            }
        }

        var queue = new Queue<MazePoint>();
        distances[Exit.Y, Exit.X] = 0;
        queue.Enqueue(Exit);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var nextDistance = distances[current.Y, current.X] + 1;

            foreach (var next in Neighbors(current))
            {
                if (!IsWall(next.X, next.Y) && distances[next.Y, next.X] < 0)
                {
                    distances[next.Y, next.X] = nextDistance;
                    queue.Enqueue(next);
                }
            }
        }

        return distances;
    }

    private static IEnumerable<MazePoint> Neighbors(MazePoint point)
    {
        yield return point with { X = point.X + 1 };
        yield return point with { X = point.X - 1 };
        yield return point with { Y = point.Y + 1 };
        yield return point with { Y = point.Y - 1 };
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

public sealed class MazeMoveMutation : IMutationOperator<int>
{
    public void Mutate(Chromosome<int> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = MutateMove(chromosome.Genes[i], random);
            }
        }

        if (random.NextDouble() < mutationRate * 2.0)
        {
            MutateSmallSegment(chromosome.Genes, random);
        }
    }

    private static int MutateMove(int current, Random random)
    {
        return random.Next(3) switch
        {
            0 => (current + 1) % 4,
            1 => (current + 3) % 4,
            _ => random.Next(4)
        };
    }

    private static void MutateSmallSegment(int[] genes, Random random)
    {
        if (genes.Length < 4)
        {
            return;
        }

        var start = random.Next(genes.Length - 1);
        var maxLength = Math.Min(8, genes.Length - start);
        var length = random.Next(2, maxLength + 1);

        for (int i = start; i < start + length; i++)
        {
            genes[i] = random.Next(4);
        }
    }
}
