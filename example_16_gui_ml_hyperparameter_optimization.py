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
from sklearn.datasets import load_wine
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import accuracy_score, confusion_matrix
from sklearn.model_selection import train_test_split

from core.ga_solver import Chromosome, CrossoverType, GeneticSolver, MutationType, Population


# =========================================================
# ML Hyperparameter Optimization - Random Forest on Wine
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


@dataclass(frozen=True)
class EvalResult:
    params_key: tuple
    params_text: str
    val_accuracy: float
    fitness: float
    val_confusion: np.ndarray


EVAL_CACHE: dict[tuple, EvalResult] = {}


def random_gene() -> float:
    return random.random()


def ml_generator() -> Chromosome[float]:
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


def evaluate_params(params: dict) -> EvalResult:
    key = params_to_key(params)
    cached = EVAL_CACHE.get(key)
    if cached is not None:
        return cached

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


def stop_condition(best: Chromosome[float]) -> bool:
    params = decode_genes(best)
    result = evaluate_params(params)
    return result.val_accuracy >= 0.99


def decode(chromosome: Chromosome[float]) -> str:
    params = decode_genes(chromosome)
    result = evaluate_params(params)
    return f"{result.params_text}, val_acc={result.val_accuracy:.4f}, fitness={result.fitness:.6f}"


def train_final_model(best: Chromosome[float]) -> dict:
    params = decode_genes(best)

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

    return {
        "params": params,
        "test_accuracy": test_acc,
        "test_confusion": cm,
        "feature_importances": importances,
    }


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


class MLHyperparameterOptimizationApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Machine Learning Hyperparameter Optimization - Random Forest")
        self.root.geometry("1450x1200")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[float] | None = None
        self.is_running = False
        self.current_best: Chromosome[float] | None = None
        self.cm_colorbar = None

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        self.ui_update_every = 3
        self.pool_redraw_every = 10
        self.queue_poll_ms = 80
        self.max_queue_items_per_tick = 2
        self.max_result_items = 100

        self.val_acc_var = tk.StringVar(value="Validation Accuracy: -")
        self.test_acc_var = tk.StringVar(value="Test Accuracy: -")
        self.best_fit_var = tk.StringVar(value="Fitness: -")
        self.params_var = tk.StringVar(value="Best Params: -")
        self.status_var = tk.StringVar(value="Status: idle")

        self.pool_color_scheme = generate_color_scheme()
        self.pool_image: tk.PhotoImage | None = None

        self.build_ui()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=10, y=10, width=250, height=380)

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

        tk.Label(controls_frame, text="Population Factor (×8)").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["1", "2", "4", "8"],
        )
        self.cmb_population.current(0)
        self.cmb_population.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Iteration Count").pack(anchor="w")
        self.cmb_iterations = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["30", "50", "80", "120", "200"],
        )
        self.cmb_iterations.current(2)
        self.cmb_iterations.pack(fill="x", pady=(4, 10))

        self.btn_start = tk.Button(controls_frame, text="Evolve", command=self.start_solver)
        self.btn_start.pack(fill="x", pady=(12, 0))

        tk.Label(controls_frame, textvariable=self.status_var, anchor="w", justify="left").pack(fill="x", pady=(14, 0))

        info_frame = tk.LabelFrame(self.root, text="Best Model", padx=10, pady=10)
        info_frame.place(x=10, y=405, width=250, height=220)

        tk.Label(info_frame, textvariable=self.val_acc_var, anchor="w", justify="left").pack(fill="x", pady=3)
        tk.Label(info_frame, textvariable=self.test_acc_var, anchor="w", justify="left").pack(fill="x", pady=3)
        tk.Label(info_frame, textvariable=self.best_fit_var, anchor="w", justify="left").pack(fill="x", pady=3)
        tk.Label(info_frame, textvariable=self.params_var, anchor="w", justify="left", wraplength=225).pack(fill="x", pady=3)

        dataset_frame = tk.LabelFrame(self.root, text="Dataset", padx=10, pady=10)
        dataset_frame.place(x=10, y=640, width=250, height=240)

        dataset_list = tk.Listbox(dataset_frame, font=("Consolas", 9))
        dataset_list.pack(fill="both", expand=True)
        dataset_list.insert(tk.END, "Dataset        : Wine")
        dataset_list.insert(tk.END, f"Samples        : {len(X)}")
        dataset_list.insert(tk.END, f"Features       : {X.shape[1]}")
        dataset_list.insert(tk.END, f"Classes        : {len(np.unique(y))}")
        dataset_list.insert(tk.END, f"Train          : {len(X_train)}")
        dataset_list.insert(tk.END, f"Validation     : {len(X_val)}")
        dataset_list.insert(tk.END, f"Test           : {len(X_test)}")
        dataset_list.insert(tk.END, f"Target names   : {', '.join(TARGET_NAMES)}")

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

        imp_frame = tk.LabelFrame(self.root, text="Feature Importances", padx=8, pady=8)
        imp_frame.place(x=275, y=480, width=560, height=400)

        self.fig_imp = Figure(figsize=(7.4, 3.3), dpi=100)
        self.ax_imp = self.fig_imp.add_subplot(111)
        self.canvas_imp = FigureCanvasTkAgg(self.fig_imp, master=imp_frame)
        self.canvas_imp.get_tk_widget().pack(fill="both", expand=True)

        notes_frame = tk.LabelFrame(self.root, text="Best Params / Notes", padx=8, pady=8)
        notes_frame.place(x=850, y=590, width=585, height=290)

        self.notes_list = tk.Listbox(notes_frame, font=("Consolas", 9))
        self.notes_list.pack(fill="both", expand=True)

        log_frame = tk.LabelFrame(self.root, text="Output Log", padx=8, pady=8)
        log_frame.place(x=10, y=880, width=1425, height=100)

        self.log_list = tk.Listbox(log_frame, font=("Consolas", 8))
        self.log_list.pack(fill="both", expand=True)

        self.redraw_confusion_matrix(None, title="Validation Confusion Matrix")
        self.redraw_feature_importances(None)

    def log(self, text: str) -> None:
        ts = time.strftime("%H:%M:%S")
        self.log_list.insert(tk.END, f"[{ts}] {text}")
        if self.log_list.size() > 200:
            self.log_list.delete(0, self.log_list.size() - 201)
        self.log_list.yview_moveto(1.0)

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)
        self.notes_list.delete(0, tk.END)
        self.log_list.delete(0, tk.END)

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

        self.val_acc_var.set("Validation Accuracy: -")
        self.test_acc_var.set("Test Accuracy: -")
        self.best_fit_var.set("Fitness: -")
        self.params_var.set("Best Params: -")
        self.status_var.set("Status: idle")

        self.current_best = None
        self.redraw_confusion_matrix(None, title="Validation Confusion Matrix")
        self.redraw_feature_importances(None)

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

        self.is_running = True
        self.btn_start.config(state="disabled")

        population_factor = int(self.cmb_population.get())
        iteration_count = int(self.cmb_iterations.get())
        elitism_rate = float(self.cmb_elitism.get())
        crossover_type = self.map_crossover_type()

        population_size = 8 * population_factor

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

        self.solver.generator_function = ml_generator
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
                self.ui_queue.put(("log", "Training final model on train+val..."))
                final_result = train_final_model(best.copy())
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

        self.ui_queue.put(("iteration", iteration, average_fitness, best.copy(), pool_snapshot))

    def process_ui_queue(self) -> None:
        processed = 0

        while processed < self.max_queue_items_per_tick:
            try:
                item = self.ui_queue.get_nowait()
            except queue.Empty:
                break

            kind = item[0]

            if kind == "iteration":
                _, iteration, average_fitness, best, pool_snapshot = item
                self.update_ui(iteration, average_fitness, best, pool_snapshot)

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

        self.val_acc_var.set(f"Validation Accuracy: {eval_result.val_accuracy:.4f}")
        self.best_fit_var.set(f"Fitness: {eval_result.fitness:.6f}")
        self.params_var.set(
            "Best Params:\n"
            f"n_estimators={params['n_estimators']}, max_depth={params['max_depth']}\n"
            f"min_samples_split={params['min_samples_split']}, min_samples_leaf={params['min_samples_leaf']}\n"
            f"max_features={params['max_features']}, bootstrap={params['bootstrap']}"
        )

        if final_result is not None:
            self.test_acc_var.set(f"Test Accuracy: {final_result['test_accuracy']:.4f}")
            self.redraw_confusion_matrix(final_result["test_confusion"], title="Test Confusion Matrix")
            self.redraw_feature_importances(final_result["feature_importances"])
            self.log(f"Final model trained | test_acc={final_result['test_accuracy']:.4f}")
        else:
            self.test_acc_var.set("Test Accuracy: -")
            self.redraw_confusion_matrix(eval_result.val_confusion, title="Validation Confusion Matrix")

        self.notes_list.delete(0, tk.END)
        self.notes_list.insert(tk.END, "Target: maximize validation accuracy")
        self.notes_list.insert(tk.END, f"Current val acc: {eval_result.val_accuracy:.4f}")
        self.notes_list.insert(tk.END, f"n_estimators: {params['n_estimators']}")
        self.notes_list.insert(tk.END, f"max_depth: {params['max_depth']}")
        self.notes_list.insert(tk.END, f"min_samples_split: {params['min_samples_split']}")
        self.notes_list.insert(tk.END, f"min_samples_leaf: {params['min_samples_leaf']}")
        self.notes_list.insert(tk.END, f"max_features: {params['max_features']}")
        self.notes_list.insert(tk.END, f"bootstrap: {params['bootstrap']}")

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
                self.ax_cm.text(j, i, str(cm[i, j]), ha="center", va="center")

        self.cm_colorbar = self.fig_cm.colorbar(im, ax=self.ax_cm, fraction=0.046, pad=0.04)
        self.canvas_cm.draw_idle()

    def redraw_feature_importances(self, importances: np.ndarray | None) -> None:
        self.ax_imp.clear()
        self.ax_imp.set_title("Feature Importances")

        if importances is None:
            self.ax_imp.text(
                0.5,
                0.5,
                "Feature importances available after final training",
                ha="center",
                va="center",
                transform=self.ax_imp.transAxes,
            )
            self.canvas_imp.draw_idle()
            return

        idx = np.argsort(importances)[::-1][:10]
        names = [FEATURE_NAMES[i] for i in idx]
        vals = importances[idx]

        self.ax_imp.bar(range(len(vals)), vals)
        self.ax_imp.set_xticks(range(len(vals)))
        self.ax_imp.set_xticklabels(names, rotation=35, ha="right")
        self.ax_imp.set_ylabel("Importance")
        self.canvas_imp.draw_idle()

    def update_ui(self, iteration: int, average_fitness: float, best, population_snapshot) -> None:
        params = decode_genes(best)
        eval_result = evaluate_params(params)

        self.append_result(
            f"{iteration:04d}: val_acc={eval_result.val_accuracy:.4f} | fitness={eval_result.fitness:.6f} | {eval_result.params_text}"
        )

        if iteration == 0 or iteration % 10 == 0:
            self.log(f"iter={iteration} best_val_acc={eval_result.val_accuracy:.4f} fitness={eval_result.fitness:.6f}")

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
    app = MLHyperparameterOptimizationApp(root)
    app.run()