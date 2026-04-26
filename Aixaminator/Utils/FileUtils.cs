using System;
using System.IO;
using System.Threading.Tasks;

namespace Aixaminator.Utils
{
    public static class FileUtils
    {
        /// <summary>
        /// Checks if a stream contains binary content by analyzing the first few bytes
        /// </summary>
        /// <param name="stream">The stream to check</param>
        /// <param name="bytesToCheck">Number of bytes to check (default: 1024)</param>
        /// <param name="nonPrintableThreshold">Threshold for percentage of non-printable characters (default: 0.3)</param>
        /// <returns>True if the stream likely contains binary content, false otherwise</returns>
        public static async Task<bool> IsBinaryContent(Stream stream, int bytesToCheck = 1024, double nonPrintableThreshold = 0.3)
        {
            // Save the current position to restore it later
            long originalPosition = stream.Position;
            
            try
            {
                byte[] buffer = new byte[bytesToCheck];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                {
                    return false; // Empty file is not binary
                }
                
                // Look for null bytes and high percentage of non-printable characters
                int nullBytes = 0;
                int nonPrintableChars = 0;
                
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0)
                    {
                        nullBytes++;
                    }
                    else if (buffer[i] < 32 && buffer[i] != 9 && buffer[i] != 10 && buffer[i] != 13) // Not tab, LF, or CR
                    {
                        nonPrintableChars++;
                    }
                }
                
                // Consider binary if it contains null bytes or high percentage of non-printable chars
                return nullBytes > 0 || (double)nonPrintableChars / bytesRead > nonPrintableThreshold;
            }
            finally
            {
                // Restore the original position
                stream.Position = originalPosition;
            }
        }
    }
} 