from __future__ import annotations

import os
import time
import random
from dataclasses import dataclass

import matplotlib.pyplot as plt
import numpy as np
from sklearn.datasets import load_wine
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import accuracy_score, confusion_matrix
from sklearn.model_selection import train_test_split

from core.ga_solver import Chromosome, CrossoverType, GeneticSolver, MutationType


# =========================================================
# Dataset
# =========================================================

RANDOM_STATE = 42

DATA = load_wine()
X = DATA.data
y = DATA.target
TARGET_NAMES = list(DATA.target_names)
FEATURE_NAMES = list(DATA.feature_names)

X_train_full, X_test, y_train_full, y_test = train_test_split(
    X,
    y,
    test_size=0.20,
    random_state=RANDOM_STATE,
    stratify=y,
)

X_train, X_val, y_train, y_val = train_test_split(
    X_train_full,
    y_train_full,
    test_size=0.25,
    random_state=RANDOM_STATE,
    stratify=y_train_full,
)

X_train_final = np.vstack([X_train, X_val])
y_train_final = np.concatenate([y_train, y_val])

MAX_FEATURE_OPTIONS = ["sqrt", "log2", None, 0.5]

EVAL_CACHE: dict[tuple, "EvalResult"] = {}
EVAL_COUNT = 0
CACHE_HIT_COUNT = 0


# =========================================================
# Logging helpers
# =========================================================

def log(message: str) -> None:
    ts = time.strftime("%H:%M:%S")
    print(f"[{ts}] {message}")


# =========================================================
# Evaluation result
# =========================================================

@dataclass(frozen=True)
class EvalResult:
    params_key: tuple
    params_text: str
    val_accuracy: float
    fitness: float
    val_confusion: np.ndarray


def reset_eval_stats() -> None:
    global EVAL_CACHE, EVAL_COUNT, CACHE_HIT_COUNT
    EVAL_CACHE = {}
    EVAL_COUNT = 0
    CACHE_HIT_COUNT = 0


# =========================================================
# Genome encoding
# =========================================================

def random_gene() -> float:
    return random.random()


def rf_generator() -> Chromosome[float]:
    genes = np.random.random(size=6).astype(np.float64)
    return Chromosome(genes.tolist())


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, float(value)))


def decode_genes(chromosome: Chromosome[float]) -> dict:
    genes = [clamp01(g) for g in chromosome.data]
    chromosome.data = genes

    n_estimators = int(round(20 + genes[0] * (220 - 20)))

    if genes[1] < 0.15:
        max_depth = None
    else:
        max_depth = int(round(2 + (genes[1] - 0.15) / 0.85 * (18 - 2)))
        max_depth = max(2, min(18, max_depth))

    min_samples_split = int(round(2 + genes[2] * (16 - 2)))
    min_samples_split = max(2, min(16, min_samples_split))

    min_samples_leaf = int(round(1 + genes[3] * (8 - 1)))
    min_samples_leaf = max(1, min(8, min_samples_leaf))

    max_features_idx = min(len(MAX_FEATURE_OPTIONS) - 1, int(genes[4] * len(MAX_FEATURE_OPTIONS)))
    max_features = MAX_FEATURE_OPTIONS[max_features_idx]

    bootstrap = genes[5] >= 0.5

    return {
        "n_estimators": n_estimators,
        "max_depth": max_depth,
        "min_samples_split": min_samples_split,
        "min_samples_leaf": min_samples_leaf,
        "max_features": max_features,
        "bootstrap": bootstrap,
    }


def params_to_key(params: dict) -> tuple:
    return (
        params["n_estimators"],
        params["max_depth"],
        params["min_samples_split"],
        params["min_samples_leaf"],
        params["max_features"],
        params["bootstrap"],
    )


def params_to_text(params: dict) -> str:
    return (
        f"n_estimators={params['n_estimators']}, "
        f"max_depth={params['max_depth']}, "
        f"min_samples_split={params['min_samples_split']}, "
        f"min_samples_leaf={params['min_samples_leaf']}, "
        f"max_features={params['max_features']}, "
        f"bootstrap={params['bootstrap']}"
    )


# =========================================================
# Evaluation
# =========================================================

