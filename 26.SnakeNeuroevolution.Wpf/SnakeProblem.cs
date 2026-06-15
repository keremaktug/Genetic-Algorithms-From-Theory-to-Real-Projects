using GACore;

namespace _26.SnakeNeuroevolution.Wpf;

public sealed class SnakeProblem : IGeneticProblem<double>
{
    public const int BoardSize = 14;
    public const int InputCount = 11;
    public const int HiddenCount = 12;
    public const int OutputCount = 3;
    public const int WeightCount = InputCount * HiddenCount + HiddenCount + HiddenCount * OutputCount + OutputCount;
    private static readonly int[] Seeds = [11, 29, 47];

    public Chromosome<double> CreateChromosome(Random random)
    {
        var weights = new double[WeightCount];

        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = NextGaussian(random) * 0.7;
        }

        return new Chromosome<double>(weights);
    }

    public double CalculateFitness(Chromosome<double> chromosome)
    {
        return Seeds.Average(seed => Simulate(chromosome.Genes, seed).Fitness);
    }

    public static SnakeSimulation Simulate(IReadOnlyList<double> weights, int seed = 11)
    {
        var random = new Random(seed);
        var snake = new LinkedList<GridPoint>();
        var center = BoardSize / 2;
        snake.AddFirst(new GridPoint(center, center));
        snake.AddLast(new GridPoint(center - 1, center));
        snake.AddLast(new GridPoint(center - 2, center));

        var direction = Direction.Right;
        var food = PlaceFood(random, snake);
        var frames = new List<SnakeFrame>();
        var steps = 0;
        var foodEaten = 0;
        var idleSteps = 0;
        var closestFoodDistance = Manhattan(snake.First!.Value, food);
        var approachScore = 0.0;
        var crashed = false;
        const int maxSteps = 520;

        frames.Add(new SnakeFrame(snake.ToArray(), food, direction, false));

        while (steps < maxSteps && idleSteps < 95)
        {
            var action = SnakePolicyNetwork.ChooseAction(weights, snake, food, direction);
            direction = Turn(direction, action);
            var head = snake.First!.Value;
            var nextHead = Move(head, direction);
            steps++;
            idleSteps++;

            if (IsWall(nextHead) || snake.Contains(nextHead))
            {
                crashed = true;
                frames.Add(new SnakeFrame(snake.ToArray(), food, direction, true));
                break;
            }

            snake.AddFirst(nextHead);

            if (nextHead == food)
            {
                foodEaten++;
                idleSteps = 0;
                closestFoodDistance = BoardSize * 2;
                food = PlaceFood(random, snake);
            }
            else
            {
                snake.RemoveLast();
            }

            var distance = Manhattan(nextHead, food);
            if (distance < closestFoodDistance)
            {
                approachScore += 0.8;
                closestFoodDistance = distance;
            }
            else
            {
                approachScore -= 0.05;
            }

            frames.Add(new SnakeFrame(snake.ToArray(), food, direction, false));
        }

        var fitness = steps * 0.12 + approachScore + foodEaten * foodEaten * 35 + foodEaten * 80;
        if (!crashed)
        {
            fitness += 25;
        }

        return new SnakeSimulation(frames, frames[^1], fitness, foodEaten, steps, crashed);
    }

    private static GridPoint PlaceFood(Random random, LinkedList<GridPoint> snake)
    {
        while (true)
        {
            var point = new GridPoint(random.Next(BoardSize), random.Next(BoardSize));
            if (!snake.Contains(point))
            {
                return point;
            }
        }
    }

    private static Direction Turn(Direction direction, SnakeAction action)
    {
        if (action == SnakeAction.Straight)
        {
            return direction;
        }

        return (direction, action) switch
        {
            (Direction.Up, SnakeAction.Left) => Direction.Left,
            (Direction.Up, SnakeAction.Right) => Direction.Right,
            (Direction.Right, SnakeAction.Left) => Direction.Up,
            (Direction.Right, SnakeAction.Right) => Direction.Down,
            (Direction.Down, SnakeAction.Left) => Direction.Right,
            (Direction.Down, SnakeAction.Right) => Direction.Left,
            (Direction.Left, SnakeAction.Left) => Direction.Down,
            (Direction.Left, SnakeAction.Right) => Direction.Up,
            _ => direction
        };
    }

    public static GridPoint Move(GridPoint point, Direction direction)
    {
        return direction switch
        {
            Direction.Up => point with { Y = point.Y - 1 },
            Direction.Right => point with { X = point.X + 1 },
            Direction.Down => point with { Y = point.Y + 1 },
            Direction.Left => point with { X = point.X - 1 },
            _ => point
        };
    }

    public static bool IsWall(GridPoint point)
    {
        return point.X < 0 || point.Y < 0 || point.X >= BoardSize || point.Y >= BoardSize;
    }

    public static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static double NextGaussian(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}

