from __future__ import annotations

import queue
import random
import threading
import tkinter as tk
from dataclasses import dataclass
from tkinter import messagebox

from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure

from core.ga_solver import Chromosome, CrossoverType, GeneticSolver, MutationType


# =========================================================
# Problem-specific code
# =========================================================

GRID_LIMIT = 19
FACTOR = 20


@dataclass
class RectangleStruct:
    rect_id: int
    width: int
    height: int
    color: str


RECTANGLES: list[RectangleStruct] = [
    RectangleStruct(1, 8, 7, "blue"),
    RectangleStruct(2, 5, 3, "red"),
    RectangleStruct(3, 2, 6, "green"),
    RectangleStruct(4, 6, 4, "brown"),
    RectangleStruct(5, 3, 3, "chartreuse"),
    RectangleStruct(6, 6, 5, "dark blue"),
    RectangleStruct(7, 1, 2, "dark cyan"),
    RectangleStruct(8, 2, 1, "dark orange"),
    RectangleStruct(9, 1, 3, "dark orchid"),
    RectangleStruct(10, 1, 1, "burlywood"),
    RectangleStruct(11, 2, 1, "cyan"),
]


def best_solution_seed() -> Chromosome[int]:
    data = [
        2, 1, 0,
        4, 3, 0,
        3, 7, 0,
        4, 9, 0,
        9, 11, 0,
        12, 3, 0,
        11, 5, 0,
        12, 9, 0,
        17, 9, 0,
        1, 1, 0,
        10, 10, 0,
    ]
    return Chromosome(data)


def variables_generator() -> Chromosome[int]:
    variables: list[int] = []

    for _ in range(len(RECTANGLES)):
        x = random.randint(0, GRID_LIMIT - 1)
        y = random.randint(0, GRID_LIMIT - 1)
        o = random.randint(0, 1)
        variables.extend([x, y, o])

    return Chromosome(variables)


def decode(chromosome: Chromosome[int]) -> str:
    chunks: list[str] = []
    j = 0
    for i in range(0, len(chromosome.data), 3):
        rect = RECTANGLES[j]
        x = chromosome.data[i]
        y = chromosome.data[i + 1]
        o = chromosome.data[i + 2]
        chunks.append(f"R{rect.rect_id}=({x},{y},{o})")
        j += 1
    return " ".join(chunks)


def get_rect_instances(chromosome: Chromosome[int]) -> list[tuple[int, int, int, int, int]]:
    """
    Returns: (id, x, y, w, h)
    """
    all_rects: list[tuple[int, int, int, int, int]] = []

    j = 0
    for i in range(0, len(chromosome.data), 3):
        rect = RECTANGLES[j]
        x = int(chromosome.data[i])
        y = int(chromosome.data[i + 1])
        o = int(chromosome.data[i + 2])

        if o == 0:
            all_rects.append((rect.rect_id, x, y, rect.width, rect.height))
        else:
            all_rects.append((rect.rect_id, x, y, rect.height, rect.width))

        j += 1

    return all_rects


def overlap_area(x1: int, y1: int, w1: int, h1: int, x2: int, y2: int, w2: int, h2: int) -> int:
    left = max(x1, x2)
    right = min(x1 + w1, x2 + w2)
    top = max(y1, y2)
    bottom = min(y1 + h1, y2 + h2)
    overlap_width = max(0, right - left)
    overlap_height = max(0, bottom - top)
    return overlap_width * overlap_height


def calculate_overlapping_area(chromosome: Chromosome[int]) -> int:
    total = 0
    all_rects = get_rect_instances(chromosome)

    for a in range(len(all_rects)):
        for b in range(len(all_rects)):
            id1, x1, y1, w1, h1 = all_rects[a]
            id2, x2, y2, w2, h2 = all_rects[b]
            if id1 != id2:
                total += overlap_area(x1, y1, w1, h1, x2, y2, w2, h2)

    return total // 2


def calculate_illegal_rectangles(chromosome: Chromosome[int]) -> int:
    illegal = 0

    j = 0
    for i in range(0, len(chromosome.data), 3):
        rect = RECTANGLES[j]
        x = int(chromosome.data[i])
        y = int(chromosome.data[i + 1])
        o = int(chromosome.data[i + 2])

        w = rect.width if o == 0 else rect.height
        h = rect.height if o == 0 else rect.width

        if (x + w) > GRID_LIMIT:
            illegal += 1
        if (y + h) > GRID_LIMIT:
            illegal += 1

        j += 1

    return illegal


