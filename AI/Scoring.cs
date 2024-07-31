namespace AI;

internal class Scoring
{
    public static int ComputeLevenshteinDistance(byte[] s, byte[] t)
    {
        if (s == null || t == null)
            throw new ArgumentNullException("Input byte arrays cannot be null");

        int n = s.Length;
        int m = t.Length;

        // Create a matrix to store distances
        int[,] d = new int[n + 1, m + 1];

        // Initialize the matrix
        for (int i = 0; i <= n; i++)
            d[i, 0] = i;

        for (int j = 0; j <= m; j++)
            d[0, j] = j;

        // Compute the Levenshtein distance
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),  // Insertion and deletion
                    d[i - 1, j - 1] + cost  // Substitution
                );
            }
        }

        // The distance is in the bottom-right cell of the matrix
        return d[n, m];
    }

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
