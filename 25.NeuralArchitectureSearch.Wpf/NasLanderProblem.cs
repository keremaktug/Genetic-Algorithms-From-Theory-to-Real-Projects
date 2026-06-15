using GACore;

namespace _25.NeuralArchitectureSearch.Wpf;

public sealed class NasLanderProblem : IGeneticProblem<double>
{
    public const int InputCount = 6;
    public const int OutputCount = 3;
    public const int MinHiddenNeurons = 4;
    public const int MaxHiddenNeurons = 12;
    public const int ArchitectureGeneCount = 3;
    public const int MaxWeightCount = InputCount * MaxHiddenNeurons + MaxHiddenNeurons +
                                      MaxHiddenNeurons * MaxHiddenNeurons + MaxHiddenNeurons +
                                      MaxHiddenNeurons * OutputCount + OutputCount;
    public const int GeneCount = ArchitectureGeneCount + MaxWeightCount;

    private static readonly LanderState[] Scenarios =
    [
        new(-0.62, 0.86, 0.24, -0.10, -0.22, 0.03),
        new(0.54, 0.90, -0.20, -0.08, 0.18, -0.02),
        new(-0.24, 0.96, 0.08, -0.12, -0.10, 0.04)
    ];

    public Chromosome<double> CreateChromosome(Random random)
    {
        var genes = new double[GeneCount];
        genes[0] = random.NextDouble();
        genes[1] = random.NextDouble();
        genes[2] = random.NextDouble();

        for (int i = ArchitectureGeneCount; i < genes.Length; i++)
        {
            genes[i] = NextGaussian(random) * 0.75;
        }

        return new Chromosome<double>(genes);
    }

    public double CalculateFitness(Chromosome<double> chromosome)
    {
        var architecture = DecodeArchitecture(chromosome.Genes);
        var total = 0.0;

        foreach (var scenario in Scenarios)
        {
            total += Simulate(chromosome.Genes, scenario).Fitness;
        }

        var modelCost = architecture.UsedWeightCount * 0.035 + architecture.LayerCount * 0.85;
        return total / Scenarios.Length + modelCost;
    }

    public static LanderSimulation Simulate(IReadOnlyList<double> genes)
    {
        return Simulate(genes, Scenarios[0]);
    }

    public static LanderSimulation Simulate(IReadOnlyList<double> genes, LanderState initialState)
    {
        var state = initialState;
        var path = new List<LanderState> { state };
        var totalFuel = 0.0;
        const double dt = 0.045;

        for (int step = 0; step < 430; step++)
        {
            var action = NasPolicyNetwork.Evaluate(genes, state);
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

    public static NetworkArchitecture DecodeArchitecture(IReadOnlyList<double> genes)
    {
        var layerCount = genes[0] < 0.52 ? 1 : 2;
        var hidden1 = DecodeNeuronCount(genes[1]);
        var hidden2 = layerCount == 1 ? 0 : DecodeNeuronCount(genes[2]);
        var usedWeightCount = layerCount == 1
            ? InputCount * hidden1 + hidden1 + hidden1 * OutputCount + OutputCount
            : InputCount * hidden1 + hidden1 + hidden1 * hidden2 + hidden2 + hidden2 * OutputCount + OutputCount;

        return new NetworkArchitecture(layerCount, hidden1, hidden2, usedWeightCount);
    }

    private static int DecodeNeuronCount(double gene)
    {
        var normalized = Clamp(gene, 0.0, 0.999999);
        return MinHiddenNeurons + (int)(normalized * (MaxHiddenNeurons - MinHiddenNeurons + 1));
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

public sealed class NasMutation : IMutationOperator<double>
{
    public void Mutate(Chromosome<double> chromosome, double mutationRate, Random random)
    {
        for (int i = 0; i < chromosome.Genes.Length; i++)
        {
            if (random.NextDouble() >= mutationRate)
            {
                continue;
            }

            if (i < NasLanderProblem.ArchitectureGeneCount)
            {
                chromosome.Genes[i] = Math.Clamp(chromosome.Genes[i] + NextGaussian(random) * 0.16, 0.0, 0.999999);
            }
            else
            {
                chromosome.Genes[i] = Math.Clamp(chromosome.Genes[i] + NextGaussian(random) * 0.24, -4.0, 4.0);
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

public static class NasPolicyNetwork
{
    public static LanderAction Evaluate(IReadOnlyList<double> genes, LanderState state)
    {
        var architecture = NasLanderProblem.DecodeArchitecture(genes);
        var inputs = new[]
        {
            state.X,
            state.Y,
            state.Vx,
            state.Vy,
            state.Angle,
            state.AngularVelocity
        };

        var index = NasLanderProblem.ArchitectureGeneCount;
        var hidden1 = EvaluateLayer(inputs, genes, ref index, architecture.Hidden1);

        if (architecture.LayerCount == 1)
        {
            return EvaluateOutput(hidden1, genes, ref index);
        }

        var hidden2 = EvaluateLayer(hidden1, genes, ref index, architecture.Hidden2);
        return EvaluateOutput(hidden2, genes, ref index);
    }

    private static double[] EvaluateLayer(IReadOnlyList<double> inputs, IReadOnlyList<double> genes, ref int index, int neuronCount)
    {
        var outputs = new double[neuronCount];

        for (int neuron = 0; neuron < neuronCount; neuron++)
        {
            var sum = 0.0;

            for (int input = 0; input < inputs.Count; input++)
            {
                sum += inputs[input] * genes[index++];
            }

            sum += genes[index++];
            outputs[neuron] = Math.Tanh(sum);
        }

        return outputs;
    }

    private static LanderAction EvaluateOutput(IReadOnlyList<double> inputs, IReadOnlyList<double> genes, ref int index)
    {
        Span<double> outputs = stackalloc double[NasLanderProblem.OutputCount];

        for (int output = 0; output < outputs.Length; output++)
        {
            var sum = 0.0;

            for (int input = 0; input < inputs.Count; input++)
            {
                sum += inputs[input] * genes[index++];
            }

            sum += genes[index++];
            outputs[output] = Sigmoid(sum);
        }

        return new LanderAction(outputs[0], outputs[1], outputs[2]);
    }

    private static double Sigmoid(double value) => 1.0 / (1.0 + Math.Exp(-value));
}

public sealed record NetworkArchitecture(int LayerCount, int Hidden1, int Hidden2, int UsedWeightCount)
{
    public IReadOnlyList<int> LayerSizes => LayerCount == 1
        ? [NasLanderProblem.InputCount, Hidden1, NasLanderProblem.OutputCount]
        : [NasLanderProblem.InputCount, Hidden1, Hidden2, NasLanderProblem.OutputCount];

    public override string ToString()
    {
        return LayerCount == 1
            ? $"6 -> {Hidden1} -> 3"
            : $"6 -> {Hidden1} -> {Hidden2} -> 3";
    }
}

public sealed record LanderState(double X, double Y, double Vx, double Vy, double Angle, double AngularVelocity);

public sealed record LanderAction(double Main, double Left, double Right);

public sealed record LanderSimulation(IReadOnlyList<LanderState> Path, LanderState FinalState, double Fitness, bool Landed, double Fuel);
