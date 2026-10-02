using Aixaminator.Data;

namespace Aixaminator.Features;

/// <summary>A highlighted range within a single paragraph. <see cref="End"/> is exclusive.</summary>
public readonly record struct HighlightRegion(int Start, int End, string Colour);

/// <summary>
/// Resolves (possibly overlapping) highlight notes into non-overlapping regions per paragraph.
/// Where highlights overlap the most recent one (highest <see cref="Note.Id"/>) wins.
/// </summary>
public static class HighlightRegions
{
    /// <param name="notes">Notes to consider; anything that isn't a positioned, coloured highlight is ignored.</param>
    /// <param name="paragraphLength">Returns the length of the paragraph with the given key (0 if it doesn't exist).</param>
    /// <returns>Regions keyed by paragraph, each list sorted by <see cref="HighlightRegion.Start"/>.</returns>
    public static Dictionary<int, List<HighlightRegion>> Compute(IEnumerable<Note> notes, Func<int, int> paragraphLength)
    {
        var regions = new Dictionary<int, List<HighlightRegion>>();

        var highlights = notes
            .Where(n => n.IsHighlight && n.Location is not null && !string.IsNullOrWhiteSpace(n.HighlightColour))
            .OrderByDescending(n => n.Id);

        foreach (var note in highlights)
        {
            var location = note.Location!;
            for (var key = location.StartKey; key <= location.EndKey; key++)
            {
                var length = paragraphLength(key);
                var start = Math.Clamp(key == location.StartKey ? location.StartOffset : 0, 0, length);
                var end = Math.Clamp(key == location.EndKey ? location.EndOffset : length, 0, length);
                if (start >= end)
                {
                    continue;
                }

                if (!regions.TryGetValue(key, out var existing))
                {
                    existing = [];
                    regions[key] = existing;
                }

                // Newer highlights were added first, so only the parts not already covered are visible.
                existing.AddRange(CarveOutAll(new HighlightRegion(start, end, note.HighlightColour!), existing));
            }
        }

        foreach (var key in regions.Where(r => r.Value.Count == 0).Select(r => r.Key).ToList())
        {
            regions.Remove(key);
        }

        foreach (var list in regions.Values)
        {
            list.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        return regions;
    }

    private static List<HighlightRegion> CarveOutAll(HighlightRegion region, IEnumerable<HighlightRegion> existing)
    {
        var segments = new List<HighlightRegion> { region };
        foreach (var taken in existing)
        {
            segments = segments.SelectMany(s => CarveOut(s, taken)).ToList();
        }
        return segments;
    }

    private static IEnumerable<HighlightRegion> CarveOut(HighlightRegion region, HighlightRegion taken)
    {
        if (taken.End <= region.Start || taken.Start >= region.End)
        {
            yield return region;
            yield break;
        }

        if (region.Start < taken.Start)
        {
            yield return region with { End = taken.Start };
        }

        if (taken.End < region.End)
        {
            yield return region with { Start = taken.End };
        }
    }
}
