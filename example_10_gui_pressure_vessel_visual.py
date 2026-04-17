from __future__ import annotations

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
# Pressure Vessel Design - Problem specific code
# =========================================================

@dataclass(frozen=True)
class VariableSpec:
    name: str
    min_value: float
    max_value: float
    kind: str  # "step" or "continuous"
    step: float = 0.0


VARIABLE_SPECS = [
    VariableSpec("Ts", 0.0625, 6.0, "step", 0.0625),   # shell thickness
    VariableSpec("Th", 0.0625, 6.0, "step", 0.0625),   # head thickness
    VariableSpec("R", 10.0, 200.0, "continuous"),      # inner radius
    VariableSpec("L", 10.0, 240.0, "continuous"),      # cylindrical length
]


def clamp(value: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, value))


def normalize_gene(index: int, value: float) -> float:
    spec = VARIABLE_SPECS[index]
    value = clamp(value, spec.min_value, spec.max_value)

    if spec.kind == "step":
        steps = round(value / spec.step)
        value = steps * spec.step
        value = clamp(value, spec.min_value, spec.max_value)

    return float(value)


def random_gene_for_index(index: int) -> float:
    spec = VARIABLE_SPECS[index]

    if spec.kind == "step":
        min_step = int(round(spec.min_value / spec.step))
        max_step = int(round(spec.max_value / spec.step))
        return float(random.randint(min_step, max_step) * spec.step)

    return float(random.uniform(spec.min_value, spec.max_value))


def vessel_generator() -> Chromosome[float]:
    genes = [random_gene_for_index(i) for i in range(len(VARIABLE_SPECS))]
    return Chromosome(genes)


def decode(chromosome: Chromosome[float]) -> str:
    ts, th, r, l = chromosome.data
    return f"Ts={ts:.4f}, Th={th:.4f}, R={r:.4f}, L={l:.4f}"


def get_design_dict(chromosome: Chromosome[float]) -> dict[str, float]:
    ts, th, r, l = chromosome.data
    return {"Ts": ts, "Th": th, "R": r, "L": l}


def vessel_cost(chromosome: Chromosome[float]) -> float:
    ts, th, r, l = chromosome.data
    return (
        0.6224 * ts * r * l
        + 1.7781 * th * r * r
        + 3.1661 * ts * ts * l
        + 19.84 * ts * ts * r
    )


def vessel_constraints(chromosome: Chromosome[float]) -> dict[str, float]:
    ts, th, r, l = chromosome.data

    # Feasible if all g_i <= 0
    g1 = -ts + 0.0193 * r
    g2 = -th + 0.00954 * r
    g3 = -np.pi * r * r * l - (4.0 / 3.0) * np.pi * r**3 + 1296000.0
    g4 = l - 240.0

    return {
        "g1_shell_thickness": float(g1),
        "g2_head_thickness": float(g2),
        "g3_volume": float(g3),
        "g4_length": float(g4),
    }


def total_penalty(chromosome: Chromosome[float]) -> float:
    constraints = vessel_constraints(chromosome)
    penalty = 0.0

    for value in constraints.values():
        violation = max(0.0, value)
        penalty += violation ** 2

    return penalty


def calculate_fitness(chromosome: Chromosome[float]) -> float:
    chromosome.data = [
        normalize_gene(i, chromosome.data[i])
        for i in range(len(chromosome.data))
    ]

    cost = vessel_cost(chromosome)
    penalty = total_penalty(chromosome)
    return float(cost + 1_000_000.0 * penalty)


def stop_condition(_best: Chromosome[float]) -> bool:
    return False


def is_feasible(chromosome: Chromosome[float]) -> bool:
    constraints = vessel_constraints(chromosome)
    return all(v <= 0.0 for v in constraints.values())


def get_population_matrix(population_snapshot: list[list[float]]) -> np.ndarray:
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
        spec = VARIABLE_SPECS[col]
        denom = spec.max_value - spec.min_value
        if denom <= 0:
            scaled[:, col] = 0
        else:
            scaled[:, col] = (matrix[:, col] - spec.min_value) / denom

    scaled = np.clip(scaled, 0.0, 1.0)
    return (scaled * 255).astype(np.uint8)


# =========================================================
# GUI
# =========================================================

class PressureVesselVisualApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Pressure Vessel Design Optimization")
        self.root.geometry("1380x780")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[float] | None = None
        self.is_running = False

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        self.ui_update_every = 2
        self.pool_redraw_every = 5
        self.queue_poll_ms = 40
        self.max_queue_items_per_tick = 4
        self.max_result_items = 200

        self.best_cost_var = tk.StringVar(value="Cost: -")
        self.feasible_var = tk.StringVar(value="Feasible: -")
        self.penalty_var = tk.StringVar(value="Penalty: -")
        self.ts_var = tk.StringVar(value="Ts: -")
        self.th_var = tk.StringVar(value="Th: -")
        self.r_var = tk.StringVar(value="R: -")
        self.l_var = tk.StringVar(value="L: -")
        self.volume_var = tk.StringVar(value="Volume: -")

        self.current_best: Chromosome[float] | None = None

        self.color_scheme = generate_color_scheme()
        self.pool_image: tk.PhotoImage | None = None

        self.build_ui()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=10, y=10, width=210, height=320)

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
            values=["0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7"],
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

        design_frame = tk.LabelFrame(self.root, text="Best Design", padx=10, pady=10)
        design_frame.place(x=10, y=345, width=210, height=260)

        tk.Label(design_frame, textvariable=self.best_cost_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(design_frame, textvariable=self.feasible_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(design_frame, textvariable=self.penalty_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(design_frame, textvariable=self.ts_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(design_frame, textvariable=self.th_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(design_frame, textvariable=self.r_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(design_frame, textvariable=self.l_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(design_frame, textvariable=self.volume_var, anchor="w").pack(fill="x", pady=2)

        result_frame = tk.LabelFrame(self.root, text="Best Chromosomes", padx=5, pady=5)
        result_frame.place(x=230, y=10, width=1130, height=170)

        self.result_list = tk.Listbox(result_frame, font=("Consolas", 10))
        self.result_list.pack(fill="both", expand=True)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        chart_frame.place(x=230, y=190, width=500, height=560)

        self.fig = Figure(figsize=(5.4, 5.2), dpi=100)
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

        vessel_frame = tk.LabelFrame(self.root, text="Pressure Vessel Visualization", padx=8, pady=8)
        vessel_frame.place(x=750, y=190, width=610, height=300)

        self.vessel_canvas = tk.Canvas(
            vessel_frame,
            width=570,
            height=250,
            bg="white",
            highlightthickness=0,
        )
        self.vessel_canvas.pack(fill="both", expand=True)

        pool_frame = tk.LabelFrame(self.root, text="Population Chart", padx=8, pady=8)
        pool_frame.place(x=750, y=500, width=300, height=250)

        self.pool_canvas = tk.Canvas(
            pool_frame,
            width=260,
            height=210,
            bg="white",
            highlightthickness=0,
        )
        self.pool_canvas.pack(fill="both", expand=True)

        constraints_frame = tk.LabelFrame(self.root, text="Constraint Values (g <= 0)", padx=8, pady=8)
        constraints_frame.place(x=1060, y=500, width=300, height=250)

        self.constraints_list = tk.Listbox(constraints_frame, font=("Consolas", 9))
        self.constraints_list.pack(fill="both", expand=True)

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)
        self.constraints_list.delete(0, tk.END)

        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(0, 1)
        self.canvas_chart.draw_idle()

        self.pool_canvas.delete("all")
        self.pool_image = None

        self.best_cost_var.set("Cost: -")
        self.feasible_var.set("Feasible: -")
        self.penalty_var.set("Penalty: -")
        self.ts_var.set("Ts: -")
        self.th_var.set("Th: -")
        self.r_var.set("R: -")
        self.l_var.set("L: -")
        self.volume_var.set("Volume: -")

        self.vessel_canvas.delete("all")
        self.current_best = None

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
            iteration_count=1500,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.08,
            crossover_type=crossover_type,
            mutation_type=MutationType.RANDOM_RESET,
            maximize_fitness=False,
        )

        self.solver.generator_function = vessel_generator
        self.solver.fitness_function = calculate_fitness
        self.solver.random_gene_function = lambda: random.uniform(0.0625, 240.0)
        self.solver.stop_condition_function = stop_condition
        self.solver.iteration_completed_callback = self.on_iteration_completed

        threading.Thread(target=self.run_solver, daemon=True).start()

    def run_solver(self) -> None:
        try:
            if self.solver is not None:
                result = self.solver.evolve()
                if result is not None:
                    self.ui_queue.put(("finished", result.copy()))
                else:
                    self.ui_queue.put(("finished_no_solution",))
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
                self.update_design_panel(solution)
                self.redraw_vessel(solution)

            elif kind == "finished_no_solution":
                pass

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

    def update_design_panel(self, chromosome: Chromosome[float]) -> None:
        self.current_best = chromosome

        design = get_design_dict(chromosome)
        cost = vessel_cost(chromosome)
        penalty = total_penalty(chromosome)
        feasible = is_feasible(chromosome)
        constraints = vessel_constraints(chromosome)

        ts = design["Ts"]
        th = design["Th"]
        r = design["R"]
        l = design["L"]
        volume = np.pi * r * r * l + (4.0 / 3.0) * np.pi * r**3

        self.best_cost_var.set(f"Cost: {cost:.4f}")
        self.feasible_var.set(f"Feasible: {'Yes' if feasible else 'No'}")
        self.penalty_var.set(f"Penalty: {penalty:.6f}")
        self.ts_var.set(f"Ts: {ts:.4f}")
        self.th_var.set(f"Th: {th:.4f}")
        self.r_var.set(f"R: {r:.4f}")
        self.l_var.set(f"L: {l:.4f}")
        self.volume_var.set(f"Volume: {volume:.2f}")

        self.constraints_list.delete(0, tk.END)
        for name, value in constraints.items():
            mark = "OK" if value <= 0 else "VIOL"
            self.constraints_list.insert(tk.END, f"{name:20s} = {value:11.6f}  {mark}")

    def update_ui(self, iteration: int, average_fitness: float, best, population_snapshot) -> None:
        self.append_result(f"{iteration:04d}: {decode(best)}")

        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.update_chart()
        self.update_design_panel(best)
        self.redraw_vessel(best)

        if population_snapshot is not None:
            self.draw_pool_graph_image(population_snapshot)

    def snapshot_population(self, population: Population[float]) -> np.ndarray:
        return get_population_matrix([chromosome.data for chromosome in population.chromosomes])

    def draw_pool_graph_image(self, population_snapshot: np.ndarray) -> None:
        if population_snapshot.size == 0:
            return

        canvas_width = max(1, int(self.pool_canvas.winfo_width()))
        canvas_height = max(1, int(self.pool_canvas.winfo_height()))

        rows, cols = population_snapshot.shape
        if rows == 0 or cols == 0:
            return

        img_w = canvas_width
        img_h = canvas_height

        scaled = scale_population_matrix_for_display(population_snapshot)

        row_idx = np.linspace(0, rows - 1, img_h).astype(np.int32)
        col_idx = np.linspace(0, cols - 1, img_w).astype(np.int32)

        sampled = scaled[row_idx][:, col_idx]
        rgb = self.color_scheme[sampled]

        ppm_header = f"P6 {img_w} {img_h} 255\n".encode("ascii")
        ppm_data = ppm_header + rgb.tobytes()

        self.pool_image = tk.PhotoImage(data=ppm_data, format="PPM")
        self.pool_canvas.delete("all")
        self.pool_canvas.create_image(0, 0, anchor="nw", image=self.pool_image)

    def redraw_vessel(self, chromosome: Chromosome[float] | None) -> None:
        self.vessel_canvas.delete("all")

        if chromosome is None:
            self.vessel_canvas.create_text(
                285,
                125,
                text="Run the solver to visualize the vessel",
                font=("Arial", 14),
            )
            return

        design = get_design_dict(chromosome)
        ts = design["Ts"]
        th = design["Th"]
        r = design["R"]
        l = design["L"]

        cw = max(1, int(self.vessel_canvas.winfo_width()))
        ch = max(1, int(self.vessel_canvas.winfo_height()))

        margin_x = 40
        margin_y = 30

        usable_w = cw - 2 * margin_x
        usable_h = ch - 2 * margin_y

        outer_half_height_units = r + th
        outer_body_length_units = l + 2 * (r + th)

        if outer_body_length_units <= 0 or outer_half_height_units <= 0:
            return

        scale_x = usable_w / outer_body_length_units
        scale_y = usable_h / (2 * outer_half_height_units)
        scale = min(scale_x, scale_y)

        body_len = l * scale
        inner_r = r * scale
        shell_t = max(1.0, ts * scale)
        head_t = max(1.0, th * scale)

        outer_r = inner_r + head_t

        center_y = ch / 2
        body_x1 = margin_x + outer_r
        body_x2 = body_x1 + body_len

        outer_top = center_y - (inner_r + shell_t)
        outer_bottom = center_y + (inner_r + shell_t)
        inner_top = center_y - inner_r
        inner_bottom = center_y + inner_r

        # Outer shell
        self.vessel_canvas.create_rectangle(
            body_x1,
            outer_top,
            body_x2,
            outer_bottom,
            fill="#cfe2f3",
            outline="#1f4e79",
            width=2,
        )

        # Inner shell
        self.vessel_canvas.create_rectangle(
            body_x1 + shell_t,
            inner_top,
            body_x2 - shell_t,
            inner_bottom,
            fill="white",
            outline="#7f7f7f",
            width=1,
        )

        # Left outer head
        self.vessel_canvas.create_oval(
            body_x1 - 2 * outer_r,
            center_y - (inner_r + head_t),
            body_x1,
            center_y + (inner_r + head_t),
            fill="#cfe2f3",
            outline="#1f4e79",
            width=2,
        )

        # Right outer head
        self.vessel_canvas.create_oval(
            body_x2,
            center_y - (inner_r + head_t),
            body_x2 + 2 * outer_r,
            center_y + (inner_r + head_t),
            fill="#cfe2f3",
            outline="#1f4e79",
            width=2,
        )

        # Left inner head
        self.vessel_canvas.create_oval(
            body_x1 - 2 * inner_r + head_t,
            center_y - inner_r,
            body_x1 - head_t,
            center_y + inner_r,
            fill="white",
            outline="#7f7f7f",
            width=1,
        )

        # Right inner head
        self.vessel_canvas.create_oval(
            body_x2 + head_t,
            center_y - inner_r,
            body_x2 + 2 * inner_r - head_t,
            center_y + inner_r,
            fill="white",
            outline="#7f7f7f",
            width=1,
        )

        # Center line
        self.vessel_canvas.create_line(
            20, center_y, cw - 20, center_y, fill="#aaaaaa", dash=(4, 3)
        )

        # Dimension L
        dim_y = outer_bottom + 18
        self.vessel_canvas.create_line(body_x1, dim_y, body_x2, dim_y, arrow=tk.BOTH)
        self.vessel_canvas.create_text(
            (body_x1 + body_x2) / 2,
            dim_y + 12,
            text=f"L = {l:.2f}",
            font=("Arial", 10, "bold"),
        )

        # Dimension R
        dim_x = body_x2 + 55
        self.vessel_canvas.create_line(dim_x, center_y, dim_x, inner_top, arrow=tk.BOTH)
        self.vessel_canvas.create_text(
            dim_x + 26,
            (center_y + inner_top) / 2,
            text=f"R = {r:.2f}",
            angle=90,
            font=("Arial", 10, "bold"),
        )

        # Shell thickness Ts
        ts_x = body_x1 + 20
        self.vessel_canvas.create_line(
            ts_x,
            inner_top,
            ts_x,
            outer_top,
            arrow=tk.BOTH,
            fill="darkgreen",
        )
        self.vessel_canvas.create_text(
            ts_x + 28,
            (inner_top + outer_top) / 2,
            text=f"Ts={ts:.3f}",
            angle=90,
            fill="darkgreen",
            font=("Arial", 9, "bold"),
        )

        # Head thickness Th
        th_y = center_y - inner_r - head_t / 2
        self.vessel_canvas.create_line(
            body_x2 + 2 * inner_r - head_t,
            th_y,
            body_x2 + 2 * outer_r,
            th_y,
            arrow=tk.BOTH,
            fill="purple",
        )
        self.vessel_canvas.create_text(
            body_x2 + 2 * outer_r + 28,
            th_y,
            text=f"Th={th:.3f}",
            fill="purple",
            font=("Arial", 9, "bold"),
        )

        feasible_text = "FEASIBLE" if is_feasible(chromosome) else "INFEASIBLE"
        feasible_color = "darkgreen" if is_feasible(chromosome) else "red"

        self.vessel_canvas.create_text(
            cw / 2,
            18,
            text=feasible_text,
            fill=feasible_color,
            font=("Arial", 14, "bold"),
        )

    def run(self) -> None:
        self.root.mainloop()


if __name__ == "__main__":
    root = tk.Tk()
    app = PressureVesselVisualApp(root)
    app.run()