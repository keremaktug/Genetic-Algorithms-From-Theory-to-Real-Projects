using System.Text;

namespace GA.Api.Sudoku
{
    public partial class SudokuGrid : UserControl
    {
        SudokuCell[,] cells = new SudokuCell[9, 9];
        Color InitialCellColor = Color.Black;
        Color NewCellColor = Color.Red;

        public SudokuGrid()
        {
            InitializeComponent();            
        }

        public void Set(SudokuData data)
        {
            Controls.Clear();

            for(int row = 0; row < 9; row++)
            {
                for(int col = 0; col < 9; col++)
                {
                    var value = data.Values[row, col].Value;
                    var is_empty = data.Values[row, col].IsEmpty;
                    cells[row, col] = new SudokuCell(row, col, value, is_empty);
                    Controls.Add(cells[row, col]);
                }
            }
        }

        public void SetValue(int x, int y, int value)
        {
            cells[x, y].Value = value;
            cells[x, y].Text = value.ToString();
        }
    }
}
