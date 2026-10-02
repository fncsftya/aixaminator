namespace Aixaminator.Features;

public static class BinaryContentDetector
{
    /// <summary>
    /// Heuristically checks whether a stream contains binary (non-text) content by inspecting its first bytes.
    /// The stream's position is restored afterwards.
    /// </summary>
    /// <param name="bytesToCheck">Number of bytes to inspect.</param>
    /// <param name="nonPrintableThreshold">Fraction of control characters above which the content is considered binary.</param>
    public static async Task<bool> IsBinaryAsync(
        Stream stream,
        CancellationToken cancellationToken = default,
        int bytesToCheck = 1024,
        double nonPrintableThreshold = 0.3)
    {
        var originalPosition = stream.Position;
        try
        {
            var buffer = new byte[bytesToCheck];
            var bytesRead = await stream.ReadAtLeastAsync(buffer, bytesToCheck, throwOnEndOfStream: false, cancellationToken);
            if (bytesRead == 0)
            {
                return false;
            }

            var nullBytes = 0;
            var nonPrintable = 0;
            foreach (var b in buffer.AsSpan(0, bytesRead))
            {
                if (b == 0)
                {
                    nullBytes++;
                }
                else if (b < 32 && b is not (9 or 10 or 13)) // not tab, LF or CR
                {
                    nonPrintable++;
                }
            }

            return nullBytes > 0 || (double)nonPrintable / bytesRead > nonPrintableThreshold;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}