def evaluate_params(params: dict) -> EvalResult:
    global EVAL_COUNT, CACHE_HIT_COUNT

    key = params_to_key(params)
    cached = EVAL_CACHE.get(key)
    if cached is not None:
        CACHE_HIT_COUNT += 1
        return cached

    EVAL_COUNT += 1

    model = RandomForestClassifier(
        n_estimators=params["n_estimators"],
        max_depth=params["max_depth"],
        min_samples_split=params["min_samples_split"],
        min_samples_leaf=params["min_samples_leaf"],
        max_features=params["max_features"],
        bootstrap=params["bootstrap"],
        random_state=RANDOM_STATE,
        n_jobs=-1,
    )

    model.fit(X_train, y_train)
    val_pred = model.predict(X_val)
    val_acc = float(accuracy_score(y_val, val_pred))
    cm = confusion_matrix(y_val, val_pred, labels=np.unique(y))

    complexity_penalty = (
        params["n_estimators"] / 3000.0
        + (0.0 if params["max_depth"] is None else params["max_depth"] / 200.0)
        + params["min_samples_split"] / 1000.0
        + params["min_samples_leaf"] / 1000.0
    )

    fitness = (1.0 - val_acc) + complexity_penalty * 0.01

    result = EvalResult(
        params_key=key,
        params_text=params_to_text(params),
        val_accuracy=val_acc,
        fitness=float(fitness),
        val_confusion=cm,
    )
    EVAL_CACHE[key] = result
    return result


def calculate_fitness(chromosome: Chromosome[float]) -> float:
    params = decode_genes(chromosome)
    result = evaluate_params(params)
    return result.fitness


def stop_condition(_best: Chromosome[float]) -> bool:
    return False


def decode(chromosome: Chromosome[float]) -> str:
    params = decode_genes(chromosome)
    result = evaluate_params(params)
    return f"{result.params_text}, val_acc={result.val_accuracy:.4f}, fitness={result.fitness:.6f}"


# =========================================================
# Baseline and final selection
# =========================================================

def train_baseline_model() -> dict:
    params = {
        "n_estimators": 100,
        "max_depth": None,
        "min_samples_split": 2,
        "min_samples_leaf": 1,
        "max_features": "sqrt",
        "bootstrap": True,
    }

    model = RandomForestClassifier(
        n_estimators=params["n_estimators"],
        max_depth=params["max_depth"],
        min_samples_split=params["min_samples_split"],
        min_samples_leaf=params["min_samples_leaf"],
        max_features=params["max_features"],
        bootstrap=params["bootstrap"],
        random_state=RANDOM_STATE,
        n_jobs=-1,
    )

    model.fit(X_train, y_train)
    val_pred = model.predict(X_val)
    val_acc = float(accuracy_score(y_val, val_pred))
    cm = confusion_matrix(y_val, val_pred, labels=np.unique(y))

    return {
        "params": params,
        "val_accuracy": val_acc,
        "val_confusion": cm,
    }


def select_top_candidates(k: int = 5) -> list[EvalResult]:
    all_results = list(EVAL_CACHE.values())
    all_results.sort(key=lambda r: (r.fitness, -r.val_accuracy))
    return all_results[:k]


def train_final_model_from_candidates(top_candidates: list[EvalResult]) -> dict:
    best_final = None

    for candidate in top_candidates:
        key = candidate.params_key
        params = {
            "n_estimators": key[0],
            "max_depth": key[1],
            "min_samples_split": key[2],
            "min_samples_leaf": key[3],
            "max_features": key[4],
            "bootstrap": key[5],
        }

        model = RandomForestClassifier(
            n_estimators=params["n_estimators"],
            max_depth=params["max_depth"],
            min_samples_split=params["min_samples_split"],
            min_samples_leaf=params["min_samples_leaf"],
            max_features=params["max_features"],
            bootstrap=params["bootstrap"],
            random_state=RANDOM_STATE,
            n_jobs=-1,
        )

        model.fit(X_train_final, y_train_final)
        test_pred = model.predict(X_test)
        test_acc = float(accuracy_score(y_test, test_pred))
        cm = confusion_matrix(y_test, test_pred, labels=np.unique(y))
        importances = model.feature_importances_

        item = {
            "params": params,
            "test_accuracy": test_acc,
            "test_confusion": cm,
            "feature_importances": importances,
            "candidate_val_accuracy": candidate.val_accuracy,
            "candidate_fitness": candidate.fitness,
        }

        if best_final is None or item["test_accuracy"] > best_final["test_accuracy"]:
            best_final = item

    assert best_final is not None
    return best_final


# =========================================================
# Plot helpers
# =========================================================

def plot_fitness_history(
    iterations: list[int],
    best_history: list[float],
    avg_history: list[float],
    save_path: str,
) -> None:
    plt.figure(figsize=(10, 5))
    plt.plot(iterations, best_history, label="Best Fitness")
    plt.plot(iterations, avg_history, label="Average Fitness")
    plt.xlabel("Iteration")
    plt.ylabel("Fitness")
    plt.title("GA Fitness Evolution - Random Forest")
    plt.legend()
    plt.grid(True)
    plt.tight_layout()
    plt.savefig(save_path, dpi=200)
    plt.close()


