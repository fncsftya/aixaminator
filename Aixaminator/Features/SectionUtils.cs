using Aixaminator.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aixaminator.Features
{
    public static class SectionUtils
    {
        /// <summary>
        /// Gets the list of valid (visible) section numbers for a document
        /// </summary>
        /// <param name="document">The document to get sections for</param>
        /// <param name="parts">The dictionary of document parts with visibility settings</param>
        /// <returns>A list of valid section numbers</returns>
        public static List<int> GetValidSections(Document document, Dictionary<int, DocumentPart> parts)
        {
            // Get all available section numbers from the document's folder
            var path = document.FullPath;
            List<int> sections = new List<int>();
            
            if (Directory.Exists(path))
            {
                sections = Directory.GetFiles(path, "*.txt")
                    .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f)))
                    .Order()
                    .ToList();
            }
            
            // Filter out hidden sections
            return sections.Where(p => !parts.ContainsKey(p) || !parts[p].Hidden).ToList();
        }

        /// <summary>
        /// Gets a part name, either from the settings or using a default format
        /// </summary>
        /// <param name="partNumber">The part number</param>
        /// <param name="parts">The dictionary of document parts with name settings</param>
        /// <returns>The formatted part name</returns>
        public static string GetPartName(int partNumber, Dictionary<int, DocumentPart> parts)
        {
            if (!parts.ContainsKey(partNumber) || string.IsNullOrWhiteSpace(parts[partNumber].Name))
            {
                return $"Part {partNumber + 1}";
            }
            return parts[partNumber].Name;
        }
    }
} 