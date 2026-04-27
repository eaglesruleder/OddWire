namespace OddWire.System
{
    public static class ArrayExtensions
    {
        public static float Avg(this float[] arr)
        {
            float result = 0;
            for(int i = 0; i < arr.Length; i++)
                result += arr[i];
            return result / arr.Length;
        }
    }
}
