namespace AI;

internal class Scoring
{
    public static double CalculateMSE(byte[] array1, byte[] array2)
    {
        if (array1 == null || array2 == null)
        {
            throw new ArgumentNullException("Arrays cannot be null.");
        }

        if (array1.Length != array2.Length)
        {
            throw new ArgumentException("Arrays must have the same length.");
        }

        double mse = 0.0;

        for (int i = 0; i < array1.Length; i++)
        {
            double diff = array1[i] - array2[i];
            mse += diff * diff;
        }

        return mse / array1.Length;
    }
}
