from __future__ import annotations

import math
import queue
import random
import threading
import tkinter as tk
from dataclasses import dataclass
from tkinter import messagebox, ttk

import numpy as np
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure

from core.ga_solver import Chromosome, CrossoverType, GeneticSolver, MutationType, Population


# =========================================================
# Analog RC Low-Pass Filter - Problem specific code
# =========================================================

TARGET_CUTOFF_HZ = 1000.0

# Reasonable analog design search space
R_MIN = 100.0
R_MAX = 1_000_000.0

C_MIN = 1e-12
C_MAX = 1e-3


@dataclass(frozen=True)
class VariableSpec:
    name: str
    min_value: float
    max_value: float


VARIABLE_SPECS = [
    VariableSpec("R", R_MIN, R_MAX),
    VariableSpec("C", C_MIN, C_MAX),
]


# E12 resistor and capacitor mantissa set
E12_SERIES = np.asarray([1.0, 1.2, 1.5, 1.8, 2.2, 2.7, 3.3, 3.9, 4.7, 5.6, 6.8, 8.2], dtype=np.float64)


def clamp(value: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, value))


def random_log_uniform(lo: float, hi: float) -> float:
    log_lo = math.log10(lo)
    log_hi = math.log10(hi)
    return 10 ** random.uniform(log_lo, log_hi)


def random_gene_for_index(index: int) -> float:
    spec = VARIABLE_SPECS[index]
    return float(random_log_uniform(spec.min_value, spec.max_value))


def normalize_gene(index: int, value: float) -> float:
    spec = VARIABLE_SPECS[index]
    return float(clamp(value, spec.min_value, spec.max_value))


def rc_generator() -> Chromosome[float]:
    genes = [random_gene_for_index(i) for i in range(len(VARIABLE_SPECS))]
    return Chromosome(genes)


def random_gene() -> float:
    # Compatibility hook for generic solver; not index-aware.
    # Fitness normalization clamps values safely.
    return float(random_log_uniform(C_MIN, R_MAX))


def get_design_dict(chromosome: Chromosome[float]) -> dict[str, float]:
    r, c = chromosome.data
    return {"R": r, "C": c}


def cutoff_frequency_hz(r: float, c: float) -> float:
    return 1.0 / (2.0 * math.pi * r * c)


def magnitude_lowpass(r: float, c: float, f_hz: np.ndarray) -> np.ndarray:
    w = 2.0 * np.pi * f_hz
    wc = 1.0 / (r * c)
    return 1.0 / np.sqrt(1.0 + (w / wc) ** 2)


def db20(x: np.ndarray) -> np.ndarray:
    return 20.0 * np.log10(np.maximum(x, 1e-15))


def nearest_e12_error(value: float) -> float:
    """
    Relative error to nearest E12 value, expressed in normalized ratio form.
    Example:
      value = 4.91k -> nearest standard maybe 4.7k => some small penalty
    """
    if value <= 0:
        return 1.0

    exponent = math.floor(math.log10(value))
    mantissa = value / (10 ** exponent)

    idx = int(np.argmin(np.abs(E12_SERIES - mantissa)))
    nearest = float(E12_SERIES[idx])

    return abs(mantissa - nearest) / nearest


def calculate_fitness(chromosome: Chromosome[float]) -> float:
    chromosome.data = [
        normalize_gene(i, chromosome.data[i])
        for i in range(len(chromosome.data))
    ]

    r, c = chromosome.data
    fc = cutoff_frequency_hz(r, c)

    # Main target: cutoff frequency close to target
    rel_fc_error = abs(fc - TARGET_CUTOFF_HZ) / TARGET_CUTOFF_HZ

    # Prefer values close to standard E12 series
    r_std_penalty = nearest_e12_error(r)
    c_std_penalty = nearest_e12_error(c)

    # Mild penalty for extremely large time constant drift or silly corners
    corner_penalty = 0.0
    if r < 500:
        corner_penalty += 0.05
    if c < 1e-11:
        corner_penalty += 0.05
    if r > 500_000:
        corner_penalty += 0.05
    if c > 1e-4:
        corner_penalty += 0.05

    return float(
        rel_fc_error * 1000.0
        + r_std_penalty * 25.0
        + c_std_penalty * 25.0
        + corner_penalty * 10.0
    )


