namespace GA.Api.TSP
{
    public class City
    {
        public int Id { get; set; }
        public float CoordinateX { get; set; }
        public float CoordinateY { get; set; }

        public City(int id, float x, float y)
        {
            Id = id;
            CoordinateX = x;
            CoordinateY = y;
        }
    }
}
