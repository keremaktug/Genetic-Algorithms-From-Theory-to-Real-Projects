using GA.Api.Events;
using GA.Api.Types;

namespace GA.Api.Rastrigin
{
    internal class Program
    {
        private static readonly Random rnd = new Random();        

        static void Main(string[] args)
        {
            var g = new GeneticSolver(8 * 1024, 400, 0.25, 0.1, Types.Enums.CrossoverType.OnePointCrossover);
            g.GeneratorFunction = VariablesGenerator;
            g.FitnessFunction = CalculateFitness;
            g.IterationCompleted += G_IterationCompleted;
            g.SolutionFound += G_SolutionFound;
            g.Evolve();
        }

        public static Chromosome VariablesGenerator()
        {
            var variables = new List<object>();                        
            variables.Add(rnd.NextDouble() * 5);            
            variables.Add(rnd.NextDouble() * 5);            
            return new Chromosome(variables);
        }

        public static double CalculateFitness(Chromosome chromosome)
        {
            var x1 = (double)chromosome.Data[0];
            var x2 = (double)chromosome.Data[1];                       
            return Rastrigin(x1, x2);
        }

        public static double Rastrigin(double x1, double x2)
        {
            double A = 10;
            double sum = A * 2;
            sum += (x1 * x2) - A * System.Math.Cos(2 * System.Math.PI * x1);
            sum += (x1 * x2) - A * System.Math.Cos(2 * System.Math.PI * x2);
            return sum;
        }

        private static void G_IterationCompleted(object sender, IterationCompletedEventArgs e)
        {
            var chromosome = e.BestChromosome;
            var x1 = (double)chromosome.Data[0];
            var x2 = (double)chromosome.Data[1];
            Console.WriteLine($"{e.IterationCount} {e.AverageFitness} | x1={x1} x2={x2} = {Rastrigin(x1, x2)}");
        }

        private static void G_SolutionFound(object sender, SolutionFoundEventArgs e)
        {
            var chromosome = e.Solution;
            var x1 = (double)chromosome.Data[0];
            var x2 = (double)chromosome.Data[1];
            Console.WriteLine($"x1={x1} x2={x2} = {Rastrigin(x1, x2)}");
            Console.WriteLine("000000");
        }
    }
}
