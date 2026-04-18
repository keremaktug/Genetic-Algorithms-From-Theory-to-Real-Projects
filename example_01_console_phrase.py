from core.ga_solver import (
    CrossoverType,
    GeneticSolver,
    MutationType,
)
from example_02_console_phrase_lib import (
    calculate_fitness,
    decode,
    phrase_generator,
    random_gene,
    stop_condition,
)

def on_iteration(iteration: int, average_fitness: float, best) -> None:
    print(
        f"iter={iteration:04d} "
        f"best={best.fitness:8.2f} "
        f"avg={average_fitness:8.2f} "
        f"text='{decode(best)}'"
    )

def on_solution(iteration: int, solution) -> None:
    print("\nSolution found")
    print(f"Iteration: {iteration}")
    print(f"Phrase    : {decode(solution)}")

def main() -> None:
    solver = GeneticSolver[int](
        population_size=1024 * 8,
        iteration_count=1000,
        elitism_ratio=0.1,
        mutation_ratio=0.1,
        crossover_type=CrossoverType.UNIFORM,
        mutation_type=MutationType.RANDOM_RESET,
        maximize_fitness=False,
    )

    solver.generator_function = phrase_generator
    solver.fitness_function = calculate_fitness
    solver.random_gene_function = random_gene
    solver.stop_condition_function = stop_condition
    solver.iteration_completed_callback = on_iteration
    solver.solution_found_callback = on_solution

    result = solver.evolve()
    if result is None:
        print("\nExact solution not found within iteration limit.")

if __name__ == "__main__":
    main()