def calculate_bounding_box(chromosome: Chromosome[int]) -> tuple[int, int, int, int]:
    min_x = 10**9
    min_y = 10**9
    max_x = -10**9
    max_y = -10**9

    j = 0
    for i in range(0, len(chromosome.data), 3):
        rect = RECTANGLES[j]
        x = int(chromosome.data[i])
        y = int(chromosome.data[i + 1])
        o = int(chromosome.data[i + 2])

        w = rect.width if o == 0 else rect.height
        h = rect.height if o == 0 else rect.width

        rx1 = x
        ry1 = y
        rx2 = x + w
        ry2 = y + h

        min_x = min(min_x, rx1)
        min_y = min(min_y, ry1)
        max_x = max(max_x, rx2)
        max_y = max(max_y, ry2)

        j += 1

    return min_x, min_y, max_x, max_y


def calculate_fitness(chromosome: Chromosome[int]) -> float:
    overlapping_area = calculate_overlapping_area(chromosome)
    bbox = calculate_bounding_box(chromosome)
    area = (bbox[2] - bbox[0]) * (bbox[3] - bbox[1])
    illegal_rects = calculate_illegal_rectangles(chromosome)
    return float((overlapping_area * 5) + (area * 2) + (illegal_rects * 10))


def stop_condition(best: Chromosome[int]) -> bool:
    # C# örneğinde exact target yoktu; burada mükemmel yerleşim için 0 kullanıyoruz.
    return best.fitness == 0.0


# =========================================================
# GUI
# =========================================================

class RectanglePackingApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Rectangle Packing")
        self.root.geometry("1180x700")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[int] | None = None
        self.is_running = False

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.current_solution: Chromosome[int] = best_solution_seed()

        self.ui_queue: queue.Queue = queue.Queue()
        self.ui_update_every = 2
        self.queue_poll_ms = 40
        self.max_queue_items_per_tick = 4

        self.label_bbox_var = tk.StringVar(value="BBox Area: -")
        self.label_overlap_var = tk.StringVar(value="Overlapping Area: -")
        self.label_illegal_var = tk.StringVar(value="Illegal Rectangle Count: -")

        self.build_ui()
        self.redraw_map()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        controls = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls.place(x=15, y=15, width=190, height=120)

        self.btn_evolve = tk.Button(controls, text="Evolve", command=self.start_solver)
        self.btn_evolve.pack(fill="x")

        info = tk.LabelFrame(self.root, text="Metrics", padx=10, pady=10)
        info.place(x=15, y=150, width=190, height=130)

        tk.Label(info, textvariable=self.label_bbox_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info, textvariable=self.label_overlap_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info, textvariable=self.label_illegal_var, anchor="w").pack(fill="x", pady=2)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        chart_frame.place(x=220, y=15, width=430, height=670)

        self.fig = Figure(figsize=(4.6, 6.2), dpi=100)
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

        map_frame = tk.LabelFrame(self.root, text="Map", padx=8, pady=8)
        map_frame.place(x=665, y=15, width=500, height=500)

        self.map_canvas = tk.Canvas(
            map_frame,
            width=GRID_LIMIT * FACTOR + 20,
            height=GRID_LIMIT * FACTOR + 20,
            bg="white",
            highlightthickness=0,
        )
        self.map_canvas.pack(anchor="center", pady=8)

        sol_frame = tk.LabelFrame(self.root, text="Best Chromosome", padx=8, pady=8)
        sol_frame.place(x=665, y=530, width=500, height=155)

        self.solution_label = tk.Label(
            sol_frame,
            text="-",
            justify="left",
            anchor="nw",
            wraplength=470,
            font=("Consolas", 10),
        )
        self.solution_label.pack(fill="both", expand=True)

    def clear_ui(self) -> None:
        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax.set_xlim(0, 10)
        self.ax.set_ylim(0, 10)
        self.canvas_chart.draw_idle()

        self.label_bbox_var.set("BBox Area: -")
        self.label_overlap_var.set("Overlapping Area: -")
        self.label_illegal_var.set("Illegal Rectangle Count: -")
        self.solution_label.config(text="-")

    def flush_ui_queue(self) -> None:
        while True:
            try:
                self.ui_queue.get_nowait()
            except queue.Empty:
                break

    def start_solver(self) -> None:
        if self.is_running:
            return

        self.clear_ui()
        self.flush_ui_queue()

        self.is_running = True
        self.btn_evolve.config(state="disabled")

        self.solver = GeneticSolver[int](
            population_size=8*1024,
            iteration_count=5000,
            elitism_ratio=0.35,
            mutation_ratio=0.1,
            crossover_type=CrossoverType.UNIFORM,
            mutation_type=MutationType.RANDOM_RESET,
            maximize_fitness=False,
        )

        self.solver.generator_function = variables_generator
        self.solver.fitness_function = calculate_fitness
        self.solver.random_gene_function = lambda: random.randint(0, 19)
        self.solver.stop_condition_function = stop_condition
        self.solver.iteration_completed_callback = self.on_iteration_completed
        self.solver.solution_found_callback = self.on_solution_found

        threading.Thread(target=self.run_solver, daemon=True).start()

    def run_solver(self) -> None:
        try:
            if self.solver is not None:
                result = self.solver.evolve()
                if result is None:
                    self.ui_queue.put(("finished_no_solution",))
        except Exception as exc:
            self.ui_queue.put(("error", str(exc)))
        finally:
            self.ui_queue.put(("run_finished",))

    def finish_run(self) -> None:
        self.is_running = False
        self.btn_evolve.config(state="normal")

    def on_iteration_completed(self, iteration: int, average_fitness: float, best: Chromosome[int]) -> None:
        if iteration != 0 and iteration % self.ui_update_every != 0 and best.fitness != 0:
            return
        self.ui_queue.put(("iteration", iteration, average_fitness, best.copy()))

    def on_solution_found(self, iteration: int, solution: Chromosome[int]) -> None:
        self.ui_queue.put(("solution_found", iteration, solution.copy()))

    def process_ui_queue(self) -> None:
        processed = 0

        while processed < self.max_queue_items_per_tick:
            try:
                item = self.ui_queue.get_nowait()
            except queue.Empty:
                break

            kind = item[0]

            if kind == "iteration":
                _, iteration, average_fitness, best = item
                self.update_ui(iteration, average_fitness, best)

            elif kind == "solution_found":
                _, iteration, solution = item
                self.current_solution = solution
                self.update_metrics(solution)
                self.redraw_map()
                self.solution_label.config(text=decode(solution))
                messagebox.showinfo("Solution Found", f"Solution found at iteration {iteration}")

            elif kind == "finished_no_solution":
                messagebox.showinfo("Finished", "Exact solution was not found within the iteration limit.")

            elif kind == "error":
                _, error_message = item
                messagebox.showerror("Error", error_message)

            elif kind == "run_finished":
                self.finish_run()

            processed += 1

        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def update_ui(self, iteration: int, average_fitness: float, best: Chromosome[int]) -> None:
        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.best_line.set_data(self.iteration_history, self.best_history)
        self.avg_line.set_data(self.iteration_history, self.avg_history)

        max_x = max(10, iteration + 1)
        max_y = max(1.0, max(self.best_history, default=0.0), max(self.avg_history, default=0.0))
        self.ax.set_xlim(0, max_x)
        self.ax.set_ylim(0, max_y * 1.1)
        self.canvas_chart.draw_idle()

        self.current_solution = best
        self.update_metrics(best)
        self.redraw_map()
        self.solution_label.config(text=decode(best))

    def update_metrics(self, chromosome: Chromosome[int]) -> None:
        bbox = calculate_bounding_box(chromosome)
        area = (bbox[2] - bbox[0]) * (bbox[3] - bbox[1])
        overlap = calculate_overlapping_area(chromosome)
        illegal = calculate_illegal_rectangles(chromosome)

        self.label_bbox_var.set(f"BBox Area: {area}")
        self.label_overlap_var.set(f"Overlapping Area: {overlap}")
        self.label_illegal_var.set(f"Illegal Rectangle Count: {illegal}")

    def redraw_map(self) -> None:
        self.map_canvas.delete("all")
        self.draw_grid()
        self.draw_rectangles()
        self.draw_bounding_box()

    def draw_grid(self) -> None:
        for i in range(1, GRID_LIMIT):
            for j in range(1, GRID_LIMIT):
                x = i * FACTOR
                y = j * FACTOR
                self.map_canvas.create_oval(x - 1.25, y - 1.25, x + 1.25, y + 1.25, fill="black", outline="")

    def draw_rectangles(self) -> None:
        j = 0
        for i in range(0, len(self.current_solution.data), 3):
            rect = RECTANGLES[j]
            x = int(self.current_solution.data[i])
            y = int(self.current_solution.data[i + 1])
            o = int(self.current_solution.data[i + 2])

            if o == 0:
                w = rect.width
                h = rect.height
            else:
                w = rect.height
                h = rect.width

            self.map_canvas.create_rectangle(
                x * FACTOR,
                y * FACTOR,
                (x + w) * FACTOR,
                (y + h) * FACTOR,
                fill=rect.color,
                outline="black",
            )
            j += 1

    def draw_bounding_box(self) -> None:
        bbox = calculate_bounding_box(self.current_solution)
        self.map_canvas.create_rectangle(
            bbox[0] * FACTOR,
            bbox[1] * FACTOR,
            bbox[2] * FACTOR,
            bbox[3] * FACTOR,
            outline="red",
            width=2,
        )

    def run(self) -> None:
        self.root.mainloop()

if __name__ == "__main__":
    root = tk.Tk()
    app = RectanglePackingApp(root)
    app.run()