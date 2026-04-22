from __future__ import annotations

import queue
import random
import threading
import time
import tkinter as tk
from dataclasses import dataclass
from tkinter import messagebox, ttk

import numpy as np
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure
from sklearn.datasets import load_digits
from sklearn.metrics import accuracy_score, confusion_matrix
from sklearn.model_selection import StratifiedKFold, train_test_split
from sklearn.neural_network import MLPClassifier
from sklearn.preprocessing import StandardScaler

from core.ga_solver1 import Chromosome, CrossoverType, GeneticSolver, MutationType, Population


# =========================================================
# Dataset
# =========================================================

RANDOM_STATE = 42
CV_SPLITS = 3

DATA = load_digits()
X = DATA.data.astype(np.float64)
y = DATA.target.astype(np.int32)
TARGET_NAMES = [str(x) for x in DATA.target_names]
FEATURE_NAMES = [f"px_{i}" for i in range(X.shape[1])]

X_search_all, X_test, y_search_all, y_test = train_test_split(
    X,
    y,
    test_size=0.20,
    random_state=RANDOM_STATE,
    stratify=y,
)

X_train_search, X_val_holdout, y_train_search, y_val_holdout = train_test_split(
    X_search_all,
    y_search_all,
    test_size=0.25,
    random_state=RANDOM_STATE,
    stratify=y_search_all,
)

X_train_final = X_search_all.copy()
y_train_final = y_search_all.copy()

ACTIVATION_OPTIONS = ["relu", "tanh", "logistic"]

EVAL_CACHE: dict[tuple, "EvalResult"] = {}
EVAL_COUNT = 0
CACHE_HIT_COUNT = 0


# =========================================================
# Evaluation result
# =========================================================

@dataclass(frozen=True)
class EvalResult:
    params_key: tuple
    params_text: str
    cv_accuracy_mean: float
    holdout_accuracy: float
    fitness: float
    holdout_confusion: np.ndarray


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


def mlp_generator() -> Chromosome[float]:
    genes = np.random.random(size=6).astype(np.float64)
    return Chromosome(genes.tolist())


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, float(value)))


def decode_genes(chromosome: Chromosome[float]) -> dict:
    """
    g0 -> hidden width        [16..160]
    g1 -> hidden layer count  [1..3]
    g2 -> alpha               logspace [1e-6 .. 1e-1]
    g3 -> learning_rate_init  logspace [1e-4 .. 1e-1]
    g4 -> batch_size          [16..128]
    g5 -> activation          categorical
    """
    genes = [clamp01(g) for g in chromosome.data]
    chromosome.data = genes

    hidden_width = int(round(16 + genes[0] * (160 - 16)))
    hidden_width = max(16, min(160, hidden_width))

    layer_count = int(round(1 + genes[1] * (3 - 1)))
    layer_count = max(1, min(3, layer_count))

    alpha = 10 ** (-6 + genes[2] * 5.0)          # 1e-6 .. 1e-1
    learning_rate_init = 10 ** (-4 + genes[3] * 3.0)  # 1e-4 .. 1e-1

    batch_size = int(round(16 + genes[4] * (128 - 16)))
    batch_size = max(16, min(128, batch_size))

    activation_idx = min(len(ACTIVATION_OPTIONS) - 1, int(genes[5] * len(ACTIVATION_OPTIONS)))
    activation = ACTIVATION_OPTIONS[activation_idx]

    hidden_layer_sizes = tuple([hidden_width] * layer_count)

    return {
        "hidden_width": hidden_width,
        "layer_count": layer_count,
        "hidden_layer_sizes": hidden_layer_sizes,
        "alpha": float(alpha),
        "learning_rate_init": float(learning_rate_init),
        "batch_size": batch_size,
        "activation": activation,
    }


def params_to_key(params: dict) -> tuple:
    return (
        params["hidden_width"],
        params["layer_count"],
        round(params["alpha"], 10),
        round(params["learning_rate_init"], 10),
        params["batch_size"],
        params["activation"],
    )


