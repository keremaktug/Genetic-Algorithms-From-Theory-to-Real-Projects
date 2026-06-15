namespace GACore;

public interface ISelectionStrategy<TGene>
{
    Chromosome<TGene> Select(IReadOnlyList<Chromosome<TGene>> population, SolverOptions options, Random random);
}
