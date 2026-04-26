using Aixaminator.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Text;

namespace Aixaminator.Features;

public static class Documents
{
    public static async Task<Document> New(DbContext context, string name, string path, string? description = null)
    {
        var document = new Document
        {
            Name = name,
            Path = path,
            Description = description
        };

        await context.AddAsync(document);
        await context.SaveChangesAsync();

        return document;
    }

    public static async Task<Document?> GetAsync(DbContext context, Guid id)
    {
        return await context.Set<Document>()
            .Include(d => d.Notes)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public static async Task<List<Document>> GetAllAsync(DbContext context)
    {
        return await context.Set<Document>().ToListAsync();
    }

    public static async Task<string> GetPartTextAsync(Document document, int part)
    {
        var path = Path.Combine(document.FullPath, $"{part}.txt");
        if (!File.Exists(path))
        {
            return string.Empty;
        }
        return await File.ReadAllTextAsync(path);
    }

    public static async Task SavePartEdits(DbContext Context, Document document, Dictionary<int, string> editData)
    {
        foreach (var (part, text) in editData)
        {
            var path = Path.Combine(document.FullPath, $"{part}.txt");
            await File.WriteAllTextAsync(path, text);
        }

        // set locations of relevant notes to null
        await Context.Set<Note>()
            .Where(n => n.DocumentId == document.Id && editData.Keys.Contains(n.DocumentPartNumber ?? -1) && n.Location != null)
            .ExecuteUpdateAsync(n => n.SetProperty(e => e.Location, e => null));
        await Context.SaveChangesAsync();
    }

    public static async Task MarkPartRead(DbContext context, Document document, int part)
    {
        if (!document.PartsRead.Contains(part))
        {
            document.PartsRead.Add(part);
            await context.SaveChangesAsync();
        }
    }

    public static async Task TogglePartRead(DbContext context, Document document, int part)
    {
        if (document.PartsRead.Contains(part))
        {
            document.PartsRead.Remove(part);
        }
        else
        {
            document.PartsRead.Add(part);
        }
        context.Entry(document).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public static MarkupString DocumentPartToHtml(IEnumerable<Note> notes, string text)
    {
        var paragraphs = new List<string>();
        var current = new StringBuilder();
        var key = 0;

        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line) || IsTerminated(line))
            {
                if (line.Length > 0)
                {
                    current.Append(' ');
                    current.Append(line.Trim());
                }
                if (current.Length > 0)
                {
                    var currentString = current.ToString();
                    if (!string.IsNullOrWhiteSpace(currentString))
                    {
                        //paragraphs.Append($"""<p data-key="{key}">{currentString}</p>""");
                        paragraphs.Add(currentString.Trim());
                        key++;
                    }
                    current.Clear();
                }
            }
            else
            {
                current.Append(' ');
                current.Append(line.Trim());
            }
        }

        if (current.Length > 0)
        {
            var finalParagraph = current.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(finalParagraph))
            {
                paragraphs.Add(finalParagraph);
            }
        }

        if (paragraphs.Count == 0)
        {
            return new MarkupString("&lt;No content&gt;");
        }

        var regions = RebuildNoteRegions(
            notes,
            paragraphs.Select((p, i) => new { p, i }).ToDictionary(x => x.i, x => x.p.Length)
        );

        var highlightedParagraphs = ApplyRegionsToParagraphs(paragraphs, regions);

        return new MarkupString(string.Join("\n", highlightedParagraphs.Select((p, i) => $"<p data-key=\"{i}\">{p}</p>")));
    }

    public static List<string> ApplyRegionsToParagraphs(
        List<string> paragraphs,
        Dictionary<int, List<(int Start, int End, string Colour)>> regions)
    {
        for (int i = 0; i < paragraphs.Count; i++)
        {
            if (regions.TryGetValue(i, out var paragraphRegions))
            {
                // Sort regions by Start field in descending order
                paragraphRegions.Sort((a, b) => b.Start.CompareTo(a.Start));

                var paragraph = paragraphs[i];
                foreach (var region in paragraphRegions)
                {
                    // Insert the closing span tag first to avoid affecting the Start index
                    paragraph = paragraph.Insert(region.End, "</span>");
                    // Insert the opening span tag
                    paragraph = paragraph.Insert(region.Start, $"<span class=\"highlight-text {region.Colour}\">");
                }
                paragraphs[i] = paragraph;
            }
        }
        return paragraphs;
    }

    public static Dictionary<int, List<(int Start, int End, string Colour)>> RebuildNoteRegions(
        IEnumerable<Note> notes, Dictionary<int, int> paragraphLengths)
    {
        return HighlightsHelper.ProcessHighlights(
            notes,
            p => paragraphLengths.TryGetValue(p, out var len) ? len : 0
        );
    }

    private static List<string> SafeLineEndings = [
        "ie.",
        "i.e.",
        "eg.",
        "e.g.",
        "dr.",
        "mr.",
        "mrs.",
        "ms.",
        "miss.",
        "rev.",
        "etc.",
        "vs.",
        "st.",
        "cf.",
        "v.",
        "viz.",
        "sc.",
        "et al.",
        "ca.",
    ];

    private static bool IsTerminated(string text)
    {
        var t = text.Trim();
        if (t.Length == 0)
        {
            return false;
        }

        if (SafeLineEndings.Any(s => t.ToLower().EndsWith(s)))
        {
            return false;
        }

        var terminatingCharacters = new[] { '.', '!', '?' };

        for (var i = t.Length - 1; i >= 0; i--)
        {
            if (terminatingCharacters.Contains(t[i]))
            {
                return true;
            }

            if (char.IsLetterOrDigit(t[i]))
            {
                return false;
            }
        }

        return true;
    }
}
