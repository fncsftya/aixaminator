namespace Aixaminator.Features;

using Aixaminator.Data;
using Region = (int Start, int End, string Colour);

public static class HighlightsHelper
{
    public static Dictionary<int, List<Region>> ProcessHighlights(
        IEnumerable<Note> notes,
        Func<int, int> paragraphLength
    )
    {
        var regions = new Dictionary<int, List<Region>>();

        foreach (var note in notes.OrderByDescending(n => n.Id))
        {
            var loc = note.Location;
            if (loc is null) continue;

            for (var p = loc.StartKey; p <= loc.EndKey; p++)
            {
                var start = p == loc.StartKey ? loc.StartOffset : 0;
                var end = p == loc.EndKey ? loc.EndOffset : paragraphLength(p);
                if (start > end) continue;

                if (!regions.TryGetValue(p, out var existing))
                {
                    existing = new List<Region>();
                    regions[p] = existing;
                }

                var newRegions = BreakOverlap((start, end, note.HighlightColour), existing);
                foreach (var nr in newRegions)
                {
                    if (nr.Start <= nr.End) existing.Add(nr);
                }
            }
        }

        return regions;
    }

    private static IEnumerable<Region> BreakOverlap(Region region, List<Region> existing)
    {
        // Start with one segment, carve out overlaps from existing
        var segments = new List<Region> { region };

        foreach (var e in existing.OrderByDescending(r => r.Start))
        {
            var nextSegments = new List<Region>();
            foreach (var s in segments)
            {
                nextSegments.AddRange(CarveOut(s, e));
            }
            segments = nextSegments;
        }

        return segments;
    }

    private static IEnumerable<Region> CarveOut(Region bigger, Region smaller)
    {
        // If no overlap, just return the original
        if (smaller.End < bigger.Start || smaller.Start > bigger.End)
            return new[] { bigger };

        // Identify overlap boundaries
        var overlapStart = Math.Max(bigger.Start, smaller.Start);
        var overlapEnd = Math.Min(bigger.End, smaller.End);

        // If bigger is fully contained, split bigger into two sub-segments
        var result = new List<Region>();
        Region leftSegment = (bigger.Start, overlapStart, bigger.Colour);
        Region rightSegment = (overlapEnd, bigger.End, bigger.Colour);

        if (leftSegment.Start < leftSegment.End)
            result.Add(leftSegment);
        if (rightSegment.Start < rightSegment.End)
            result.Add(rightSegment);

        return result;
    }
}