def stop_condition(best: Chromosome[float]) -> bool:
    # Good enough stop
    return best.fitness < 0.25


def decode(chromosome: Chromosome[float]) -> str:
    r, c = chromosome.data
    fc = cutoff_frequency_hz(r, c)
    return f"R={format_resistance(r)}, C={format_capacitance(c)}, fc={fc:.2f} Hz, fitness={chromosome.fitness:.4f}"


def format_resistance(r: float) -> str:
    if r >= 1_000_000:
        return f"{r / 1_000_000:.4f} MΩ"
    if r >= 1_000:
        return f"{r / 1_000:.4f} kΩ"
    return f"{r:.4f} Ω"


def format_capacitance(c: float) -> str:
    if c >= 1e-3:
        return f"{c * 1e3:.4f} mF"
    if c >= 1e-6:
        return f"{c * 1e6:.4f} µF"
    if c >= 1e-9:
        return f"{c * 1e9:.4f} nF"
    if c >= 1e-12:
        return f"{c * 1e12:.4f} pF"
    return f"{c:.4e} F"


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

    scaled = np.zeros_like(matrix, dtype=np.float64)

    for col in range(matrix.shape[1]):
        lo = VARIABLE_SPECS[col].min_value
        hi = VARIABLE_SPECS[col].max_value

        # log scaling is better for R/C visualization
        m = np.clip(matrix[:, col], lo, hi)
        scaled[:, col] = (np.log10(m) - math.log10(lo)) / (math.log10(hi) - math.log10(lo))

    scaled = np.clip(scaled, 0.0, 1.0)
    return (scaled * 255).astype(np.uint8)


# =========================================================
# GUI
# =========================================================

class AnalogRCFilterApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Analog Circuit Design - RC Low-Pass Filter")
        self.root.geometry("1380x820")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[float] | None = None
        self.is_running = False
        self.current_best: Chromosome[float] | None = None

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        self.ui_update_every = 4
        self.pool_redraw_every = 12
        self.queue_poll_ms = 50
        self.max_queue_items_per_tick = 3
        self.max_result_items = 120

        self.best_r_var = tk.StringVar(value="R: -")
        self.best_c_var = tk.StringVar(value="C: -")
        self.best_fc_var = tk.StringVar(value="fc: -")
        self.best_err_var = tk.StringVar(value="Target Error: -")
        self.best_fit_var = tk.StringVar(value="Fitness: -")

        self.pool_color_scheme = generate_color_scheme()
        self.pool_image: tk.PhotoImage | None = None

        self.build_ui()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=10, y=10, width=220, height=320)

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

        tk.Label(controls_frame, text="Population Size (1024)").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["1", "2", "4", "8", "16"],
        )
        self.cmb_population.current(2)
        self.cmb_population.pack(fill="x", pady=(4, 10))

        self.btn_start = tk.Button(controls_frame, text="Evolve", command=self.start_solver)
        self.btn_start.pack(fill="x", pady=(10, 0))

        info_frame = tk.LabelFrame(self.root, text="Best Design", padx=10, pady=10)
        info_frame.place(x=10, y=345, width=220, height=180)

        tk.Label(info_frame, textvariable=self.best_r_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.best_c_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.best_fc_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.best_err_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.best_fit_var, anchor="w").pack(fill="x", pady=2)

        best_frame = tk.LabelFrame(self.root, text="Best Chromosomes", padx=5, pady=5)
        best_frame.place(x=240, y=10, width=1130, height=170)

        self.result_list = tk.Listbox(best_frame, font=("Consolas", 10))
        self.result_list.pack(fill="both", expand=True)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        chart_frame.place(x=240, y=190, width=520, height=290)

        self.fig = Figure(figsize=(5.3, 2.5), dpi=100)
        self.ax = self.fig.add_subplot(111)
        self.ax.set_xlabel("Iteration")
        self.ax.set_ylabel("Fitness")
        self.ax.grid(True, alpha=0.3)
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(0, 1)

        self.best_line, = self.ax.plot([], [], label="Best Fitness")
        self.avg_line, = self.ax.plot([], [], label="Average Fitness")
        self.ax.legend()

        self.canvas_chart = FigureCanvasTkAgg(self.fig, master=chart_frame)
        self.canvas_chart.get_tk_widget().pack(fill="both", expand=True)

        response_frame = tk.LabelFrame(self.root, text="Frequency Response", padx=8, pady=8)
        response_frame.place(x=240, y=490, width=760, height=320)

        self.fig_resp = Figure(figsize=(7.5, 2.8), dpi=100)
        self.ax_resp = self.fig_resp.add_subplot(111)
        self.ax_resp.set_xscale("log")
        self.ax_resp.set_xlabel("Frequency (Hz)")
        self.ax_resp.set_ylabel("Magnitude (dB)")
        self.ax_resp.grid(True, which="both", alpha=0.3)

        self.response_canvas = FigureCanvasTkAgg(self.fig_resp, master=response_frame)
        self.response_canvas.get_tk_widget().pack(fill="both", expand=True)

        pool_frame = tk.LabelFrame(self.root, text="Population Chart", padx=8, pady=8)
        pool_frame.place(x=780, y=190, width=590, height=140)

        self.pool_canvas = tk.Canvas(
            pool_frame,
            width=550,
            height=95,
            bg="white",
            highlightthickness=0,
        )
        self.pool_canvas.pack(fill="both", expand=True)

        summary_frame = tk.LabelFrame(self.root, text="Design Notes", padx=8, pady=8)
        summary_frame.place(x=780, y=340, width=590, height=140)

        self.summary_list = tk.Listbox(summary_frame, font=("Consolas", 10))
        self.summary_list.pack(fill="both", expand=True)

        schematic_frame = tk.LabelFrame(self.root, text="Circuit Schematic", padx=8, pady=8)
        schematic_frame.place(x=1010, y=490, width=360, height=320)

        self.schematic_canvas = tk.Canvas(
            schematic_frame,
            width=320,
            height=270,
            bg="white",
            highlightthickness=0,
        )
        self.schematic_canvas.pack(fill="both", expand=True)

        self.redraw_schematic(None)

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)
        self.summary_list.delete(0, tk.END)

        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(0, 1)
        self.canvas_chart.draw_idle()

        self.ax_resp.clear()
        self.ax_resp.set_xscale("log")
        self.ax_resp.set_xlabel("Frequency (Hz)")
        self.ax_resp.set_ylabel("Magnitude (dB)")
        self.ax_resp.grid(True, which="both", alpha=0.3)
        self.response_canvas.draw_idle()

        self.pool_canvas.delete("all")
        self.pool_image = None

        self.best_r_var.set("R: -")
        self.best_c_var.set("C: -")
        self.best_fc_var.set("fc: -")
        self.best_err_var.set("Target Error: -")
        self.best_fit_var.set("Fitness: -")

        self.current_best = None
        self.redraw_schematic(None)

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
        elitism_rate = float(self.cmb_elitism.get())
        crossover_type = self.map_crossover_type()

        self.solver = GeneticSolver[float](
            population_size=1024 * population_factor,
            iteration_count=1800,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.08,
            crossover_type=crossover_type,
            mutation_type=MutationType.RANDOM_RESET,
            maximize_fitness=False,
        )

        self.solver.generator_function = rc_generator
        self.solver.fitness_function = calculate_fitness
        self.solver.random_gene_function = random_gene
        self.solver.stop_condition_function = stop_condition
        self.solver.iteration_completed_callback = self.on_iteration_completed

        threading.Thread(target=self.run_solver, daemon=True).start()

    def run_solver(self) -> None:
        try:
            if self.solver is not None:
                result = self.solver.evolve()
                if result is not None:
                    self.ui_queue.put(("finished", result.copy()))
        except Exception as exc:
            self.ui_queue.put(("error", str(exc)))
        finally:
            self.ui_queue.put(("run_finished",))

    def finish_run(self) -> None:
        self.is_running = False
        self.btn_start.config(state="normal")

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
                _, solution = item
                self.update_design_panels(solution)

            elif kind == "error":
                _, error_message = item
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

    def update_chart(self) -> None:
        self.best_line.set_data(self.iteration_history, self.best_history)
        self.avg_line.set_data(self.iteration_history, self.avg_history)

        max_x = max(1, self.iteration_history[-1] + 1) if self.iteration_history else 1
        min_y = min(self.best_history + self.avg_history, default=0.0)
        max_y = max(self.best_history + self.avg_history, default=1.0)

        if min_y == max_y:
            min_y -= 1
            max_y += 1

        self.ax.set_xlim(0, max_x)
        self.ax.set_ylim(min_y * 0.98, max_y * 1.02)
        self.canvas_chart.draw_idle()

    def update_design_panels(self, chromosome: Chromosome[float]) -> None:
        self.current_best = chromosome
        design = get_design_dict(chromosome)
        r = design["R"]
        c = design["C"]
        fc = cutoff_frequency_hz(r, c)
        error_pct = abs(fc - TARGET_CUTOFF_HZ) / TARGET_CUTOFF_HZ * 100.0

        self.best_r_var.set(f"R: {format_resistance(r)}")
        self.best_c_var.set(f"C: {format_capacitance(c)}")
        self.best_fc_var.set(f"fc: {fc:.4f} Hz")
        self.best_err_var.set(f"Target Error: {error_pct:.4f}%")
        self.best_fit_var.set(f"Fitness: {chromosome.fitness:.6f}")

        self.summary_list.delete(0, tk.END)
        self.summary_list.insert(tk.END, f"Target cutoff: {TARGET_CUTOFF_HZ:.2f} Hz")
        self.summary_list.insert(tk.END, f"Found cutoff : {fc:.4f} Hz")
        self.summary_list.insert(tk.END, f"R std penalty: {nearest_e12_error(r):.6f}")
        self.summary_list.insert(tk.END, f"C std penalty: {nearest_e12_error(c):.6f}")
        self.summary_list.insert(tk.END, f"|fc-target|  : {abs(fc - TARGET_CUTOFF_HZ):.4f} Hz")

        self.redraw_response(chromosome)
        self.redraw_schematic(chromosome)

    def update_ui(self, iteration: int, average_fitness: float, best, population_snapshot) -> None:
        self.append_result(f"{iteration:04d}: {decode(best)}")

        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.update_chart()
        self.update_design_panels(best)

        if population_snapshot is not None:
            self.draw_population_chart(population_snapshot)

    def redraw_response(self, chromosome: Chromosome[float] | None) -> None:
        self.ax_resp.clear()
        self.ax_resp.set_xscale("log")
        self.ax_resp.set_xlabel("Frequency (Hz)")
        self.ax_resp.set_ylabel("Magnitude (dB)")
        self.ax_resp.grid(True, which="both", alpha=0.3)

        f = np.logspace(0, 6, 1000)

        # Target response
        ideal_mag = 1.0 / np.sqrt(1.0 + (f / TARGET_CUTOFF_HZ) ** 2)
        self.ax_resp.plot(f, db20(ideal_mag), label=f"Target fc={TARGET_CUTOFF_HZ:.0f} Hz")

        if chromosome is not None:
            r, c = chromosome.data
            mag = magnitude_lowpass(r, c, f)
            fc = cutoff_frequency_hz(r, c)
            self.ax_resp.plot(f, db20(mag), label=f"Found fc={fc:.2f} Hz")
            self.ax_resp.axvline(TARGET_CUTOFF_HZ, linestyle="--")
            self.ax_resp.axvline(fc, linestyle=":")

        self.ax_resp.legend()
        self.response_canvas.draw_idle()

    def redraw_schematic(self, chromosome: Chromosome[float] | None) -> None:
        self.schematic_canvas.delete("all")
        cw = max(1, int(self.schematic_canvas.winfo_width()))
        ch = max(1, int(self.schematic_canvas.winfo_height()))

        mid_y = ch // 2
        left_x = 30
        res_x1 = 70
        res_x2 = 170
        node_x = 210
        cap_y2 = ch - 60

        # input line
        self.schematic_canvas.create_line(left_x, mid_y, res_x1, mid_y, width=2)
        self.schematic_canvas.create_text(left_x, mid_y - 16, text="Vin", anchor="w", font=("Arial", 10, "bold"))

        # resistor zigzag
        zig_x = np.linspace(res_x1, res_x2, 9)
        zig_y = [mid_y, mid_y - 12, mid_y + 12, mid_y - 12, mid_y + 12, mid_y - 12, mid_y + 12, mid_y - 12, mid_y]
        points = []
        for x, y in zip(zig_x, zig_y):
            points.extend([x, y])
        self.schematic_canvas.create_line(*points, width=2)

        # node and output
        self.schematic_canvas.create_line(res_x2, mid_y, node_x, mid_y, width=2)
        self.schematic_canvas.create_oval(node_x - 3, mid_y - 3, node_x + 3, mid_y + 3, fill="black")
        self.schematic_canvas.create_line(node_x, mid_y, cw - 40, mid_y, width=2)
        self.schematic_canvas.create_text(cw - 65, mid_y - 16, text="Vout", anchor="w", font=("Arial", 10, "bold"))

        # capacitor to ground
        self.schematic_canvas.create_line(node_x, mid_y, node_x, mid_y + 40, width=2)
        self.schematic_canvas.create_line(node_x - 18, mid_y + 40, node_x + 18, mid_y + 40, width=2)
        self.schematic_canvas.create_line(node_x - 18, mid_y + 52, node_x + 18, mid_y + 52, width=2)
        self.schematic_canvas.create_line(node_x, mid_y + 52, node_x, cap_y2, width=2)

        # ground
        gx = node_x
        gy = cap_y2
        self.schematic_canvas.create_line(gx - 20, gy, gx + 20, gy, width=2)
        self.schematic_canvas.create_line(gx - 14, gy + 8, gx + 14, gy + 8, width=2)
        self.schematic_canvas.create_line(gx - 8, gy + 16, gx + 8, gy + 16, width=2)

        self.schematic_canvas.create_text((res_x1 + res_x2) / 2, mid_y - 28, text="R", font=("Arial", 11, "bold"))
        self.schematic_canvas.create_text(node_x + 34, mid_y + 47, text="C", font=("Arial", 11, "bold"))

        if chromosome is None:
            self.schematic_canvas.create_text(cw / 2, 28, text="RC Low-Pass Filter", font=("Arial", 13, "bold"))
            return

        r, c = chromosome.data
        fc = cutoff_frequency_hz(r, c)

        self.schematic_canvas.create_text(cw / 2, 22, text="RC Low-Pass Filter", font=("Arial", 13, "bold"))
        self.schematic_canvas.create_text((res_x1 + res_x2) / 2, mid_y - 48, text=format_resistance(r), font=("Arial", 9))
        self.schematic_canvas.create_text(node_x + 62, mid_y + 47, text=format_capacitance(c), font=("Arial", 9))
        self.schematic_canvas.create_text(cw / 2, ch - 20, text=f"fc = {fc:.4f} Hz", font=("Arial", 10, "bold"))

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
    app = AnalogRCFilterApp(root)
    app.run()