def params_to_text(params: dict) -> str:
    return (
        f"layers={params['hidden_layer_sizes']}, "
        f"alpha={params['alpha']:.6g}, "
        f"lr={params['learning_rate_init']:.6g}, "
        f"batch={params['batch_size']}, "
        f"activation={params['activation']}"
    )


# =========================================================
# Model helpers
# =========================================================

def make_model(params: dict) -> MLPClassifier:
    return MLPClassifier(
        hidden_layer_sizes=params["hidden_layer_sizes"],
        activation=params["activation"],
        solver="adam",
        alpha=params["alpha"],
        learning_rate_init=params["learning_rate_init"],
        batch_size=params["batch_size"],
        max_iter=120,
        early_stopping=False,
        shuffle=True,
        random_state=RANDOM_STATE,
    )


def fit_scaled_model(x_train: np.ndarray, y_train: np.ndarray, x_eval: np.ndarray, params: dict):
    scaler = StandardScaler()
    x_train_scaled = scaler.fit_transform(x_train)
    x_eval_scaled = scaler.transform(x_eval)

    model = make_model(params)
    model.fit(x_train_scaled, y_train)
    pred = model.predict(x_eval_scaled)
    return model, pred, scaler


# =========================================================
# Fitness evaluation
# =========================================================

def evaluate_params(params: dict) -> EvalResult:
    global EVAL_COUNT, CACHE_HIT_COUNT

    key = params_to_key(params)
    cached = EVAL_CACHE.get(key)
    if cached is not None:
        CACHE_HIT_COUNT += 1
        return cached

    EVAL_COUNT += 1

    cv = StratifiedKFold(n_splits=CV_SPLITS, shuffle=True, random_state=RANDOM_STATE)
    cv_scores: list[float] = []

    for train_idx, val_idx in cv.split(X_train_search, y_train_search):
        x_tr = X_train_search[train_idx]
        y_tr = y_train_search[train_idx]
        x_va = X_train_search[val_idx]
        y_va = y_train_search[val_idx]

        _, pred, _ = fit_scaled_model(x_tr, y_tr, x_va, params)
        cv_scores.append(float(accuracy_score(y_va, pred)))

    cv_accuracy_mean = float(np.mean(cv_scores))

    _, holdout_pred, _ = fit_scaled_model(X_train_search, y_train_search, X_val_holdout, params)
    holdout_accuracy = float(accuracy_score(y_val_holdout, holdout_pred))
    holdout_cm = confusion_matrix(y_val_holdout, holdout_pred, labels=np.unique(y))

    # lower is better
    complexity_penalty = (
        params["hidden_width"] / 5000.0
        + params["layer_count"] / 500.0
        + params["batch_size"] / 5000.0
    )
    fitness = (1.0 - cv_accuracy_mean) + complexity_penalty * 0.01

    result = EvalResult(
        params_key=key,
        params_text=params_to_text(params),
        cv_accuracy_mean=cv_accuracy_mean,
        holdout_accuracy=holdout_accuracy,
        fitness=float(fitness),
        holdout_confusion=holdout_cm,
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
    return (
        f"{result.params_text}, "
        f"cv_acc={result.cv_accuracy_mean:.4f}, "
        f"holdout_acc={result.holdout_accuracy:.4f}, "
        f"fitness={result.fitness:.6f}"
    )


# =========================================================
# Baseline and final selection
# =========================================================

def train_baseline_model() -> dict:
    params = {
        "hidden_width": 64,
        "layer_count": 1,
        "hidden_layer_sizes": (64,),
        "alpha": 1e-4,
        "learning_rate_init": 1e-3,
        "batch_size": 32,
        "activation": "relu",
    }

    _, pred, _ = fit_scaled_model(X_train_search, y_train_search, X_val_holdout, params)
    holdout_acc = float(accuracy_score(y_val_holdout, pred))
    cm = confusion_matrix(y_val_holdout, pred, labels=np.unique(y))

    return {
        "params": params,
        "holdout_accuracy": holdout_acc,
        "holdout_confusion": cm,
    }


def select_top_candidates(k: int = 5) -> list[EvalResult]:
    all_results = list(EVAL_CACHE.values())
    all_results.sort(key=lambda r: (r.fitness, -r.holdout_accuracy))
    return all_results[:k]


def train_final_model_from_candidates(top_candidates: list[EvalResult]) -> dict:
    best_final = None

    for candidate in top_candidates:
        key = candidate.params_key
        params = {
            "hidden_width": key[0],
            "layer_count": key[1],
            "hidden_layer_sizes": tuple([key[0]] * key[1]),
            "alpha": key[2],
            "learning_rate_init": key[3],
            "batch_size": key[4],
            "activation": key[5],
        }

        scaler = StandardScaler()
        x_train_scaled = scaler.fit_transform(X_train_final)
        x_test_scaled = scaler.transform(X_test)

        model = make_model(params)
        model.fit(x_train_scaled, y_train_final)

        test_pred = model.predict(x_test_scaled)
        test_acc = float(accuracy_score(y_test, test_pred))
        cm = confusion_matrix(y_test, test_pred, labels=np.unique(y))

        loss_curve = list(model.loss_curve_) if hasattr(model, "loss_curve_") else []

        item = {
            "params": params,
            "test_accuracy": test_acc,
            "test_confusion": cm,
            "loss_curve": loss_curve,
            "candidate_cv_accuracy": candidate.cv_accuracy_mean,
            "candidate_holdout_accuracy": candidate.holdout_accuracy,
        }

        if best_final is None or item["test_accuracy"] > best_final["test_accuracy"]:
            best_final = item

    assert best_final is not None
    return best_final


# =========================================================
# Population visualization
# =========================================================

def population_to_matrix(population_snapshot: list[list[float]]) -> np.ndarray:
    if not population_snapshot:
        return np.empty((0, 0), dtype=np.float64)
    return np.asarray(population_snapshot, dtype=np.float64)


def generate_color_scheme() -> np.ndarray:
    import colorsys

    colors = np.zeros((256, 3), dtype=np.uint8)
    for i in range(256):
        h = (240 - (240 * i / 255)) / 360.0
        r, g, b = colorsys.hsv_to_rgb(h, 0.9, 0.95)
        colors[i] = (int(r * 255), int(g * 255), int(b * 255))
    return colors


def scale_population_matrix_for_display(matrix: np.ndarray) -> np.ndarray:
    if matrix.size == 0:
        return np.empty((0, 0), dtype=np.uint8)
    scaled = np.clip(matrix, 0.0, 1.0)
    return (scaled * 255).astype(np.uint8)


# =========================================================
# GUI
# =========================================================

class MLPHyperparameterOptimizationApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Machine Learning Hyperparameter Optimization - MLPClassifier")
        self.root.geometry("1450x930")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[float] | None = None
        self.is_running = False
        self.current_best: Chromosome[float] | None = None
        self.cm_colorbar = None

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        self.ui_update_every = 2
        self.pool_redraw_every = 8
        self.queue_poll_ms = 70
        self.max_queue_items_per_tick = 3
        self.max_result_items = 120

        self.baseline_acc_var = tk.StringVar(value="Baseline Holdout Acc: -")
        self.cv_acc_var = tk.StringVar(value="Best CV Accuracy: -")
        self.val_acc_var = tk.StringVar(value="Best Holdout Accuracy: -")
        self.test_acc_var = tk.StringVar(value="Final Test Accuracy: -")
        self.best_fit_var = tk.StringVar(value="Fitness: -")
        self.iteration_var = tk.StringVar(value="Current Iteration: -")
        self.unique_eval_var = tk.StringVar(value="Unique Evals: 0")
        self.cache_hit_var = tk.StringVar(value="Cache Hits: 0")
        self.status_var = tk.StringVar(value="Status: idle")

        self.pool_color_scheme = generate_color_scheme()
        self.pool_image: tk.PhotoImage | None = None

        self.build_ui()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=10, y=10, width=250, height=400)

        tk.Label(controls_frame, text="Crossover Type").pack(anchor="w")
        self.cmb_crossover = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["OnePointCrossover", "UniformCrossover"],
        )
        self.cmb_crossover.current(1)
        self.cmb_crossover.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Elitism Rate").pack(anchor="w")
        self.cmb_elitism = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["0.1", "0.2", "0.3", "0.4", "0.5"],
        )
        self.cmb_elitism.current(2)
        self.cmb_elitism.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Population Factor (×16)").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["1", "2", "4"],
        )
        self.cmb_population.current(1)
        self.cmb_population.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Iteration Count").pack(anchor="w")
        self.cmb_iterations = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["30", "50", "70", "100"],
        )
        self.cmb_iterations.current(1)
        self.cmb_iterations.pack(fill="x", pady=(4, 10))

        self.btn_start = tk.Button(controls_frame, text="Evolve", command=self.start_solver)
        self.btn_start.pack(fill="x", pady=(12, 0))

        tk.Label(controls_frame, textvariable=self.status_var, anchor="w", justify="left").pack(fill="x", pady=(14, 0))

        info_frame = tk.LabelFrame(self.root, text="Best Model", padx=10, pady=10)
        info_frame.place(x=10, y=425, width=250, height=250)

        tk.Label(info_frame, textvariable=self.baseline_acc_var, anchor="w", justify="left").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.cv_acc_var, anchor="w", justify="left").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.val_acc_var, anchor="w", justify="left").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.test_acc_var, anchor="w", justify="left").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.best_fit_var, anchor="w", justify="left").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.iteration_var, anchor="w", justify="left").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.unique_eval_var, anchor="w", justify="left").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.cache_hit_var, anchor="w", justify="left").pack(fill="x", pady=2)

        dataset_frame = tk.LabelFrame(self.root, text="Dataset", padx=10, pady=10)
        dataset_frame.place(x=10, y=690, width=250, height=220)

        dataset_list = tk.Listbox(dataset_frame, font=("Consolas", 9))
        dataset_list.pack(fill="both", expand=True)
        dataset_list.insert(tk.END, "Dataset        : Digits")
        dataset_list.insert(tk.END, f"Samples        : {len(X)}")
        dataset_list.insert(tk.END, f"Features       : {X.shape[1]}")
        dataset_list.insert(tk.END, f"Classes        : {len(np.unique(y))}")
        dataset_list.insert(tk.END, f"Train search   : {len(X_train_search)}")
        dataset_list.insert(tk.END, f"Holdout val    : {len(X_val_holdout)}")
        dataset_list.insert(tk.END, f"Test           : {len(X_test)}")

        best_frame = tk.LabelFrame(self.root, text="Best Chromosomes", padx=5, pady=5)
        best_frame.place(x=275, y=10, width=1160, height=170)

        self.result_list = tk.Listbox(best_frame, font=("Consolas", 9))
        self.result_list.pack(fill="both", expand=True)

        fitness_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        fitness_frame.place(x=275, y=190, width=560, height=280)

        self.fig_fit = Figure(figsize=(5.7, 2.5), dpi=100)
        self.ax_fit = self.fig_fit.add_subplot(111)
        self.ax_fit.set_xlabel("Iteration")
        self.ax_fit.set_ylabel("Fitness")
        self.ax_fit.grid(True, alpha=0.3)
        self.ax_fit.set_xlim(0, 1)
        self.ax_fit.set_ylim(0, 1)

        self.best_line, = self.ax_fit.plot([], [], label="Best Fitness")
        self.avg_line, = self.ax_fit.plot([], [], label="Average Fitness")
        self.ax_fit.legend()

        self.canvas_fit = FigureCanvasTkAgg(self.fig_fit, master=fitness_frame)
        self.canvas_fit.get_tk_widget().pack(fill="both", expand=True)

        pool_frame = tk.LabelFrame(self.root, text="Population Chart", padx=8, pady=8)
        pool_frame.place(x=850, y=190, width=585, height=120)

        self.pool_canvas = tk.Canvas(
            pool_frame,
            width=545,
            height=80,
            bg="white",
            highlightthickness=0,
        )
        self.pool_canvas.pack(fill="both", expand=True)

        cm_frame = tk.LabelFrame(self.root, text="Confusion Matrix", padx=8, pady=8)
        cm_frame.place(x=850, y=320, width=585, height=260)

        self.fig_cm = Figure(figsize=(5.7, 2.3), dpi=100)
        self.ax_cm = self.fig_cm.add_subplot(111)
        self.canvas_cm = FigureCanvasTkAgg(self.fig_cm, master=cm_frame)
        self.canvas_cm.get_tk_widget().pack(fill="both", expand=True)

        loss_frame = tk.LabelFrame(self.root, text="Training Loss Curve", padx=8, pady=8)
        loss_frame.place(x=275, y=480, width=560, height=290)

        self.fig_loss = Figure(figsize=(5.4, 2.5), dpi=100)
        self.ax_loss = self.fig_loss.add_subplot(111)
        self.canvas_loss = FigureCanvasTkAgg(self.fig_loss, master=loss_frame)
        self.canvas_loss.get_tk_widget().pack(fill="both", expand=True)

        notes_frame = tk.LabelFrame(self.root, text="Best Params / Notes", padx=8, pady=8)
        notes_frame.place(x=850, y=590, width=585, height=180)

        self.notes_list = tk.Listbox(notes_frame, font=("Consolas", 9))
        self.notes_list.pack(fill="both", expand=True)

        log_frame = tk.LabelFrame(self.root, text="Output Log", padx=8, pady=8)
        log_frame.place(x=275, y=780, width=1160, height=130)

        self.log_text = tk.Text(
            log_frame,
            font=("Consolas", 8),
            bg="#111111",
            fg="#dddddd",
            insertbackground="#dddddd",
            wrap="none",
        )
        self.log_text.pack(fill="both", expand=True)
        self.log_text.config(state="disabled")

        self.redraw_confusion_matrix(None, title="Holdout Confusion Matrix")
        self.redraw_loss_curve(None)

    def log(self, text: str) -> None:
        ts = time.strftime("%H:%M:%S")
        self.log_text.config(state="normal")
        self.log_text.insert("end", f"[{ts}] {text}\n")
        if int(self.log_text.index("end-1c").split(".")[0]) > 300:
            self.log_text.delete("1.0", "50.0")
        self.log_text.see("end")
        self.log_text.config(state="disabled")

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)
        self.notes_list.delete(0, tk.END)

        self.log_text.config(state="normal")
        self.log_text.delete("1.0", "end")
        self.log_text.config(state="disabled")

        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax_fit.set_xlim(0, 1)
        self.ax_fit.set_ylim(0, 1)
        self.canvas_fit.draw_idle()

        self.pool_canvas.delete("all")
        self.pool_image = None

        self.baseline_acc_var.set("Baseline Holdout Acc: -")
        self.cv_acc_var.set("Best CV Accuracy: -")
        self.val_acc_var.set("Best Holdout Accuracy: -")
        self.test_acc_var.set("Final Test Accuracy: -")
        self.best_fit_var.set("Fitness: -")
        self.iteration_var.set("Current Iteration: -")
        self.unique_eval_var.set("Unique Evals: 0")
        self.cache_hit_var.set("Cache Hits: 0")
        self.status_var.set("Status: idle")

        self.current_best = None
        self.redraw_confusion_matrix(None, title="Holdout Confusion Matrix")
        self.redraw_loss_curve(None)

    def flush_ui_queue(self) -> None:
        while True:
            try:
                self.ui_queue.get_nowait()
            except queue.Empty:
                break

    def map_crossover_type(self) -> CrossoverType:
        if self.cmb_crossover.get() == "OnePointCrossover":
            return CrossoverType.ONE_POINT
        return CrossoverType.UNIFORM

    def start_solver(self) -> None:
        if self.is_running:
            return

        self.clear_ui()
        self.flush_ui_queue()
        reset_eval_stats()

        self.is_running = True
        self.btn_start.config(state="disabled")

        population_factor = int(self.cmb_population.get())
        iteration_count = int(self.cmb_iterations.get())
        elitism_rate = float(self.cmb_elitism.get())
        crossover_type = self.map_crossover_type()

        population_size = 16 * population_factor

        self.status_var.set("Status: baseline")
        self.log("Preparing baseline MLP model...")
        baseline = train_baseline_model()
        self.baseline_acc_var.set(f"Baseline Holdout Acc: {baseline['holdout_accuracy']:.4f}")
        self.redraw_confusion_matrix(baseline["holdout_confusion"], title="Baseline Holdout Confusion Matrix")
        self.log(f"Baseline holdout accuracy = {baseline['holdout_accuracy']:.4f}")

        self.status_var.set("Status: running")
        self.log(f"Starting GA | population={population_size} | iterations={iteration_count}")

        self.solver = GeneticSolver[float](
            population_size=population_size,
            iteration_count=iteration_count,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.08,
            crossover_type=crossover_type,
            mutation_type=MutationType.RANDOM_RESET,
            maximize_fitness=False,
        )

        self.solver.generator_function = mlp_generator
        self.solver.fitness_function = calculate_fitness
        self.solver.random_gene_function = random_gene
        self.solver.stop_condition_function = stop_condition
        self.solver.iteration_completed_callback = self.on_iteration_completed

        threading.Thread(target=self.run_solver, daemon=True).start()

    def run_solver(self) -> None:
        try:
            best = None
            if self.solver is not None:
                result = self.solver.evolve()
                best = result if result is not None else self.solver.population.get_fittest()

            if best is not None:
                self.ui_queue.put(("log", f"Search finished | unique_evals={EVAL_COUNT} | cache_hits={CACHE_HIT_COUNT}"))
                self.ui_queue.put(("log", "Selecting top candidates for final evaluation..."))
                top_candidates = select_top_candidates(5)
                final_result = train_final_model_from_candidates(top_candidates)
                self.ui_queue.put(("finished", best.copy(), final_result))
        except Exception as exc:
            self.ui_queue.put(("error", str(exc)))
        finally:
            self.ui_queue.put(("run_finished",))

    def finish_run(self) -> None:
        self.is_running = False
        self.btn_start.config(state="normal")
        self.status_var.set("Status: idle")
        self.log("Run finished")

    def on_iteration_completed(self, iteration: int, average_fitness: float, best) -> None:
        if iteration != 0 and iteration % self.ui_update_every != 0:
            return

        pool_snapshot = None
        if self.solver is not None and iteration % self.pool_redraw_every == 0:
            pool_snapshot = self.snapshot_population(self.solver.population)

        self.ui_queue.put(("iteration", iteration, average_fitness, best.copy(), pool_snapshot, EVAL_COUNT, CACHE_HIT_COUNT))

    def process_ui_queue(self) -> None:
        processed = 0

        while processed < self.max_queue_items_per_tick:
            try:
                item = self.ui_queue.get_nowait()
            except queue.Empty:
                break

            kind = item[0]

            if kind == "iteration":
                _, iteration, average_fitness, best, pool_snapshot, eval_count, cache_hits = item
                self.update_ui(iteration, average_fitness, best, pool_snapshot, eval_count, cache_hits)

            elif kind == "finished":
                _, best, final_result = item
                self.update_best_panels(best, final_result)

            elif kind == "log":
                _, msg = item
                self.log(msg)

            elif kind == "error":
                _, error_message = item
                self.log(f"Error: {error_message}")
                messagebox.showerror("Error", error_message)

            elif kind == "run_finished":
                self.finish_run()

            processed += 1

        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def append_result(self, text: str) -> None:
        self.result_list.insert(tk.END, text)
        if self.result_list.size() > self.max_result_items:
            overflow = self.result_list.size() - self.max_result_items
            self.result_list.delete(0, overflow - 1)
        self.result_list.yview_moveto(1.0)

    def update_fitness_chart(self) -> None:
        self.best_line.set_data(self.iteration_history, self.best_history)
        self.avg_line.set_data(self.iteration_history, self.avg_history)

        max_x = max(1, self.iteration_history[-1] + 1) if self.iteration_history else 1
        min_y = min(self.best_history + self.avg_history, default=0.0)
        max_y = max(self.best_history + self.avg_history, default=1.0)

        if min_y == max_y:
            min_y -= 1.0
            max_y += 1.0

        self.ax_fit.set_xlim(0, max_x)
        self.ax_fit.set_ylim(min_y * 0.98, max_y * 1.02)
        self.canvas_fit.draw_idle()

    def update_best_panels(self, chromosome: Chromosome[float], final_result: dict | None = None) -> None:
        self.current_best = chromosome

        params = decode_genes(chromosome)
        eval_result = evaluate_params(params)

        self.cv_acc_var.set(f"Best CV Accuracy: {eval_result.cv_accuracy_mean:.4f}")
        self.val_acc_var.set(f"Best Holdout Accuracy: {eval_result.holdout_accuracy:.4f}")
        self.best_fit_var.set(f"Fitness: {eval_result.fitness:.6f}")

        if final_result is not None:
            self.test_acc_var.set(f"Final Test Accuracy: {final_result['test_accuracy']:.4f}")
            self.redraw_confusion_matrix(final_result["test_confusion"], title="Final Test Confusion Matrix")
            self.redraw_loss_curve(final_result["loss_curve"])
            self.log(f"Final selection done | test_acc={final_result['test_accuracy']:.4f}")
            final_params = final_result["params"]
        else:
            self.test_acc_var.set("Final Test Accuracy: -")
            self.redraw_confusion_matrix(eval_result.holdout_confusion, title="Best Holdout Confusion Matrix")
            self.redraw_loss_curve(None)
            final_params = params

        self.notes_list.delete(0, tk.END)
        self.notes_list.insert(tk.END, f"Current best CV acc: {eval_result.cv_accuracy_mean:.4f}")
        self.notes_list.insert(tk.END, f"Current best holdout acc: {eval_result.holdout_accuracy:.4f}")
        self.notes_list.insert(tk.END, f"hidden_layer_sizes: {final_params['hidden_layer_sizes']}")
        self.notes_list.insert(tk.END, f"alpha: {final_params['alpha']:.6g}")
        self.notes_list.insert(tk.END, f"learning_rate_init: {final_params['learning_rate_init']:.6g}")
        self.notes_list.insert(tk.END, f"batch_size: {final_params['batch_size']}")
        self.notes_list.insert(tk.END, f"activation: {final_params['activation']}")

    def redraw_confusion_matrix(self, cm: np.ndarray | None, title: str) -> None:
        self.ax_cm.clear()
        self.ax_cm.set_title(title)

        if self.cm_colorbar is not None:
            self.cm_colorbar.remove()
            self.cm_colorbar = None

        if cm is None:
            self.ax_cm.text(0.5, 0.5, "No matrix yet", ha="center", va="center", transform=self.ax_cm.transAxes)
            self.canvas_cm.draw_idle()
            return

        im = self.ax_cm.imshow(cm, aspect="auto")
        self.ax_cm.set_xticks(range(len(TARGET_NAMES)))
        self.ax_cm.set_yticks(range(len(TARGET_NAMES)))
        self.ax_cm.set_xticklabels(TARGET_NAMES)
        self.ax_cm.set_yticklabels(TARGET_NAMES)
        self.ax_cm.set_xlabel("Predicted")
        self.ax_cm.set_ylabel("Actual")

        for i in range(cm.shape[0]):
            for j in range(cm.shape[1]):
                self.ax_cm.text(j, i, str(cm[i, j]), ha="center", va="center", fontsize=8)

        self.cm_colorbar = self.fig_cm.colorbar(im, ax=self.ax_cm, fraction=0.046, pad=0.04)
        self.canvas_cm.draw_idle()

    def redraw_loss_curve(self, loss_curve: list[float] | None) -> None:
        self.ax_loss.clear()
        self.ax_loss.set_title("Training Loss Curve")

        if not loss_curve:
            self.ax_loss.text(
                0.5,
                0.5,
                "Training loss curve will appear after final selection",
                ha="center",
                va="center",
                transform=self.ax_loss.transAxes,
            )
            self.canvas_loss.draw_idle()
            return

        self.ax_loss.plot(range(1, len(loss_curve) + 1), loss_curve)
        self.ax_loss.set_xlabel("Epoch")
        self.ax_loss.set_ylabel("Loss")
        self.ax_loss.grid(True, alpha=0.3)
        self.canvas_loss.draw_idle()

    def update_ui(
        self,
        iteration: int,
        average_fitness: float,
        best,
        population_snapshot,
        eval_count: int,
        cache_hits: int,
    ) -> None:
        params = decode_genes(best)
        eval_result = evaluate_params(params)

        self.append_result(
            f"{iteration:04d}: cv_acc={eval_result.cv_accuracy_mean:.4f} | holdout_acc={eval_result.holdout_accuracy:.4f} | "
            f"fitness={eval_result.fitness:.6f} | {eval_result.params_text}"
        )

        if iteration == 0 or iteration % 5 == 0:
            self.log(
                f"iter={iteration} | best_cv={eval_result.cv_accuracy_mean:.4f} | "
                f"holdout={eval_result.holdout_accuracy:.4f} | unique_evals={eval_count} | cache_hits={cache_hits}"
            )

        self.iteration_var.set(f"Current Iteration: {iteration}")
        self.unique_eval_var.set(f"Unique Evals: {eval_count}")
        self.cache_hit_var.set(f"Cache Hits: {cache_hits}")

        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.update_fitness_chart()
        self.update_best_panels(best)

        if population_snapshot is not None:
            self.draw_population_chart(population_snapshot)

    def snapshot_population(self, population: Population[float]) -> np.ndarray:
        return population_to_matrix([chromosome.data for chromosome in population.chromosomes])

    def draw_population_chart(self, population_snapshot: np.ndarray) -> None:
        if population_snapshot.size == 0:
            return

        canvas_width = max(1, int(self.pool_canvas.winfo_width()))
        canvas_height = max(1, int(self.pool_canvas.winfo_height()))

        rows, cols = population_snapshot.shape
        if rows == 0 or cols == 0:
            return

        scaled = scale_population_matrix_for_display(population_snapshot)

        row_idx = np.linspace(0, rows - 1, canvas_height).astype(np.int32)
        col_idx = np.linspace(0, cols - 1, canvas_width).astype(np.int32)

        sampled = scaled[row_idx][:, col_idx]
        rgb = self.pool_color_scheme[sampled]

        ppm_header = f"P6 {canvas_width} {canvas_height} 255\n".encode("ascii")
        ppm_data = ppm_header + rgb.tobytes()

        self.pool_image = tk.PhotoImage(data=ppm_data, format="PPM")
        self.pool_canvas.delete("all")
        self.pool_canvas.create_image(0, 0, anchor="nw", image=self.pool_image)

    def run(self) -> None:
        self.root.mainloop()

if __name__ == "__main__":
    root = tk.Tk()
    app = MLPHyperparameterOptimizationApp(root)
    app.run()