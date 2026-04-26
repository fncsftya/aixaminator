using System.Text.Json.Serialization;

namespace Aixaminator.Data;

// Based on https://stackoverflow.com/a/48236789
// Note: They use startTextIndex and endTextIndex. Those are used
// if each node has children. In our case, we only have top-level <p> tags.

public class HighlightLocation
{
    [JsonPropertyName("startKey")]
    public int StartKey { get; set; }

    [JsonPropertyName("endKey")]
    public int EndKey { get; set; }

    [JsonPropertyName("startOffset")]
    public int StartOffset { get; set; }

    [JsonPropertyName("endOffset")]
    public int EndOffset { get; set; }
}
