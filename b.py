import colorsys
import math
import queue
import random
import threading
import tkinter as tk
from tkinter import ttk, messagebox

from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure

from core.ga_solver import (
    Chromosome,
    CrossoverType,
    GeneticSolver,
    MutationType,
    Population,
)


LETTERS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ 1234567890,."
PHRASE = "Those who live in glass houses should not throw stones"


def decode(chromosome: Chromosome[str]) -> str:
    return "".join(chromosome.data)


def random_gene() -> str:
    return random.choice(LETTERS)


def phrase_generator() -> Chromosome[str]:
    return Chromosome([random_gene() for _ in range(len(PHRASE))])


def calculate_fitness(chromosome: Chromosome[str]) -> float:
    return float(
        sum(abs(ord(PHRASE[i]) - ord(chromosome.data[i])) for i in range(len(PHRASE)))
    )


def stop_condition(best: Chromosome[str]) -> bool:
    return best.fitness == 0


def generate_color_scheme(count: int) -> list[tuple[int, int, int]]:
    if count <= 0:
        return []

    colors: list[tuple[int, int, int]] = []
    for i in range(count):
        h = i / count
        r, g, b = colorsys.hls_to_rgb(h, 0.5, 0.75)
        colors.append((int(r * 255), int(g * 255), int(b * 255)))
    return colors


class PhraseEvolutionApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Phrase Evolution GUI")
        self.root.geometry("1100x700")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[str] | None = None
        self.is_running = False

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.ui_queue: queue.Queue = queue.Queue()

        # Performance knobs
        self.ui_update_every = 10
        self.pool_redraw_every = 40
        self.queue_poll_ms = 60
        self.max_queue_items_per_tick = 8
        self.max_result_items = 200

        self.domain_values = list(LETTERS)
        self.domain_index = {ch: i for i, ch in enumerate(self.domain_values)}
        self.color_scheme = generate_color_scheme(len(self.domain_values))

        self.pool_image: tk.PhotoImage | None = None

        self.build_ui()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        left_frame = tk.LabelFrame(self.root, text="Parameters", padx=10, pady=10)
        left_frame.place(x=10, y=10, width=180, height=330)

        tk.Label(left_frame, text="Crossover Type").pack(anchor="w")
        self.cmb_crossover = ttk.Combobox(
            left_frame,
            state="readonly",
            values=["OnePointCrossover", "UniformCrossover", "PMX"],
        )
        self.cmb_crossover.current(1)
        self.cmb_crossover.pack(fill="x", pady=(0, 10))

        tk.Label(left_frame, text="Elitism Rate").pack(anchor="w")
        self.cmb_elitism = ttk.Combobox(
            left_frame,
            state="readonly",
            values=["0.1", "0.2", "0.3", "0.4", "0.5", "0.6", "0.7", "0.8", "0.9", "1.0"],
        )
        self.cmb_elitism.current(2)
        self.cmb_elitism.pack(fill="x", pady=(0, 10))

        tk.Label(left_frame, text="Mutation Type").pack(anchor="w")
        self.cmb_mutation = ttk.Combobox(
            left_frame,
            state="readonly",
            values=["RandomReset", "Swap", "Scramble", "Inverse"],
        )
        self.cmb_mutation.current(0)
        self.cmb_mutation.pack(fill="x", pady=(0, 10))

        tk.Label(left_frame, text="Population Size (1024)").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            left_frame,
            state="readonly",
            values=["1", "2", "4", "8", "16", "32", "64", "128"],
        )
        self.cmb_population.current(5)
        self.cmb_population.pack(fill="x", pady=(0, 15))

        self.btn_start = tk.Button(left_frame, text="Start", command=self.start_solver)
        self.btn_start.pack(fill="x")

        result_frame = tk.LabelFrame(self.root, text="Best Chromosomes", padx=5, pady=5)
        result_frame.place(x=200, y=10, width=880, height=220)

        self.result_list = tk.Listbox(result_frame, font=("Consolas", 10))
        self.result_list.pack(fill="both", expand=True)

        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=5, pady=5)
        chart_frame.place(x=200, y=240, width=430, height=440)

        self.fig = Figure(figsize=(5, 4), dpi=100)
        self.ax = self.fig.add_subplot(111)
        self.ax.set_xlabel("Iteration")
        self.ax.set_ylabel("Fitness")
        self.ax.grid(True, alpha=0.3)

        self.best_line, = self.ax.plot([], [], label="Best Fitness")
        self.avg_line, = self.ax.plot([], [], label="Average Fitness")
        self.ax.legend()

        self.canvas = FigureCanvasTkAgg(self.fig, master=chart_frame)
        self.canvas.get_tk_widget().pack(fill="both", expand=True)

        pool_frame = tk.LabelFrame(self.root, text="Genetic Pool", padx=5, pady=5)
        pool_frame.place(x=650, y=240, width=430, height=440)

        self.pool_canvas = tk.Canvas(pool_frame, bg="white")
        self.pool_canvas.pack(fill="both", expand=True)

    def clear_ui(self) -> None:
        self.result_list.delete(0, tk.END)

        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax.relim()
        self.ax.autoscale_view()
        self.canvas.draw_idle()

        self.pool_canvas.delete("all")
        self.pool_image = None

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
        if value == "RandomReset":
            return MutationType.RANDOM_RESET
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

        self.solver = GeneticSolver[str](
            population_size=1024 * population_factor,
            iteration_count=1000,
            elitism_ratio=elitism_rate,
            mutation_ratio=0.01,
            crossover_type=crossover_type,
            mutation_type=mutation_type,
            maximize_fitness=False,
        )

        self.solver.generator_function = phrase_generator
        self.solver.fitness_function = calculate_fitness
        self.solver.random_gene_function = random_gene
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
        self.btn_start.config(state="normal")

    def on_iteration_completed(
        self,
        iteration: int,
        average_fitness: float,
        best: Chromosome[str],
    ) -> None:
        should_push = (
            iteration == 0
            or iteration % self.ui_update_every == 0
            or best.fitness == 0
        )
        if not should_push:
            return

        pool_snapshot = None
        if self.solver is not None and iteration % self.pool_redraw_every == 0:
            pool_snapshot = self.snapshot_population(self.solver.population)

        self.ui_queue.put(
            (
                "iteration",
                iteration,
                average_fitness,
                best.copy(),
                pool_snapshot,
            )
        )

    def on_solution_found(self, iteration: int, solution: Chromosome[str]) -> None:
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
                _, iteration, average_fitness, best, pool_snapshot = item
                self.update_ui(iteration, average_fitness, best, pool_snapshot)

            elif kind == "solution_found":
                _, iteration, solution = item
                messagebox.showinfo(
                    "Solution Found",
                    f"Iteration Count: {iteration}\n{decode(solution)}",
                )

            elif kind == "finished_no_solution":
                messagebox.showinfo(
                    "Finished",
                    "Exact solution was not found within the iteration limit.",
                )

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
        self.ax.relim()
        self.ax.autoscale_view()
        self.canvas.draw_idle()

    def update_ui(
        self,
        iteration: int,
        average_fitness: float,
        best: Chromosome[str],
        pool_snapshot: list[list[str]] | None,
    ) -> None:
        self.append_result(f"{iteration:04d}: {decode(best)}")

        self.iteration_history.append(iteration)
        self.best_history.append(best.fitness)
        self.avg_history.append(average_fitness)

        self.update_chart()

        if pool_snapshot is not None:
            self.draw_pool_graph_image(pool_snapshot)

    def snapshot_population(self, population: Population[str]) -> list[list[str]]:
        return [chromosome.data.copy() for chromosome in population.chromosomes]

    def draw_pool_graph_image(self, population_snapshot: list[list[str]]) -> None:
        if not population_snapshot:
            return

        canvas_width = max(1, int(self.pool_canvas.winfo_width()))
        canvas_height = max(1, int(self.pool_canvas.winfo_height()))

        if canvas_width <= 1 or canvas_height <= 1:
            return

        rows = len(population_snapshot)
        cols = len(population_snapshot[0])

        if rows == 0 or cols == 0:
            return

        # Downsample source population into image resolution
        img_w = min(canvas_width, cols)
        img_h = min(canvas_height, rows)

        if img_w <= 0 or img_h <= 0:
            return

        row_scale = rows / img_h
        col_scale = cols / img_w

        ppm = bytearray()
        header = f"P6 {img_w} {img_h} 255\n".encode("ascii")
        ppm.extend(header)

        for y in range(img_h):
            src_row = min(rows - 1, int(y * row_scale))
            chromosome = population_snapshot[src_row]

            for x in range(img_w):
                src_col = min(cols - 1, int(x * col_scale))
                gene = chromosome[src_col]
                idx = self.domain_index.get(gene, 0)
                r, g, b = self.color_scheme[idx]
                ppm.extend((r, g, b))

        self.pool_image = tk.PhotoImage(data=bytes(ppm), format="PPM")
        self.pool_canvas.delete("all")
        self.pool_canvas.create_image(0, 0, anchor="nw", image=self.pool_image)

        # Stretch visually if the generated image is smaller than canvas
        if img_w != canvas_width or img_h != canvas_height:
            # Keep it simple: center the image rather than scaling expensively
            self.pool_canvas.delete("all")
            x = max(0, (canvas_width - img_w) // 2)
            y = max(0, (canvas_height - img_h) // 2)
            self.pool_canvas.create_image(x, y, anchor="nw", image=self.pool_image)

    def run(self) -> None:
        self.root.mainloop()


if __name__ == "__main__":
    root = tk.Tk()
    app = PhraseEvolutionApp(root)
    app.run()