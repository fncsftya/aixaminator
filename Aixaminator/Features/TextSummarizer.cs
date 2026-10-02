using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aixaminator.Features;

/// <summary>
/// Utility class for extracting important highlights from text without using LLM APIs.
/// Implements various extractive summarization algorithms.
/// </summary>
public static class TextSummarizer
{
    /// <summary>
    /// List of common English stop words that don't carry significant meaning
    /// </summary>
    private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "but", "or", "for", "nor", "on", "at", "to", "from", "by",
        "about", "in", "of", "with", "that", "this", "these", "those", "is", "are", "was",
        "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will",
        "would", "shall", "should", "can", "could", "may", "might", "must", "it", "its",
        "they", "them", "their", "we", "us", "our", "you", "your", "he", "him", "his",
        "she", "her", "hers", "i", "me", "my", "mine"
    };

    /// <summary>
    /// Extracts key sentences from a text using multiple heuristic methods
    /// </summary>
    /// <param name="text">The input text to extract highlights from</param>
    /// <param name="maxHighlights">Maximum number of highlights to extract</param>
    /// <param name="maxLength">Maximum total length of extracted highlights in characters</param>
    /// <returns>A list of extracted highlight sentences</returns>
    public static List<string> ExtractKeyHighlights(string text, int maxHighlights = 10, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        // Split text into sentences
        var sentences = SplitIntoSentences(text);
        if (sentences.Count == 0)
            return new List<string>();

        // Score sentences using different criteria
        var sentenceScores = new Dictionary<string, double>();
        foreach (var sentence in sentences)
        {
            // Skip very short sentences
            if (sentence.Length < 20)
                continue;

            double score = ScoreSentence(sentence, sentences);
            sentenceScores[sentence] = score;
        }

        // Get top scoring sentences
        var highlights = sentenceScores
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .Take(maxHighlights)
            .ToList();

        // Reorder sentences by their original position in the text
        highlights = ReorderByOriginalPosition(highlights, text);

        // Trim the highlights if they exceed the maximum length
        return TrimHighlights(highlights, maxLength);
    }

    /// <summary>
    /// Splits text into sentences using regex
    /// </summary>
    private static List<string> SplitIntoSentences(string text)
    {
        // Pattern to split text into sentences
        var sentencePattern = @"(?<=[.!?])\s+(?=[A-Z])";

        // Split into sentences and remove any empty ones
        var sentences = Regex.Split(text, sentencePattern)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }

    /// <summary>
    /// Score a sentence based on various heuristics
    /// </summary>
    private static double ScoreSentence(string sentence, List<string> allSentences)
    {
        double score = 0;

        // 1. Position-based scoring (topic sentences)
        if (allSentences.Count > 0 && allSentences[0] == sentence)
            score += 0.5; // First sentence carries more weight

        // Last sentence often contains conclusions
        if (allSentences.Count > 0 && allSentences[^1] == sentence)
            score += 0.3;

        // 2. Length-based scoring (not too short, not too long)
        var wordCount = sentence.Split(' ').Length;
        if (wordCount >= 8 && wordCount <= 25)
            score += 0.3;

        // 3. Content-based scoring
        // Check for indicator phrases that suggest importance
        var indicatorPhrases = new Dictionary<string, double>
        {
            // Conclusion and summary indicators (highest weight)
            { "in conclusion", 0.7 },
            { "to conclude", 0.7 },
            { "to summarize", 0.7 },
            { "in summary", 0.7 },
            { "therefore", 0.6 },
            { "thus", 0.6 },
            { "hence", 0.6 },
            { "consequently", 0.6 },
            { "as a result", 0.6 },
            { "overall", 0.5 },
            { "ultimately", 0.5 },
            { "finally", 0.5 },
            
            // Importance indicators
            { "important", 0.6 },
            { "significant", 0.6 },
            { "essential", 0.6 },
            { "crucial", 0.6 },
            { "critical", 0.6 },
            { "key", 0.6 },
            { "fundamental", 0.6 },
            { "vital", 0.6 },
            { "necessary", 0.5 },
            { "primary", 0.5 },
            { "major", 0.5 },
            { "central", 0.5 },
            
            // Definition and explanation indicators
            { "is defined as", 0.5 },
            { "refers to", 0.5 },
            { "can be described as", 0.5 },
            { "means that", 0.5 },
            { "is characterized by", 0.5 },
            { "for example", 0.4 },
            { "for instance", 0.4 },
            { "specifically", 0.4 },
            { "in particular", 0.4 },
            { "notably", 0.4 },
            { "particularly", 0.4 },
            
            // Research and evidence indicators (common in academic texts)
            { "research shows", 0.5 },
            { "studies indicate", 0.5 },
            { "evidence suggests", 0.5 },
            { "according to", 0.5 },
            { "findings suggest", 0.5 },
            { "data indicates", 0.5 },
            { "analysis reveals", 0.5 },
            { "demonstrates that", 0.5 },
            { "proves that", 0.5 },
            { "confirms that", 0.5 },
            
            // Contrast and comparison indicators
            { "however", 0.4 },
            { "nevertheless", 0.4 },
            { "conversely", 0.4 },
            { "in contrast", 0.4 },
            { "on the other hand", 0.4 },
            { "unlike", 0.4 },
            { "whereas", 0.4 },
            { "while", 0.3 },
            
            // Cause and effect indicators
            { "because", 0.4 },
            { "since", 0.4 },
            { "due to", 0.4 },
            { "leads to", 0.4 },
            { "results in", 0.4 },
            { "causes", 0.4 },
            { "affects", 0.4 },
            { "influences", 0.4 },
            
            // Domain-specific indicators (common in science/technical writing)
            { "hypothesis", 0.5 },
            { "theory", 0.5 },
            { "principle", 0.5 },
            { "concept", 0.5 },
            { "mechanism", 0.5 },
            { "method", 0.4 },
            { "technique", 0.4 },
            { "algorithm", 0.4 },
            { "framework", 0.4 },
            { "process", 0.4 },
            { "function", 0.4 },
            { "equation", 0.4 },
            
            // Historical and factual indicators
            { "historically", 0.4 },
            { "traditionally", 0.4 },
            { "originally", 0.4 },
            { "established", 0.4 },
            { "discovered", 0.4 },
            { "invented", 0.4 },
            { "developed", 0.4 },
            { "founded", 0.4 },
            { "created", 0.3 },
            
            // Wikipedia-specific indicators
            { "notable", 0.5 },
            { "known for", 0.5 },
            { "famous for", 0.5 },
            { "significant contribution", 0.5 },
            { "main article", 0.4 },
            { "further information", 0.4 }
        };

        var lowerSentence = sentence.ToLower();
        foreach (var phrase in indicatorPhrases)
        {
            if (lowerSentence.Contains(phrase.Key))
                score += phrase.Value;
        }

        // 4. Check for presence of proper nouns, numbers, and dates
        if (ContainsProperNoun(sentence))
            score += 0.25;

        if (ContainsNumbers(sentence))
            score += 0.25;

        if (ContainsDates(sentence))
            score += 0.3;

        // 5. Check for presence of quotes (often important in non-fiction)
        if (sentence.Contains("\"") || sentence.Contains(""") || sentence.Contains("""))
            score += 0.35;

        return score;
    }

    /// <summary>
    /// Checks if a sentence contains proper nouns (simplified check)
    /// </summary>
    private static bool ContainsProperNoun(string sentence)
    {
        // Simple check for capitalized words that aren't at the beginning of the sentence
        var words = sentence.Split(' ');
        for (int i = 1; i < words.Length; i++)
        {
            if (words[i].Length > 1 && char.IsUpper(words[i][0]))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if a sentence contains numbers
    /// </summary>
    private static bool ContainsNumbers(string sentence)
    {
        return Regex.IsMatch(sentence, @"\d+");
    }

    /// <summary>
    /// Checks if a sentence contains dates
    /// </summary>
    private static bool ContainsDates(string sentence)
    {
        // Check for year patterns (e.g., 1999, 2023)
        if (Regex.IsMatch(sentence, @"\b(19|20)\d{2}\b"))
            return true;

        // Check for month names
        string[] months = { "january", "february", "march", "april", "may", "june",
                           "july", "august", "september", "october", "november", "december" };

        var lowerSentence = sentence.ToLower();
        foreach (var month in months)
        {
            if (lowerSentence.Contains(month))
                return true;
        }

        // Check for date formats (e.g., 01/01/2023, 1-1-2023)
        if (Regex.IsMatch(sentence, @"\b\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{2,4}\b"))
            return true;

        return false;
    }

    /// <summary>
    /// Reorder highlights to match their original position in the text
    /// </summary>
    private static List<string> ReorderByOriginalPosition(List<string> highlights, string originalText)
    {
        var positionMap = new Dictionary<string, int>();

        foreach (var highlight in highlights)
        {
            int position = originalText.IndexOf(highlight);
            if (position >= 0)
                positionMap[highlight] = position;
        }

        return highlights
            .Where(h => positionMap.ContainsKey(h))
            .OrderBy(h => positionMap[h])
            .ToList();
    }

    /// <summary>
    /// Removes stop words from a sentence to reduce its length
    /// </summary>
    /// <param name="sentence">The sentence to process</param>
    /// <returns>The sentence with stop words removed</returns>
    private static string RemoveStopWords(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence))
            return sentence;

        // Extract punctuation at the end of the sentence to preserve it
        string endPunctuation = "";
        if (sentence.Length > 0 && ".!?".Contains(sentence[^1]))
        {
            endPunctuation = sentence[^1].ToString();
            sentence = sentence.Substring(0, sentence.Length - 1);
        }

        // Split the sentence into words, preserving important punctuation
        var pattern = @"(\w+)([.,;:!?]?)";
        var matches = Regex.Matches(sentence, pattern);

        var processedWords = new List<string>();

        foreach (Match match in matches)
        {
            string word = match.Groups[1].Value;
            string punctuation = match.Groups[2].Value;

            // Skip stop words but add any punctuation to the previous word if possible
            if (!StopWords.Contains(word.ToLower()))
            {
                processedWords.Add(word + punctuation);
            }
            else if (!string.IsNullOrEmpty(punctuation) && processedWords.Count > 0)
            {
                // If we're removing a stop word with punctuation, add the punctuation to the previous word
                processedWords[^1] = processedWords[^1] + punctuation;
            }
        }

        // If removing stop words would make the sentence too short or meaningless, return the original
        if (processedWords.Count < 3 || processedWords.Count < matches.Count * 0.4)
            return sentence + endPunctuation;

        // Reconstruct the sentence with preserved punctuation
        return string.Join(" ", processedWords) + endPunctuation;
    }

    /// <summary>
    /// Trims the list of highlights to ensure they don't exceed the maximum length.
    /// Uses several strategies in sequence:
    /// 1. Remove stop words from all highlights to reduce length
    /// 2. Ensure representation from different parts of the document
    /// 3. Remove highlights at regular intervals if still over limit
    /// 4. Take shortest highlights if all else fails
    /// </summary>
    /// <param name="highlights">The list of highlights to trim</param>
    /// <param name="maxLength">The maximum total length allowed</param>
    /// <returns>A trimmed list of highlights within the length limit</returns>
    private static List<string> TrimHighlights(List<string> highlights, int maxLength)
    {
        if (highlights == null || highlights.Count == 0)
            return new List<string>();

        // Calculate total length of all highlights
        int totalLength = highlights.Sum(h => h.Length);

        // If we're already under the maximum length, return all highlights
        if (totalLength <= maxLength)
            return highlights.ToList();

        // First try: Remove stop words from all highlights to reduce length
        var highlightsWithoutStopWords = highlights.Select(RemoveStopWords).ToList();
        int lengthWithoutStopWords = highlightsWithoutStopWords.Sum(h => h.Length);

        if (lengthWithoutStopWords <= maxLength)
        {
            return highlightsWithoutStopWords;
        }

        // If removing stop words wasn't enough, continue with the original algorithm
        // but use the highlights with stop words removed as our input

        // First pass: Divide document into N equal parts (where N is the number of highlights)
        // and ensure we have at least 1 highlight from each part
        int numParts = Math.Min(5, highlightsWithoutStopWords.Count); // Use at most 5 parts
        var parts = new List<List<string>>();

        int itemsPerPart = (int)Math.Ceiling(highlightsWithoutStopWords.Count / (double)numParts);
        for (int i = 0; i < numParts; i++)
        {
            int startIdx = i * itemsPerPart;
            int count = Math.Min(itemsPerPart, highlightsWithoutStopWords.Count - startIdx);
            if (count <= 0) break;

            parts.Add(highlightsWithoutStopWords.Skip(startIdx).Take(count).ToList());
        }

        // Select at least one highlight from each part
        var selectedHighlights = parts.Select(part => part.FirstOrDefault())
                                     .OfType<string>()
                                     .ToList();

        // If selecting one per part already exceeds the max length, 
        // we need a different approach - take the shortest from each part
        if (selectedHighlights.Sum(h => h.Length) > maxLength)
        {
            selectedHighlights = parts.Select(part => part.OrderBy(h => h.Length).FirstOrDefault())
                                     .OfType<string>()
                                     .ToList();

            // If we're still over the limit, sort by length and take shortest ones first
            if (selectedHighlights.Sum(h => h.Length) > maxLength)
            {
                selectedHighlights = new List<string>();
                foreach (var highlight in highlightsWithoutStopWords.OrderBy(h => h.Length))
                {
                    if (selectedHighlights.Sum(h => h.Length) + highlight.Length <= maxLength)
                    {
                        selectedHighlights.Add(highlight);
                    }
                }

                // Restore original order
                selectedHighlights = selectedHighlights.OrderBy(h => highlightsWithoutStopWords.IndexOf(h)).ToList();
                return selectedHighlights;
            }
        }

        // Now add remaining highlights as space permits
        // First, add highlights from each part in round-robin fashion
        var remainingHighlights = highlightsWithoutStopWords.Except(selectedHighlights).ToList();
        var partIndices = new Dictionary<string, int>();

        // Assign part index to each remaining highlight
        for (int i = 0; i < parts.Count; i++)
        {
            foreach (var highlight in parts[i].Except(selectedHighlights))
            {
                partIndices[highlight] = i;
            }
        }

        // Sort remaining highlights by their position in the original list
        remainingHighlights = remainingHighlights.OrderBy(h => highlightsWithoutStopWords.IndexOf(h)).ToList();

        // Keep adding highlights until we hit the length limit
        int currentLength = selectedHighlights.Sum(h => h.Length);
        foreach (var highlight in remainingHighlights)
        {
            if (currentLength + highlight.Length <= maxLength)
            {
                selectedHighlights.Add(highlight);
                currentLength += highlight.Length;
            }
        }

        // If we still exceed the limit, remove highlights at regular intervals
        if (currentLength > maxLength && selectedHighlights.Count > numParts)
        {
            var finalHighlights = new List<string>();
            int skipInterval = 2; // Start by removing every 3rd item

            while (skipInterval > 0)
            {
                finalHighlights = new List<string>();
                for (int i = 0; i < selectedHighlights.Count; i++)
                {
                    if (i % skipInterval != 0) // Skip at regular intervals
                    {
                        finalHighlights.Add(selectedHighlights[i]);
                    }
                }

                currentLength = finalHighlights.Sum(h => h.Length);
                if (currentLength <= maxLength || finalHighlights.Count <= numParts)
                    break;

                skipInterval--; // Try a more aggressive interval
            }

            // If we still exceed the length, just take the shortest highlights
            if (currentLength > maxLength)
            {
                return TrimHighlights(selectedHighlights.OrderBy(h => h.Length).ToList(), maxLength);
            }

            return finalHighlights.OrderBy(h => highlightsWithoutStopWords.IndexOf(h)).ToList();
        }

        // Return the selected highlights in their original order
        return selectedHighlights.OrderBy(h => highlightsWithoutStopWords.IndexOf(h)).ToList();
    }
}