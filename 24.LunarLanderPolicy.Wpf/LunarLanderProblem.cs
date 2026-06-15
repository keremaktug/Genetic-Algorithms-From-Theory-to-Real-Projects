using GACore;

namespace _24.LunarLanderPolicy.Wpf;

public sealed class LunarLanderProblem : IGeneticProblem<double>
{
    public const int InputCount = 6;
    public const int HiddenCount = 8;
    public const int OutputCount = 3;
    public const int WeightCount = InputCount * HiddenCount + HiddenCount + HiddenCount * OutputCount + OutputCount;

    public Chromosome<double> CreateChromosome(Random random)
    {
        var weights = new double[WeightCount];

        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = NextGaussian(random) * 0.8;
        }

        return new Chromosome<double>(weights);
    }

    public double CalculateFitness(Chromosome<double> chromosome)
    {
        return Simulate(chromosome.Genes).Fitness;
    }

    public static LanderSimulation Simulate(IReadOnlyList<double> weights)
    {
        var state = new LanderState(-0.62, 0.86, 0.24, -0.10, -0.22, 0.03);
        var path = new List<LanderState> { state };
        var totalFuel = 0.0;
        const double dt = 0.045;

        for (int step = 0; step < 420; step++)
        {
            var action = PolicyNetwork.Evaluate(weights, state);
            totalFuel += action.Main + action.Left + action.Right;

            var sideThrust = (action.Right - action.Left) * 0.42;
            var mainThrust = action.Main * 1.25;
            var angle = state.Angle;
            var ax = Math.Sin(angle) * mainThrust + sideThrust;
            var ay = Math.Cos(angle) * mainThrust - 0.36;
            var angularAcceleration = (action.Right - action.Left) * 0.72 - angle * 0.035;

            state = state with
            {
                Vx = Clamp(state.Vx + ax * dt, -2.2, 2.2),
                Vy = Clamp(state.Vy + ay * dt, -2.2, 2.2),
                AngularVelocity = Clamp(state.AngularVelocity + angularAcceleration * dt, -2.2, 2.2)
            };

            state = state with
            {
                X = state.X + state.Vx * dt,
                Y = state.Y + state.Vy * dt,
                Angle = NormalizeAngle(state.Angle + state.AngularVelocity * dt)
            };

            path.Add(state);

            if (state.Y <= 0)
            {
                break;
            }

            if (Math.Abs(state.X) > 1.25 || state.Y > 1.18)
            {
                break;
            }
        }

        var landed = state.Y <= 0 &&
            Math.Abs(state.X) <= 0.16 &&
            Math.Abs(state.Vx) <= 0.14 &&
            Math.Abs(state.Vy) <= 0.18 &&
            Math.Abs(state.Angle) <= 0.16 &&
            Math.Abs(state.AngularVelocity) <= 0.25;

        var penalty =
            Math.Abs(state.X) * 95 +
            Math.Abs(state.Y) * 55 +
            Math.Abs(state.Vx) * 145 +
            Math.Abs(state.Vy) * 160 +
            Math.Abs(state.Angle) * 80 +
            Math.Abs(state.AngularVelocity) * 38 +
            totalFuel * 0.018;

        if (state.Y > 0)
        {
            penalty += 60;
        }

        if (!landed && state.Y <= 0)
        {
            penalty += 45;
        }

        if (landed)
        {
            penalty *= 0.18;
        }

        return new LanderSimulation(path, state, penalty, landed, totalFuel);
    }

    private static double NextGaussian(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    private static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI) angle -= Math.PI * 2;
        while (angle < -Math.PI) angle += Math.PI * 2;
        return angle;
    }
}

public sealed class NeuralWeightMutation : IMutationOperator<double>
{
    public void Mutate(Chromosome<double> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                chromosome.Genes[i] = Math.Clamp(chromosome.Genes[i] + NextGaussian(random) * 0.25, -4.0, 4.0);
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

public static class PolicyNetwork
{
    public static LanderAction Evaluate(IReadOnlyList<double> weights, LanderState state)
    {
        var inputs = new[]
        {
            state.X,
            state.Y,
            state.Vx,
            state.Vy,
            state.Angle,
            state.AngularVelocity
        };

        var hidden = new double[LunarLanderProblem.HiddenCount];
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

        var outputs = new double[LunarLanderProblem.OutputCount];

        for (int o = 0; o < outputs.Length; o++)
        {
            var sum = 0.0;

            for (int h = 0; h < hidden.Length; h++)
            {
                sum += hidden[h] * weights[index++];
            }

            sum += weights[index++];
            outputs[o] = Sigmoid(sum);
        }

        return new LanderAction(outputs[0], outputs[1], outputs[2]);
    }

    private static double Sigmoid(double value) => 1.0 / (1.0 + Math.Exp(-value));
}

public sealed record LanderState(double X, double Y, double Vx, double Vy, double Angle, double AngularVelocity);

public sealed record LanderAction(double Main, double Left, double Right);

public sealed record LanderSimulation(IReadOnlyList<LanderState> Path, LanderState FinalState, double Fitness, bool Landed, double Fuel);
