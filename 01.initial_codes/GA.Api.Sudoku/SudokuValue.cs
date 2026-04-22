namespace GA.Api.Sudoku
{
    public class SudokuValue
    {
        public int Value { get; set; }

        public bool IsEmpty { get; set; }

        public SudokuValue()
        {
            Value = 0;
            IsEmpty = false;
        }

        public SudokuValue(int value, bool is_empty)
        {
            Value = value;
            IsEmpty = is_empty;
        }

        public override string ToString()
        {
            return $"{Value} {IsEmpty}";
        }
    }
}
