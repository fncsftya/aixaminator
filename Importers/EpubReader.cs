using System.Text;
using VersOne.Epub;
using VersOne.Epub.Schema;
using HtmlAgilityPack;

namespace Importers;

public class EpubReader
{
    /// <summary>
    /// Extracts all text content from an EPUB file (version 2 or 3).
    /// </summary>
    /// <param name="epubStream">The stream containing the EPUB file</param>
    /// <returns>A string containing all the text content from the EPUB file</returns>
    public string ExtractText(Stream epubStream)
    {
        var result = new StringBuilder();

        // Open the EPUB book
        using var bookRef = VersOne.Epub.EpubReader.OpenBook(epubStream);
        
        // Add book title if available
        if (!string.IsNullOrEmpty(bookRef.Title))
        {
            result.AppendLine(bookRef.Title);
            result.AppendLine();
        }

        // Process all HTML content files in reading order
        foreach (var spineItemRef in bookRef.GetReadingOrder())
        {
            try
            {
                // Read the HTML content
                string? html = null;
                if (spineItemRef is EpubLocalTextContentFileRef localContentFileRef)
                {
                    // Read the content using ReadString instead of ReadContentAsText
                    html = localContentFileRef.ReadContentAsText();
                }
                
                // If we have content, convert HTML to plain text using HtmlAgilityPack
                if (!string.IsNullOrWhiteSpace(html))
                {
                    string plainText = ConvertHtmlToText(html);
                    if (!string.IsNullOrWhiteSpace(plainText))
                    {
                        result.AppendLine(plainText);
                        result.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle exceptions gracefully - skip problematic content and continue
                result.AppendLine($"Error extracting content: {ex.Message}");
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts HTML content to plain text using HtmlAgilityPack for better parsing results
    /// </summary>
    /// <param name="html">The HTML content to convert</param>
    /// <returns>The extracted plain text with preserved structure</returns>
    private string ConvertHtmlToText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        var sb = new StringBuilder();
        
        // Extract text from the document body, preserving structure
        ProcessNode(doc.DocumentNode, sb);
        
        // Normalize whitespace - replace multiple newlines with just two
        string result = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n");
        
        return result.Trim();
    }
    
    /// <summary>
    /// Recursively process HTML nodes to extract text while preserving structure
    /// </summary>
    private void ProcessNode(HtmlNode node, StringBuilder sb)
    {
        if (node == null) return;
        
        // Skip comment nodes and script/style nodes
        if (node.NodeType == HtmlNodeType.Comment ||
            node.Name.Equals("script", StringComparison.OrdinalIgnoreCase) ||
            node.Name.Equals("style", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        
        // Handle text nodes
        if (node.NodeType == HtmlNodeType.Text)
        {
            string text = node.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Decode HTML entities and add to the result
                sb.Append(HtmlEntity.DeEntitize(text.Trim()));
                
                // Add space if this is inline text and the last character isn't a space
                if (sb.Length > 0 && !char.IsWhiteSpace(sb[sb.Length - 1]))
                {
                    sb.Append(' ');
                }
            }
            return;
        }
        
        // Handle special block elements
        bool isBlockElement = IsBlockElement(node.Name);
        
        // Add newlines before block elements
        if (isBlockElement && sb.Length > 0)
        {
            sb.AppendLine();
        }
        
        // Special handling for headings - add emphasis
        if (node.Name.StartsWith("h") && node.Name.Length == 2 && char.IsDigit(node.Name[1]))
        {
            // Process children
            foreach (var child in node.ChildNodes)
            {
                ProcessNode(child, sb);
            }
            
            // Add extra newline after headings
            sb.AppendLine();
            return;
        }
        
        // Special handling for list items
        if (node.Name.Equals("li", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("• "); // Add bullet for list items
        }
        
        // Process all child nodes
        foreach (var child in node.ChildNodes)
        {
            ProcessNode(child, sb);
        }
        
        // Add newlines after certain block elements
        if (isBlockElement)
        {
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Determines if an HTML element is a block element that should have newlines around it
    /// </summary>
    private bool IsBlockElement(string nodeName)
    {
        string[] blockElements = {
            "div", "p", "h1", "h2", "h3", "h4", "h5", "h6", 
            "ul", "ol", "li", "table", "tr", "pre", "blockquote", 
            "hr", "section", "article", "aside", "header", "footer",
            "nav", "figure", "figcaption", "form", "fieldset", "dl",
            "dt", "dd", "address"
        };
        
        return blockElements.Contains(nodeName.ToLowerInvariant());
    }
}
