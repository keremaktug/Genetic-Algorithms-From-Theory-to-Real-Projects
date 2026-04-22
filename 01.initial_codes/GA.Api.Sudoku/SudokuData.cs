using System.Text;

namespace GA.Api.Sudoku
{
    public class SudokuData
    {
        public SudokuValue[,] Values { get; set; }

        public List<int> ValuesWillBePlaced { get; set; }

        public SudokuData()
        {
            Values = new SudokuValue[9, 9];
            ValuesWillBePlaced = new List<int>();

            var k = 1;

            for(int i = 0; i < 9; i++)
            {
                for(int j = 0; j < 9; j++)
                {
                    Values[i, j] = new SudokuValue(k, false);
                    k++;
                }
            }
        }

        public SudokuData(string values)
        {
            if (values.Length != 81)
            {
                throw new Exception("Sudoku values are not correct");
            }

            Values = new SudokuValue[9, 9];
            
            var values_ch = values.ToCharArray();

            for(int i = 0; i < 81; i++)
            {
                var row = (int)(i / 9);
                var col = i % 9;
                var value = Int32.Parse(values_ch[i].ToString());
                Values[row, col] = new SudokuValue(value, value == 0 ? true : false);
            }

            var n1 = 9 - values.Where(x => x == '1').Count();
            var n2 = 9 - values.Where(x => x == '2').Count();
            var n3 = 9 - values.Where(x => x == '3').Count();
            var n4 = 9 - values.Where(x => x == '4').Count();
            var n5 = 9 - values.Where(x => x == '5').Count();
            var n6 = 9 - values.Where(x => x == '6').Count();
            var n7 = 9 - values.Where(x => x == '7').Count();
            var n8 = 9 - values.Where(x => x == '8').Count();
            var n9 = 9 - values.Where(x => x == '9').Count();
            var n_count = n1 + n2 + n3 + n4 + n5 + n6 + n7 + n8 + n9;

            ValuesWillBePlaced = new List<int>();

            for (int i = 0; i < n1; i++) ValuesWillBePlaced.Add(1);
            for (int i = 0; i < n2; i++) ValuesWillBePlaced.Add(2);
            for (int i = 0; i < n3; i++) ValuesWillBePlaced.Add(3);
            for (int i = 0; i < n4; i++) ValuesWillBePlaced.Add(4);
            for (int i = 0; i < n5; i++) ValuesWillBePlaced.Add(5);
            for (int i = 0; i < n6; i++) ValuesWillBePlaced.Add(6);
            for (int i = 0; i < n7; i++) ValuesWillBePlaced.Add(7);
            for (int i = 0; i < n8; i++) ValuesWillBePlaced.Add(8);
            for (int i = 0; i < n9; i++) ValuesWillBePlaced.Add(9);
        }

        public SudokuValue Get(int row, int col)
        {
            return Values[row, col];
        }

        public void Set(int row, int col, SudokuValue value)
        {
            Values[row, col] = value;
        }

        public int GetRowTotal(int row)
        {
            var r = 0;

            for(int i = 0; i < 9; i++)
            {
                r += Values[row, i].Value;
            }

            return r;
        }

        public int GetColTotal(int col)
        {
            var r = 0;

            for (int i = 0; i < 9; i++)
            {
                r += Values[i, col].Value;
            }

            return r;
        }

        public List<SudokuValue> GetRow(int row)
        {
            var r = new List<SudokuValue>();

            for(int i = 0; i < 9; i++)
            {
                r.Add(Values[row, i]);
            }

            return r;
        }

        public List<SudokuValue> GetColumn(int col)
        {
            var r = new List<SudokuValue>();

            for(int i = 0; i < 9; i++)
            {
                r.Add(Values[i, col]);
            }

            return r;
        }

        public int GetRowDuplicationCount(int row)
        {
            var row_data = GetRow(row);

            var set = new HashSet<int>();

            for(int i = 0; i < row_data.Count; i++)
            {
                if (!set.Contains(row_data[i].Value))
                {
                    set.Add(row_data[i].Value);
                }
            }

            return 9 - set.Count;
        }

        public int GetColDuplicationCount(int col)
        {
            var col_data = GetColumn(col);

            var set = new HashSet<int>();

            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    if (!set.Contains(col_data[i].Value))
                    {
                        set.Add(col_data[i].Value);
                    }
                }
            }

            return 9 - set.Count;
        }        

        public int GetBoxDuplicationCount(int box_num)
        {
            var box_values = GetBoxValues(box_num);

            var set = new HashSet<int>();

            for (int i = 0; i < 9; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    if (!set.Contains(box_values[i].Value))
                    {
                        set.Add(box_values[i].Value);
                    }
                }
            }

            return 9 - set.Count;
        }

        public List<SudokuValue> GetBoxValues(int box_num)
        {
            var r = new List<SudokuValue>();

            if(box_num == 0)
            {
                for(int i = 0; i < 3; i++)
                {
                    for(int j = 0; j < 3; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }
            else if(box_num == 1)
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 3; j < 6; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }
            else if (box_num == 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 6; j < 9; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }
            else if(box_num == 3)
            {
                for (int i = 3; i < 6; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }
            else if (box_num == 4)
            {
                for (int i = 3; i < 6; i++)
                {
                    for (int j = 3; j < 6; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }
            else if (box_num == 5)
            {
                for (int i = 3; i < 6; i++)
                {
                    for (int j = 6; j < 9; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }
            else if (box_num == 6)
            {
                for (int i = 6; i < 9; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }
            else if (box_num == 7)
            {
                for (int i = 6; i < 9; i++)
                {
                    for (int j = 3; j < 6; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }
            else if (box_num == 8)
            {
                for (int i = 6; i < 9; i++)
                {
                    for (int j = 6; j < 9; j++)
                    {
                        r.Add(Get(i, j));
                    }
                }
            }

            r = r.OrderBy(i => i.Value).ToList();

            return r;
        }

        public List<SudokuValue> GetEmptyValues()
        {
            var r = new List<SudokuValue>();

            for(int row = 0; row < 9; row++)
            {
                for(int col = 0; col < 9; col++)
                {
                    var value = Values[row, col];

                    if (value.IsEmpty)
                    {
                        r.Add(value);
                    }
                }
            }

            return r;
        }

        public override string ToString()
        {
            var sbuilder = new StringBuilder();

            for(int row = 0; row < 9; row++)
            {
                sbuilder.AppendLine($"{Values[row, 0].Value} {Values[row, 1].Value} {Values[row, 2].Value} {Values[row, 3].Value} {Values[row, 4].Value} {Values[row, 5].Value} {Values[row, 6].Value} {Values[row, 7].Value} {Values[row, 8].Value}");
            }

            return sbuilder.ToString();
        }
    }
}
