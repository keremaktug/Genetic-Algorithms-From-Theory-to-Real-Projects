using System.Collections.Generic;

namespace GA.Api
{
    public static class CollectionExtension
    {
        public static void Fill<T>(this List<T> emptySource, T val, int number) where T : new()
        {
            for (int i = 0; i < number; i++)
            {
                emptySource.Add(val);
            }
        }
    }
}
