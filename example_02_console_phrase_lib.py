import random

from core.ga_solver import (
    Chromosome,
    CrossoverType,
    GeneticSolver,
    MutationType,
)


LETTERS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,."
PHRASE = "Those who live in glass houses should not throw stones"


def decode(chromosome: Chromosome[str]) -> str:
    return "".join(chromosome.data)


def random_gene() -> str:
    return random.choice(LETTERS)


def phrase_generator() -> Chromosome[str]:
    characters = [random_gene() for _ in range(len(PHRASE))]
    return Chromosome(characters)


def calculate_fitness(chromosome: Chromosome[str]) -> float:
    return float(
        sum(abs(ord(PHRASE[i]) - ord(chromosome.data[i])) for i in range(len(PHRASE)))
    )


def stop_condition(best: Chromosome[str]) -> bool:
    return best.fitness == 0


def on_iteration(iteration: int, average_fitness: float, best: Chromosome[str]) -> None:
    print(
        f"iter={iteration:04d} "
        f"best={best.fitness:8.2f} "
        f"avg={average_fitness:8.2f} "
        f"text='{decode(best)}'"
    )


def on_solution(iteration: int, solution: Chromosome[str]) -> None:
    print("\nSolution found")
    print(f"Iteration: {iteration}")
    print(f"Phrase    : {decode(solution)}")


def main() -> None:
    solver = GeneticSolver[str](
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