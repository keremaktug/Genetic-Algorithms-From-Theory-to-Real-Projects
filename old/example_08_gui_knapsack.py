from __future__ import annotations

import queue
import threading
import tkinter as tk
from tkinter import messagebox, ttk

import numpy as np
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure

from core.ga_solver1 import CrossoverType, GeneticSolver, MutationType, Population
from old.example_07_knapsack_lib import (
    CAPACITY,
    ITEMS,
    calculate_fitness,
    calculate_totals,
    decode,
    generate_color_scheme,
    get_selected_items,
    knapsack_generator,
    population_to_index_matrix,
    random_gene,
    stop_condition,
)


class KnapsackApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Knapsack Problem")
        self.root.geometry("1180x720")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[int] | None = None
        self.is_running = False

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        self.ui_update_every = 2
        self.pool_redraw_every = 4
        self.queue_poll_ms = 40
        self.max_queue_items_per_tick = 4
        self.max_result_items = 200

        self.best_value_var = tk.StringVar(value="Best Value: -")
        self.best_weight_var = tk.StringVar(value="Best Weight: -")
        self.capacity_var = tk.StringVar(value=f"Capacity: {CAPACITY}")

        self.color_scheme = generate_color_scheme()
        self.pool_image: tk.PhotoImage | None = None

        self.build_ui()
        self.populate_items_table()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=10, y=10, width=190, height=320)

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
            values=["0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9"],
        )
        self.cmb_elitism.current(2)
        self.cmb_elitism.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Mutation Type").pack(anchor="w")
        self.cmb_mutation = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["RandomReset"],
        )
        self.cmb_mutation.current(0)
        self.cmb_mutation.pack(fill="x", pady=(4, 10))

        tk.Label(controls_frame, text="Population Size (1024)").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["1", "2", "4", "8", "16", "32"],
        )
        self.cmb_population.current(2)
        self.cmb_population.pack(fill="x", pady=(4, 10))

        self.btn_start = tk.Button(controls_frame, text="Evolve", command=self.start_solver)
        self.btn_start.pack(fill="x", pady=(10, 0))

        info_frame = tk.LabelFrame(self.root, text="Best Solution", padx=10, pady=10)
        info_frame.place(x=10, y=345, width=190, height=130)

        tk.Label(info_frame, textvariable=self.best_value_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.best_weight_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.capacity_var, anchor="w").pack(fill="x", pady=2)

        items_frame = tk.LabelFrame(self.root, text="Items", padx=8, pady=8)
        items_frame.place(x=10, y=490, width=190, height=215)

        self.items_list = tk.Listbox(items_frame, font=("Consolas", 9))
        self.items_list.pack(fill="both", expand=True)

        best_frame = tk.LabelFrame(self.root, text="Best Chromosomes", padx=5, pady=5)
        best_frame.place(x=210, y=10, width=960, height=180)

        self.result_list = tk.Listbox(best_frame, font=("Consolas", 10))
        self.result_list.pack(fill="both", expand=True)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        chart_frame.place(x=210, y=200, width=470, height=505)

        self.fig = Figure(figsize=(5.2, 4.8), dpi=100)
        self.ax = self.fig.add_subplot(111)
        self.ax.set_xlabel("Iteration")
        self.ax.set_ylabel("Fitness")
        self.ax.grid(True, alpha=0.3)
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(-10, 10)

        self.best_line, = self.ax.plot([], [], label="Best Fitness")
        self.avg_line, = self.ax.plot([], [], label="Average Fitness")
        self.ax.legend()

        self.canvas_chart = FigureCanvasTkAgg(self.fig, master=chart_frame)
        self.canvas_chart.get_tk_widget().pack(fill="both", expand=True)

        pool_frame = tk.LabelFrame(self.root, text="Population Chart", padx=8, pady=8)
        pool_frame.place(x=700, y=200, width=470, height=250)

        self.pool_canvas = tk.Canvas(
            pool_frame,
            width=430,
            height=210,
            bg="white",
            highlightthickness=0,
        )
        self.pool_canvas.pack(fill="both", expand=True)

        selected_frame = tk.LabelFrame(self.root, text="Selected Items", padx=8, pady=8)
        selected_frame.place(x=700, y=465, width=470, height=240)

        self.selected_list = tk.Listbox(selected_frame, font=("Consolas", 10))
        self.selected_list.pack(fill="both", expand=True)

    def populate_items_table(self) -> None:
        self.items_list.delete(0, tk.END)
        for item in ITEMS:
            self.items_list.insert(
                tk.END,
                f"id={item.item_id:2d}  w={item.weight:2d}  v={item.value:2d}",
            )

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)
        self.selected_list.delete(0, tk.END)

        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax.set_xlim(0, 1)
        self.ax.set_ylim(-10, 10)
        self.canvas_chart.draw_idle()

        self.pool_canvas.delete("all")
        self.pool_image = None

        self.best_value_var.set("Best Value: -")
        self.best_weight_var.set("Best Weight: -")

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

        self.solver = GeneticSolver[int](
            population_size=1024 * population_factor,
            iteration_count=1500,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.03,
            crossover_type=crossover_type,
            mutation_type=MutationType.RANDOM_RESET,
            maximize_fitness=False,
        )

        self.solver.generator_function = knapsack_generator
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
                self.update_best_item_panel(solution)

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

        if self.iteration_history:
            max_x = max(1, self.iteration_history[-1] + 1)
        else:
            max_x = 1

        min_y = min(self.best_history + self.avg_history, default=-10.0)
        max_y = max(self.best_history + self.avg_history, default=10.0)

        if min_y == max_y:
            min_y -= 1
            max_y += 1

        self.ax.set_xlim(0, max_x)
        self.ax.set_ylim(min_y * 1.1, max_y * 1.1)
        self.canvas_chart.draw_idle()

    def update_best_item_panel(self, chromosome) -> None:
        self.selected_list.delete(0, tk.END)
        selected_items = get_selected_items(chromosome)
        total_weight, total_value = calculate_totals(chromosome)

        for item in selected_items:
            self.selected_list.insert(
                tk.END,
                f"id={item.item_id:2d}  w={item.weight:2d}  v={item.value:2d}",
            )

        self.best_value_var.set(f"Best Value: {total_value}")
        self.best_weight_var.set(f"Best Weight: {total_weight}/{CAPACITY}")

    def update_ui(self, iteration: int, average_fitness: float, best, population_snapshot) -> None:
        self.append_result(f"{iteration:04d}: {decode(best)}")

        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.update_chart()
        self.update_best_item_panel(best)

        if population_snapshot is not None:
            self.draw_pool_graph_image(population_snapshot)

    def snapshot_population(self, population: Population[int]) -> np.ndarray:
        return population_to_index_matrix([chromosome.data for chromosome in population.chromosomes])

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

        row_idx = np.linspace(0, rows - 1, img_h).astype(np.int32)
        col_idx = np.linspace(0, cols - 1, img_w).astype(np.int32)

        sampled = population_snapshot[row_idx][:, col_idx]
        rgb = self.color_scheme[sampled]

        ppm_header = f"P6 {img_w} {img_h} 255\n".encode("ascii")
        ppm_data = ppm_header + rgb.tobytes()

        self.pool_image = tk.PhotoImage(data=ppm_data, format="PPM")
        self.pool_canvas.delete("all")
        self.pool_canvas.create_image(0, 0, anchor="nw", image=self.pool_image)

    def run(self) -> None:
        self.root.mainloop()


if __name__ == "__main__":
    root = tk.Tk()
    app = KnapsackApp(root)
    app.run()