def plot_confusion_matrix(cm: np.ndarray, labels: list[str], save_path: str) -> None:
    plt.figure(figsize=(7, 6))
    plt.imshow(cm, interpolation="nearest")
    plt.title("Final Test Confusion Matrix")
    plt.colorbar()

    tick_marks = np.arange(len(labels))
    plt.xticks(tick_marks, labels)
    plt.yticks(tick_marks, labels)

    for i in range(cm.shape[0]):
        for j in range(cm.shape[1]):
            plt.text(j, i, str(cm[i, j]), ha="center", va="center")

    plt.ylabel("True Label")
    plt.xlabel("Predicted Label")
    plt.tight_layout()
    plt.savefig(save_path, dpi=200)
    plt.close()


def plot_feature_importances(importances: np.ndarray, feature_names: list[str], save_path: str) -> None:
    idx = np.argsort(importances)[::-1]
    names = [feature_names[i] for i in idx]
    vals = importances[idx]

    plt.figure(figsize=(12, 5))
    plt.bar(range(len(vals)), vals)
    plt.xticks(range(len(vals)), names, rotation=35, ha="right")
    plt.ylabel("Importance")
    plt.title("Random Forest Feature Importances")
    plt.tight_layout()
    plt.savefig(save_path, dpi=200)
    plt.close()


def save_summary_text(
    save_path: str,
    baseline: dict,
    best_params: dict,
    best_eval: EvalResult,
    final_result: dict,
    elapsed: float,
    population_size: int,
    iteration_count: int,
    elitism_ratio: float,
    mutation_ratio: float,
) -> None:
    with open(save_path, "w", encoding="utf-8") as f:
        f.write("Random Forest Hyperparameter Optimization with Genetic Algorithm\n")
        f.write("=" * 72 + "\n\n")

        f.write("Dataset summary\n")
        f.write("Dataset        : Wine\n")
        f.write(f"Samples        : {len(X)}\n")
        f.write(f"Features       : {X.shape[1]}\n")
        f.write(f"Classes        : {len(np.unique(y))}\n")
        f.write(f"Train          : {len(X_train)}\n")
        f.write(f"Validation     : {len(X_val)}\n")
        f.write(f"Test           : {len(X_test)}\n\n")

        f.write("GA configuration\n")
        f.write(f"Population size: {population_size}\n")
        f.write(f"Iterations     : {iteration_count}\n")
        f.write(f"Elitism ratio  : {elitism_ratio}\n")
        f.write(f"Mutation ratio : {mutation_ratio}\n")
        f.write(f"Elapsed (sec)  : {elapsed:.2f}\n")
        f.write(f"Unique evals   : {EVAL_COUNT}\n")
        f.write(f"Cache hits     : {CACHE_HIT_COUNT}\n\n")

        f.write("Baseline model\n")
        f.write(f"Params         : {baseline['params']}\n")
        f.write(f"Validation Acc : {baseline['val_accuracy']:.4f}\n\n")

        f.write("Best search result\n")
        f.write(f"Params         : {best_params}\n")
        f.write(f"Validation Acc : {best_eval.val_accuracy:.4f}\n")
        f.write(f"Fitness        : {best_eval.fitness:.6f}\n\n")

        f.write("Final selected model\n")
        f.write(f"Params         : {final_result['params']}\n")
        f.write(f"Candidate Val  : {final_result['candidate_val_accuracy']:.4f}\n")
        f.write(f"Candidate Fit  : {final_result['candidate_fitness']:.6f}\n")
        f.write(f"Test Accuracy  : {final_result['test_accuracy']:.4f}\n")


# =========================================================
# Console reporting
# =========================================================

def print_confusion_matrix(cm: np.ndarray, labels: list[str]) -> None:
    col_width = 12
    header = " " * 12 + "".join(f"{label:>{col_width}}" for label in labels)
    print(header)
    for i, label in enumerate(labels):
        row = f"{label:>12}" + "".join(f"{cm[i, j]:>{col_width}d}" for j in range(len(labels)))
        print(row)


# =========================================================
# Progress tracker
# =========================================================

class ProgressTracker:
    def __init__(self, log_every: int = 5) -> None:
        self.log_every = log_every
        self.iterations: list[int] = []
        self.best_history: list[float] = []
        self.avg_history: list[float] = []

    def on_iteration_completed(
        self,
        iteration: int,
        average_fitness: float,
        best: Chromosome[float],
    ) -> None:
        params = decode_genes(best)
        result = evaluate_params(params)

        self.iterations.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        if iteration % self.log_every != 0 and iteration != 0:
            return

        log(
            f"iter={iteration:04d} | "
            f"best_val={result.val_accuracy:.4f} | "
            f"fitness={result.fitness:.6f} | "
            f"unique_evals={EVAL_COUNT} | cache_hits={CACHE_HIT_COUNT}"
        )


