using System;
using GA.Api.Types;

namespace GA.Api.Events
{
    public class SolutionFoundEventArgs : EventArgs
    {
        public int IterationCount { get; }

        public Chromosome Solution { get; }

        public SolutionFoundEventArgs(int iteration_count, Chromosome solution)
        {
            IterationCount = iteration_count;
            Solution = solution;
        }
    }
}
