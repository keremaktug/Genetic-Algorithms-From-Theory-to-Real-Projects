using System.Drawing;

namespace GA.Api._8Queens
{
    public class RectangleStruct
    {
        public int Id { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Brush Brush { get; set; }

        public RectangleStruct(int id, int width, int height, Brush brush)
        {
            Id = id;
            Width = width;
            Height = height;
            Brush = brush;
        }
    }
}
