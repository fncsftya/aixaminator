namespace Shared.AI;

public static class Utils
{
   public static double GetCosineSimilarity(ReadOnlyMemory<float> V1m, ReadOnlyMemory<float> V2m)
    {
        int N = 0;
        var V1 = V1m.ToArray();
        var V2 = V2m.ToArray();

        N = ((V2.Length < V1.Length) ? V2.Length : V1.Length);
        double dot = 0.0d;
        double mag1 = 0.0d;
        double mag2 = 0.0d;
        for (int n = 0; n < N; n++)
        {
            dot += V1[n] * V2[n];
            mag1 += Math.Pow(V1[n], 2.0);
            mag2 += Math.Pow(V2[n], 2.0);
        }

        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
}
