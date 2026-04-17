from __future__ import annotations

import random
from typing import Final

import numpy as np

from core.ga_solver import (
    Chromosome,
    CrossoverType,
    GeneticSolver,
    MutationType,
)


LETTERS: Final[str] = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,."
PHRASE: Final[str] = "Those who live in glass houses should not throw stones"

LETTER_COUNT: Final[int] = len(LETTERS)
PHRASE_LENGTH: Final[int] = len(PHRASE)

# Character <-> index maps
LETTER_TO_INDEX: dict[str, int] = {ch: i for i, ch in enumerate(LETTERS)}
INDEX_TO_LETTER: dict[int, str] = {i: ch for i, ch in enumerate(LETTERS)}

# NumPy arrays for fast lookup
LETTER_CODES: np.ndarray = np.fromiter((ord(ch) for ch in LETTERS), dtype=np.int32)
PHRASE_CODES: np.ndarray = np.fromiter((ord(ch) for ch in PHRASE), dtype=np.int32)


def encode_text_to_indices(text: str) -> list[int]:
    """
    Convert a text into LETTERS-domain indices.

    Raises:
        ValueError: if text contains a character not present in LETTERS.
    """
    indices: list[int] = []
    for ch in text:
        if ch not in LETTER_TO_INDEX:
            raise ValueError(f"Character {ch!r} is not present in LETTERS")
        indices.append(LETTER_TO_INDEX[ch])
    return indices


def decode_indices(indices: list[int] | np.ndarray) -> str:
    """
    Convert LETTERS-domain indices back into text.
    """
    idx = np.asarray(indices, dtype=np.int32)
    codes = LETTER_CODES[idx]
    return "".join(chr(x) for x in codes.tolist())


def decode(chromosome: Chromosome[int]) -> str:
    """
    Decode a chromosome whose genes are LETTERS-domain integer indices.
    """
    return decode_indices(chromosome.data)


def random_gene() -> int:
    """
    Generate a random gene in the valid LETTERS index domain.
    """
    return random.randrange(LETTER_COUNT)


def phrase_generator() -> Chromosome[int]:
    """
    Generate a random chromosome for the target phrase problem.

    Each gene is an integer index into LETTERS.
    """
    genes = np.random.randint(
        low=0,
        high=LETTER_COUNT,
        size=PHRASE_LENGTH,
        dtype=np.int32,
    )
    return Chromosome(genes.tolist())


def calculate_fitness(chromosome: Chromosome[int]) -> float:
    """
    Fitness function for phrase evolution.

    Lower is better.
    Perfect solution => 0.0
    """
    idx = np.asarray(chromosome.data, dtype=np.int32)
    candidate_codes = LETTER_CODES[idx]
    return float(np.abs(PHRASE_CODES - candidate_codes).sum())


def stop_condition(best: Chromosome[int]) -> bool:
    """
    Stop when exact phrase is found.
    """
    return best.fitness == 0.0


def chromosome_to_numpy(chromosome: Chromosome[int]) -> np.ndarray:
    """
    Convert a chromosome to a NumPy int32 array.
    """
    return np.asarray(chromosome.data, dtype=np.int32)


def population_to_index_matrix(
    population_snapshot: list[list[int]],
) -> np.ndarray:
    """
    Convert a population snapshot into a 2D NumPy array of indices.

    Returns:
        shape = (population_size, chromosome_length)
    """
    if not population_snapshot:
        return np.empty((0, 0), dtype=np.int32)

    return np.asarray(population_snapshot, dtype=np.int32)


def generate_color_scheme(count: int) -> np.ndarray:
    """
    Generate an HLS-like color scheme as uint8 RGB array.

    Returns:
        shape = (count, 3), dtype=uint8
    """
    import colorsys

    colors = np.zeros((count, 3), dtype=np.uint8)

    for i in range(count):
        h = i / count
        r, g, b = colorsys.hls_to_rgb(h, 0.5, 0.75)
        colors[i] = (int(r * 255), int(g * 255), int(b * 255))

    return colors


def solve_phrase_console(
    population_size: int = 1024 * 8,
    iteration_count: int = 1000,
    elitism_ratio: float = 0.1,
    mutation_ratio: float = 0.1,
    crossover_type: CrossoverType = CrossoverType.UNIFORM,
    mutation_type: MutationType = MutationType.RANDOM_RESET,
    print_every: int = 1,
) -> Chromosome[int] | None:
    """
    Run the phrase evolution problem in console mode.

    This is a convenience wrapper built on top of the generic solver.
    """

    def on_iteration(iteration: int, average_fitness: float, best: Chromosome[int]) -> None:
        if print_every > 0 and iteration % print_every == 0:
            print(
                f"iter={iteration:04d} "
                f"best={best.fitness:8.2f} "
                f"avg={average_fitness:8.2f} "
                f"text='{decode(best)}'"
            )

    def on_solution(iteration: int, solution: Chromosome[int]) -> None:
        print("\nSolution found")
        print(f"Iteration: {iteration}")
        print(f"Phrase    : {decode(solution)}")

    solver = GeneticSolver[int](
        population_size=population_size,
        iteration_count=iteration_count,
        elitism_ratio=elitism_ratio,
        mutation_ratio=mutation_ratio,
        crossover_type=crossover_type,
        mutation_type=mutation_type,
        maximize_fitness=False,
    )

    solver.generator_function = phrase_generator
    solver.fitness_function = calculate_fitness
    solver.random_gene_function = random_gene
    solver.stop_condition_function = stop_condition
    solver.iteration_completed_callback = on_iteration
    solver.solution_found_callback = on_solution

    return solver.evolve()


def run_console_demo() -> None:
    """
    A tiny demo entry point so this file can be run directly.
    """
    result = solve_phrase_console(
        population_size=1024 * 4,
        iteration_count=1000,
        elitism_ratio=0.1,
        mutation_ratio=0.1,
        crossover_type=CrossoverType.UNIFORM,
        mutation_type=MutationType.RANDOM_RESET,
        print_every=1,
    )

    if result is None:
        print("\nExact solution not found within iteration limit.")


if __name__ == "__main__":
    run_console_demo()