public sealed class SnakeWeightMutation : IMutationOperator<double>
{
    public void Mutate(Chromosome<double> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = Math.Clamp(chromosome.Genes[i] + NextGaussian(random) * 0.22, -4.0, 4.0);
            }
        }
    }

    private static double NextGaussian(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}

public static class SnakePolicyNetwork
{
    public static SnakeAction ChooseAction(IReadOnlyList<double> weights, LinkedList<GridPoint> snake, GridPoint food, Direction direction)
    {
        var inputs = BuildInputs(snake, food, direction);
        var hidden = new double[SnakeProblem.HiddenCount];
        var index = 0;

        for (int h = 0; h < hidden.Length; h++)
        {
            var sum = 0.0;
            for (int i = 0; i < inputs.Length; i++)
            {
                sum += inputs[i] * weights[index++];
            }

            sum += weights[index++];
            hidden[h] = Math.Tanh(sum);
        }

        var bestIndex = 0;
        var bestValue = double.NegativeInfinity;

        for (int output = 0; output < SnakeProblem.OutputCount; output++)
        {
            var sum = 0.0;
            for (int h = 0; h < hidden.Length; h++)
            {
                sum += hidden[h] * weights[index++];
            }

            sum += weights[index++];

            if (sum > bestValue)
            {
                bestValue = sum;
                bestIndex = output;
            }
        }

        return bestIndex switch
        {
            0 => SnakeAction.Left,
            1 => SnakeAction.Straight,
            _ => SnakeAction.Right
        };
    }

    private static double[] BuildInputs(LinkedList<GridPoint> snake, GridPoint food, Direction direction)
    {
        var head = snake.First!.Value;
        var leftDirection = RelativeDirection(direction, SnakeAction.Left);
        var rightDirection = RelativeDirection(direction, SnakeAction.Right);
        var straightDirection = direction;
        var body = snake.Skip(1).ToHashSet();

        return
        [
            Danger(head, leftDirection, body),
            Danger(head, straightDirection, body),
            Danger(head, rightDirection, body),
            food.X < head.X ? 1 : 0,
            food.X > head.X ? 1 : 0,
            food.Y < head.Y ? 1 : 0,
            food.Y > head.Y ? 1 : 0,
            direction == Direction.Up ? 1 : 0,
            direction == Direction.Right ? 1 : 0,
            direction == Direction.Down ? 1 : 0,
            direction == Direction.Left ? 1 : 0
        ];
    }

    private static Direction RelativeDirection(Direction direction, SnakeAction action)
    {
        if (action == SnakeAction.Straight)
        {
            return direction;
        }

        return (direction, action) switch
        {
            (Direction.Up, SnakeAction.Left) => Direction.Left,
            (Direction.Up, SnakeAction.Right) => Direction.Right,
            (Direction.Right, SnakeAction.Left) => Direction.Up,
            (Direction.Right, SnakeAction.Right) => Direction.Down,
            (Direction.Down, SnakeAction.Left) => Direction.Right,
            (Direction.Down, SnakeAction.Right) => Direction.Left,
            (Direction.Left, SnakeAction.Left) => Direction.Down,
            (Direction.Left, SnakeAction.Right) => Direction.Up,
            _ => direction
        };
    }

    private static double Danger(GridPoint head, Direction direction, HashSet<GridPoint> body)
    {
        var next = SnakeProblem.Move(head, direction);
        return SnakeProblem.IsWall(next) || body.Contains(next) ? 1.0 : 0.0;
    }
}

public enum Direction
{
    Up,
    Right,
    Down,
    Left
}

public enum SnakeAction
{
    Left,
    Straight,
    Right
}

public readonly record struct GridPoint(int X, int Y);

public sealed record SnakeFrame(IReadOnlyList<GridPoint> Snake, GridPoint Food, Direction Direction, bool Crashed);

public sealed record SnakeSimulation(IReadOnlyList<SnakeFrame> Frames, SnakeFrame FinalFrame, double Fitness, int FoodEaten, int Steps, bool Crashed);
