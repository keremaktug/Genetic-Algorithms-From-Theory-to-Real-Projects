from __future__ import annotations

import random
from dataclasses import dataclass
from typing import Final

import numpy as np

from core.ga_solver1 import Chromosome


@dataclass(frozen=True)
class Item:
    item_id: int
    weight: int
    value: int


CAPACITY: Final[int] = 35

ITEMS: list[Item] = [
    Item(1, 2, 6),
    Item(2, 3, 5),
    Item(3, 6, 8),
    Item(4, 7, 9),
    Item(5, 5, 6),
    Item(6, 9, 7),
    Item(7, 4, 5),
    Item(8, 3, 4),
    Item(9, 8, 11),
    Item(10, 10, 13),
    Item(11, 11, 15),
    Item(12, 13, 18),
    Item(13, 1, 2),
    Item(14, 12, 16),
]

ITEM_COUNT: Final[int] = len(ITEMS)

WEIGHTS = np.asarray([item.weight for item in ITEMS], dtype=np.int32)
VALUES = np.asarray([item.value for item in ITEMS], dtype=np.int32)


def random_gene() -> int:
    return random.randint(0, 1)


def knapsack_generator() -> Chromosome[int]:
    genes = np.random.randint(0, 2, size=ITEM_COUNT, dtype=np.int32)
    return Chromosome(genes.tolist())


def chromosome_to_numpy(chromosome: Chromosome[int]) -> np.ndarray:
    return np.asarray(chromosome.data, dtype=np.int32)


def calculate_totals(chromosome: Chromosome[int]) -> tuple[int, int]:
    genes = chromosome_to_numpy(chromosome)
    total_weight = int(np.dot(genes, WEIGHTS))
    total_value = int(np.dot(genes, VALUES))
    return total_weight, total_value


def calculate_fitness(chromosome: Chromosome[int]) -> float:
    """
    Lower is better.

    Strategy:
    - If within capacity: reward higher value by making score smaller
    - If overweight: add strong penalty
    """
    total_weight, total_value = calculate_totals(chromosome)

    if total_weight <= CAPACITY:
        # Better solutions have smaller fitness
        return float(-total_value)

    overweight = total_weight - CAPACITY
    penalty = 1000 + overweight * 100 + total_weight
    return float(penalty)


def stop_condition(_best: Chromosome[int]) -> bool:
    # Knapsack usually doesn't have a known exact stop target here.
    return False


def decode(chromosome: Chromosome[int]) -> str:
    selected_ids = [str(ITEMS[i].item_id) for i, gene in enumerate(chromosome.data) if gene == 1]
    total_weight, total_value = calculate_totals(chromosome)
    return (
        f"items=[{', '.join(selected_ids)}] "
        f"weight={total_weight}/{CAPACITY} "
        f"value={total_value}"
    )


def get_selected_items(chromosome: Chromosome[int]) -> list[Item]:
    return [ITEMS[i] for i, gene in enumerate(chromosome.data) if gene == 1]


def population_to_index_matrix(population_snapshot: list[list[int]]) -> np.ndarray:
    if not population_snapshot:
        return np.empty((0, 0), dtype=np.int32)
    return np.asarray(population_snapshot, dtype=np.int32)


def generate_color_scheme() -> np.ndarray:
    """
    0 -> light gray
    1 -> green
    """
    return np.asarray(
        [
            [220, 220, 220],  # not selected
            [60, 180, 75],    # selected
        ],
        dtype=np.uint8,
    )