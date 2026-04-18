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
from core.ga_solver import Chromosome, CrossoverType, GeneticSolver, MutationType

@dataclass
class City:
    city_id: int
    x: float
    y: float

CITIES: list[City] = []

def degree_to_radian(degree: float) -> float:
    return degree * math.pi / 180.0

def distance(ax: float, ay: float, bx: float, by: float) -> float:
    return math.hypot(ax - bx, ay - by)

def load_circular_cities(radius: float = 125.0, point_count: int = 20) -> list[City]:    
    cities: list[City] = []
    j = 1
    for degree in range(0, 360, 360 // point_count):
        px = math.sin(degree_to_radian(degree)) * radius
        py = math.cos(degree_to_radian(degree)) * radius
        cities.append(City(j, px, py))
        j += 1

    return cities


def path_generator() -> Chromosome[int]:
    city_indices = np.random.permutation(len(CITIES)).astype(np.int32)
    return Chromosome(city_indices.tolist())


def decode(chromosome: Chromosome[int]) -> str:
    return " -> ".join(str(CITIES[idx].city_id) for idx in chromosome.data)

def calculate_fitness_tsp(chromosome: Chromosome[int]) -> float:
    total = 0.0

    for i in range(len(chromosome.data) - 1):
        ca = CITIES[chromosome.data[i]]
        cb = CITIES[chromosome.data[i + 1]]
        total += distance(ca.x, ca.y, cb.x, cb.y)

    beg = CITIES[chromosome.data[0]]
    end = CITIES[chromosome.data[-1]]
    total += distance(beg.x, beg.y, end.x, end.y)

    return total

def stop_condition(_best: Chromosome[int]) -> bool:
    return False

def generate_color_scheme(count: int) -> np.ndarray:
    import colorsys

    colors = np.zeros((count, 3), dtype=np.uint8)
    for i in range(count):
        h = i / max(1, count)
        r, g, b = colorsys.hls_to_rgb(h, 0.5, 0.75)
        colors[i] = (int(r * 255), int(g * 255), int(b * 255))
    return colors

class TSPApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Traveling Salesman Problem")
        self.root.geometry("1160x700")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[int] | None = None
        self.is_running = False
        self.best_solution: Chromosome[int] | None = None

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        self.ui_update_every = 2
        self.pool_redraw_every = 6
        self.queue_poll_ms = 40
        self.max_queue_items_per_tick = 4

        self.total_length_var = tk.StringVar(value="Total Length: -")

        self.city_color_scheme: np.ndarray | None = None
        self.pool_image: tk.PhotoImage | None = None

        self.map_default_width = 410
        self.map_default_height = 320
        self.pool_default_width = 430
        self.pool_default_height = 320

        self.build_ui()
        self.init_defaults()
        self.load_data(mode="circular")

        self.root.after_idle(self.initial_render)
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def initial_render(self) -> None:
        self.root.update_idletasks()
        self.redraw_map()

    def build_ui(self) -> None:
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=10, y=10, width=180, height=340)

        tk.Label(controls_frame, text="Crossover Type").pack(anchor="w")
        self.cmb_crossover = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["OnePointCrossover", "UniformCrossover", "PMX"],
        )
        self.cmb_crossover.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Elitism Rate").pack(anchor="w")
        self.cmb_elitism = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0"],
        )
        self.cmb_elitism.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Mutation Type").pack(anchor="w")
        self.cmb_mutation = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["Swap", "Scramble", "Inverse"],
        )
        self.cmb_mutation.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Population Size (1024)").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["1", "2", "4", "8", "16", "32", "64", "128"],
        )
        self.cmb_population.pack(fill="x", pady=(4, 10))

        self.btn_evolve = tk.Button(controls_frame, text="Evolve", command=self.start_solver)
        self.btn_evolve.pack(fill="x", pady=(8, 10))

        self.lbl_total_length = tk.Label(
            controls_frame,
            textvariable=self.total_length_var,
            anchor="w",
            justify="left",
        )
        self.lbl_total_length.pack(fill="x", pady=(8, 0))

        map_frame = tk.LabelFrame(self.root, text="Map", padx=8, pady=8)
        map_frame.place(x=200, y=10, width=450, height=360)

        self.map_canvas = tk.Canvas(
            map_frame,
            width=self.map_default_width,
            height=self.map_default_height,
            bg="white",
            highlightthickness=0,
        )
        self.map_canvas.pack(fill="both", expand=True)

        pool_frame = tk.LabelFrame(self.root, text="Population Chart", padx=8, pady=8)
        pool_frame.place(x=670, y=10, width=470, height=360)

        self.pool_canvas = tk.Canvas(
            pool_frame,
            width=self.pool_default_width,
            height=self.pool_default_height,
            bg="white",
            highlightthickness=0,
        )
        self.pool_canvas.pack(fill="both", expand=True)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        chart_frame.place(x=10, y=370, width=1130, height=320)

        self.fig = Figure(figsize=(11, 2.9), dpi=100)
        self.ax = self.fig.add_subplot(111)
        self.ax.set_xlabel("Iteration")
        self.ax.set_ylabel("Fitness")
        self.ax.grid(True, alpha=0.3)
        self.ax.set_xlim(0, 10)
        self.ax.set_ylim(0, 10)

        self.best_line, = self.ax.plot([], [], label="Best Fitness")
        self.avg_line, = self.ax.plot([], [], label="Average Fitness")
        self.ax.legend()

        self.canvas_chart = FigureCanvasTkAgg(self.fig, master=chart_frame)
        self.canvas_chart.get_tk_widget().pack(fill="both", expand=True)

    def init_defaults(self) -> None:
        self.cmb_population.current(2)
        self.cmb_crossover.current(2)
        self.cmb_mutation.current(0)
        self.cmb_elitism.current(3)

    def load_data(self, mode: str = "circular") -> None:
        global CITIES

        if mode == "circular":
            CITIES = load_circular_cities()
        else:
            CITIES = load_circular_cities()

        self.city_color_scheme = generate_color_scheme(len(CITIES))
        self.best_solution = None
        self.total_length_var.set("Total Length: -")

    def clear_ui(self) -> None:
        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax.set_xlim(0, 10)
        self.ax.set_ylim(0, 10)
        self.canvas_chart.draw_idle()

        self.pool_canvas.delete("all")
        self.pool_image = None

    def flush_ui_queue(self) -> None:
        while True:
            try:
                self.ui_queue.get_nowait()
            except queue.Empty:
                break

    def _safe_canvas_size(self, canvas: tk.Canvas, default_w: int, default_h: int) -> tuple[int, int]:
        width = int(canvas.winfo_width())
        height = int(canvas.winfo_height())

        if width <= 2:
            width = default_w
        if height <= 2:
            height = default_h

        return width, height

    def map_crossover_type(self) -> CrossoverType:
        value = self.cmb_crossover.get()
        if value == "OnePointCrossover":
            return CrossoverType.ONE_POINT
        if value == "UniformCrossover":
            return CrossoverType.UNIFORM
        return CrossoverType.PMX

    def map_mutation_type(self) -> MutationType:
        value = self.cmb_mutation.get()
        if value == "Swap":
            return MutationType.SWAP
        if value == "Scramble":
            return MutationType.SCRAMBLE
        return MutationType.INVERSE

    def start_solver(self) -> None:
        if self.is_running:
            return

        self.clear_ui()
        self.flush_ui_queue()

        self.is_running = True
        self.btn_evolve.config(state="disabled")

        pop_factor = int(self.cmb_population.get())
        crossover_type = self.map_crossover_type()
        mutation_type = self.map_mutation_type()
        elitism_rate = float(self.cmb_elitism.get())

        self.solver = GeneticSolver[int](
            population_size=1024 * pop_factor,
            iteration_count=2500,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.35,
            crossover_type=crossover_type,
            mutation_type=mutation_type,
            maximize_fitness=False,
        )

        self.solver.generator_function = path_generator
        self.solver.fitness_function = calculate_fitness_tsp
        self.solver.stop_condition_function = stop_condition
        self.solver.iteration_completed_callback = self.on_iteration_completed

        threading.Thread(target=self.run_solver, daemon=True).start()

    def run_solver(self) -> None:
        try:
            if self.solver is not None:
                result = self.solver.evolve()
                if result is not None:
                    self.ui_queue.put(("finished_with_solution", result.copy()))
                else:
                    self.ui_queue.put(("finished_no_solution",))
        except Exception as exc:
            self.ui_queue.put(("error", str(exc)))
        finally:
            self.ui_queue.put(("run_finished",))

    def finish_run(self) -> None:
        self.is_running = False
        self.btn_evolve.config(state="normal")

    def on_iteration_completed(
        self,
        iteration: int,
        average_fitness: float,
        best: Chromosome[int],
    ) -> None:
        if iteration != 0 and iteration % self.ui_update_every != 0:
            return

        pool_snapshot = None
        if self.solver is not None and iteration % self.pool_redraw_every == 0:
            pool_snapshot = np.asarray(
                [chromosome.data for chromosome in self.solver.population.chromosomes],
                dtype=np.int32,
            )

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

            elif kind == "finished_with_solution":
                _, solution = item
                self.best_solution = solution
                self.redraw_map()

            elif kind == "finished_no_solution":
                pass

            elif kind == "error":
                _, error_message = item
                messagebox.showerror("Error", error_message)

            elif kind == "run_finished":
                self.finish_run()

            processed += 1

        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def update_ui(
        self,
        iteration: int,
        average_fitness: float,
        best: Chromosome[int],
        pool_snapshot: np.ndarray | None,
    ) -> None:
        self.best_solution = best

        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.best_line.set_data(self.iteration_history, self.best_history)
        self.avg_line.set_data(self.iteration_history, self.avg_history)

        max_x = max(10, iteration + 1)
        max_y = max(
            1.0,
            max(self.best_history, default=0.0),
            max(self.avg_history, default=0.0),
        )
        self.ax.set_xlim(0, max_x)
        self.ax.set_ylim(0, max_y * 1.1)
        self.canvas_chart.draw_idle()

        self.total_length_var.set(f"Total Length: {int(calculate_fitness_tsp(best))}")
        self.redraw_map()

        if pool_snapshot is not None:
            self.draw_population_chart(pool_snapshot)

    def redraw_map(self) -> None:
        self.map_canvas.delete("all")
        self.draw_cities()
        self.draw_lines()

    def draw_cities(self) -> None:
        map_width, map_height = self._safe_canvas_size(
            self.map_canvas,
            self.map_default_width,
            self.map_default_height,
        )

        for city in CITIES:
            px = city.x + (map_width / 2)
            py = city.y + (map_height / 2)

            self.map_canvas.create_oval(px, py, px + 5, py + 5, fill="red", outline="")
            self.map_canvas.create_text(px + 15, py + 12, text=str(city.city_id), fill="black")

    def draw_lines(self) -> None:
        if self.best_solution is None:
            return

        map_width, map_height = self._safe_canvas_size(
            self.map_canvas,
            self.map_default_width,
            self.map_default_height,
        )

        hw = map_width / 2
        hh = map_height / 2

        for i in range(len(self.best_solution.data) - 1):
            ca = CITIES[self.best_solution.data[i]]
            cb = CITIES[self.best_solution.data[i + 1]]
            self.map_canvas.create_line(
                ca.x + hw,
                ca.y + hh,
                cb.x + hw,
                cb.y + hh,
                fill="red",
                width=2,
            )

        beg = CITIES[self.best_solution.data[0]]
        end = CITIES[self.best_solution.data[-1]]
        self.map_canvas.create_line(
            beg.x + hw,
            beg.y + hh,
            end.x + hw,
            end.y + hh,
            fill="red",
            width=2,
        )

    def draw_population_chart(self, population_snapshot: np.ndarray) -> None:
        if population_snapshot.size == 0 or self.city_color_scheme is None:
            return

        canvas_width, canvas_height = self._safe_canvas_size(
            self.pool_canvas,
            self.pool_default_width,
            self.pool_default_height,
        )

        rows, cols = population_snapshot.shape
        if rows == 0 or cols == 0:
            return

        img_w = canvas_width
        img_h = canvas_height

        row_idx = np.linspace(0, rows - 1, img_h).astype(np.int32)
        col_idx = np.linspace(0, cols - 1, img_w).astype(np.int32)

        sampled = population_snapshot[row_idx][:, col_idx]
        rgb = self.city_color_scheme[sampled]

        ppm_header = f"P6 {img_w} {img_h} 255\n".encode("ascii")
        ppm_data = ppm_header + rgb.tobytes()

        self.pool_image = tk.PhotoImage(data=ppm_data, format="PPM")
        self.pool_canvas.delete("all")
        self.pool_canvas.create_image(0, 0, anchor="nw", image=self.pool_image)

    def run(self) -> None:
        self.root.mainloop()

if __name__ == "__main__":
    root = tk.Tk()
    app = TSPApp(root)
    app.run()