from __future__ import annotations

import queue
import threading
import tkinter as tk
from tkinter import messagebox, ttk

import numpy as np
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure

from core.ga_solver import CrossoverType, GeneticSolver, MutationType, Population
from example_11_vrp_lib import (
    CUSTOMERS,
    CUSTOMER_COUNT,
    DEPOT_X,
    DEPOT_Y,
    VEHICLE_CAPACITY,
    calculate_fitness,
    decode,
    generate_color_scheme,
    get_route_summary,
    population_to_index_matrix,
    split_routes,
    total_distance,
    stop_condition,
    vrp_generator,
)


class VRPApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Vehicle Routing Problem")
        self.root.geometry("1280x760")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[int] | None = None
        self.is_running = False
        self.best_solution = None

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        self.ui_update_every = 2
        self.pool_redraw_every = 5
        self.queue_poll_ms = 40
        self.max_queue_items_per_tick = 4
        self.max_result_items = 200

        self.total_distance_var = tk.StringVar(value="Total Distance: -")
        self.route_count_var = tk.StringVar(value="Vehicle Count: -")
        self.capacity_var = tk.StringVar(value=f"Vehicle Capacity: {VEHICLE_CAPACITY}")

        self.pool_color_scheme = generate_color_scheme(CUSTOMER_COUNT)
        self.route_colors = [
            "#e41a1c", "#377eb8", "#4daf4a", "#984ea3",
            "#ff7f00", "#a65628", "#f781bf", "#999999",
        ]
        self.pool_image: tk.PhotoImage | None = None

        self.build_ui()
        self.redraw_map()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=10, y=10, width=210, height=310)

        tk.Label(controls_frame, text="Crossover Type").pack(anchor="w")
        self.cmb_crossover = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["OnePointCrossover", "UniformCrossover", "PMX"],
        )
        self.cmb_crossover.current(2)
        self.cmb_crossover.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Mutation Type").pack(anchor="w")
        self.cmb_mutation = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["Swap", "Scramble", "Inverse"],
        )
        self.cmb_mutation.current(0)
        self.cmb_mutation.pack(fill="x", pady=(4, 10))

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

        info_frame = tk.LabelFrame(self.root, text="Best Solution", padx=10, pady=10)
        info_frame.place(x=10, y=335, width=210, height=120)

        tk.Label(info_frame, textvariable=self.total_distance_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.route_count_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.capacity_var, anchor="w").pack(fill="x", pady=2)

        best_frame = tk.LabelFrame(self.root, text="Best Chromosomes", padx=5, pady=5)
        best_frame.place(x=230, y=10, width=1040, height=180)

        self.result_list = tk.Listbox(best_frame, font=("Consolas", 10))
        self.result_list.pack(fill="both", expand=True)

        map_frame = tk.LabelFrame(self.root, text="Route Map", padx=8, pady=8)
        map_frame.place(x=230, y=200, width=500, height=540)

        self.map_canvas = tk.Canvas(
            map_frame,
            width=460,
            height=500,
            bg="white",
            highlightthickness=0,
        )
        self.map_canvas.pack(fill="both", expand=True)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        chart_frame.place(x=750, y=200, width=520, height=260)

        self.fig = Figure(figsize=(5.2, 2.3), dpi=100)
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

        pool_frame = tk.LabelFrame(self.root, text="Population Chart", padx=8, pady=8)
        pool_frame.place(x=750, y=470, width=520, height=120)

        self.pool_canvas = tk.Canvas(
            pool_frame,
            width=480,
            height=80,
            bg="white",
            highlightthickness=0,
        )
        self.pool_canvas.pack(fill="both", expand=True)

        routes_frame = tk.LabelFrame(self.root, text="Route Summary", padx=8, pady=8)
        routes_frame.place(x=750, y=600, width=520, height=140)

        self.routes_list = tk.Listbox(routes_frame, font=("Consolas", 9))
        self.routes_list.pack(fill="both", expand=True)

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)
        self.routes_list.delete(0, tk.END)

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

        self.total_distance_var.set("Total Distance: -")
        self.route_count_var.set("Vehicle Count: -")

        self.best_solution = None
        self.redraw_map()

    def flush_ui_queue(self) -> None:
        while True:
            try:
                self.ui_queue.get_nowait()
            except queue.Empty:
                break

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
        self.btn_start.config(state="disabled")

        population_factor = int(self.cmb_population.get())
        elitism_rate = float(self.cmb_elitism.get())
        crossover_type = self.map_crossover_type()
        mutation_type = self.map_mutation_type()

        self.solver = GeneticSolver[int](
            population_size=1024 * population_factor,
            iteration_count=1800,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.06,
            crossover_type=crossover_type,
            mutation_type=mutation_type,
            maximize_fitness=False,
        )

        self.solver.generator_function = vrp_generator
        self.solver.fitness_function = calculate_fitness
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
                self.update_solution_panels(solution)

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

    def update_solution_panels(self, chromosome) -> None:
        self.best_solution = chromosome
        routes = split_routes(chromosome)
        total = total_distance(chromosome)

        self.total_distance_var.set(f"Total Distance: {total:.2f}")
        self.route_count_var.set(f"Vehicle Count: {len(routes)}")

        self.routes_list.delete(0, tk.END)
        for line in get_route_summary(chromosome):
            self.routes_list.insert(tk.END, line)

        self.redraw_map()

    def update_ui(self, iteration: int, average_fitness: float, best, population_snapshot) -> None:
        self.append_result(f"{iteration:04d}: {decode(best)}")

        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.update_chart()
        self.update_solution_panels(best)

        if population_snapshot is not None:
            self.draw_population_chart(population_snapshot)

    def snapshot_population(self, population: Population[int]) -> np.ndarray:
        return population_to_index_matrix([chromosome.data for chromosome in population.chromosomes])

    def _safe_canvas_size(self, canvas: tk.Canvas, default_w: int, default_h: int) -> tuple[int, int]:
        width = int(canvas.winfo_width())
        height = int(canvas.winfo_height())
        if width <= 2:
            width = default_w
        if height <= 2:
            height = default_h
        return width, height

    def redraw_map(self) -> None:
        self.map_canvas.delete("all")

        width, height = self._safe_canvas_size(self.map_canvas, 460, 500)

        # World bounds
        xs = [c.x for c in CUSTOMERS] + [DEPOT_X]
        ys = [c.y for c in CUSTOMERS] + [DEPOT_Y]
        min_x, max_x = min(xs), max(xs)
        min_y, max_y = min(ys), max(ys)

        margin = 40
        usable_w = width - 2 * margin
        usable_h = height - 2 * margin

        span_x = max(1.0, max_x - min_x)
        span_y = max(1.0, max_y - min_y)
        scale = min(usable_w / span_x, usable_h / span_y)

        def map_point(x: float, y: float) -> tuple[float, float]:
            px = margin + (x - min_x) * scale
            py = margin + (max_y - y) * scale
            return px, py

        depot_px, depot_py = map_point(DEPOT_X, DEPOT_Y)
        self.map_canvas.create_oval(depot_px - 7, depot_py - 7, depot_px + 7, depot_py + 7, fill="black")
        self.map_canvas.create_text(depot_px + 18, depot_py, text="Depot", anchor="w")

        for customer in CUSTOMERS:
            px, py = map_point(customer.x, customer.y)
            self.map_canvas.create_oval(px - 4, py - 4, px + 4, py + 4, fill="red")
            self.map_canvas.create_text(px + 10, py + 8, text=str(customer.customer_id), anchor="w")

        if self.best_solution is None:
            return

        routes = split_routes(self.best_solution)

        for route_idx, route in enumerate(routes):
            color = self.route_colors[route_idx % len(self.route_colors)]
            points = [(DEPOT_X, DEPOT_Y)]
            for idx in route:
                c = CUSTOMERS[idx]
                points.append((c.x, c.y))
            points.append((DEPOT_X, DEPOT_Y))

            for i in range(len(points) - 1):
                x1, y1 = map_point(*points[i])
                x2, y2 = map_point(*points[i + 1])
                self.map_canvas.create_line(x1, y1, x2, y2, fill=color, width=2)

    def draw_population_chart(self, population_snapshot: np.ndarray) -> None:
        if population_snapshot.size == 0:
            return

        canvas_width, canvas_height = self._safe_canvas_size(self.pool_canvas, 480, 80)

        rows, cols = population_snapshot.shape
        if rows == 0 or cols == 0:
            return

        img_w = canvas_width
        img_h = canvas_height

        row_idx = np.linspace(0, rows - 1, img_h).astype(np.int32)
        col_idx = np.linspace(0, cols - 1, img_w).astype(np.int32)

        sampled = population_snapshot[row_idx][:, col_idx]
        rgb = self.pool_color_scheme[sampled]

        ppm_header = f"P6 {img_w} {img_h} 255\n".encode("ascii")
        ppm_data = ppm_header + rgb.tobytes()

        self.pool_image = tk.PhotoImage(data=ppm_data, format="PPM")
        self.pool_canvas.delete("all")
        self.pool_canvas.create_image(0, 0, anchor="nw", image=self.pool_image)

    def run(self) -> None:
        self.root.mainloop()


if __name__ == "__main__":
    root = tk.Tk()
    app = VRPApp(root)
    app.run()