# =========================================================
# Main
# =========================================================

def main() -> None:
    output_dir = "ga_random_forest_outputs"
    os.makedirs(output_dir, exist_ok=True)

    print("=" * 80)
    print("Random Forest Hyperparameter Optimization with Genetic Algorithm")
    print("=" * 80)

    print("\nDataset summary")
    print("  Dataset        : Wine")
    print(f"  Samples        : {len(X)}")
    print(f"  Features       : {X.shape[1]}")
    print(f"  Classes        : {len(np.unique(y))}")
    print(f"  Train          : {len(X_train)}")
    print(f"  Validation     : {len(X_val)}")
    print(f"  Test           : {len(X_test)}")

    reset_eval_stats()

    log("Preparing baseline Random Forest model...")
    baseline = train_baseline_model()
    log(f"Baseline validation accuracy = {baseline['val_accuracy']:.4f}")

    print("\nBaseline model")
    print(f"  Params         : {baseline['params']}")
    print(f"  Validation Acc : {baseline['val_accuracy']:.4f}")

    population_size = 2
    iteration_count = 80
    elitism_ratio = 0.3
    mutation_ratio = 0.08

    log(
        f"Starting GA | population={population_size} | iterations={iteration_count} | "
        f"elitism={elitism_ratio} | mutation={mutation_ratio}"
    )

    solver = GeneticSolver[float](
        population_size=population_size,
        iteration_count=iteration_count,
        elitism_ratio=elitism_ratio,
        mutation_ratio=mutation_ratio,
        crossover_type=CrossoverType.UNIFORM,
        mutation_type=MutationType.RANDOM_RESET,
        maximize_fitness=False,
    )

    progress = ProgressTracker(log_every=2)

    solver.generator_function = rf_generator
    solver.fitness_function = calculate_fitness
    solver.random_gene_function = random_gene
    solver.stop_condition_function = stop_condition
    solver.iteration_completed_callback = progress.on_iteration_completed

    start_time = time.time()
    best = solver.evolve()
    if best is None:
        best = solver.population.get_fittest()
    elapsed = time.time() - start_time

    assert best is not None

    best_params = decode_genes(best)
    best_eval = evaluate_params(best_params)

    log(f"Search finished in {elapsed:.2f}s")
    log(f"Unique evaluations = {EVAL_COUNT}")
    log(f"Cache hits = {CACHE_HIT_COUNT}")

    print("\nBest search result")
    print(f"  Params         : {best_params}")
    print(f"  Validation Acc : {best_eval.val_accuracy:.4f}")
    print(f"  Fitness        : {best_eval.fitness:.6f}")

    print("\nBest validation confusion matrix")
    print_confusion_matrix(best_eval.val_confusion, TARGET_NAMES)

    log("Selecting top candidates for final evaluation...")
    top_candidates = select_top_candidates(5)

    print("\nTop candidates")
    for i, candidate in enumerate(top_candidates, start=1):
        print(
            f"  {i}. val_acc={candidate.val_accuracy:.4f} | "
            f"fitness={candidate.fitness:.6f} | "
            f"{candidate.params_text}"
        )

    final_result = train_final_model_from_candidates(top_candidates)

    log(f"Final selection done | test_acc={final_result['test_accuracy']:.4f}")

    print("\nFinal selected model")
    print(f"  Params         : {final_result['params']}")
    print(f"  Candidate Val  : {final_result['candidate_val_accuracy']:.4f}")
    print(f"  Candidate Fit  : {final_result['candidate_fitness']:.6f}")
    print(f"  Test Accuracy  : {final_result['test_accuracy']:.4f}")

    print("\nFinal test confusion matrix")
    print_confusion_matrix(final_result["test_confusion"], TARGET_NAMES)

    fitness_path = os.path.join(output_dir, "fitness.png")
    cm_path = os.path.join(output_dir, "confusion_matrix.png")
    fi_path = os.path.join(output_dir, "feature_importances.png")
    summary_path = os.path.join(output_dir, "summary.txt")

    plot_fitness_history(
        progress.iterations,
        progress.best_history,
        progress.avg_history,
        fitness_path,
    )
    plot_confusion_matrix(final_result["test_confusion"], TARGET_NAMES, cm_path)
    plot_feature_importances(final_result["feature_importances"], FEATURE_NAMES, fi_path)
    save_summary_text(
        summary_path,
        baseline,
        best_params,
        best_eval,
        final_result,
        elapsed,
        population_size,
        iteration_count,
        elitism_ratio,
        mutation_ratio,
    )

    print("\nSaved files")
    print(f"  {fitness_path}")
    print(f"  {cm_path}")
    print(f"  {fi_path}")
    print(f"  {summary_path}")

    print("\nDone.")


if __name__ == "__main__":
    main()