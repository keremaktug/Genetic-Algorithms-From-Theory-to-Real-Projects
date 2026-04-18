from __future__ import annotations
import os
import queue
import random
import threading
import tkinter as tk
from tkinter import messagebox, ttk
from typing import Final
import numpy as np
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from matplotlib.figure import Figure
from core.ga_solver import Chromosome, CrossoverType, GeneticSolver, MutationType

# =========================================================
# 8 Queens problem-specific code
# =========================================================

BOARD_SIZE: Final[int] = 8


def solution_generator() -> Chromosome[int]:
    genes = np.random.permutation(BOARD_SIZE).astype(np.int32)
    return Chromosome(genes.tolist())


def decode(chromosome: Chromosome[int]) -> str:
    return str(chromosome.data)


def get_queen_positions(chromosome: Chromosome[int]) -> list[tuple[int, int]]:
    return [(col, row) for col, row in enumerate(chromosome.data)]


def calculate_fitness(chromosome: Chromosome[int]) -> float:
    rows = np.asarray(chromosome.data, dtype=np.int32)
    cols = np.arange(BOARD_SIZE, dtype=np.int32)

    diag1 = rows - cols
    diag2 = rows + cols

    conflicts = 0

    _, counts1 = np.unique(diag1, return_counts=True)
    _, counts2 = np.unique(diag2, return_counts=True)

    for c in counts1:
        if c > 1:
            conflicts += c * (c - 1) // 2

    for c in counts2:
        if c > 1:
            conflicts += c * (c - 1) // 2

    return float(conflicts)


def stop_condition(best: Chromosome[int]) -> bool:
    return best.fitness == 0.0


def random_gene() -> int:
    return random.randrange(BOARD_SIZE)

# =========================================================
# GUI code
# =========================================================

class QueensApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("8 Queens")
        self.root.geometry("1120x620")
        self.root.resizable(False, False)

        self.solver: GeneticSolver[int] | None = None
        self.is_running = False

        self.best_history: list[float] = []
        self.avg_history: list[float] = []
        self.iteration_history: list[int] = []

        self.current_queen_positions: list[tuple[int, int]] = []

        self.ui_queue: queue.Queue = queue.Queue()

        self.ui_update_every = 2
        self.queue_poll_ms = 40
        self.max_queue_items_per_tick = 4

        self.cell_size = 42
        self.board_draw_size = 336
        self.queen_margin = 7

        self.chessboard_image: tk.PhotoImage | None = None
        self.queen_image: tk.PhotoImage | None = None

        self.best_info_var = tk.StringVar(value="Best: -")
        self.avg_info_var = tk.StringVar(value="Average: -")
        self.iteration_info_var = tk.StringVar(value="Iteration: -")

        self.build_ui()
        self.load_images()
        self.redraw_board()
        self.root.after(self.queue_poll_ms, self.process_ui_queue)

    def build_ui(self) -> None:
        # Left column
        controls_frame = tk.LabelFrame(self.root, text="Controls", padx=10, pady=10)
        controls_frame.place(x=16, y=16, width=190, height=170)

        tk.Label(controls_frame, text="Population Size").pack(anchor="w")
        self.cmb_population = ttk.Combobox(
            controls_frame,
            state="readonly",
            values=["4", "8", "16", "32", "64", "128", "256"],
        )
        self.cmb_population.current(1)
        self.cmb_population.pack(fill="x", pady=(6, 12))

        self.btn_evolve = tk.Button(
            controls_frame,
            text="Evolve",
            command=self.start_solver,
            height=1,
        )
        self.btn_evolve.pack(fill="x")

        info_frame = tk.LabelFrame(self.root, text="Status", padx=10, pady=10)
        info_frame.place(x=16, y=202, width=190, height=130)

        tk.Label(info_frame, textvariable=self.iteration_info_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.best_info_var, anchor="w").pack(fill="x", pady=2)
        tk.Label(info_frame, textvariable=self.avg_info_var, anchor="w").pack(fill="x", pady=2)

        # Middle column
        chart_frame = tk.LabelFrame(self.root, text="Fitness Chart", padx=8, pady=8)
        chart_frame.place(x=220, y=16, width=430, height=588)

        self.fig = Figure(figsize=(4.7, 5.6), dpi=100)
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

        # Right column top
        board_frame = tk.LabelFrame(self.root, text="Chessboard", padx=8, pady=8)
        board_frame.place(x=666, y=16, width=430, height=410)

        self.board_canvas = tk.Canvas(
            board_frame,
            width=self.board_draw_size,
            height=self.board_draw_size,
            bg="white",
            highlightthickness=0,
        )
        self.board_canvas.pack(anchor="center", pady=10)

        # Right column bottom
        solution_frame = tk.LabelFrame(self.root, text="Best Chromosome", padx=8, pady=8)
        solution_frame.place(x=666, y=442, width=430, height=162)

        self.solution_label = tk.Label(
            solution_frame,
            text="-",
            justify="left",
            anchor="nw",
            font=("Consolas", 12),
            wraplength=395,
        )
        self.solution_label.pack(fill="both", expand=True)

    def load_images(self) -> None:
        board_path = "chessboard.png"
        queen_path = "queen.png"

        if os.path.exists(board_path):
            self.chessboard_image = tk.PhotoImage(file=board_path)
        if os.path.exists(queen_path):
            self.queen_image = tk.PhotoImage(file=queen_path)

    def clear_ui(self) -> None:
        self.best_history.clear()
        self.avg_history.clear()
        self.iteration_history.clear()
        self.current_queen_positions.clear()

        self.best_line.set_data([], [])
        self.avg_line.set_data([], [])
        self.ax.set_xlim(0, 10)
        self.ax.set_ylim(0, 10)
        self.canvas_chart.draw_idle()

        self.iteration_info_var.set("Iteration: -")
        self.best_info_var.set("Best: -")
        self.avg_info_var.set("Average: -")
        self.solution_label.config(text="-")

        self.redraw_board()

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

        population_size = int(self.cmb_population.get())

        self.solver = GeneticSolver[int](
            population_size=population_size,
            iteration_count=1000,
            elitism_ratio=0.2,
            mutation_ratio=0.05,
            crossover_type=CrossoverType.PMX,
            mutation_type=MutationType.SWAP,
            maximize_fitness=False,
        )

        self.solver.generator_function = solution_generator
        self.solver.fitness_function = calculate_fitness
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

        self.ui_queue.put(
            (
                "iteration",
                iteration,
                average_fitness,
                best.copy(),
            )
        )

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
                self.current_queen_positions = get_queen_positions(solution)
                self.redraw_board()
                self.solution_label.config(text=decode(solution))
                messagebox.showinfo(
                    "Solution Found",
                    f"Solution found at iteration {iteration}\n{decode(solution)}",
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

    def update_ui(self, iteration: int, average_fitness: float, best: Chromosome[int]) -> None:
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

        self.current_queen_positions = get_queen_positions(best)
        self.redraw_board()

        self.iteration_info_var.set(f"Iteration: {iteration}")
        self.best_info_var.set(f"Best: {best.fitness:.0f}")
        self.avg_info_var.set(f"Average: {average_fitness:.2f}")
        self.solution_label.config(text=decode(best))

    def redraw_board(self) -> None:
        self.board_canvas.delete("all")
        self.draw_board()
        self.draw_queens()

    def draw_board(self) -> None:
        if self.chessboard_image is not None:
            self.board_canvas.create_image(0, 0, anchor="nw", image=self.chessboard_image)
            return

        colors = ["#f0c38e", "#c98a3f"]
        for row in range(BOARD_SIZE):
            for col in range(BOARD_SIZE):
                x1 = col * self.cell_size
                y1 = row * self.cell_size
                x2 = x1 + self.cell_size
                y2 = y1 + self.cell_size
                color = colors[(row + col) % 2]
                self.board_canvas.create_rectangle(x1, y1, x2, y2, fill=color, outline="")

    def draw_queens(self) -> None:
        for col, row in self.current_queen_positions:
            x = col * self.cell_size + self.queen_margin
            y = row * self.cell_size + self.queen_margin

            if self.queen_image is not None:
                self.board_canvas.create_image(x, y, anchor="nw", image=self.queen_image)
            else:
                self.board_canvas.create_text(
                    x + 14,
                    y + 14,
                    text="Q",
                    font=("Arial", 20, "bold"),
                )

    def run(self) -> None:
        self.root.mainloop()


if __name__ == "__main__":
    root = tk.Tk()
    app = QueensApp(root)
    app.run()