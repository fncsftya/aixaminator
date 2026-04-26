using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Importers;

public class WikiReader
{
    private readonly HttpClient _httpClient;
    private const string WikiApiUrl = "https://en.wikipedia.org/w/rest.php/v1/page/";
    private const string WikiBaseUrl = "https://en.wikipedia.org/wiki/";

    public WikiReader()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Aixaminator/1.0");
    }

    /// <summary>
    /// Extracts content from a Wikipedia page using the URL
    /// </summary>
    /// <param name="wikiUrl">The URL of the Wikipedia page</param>
    /// <returns>The extracted content as text</returns>
    public async Task<string> ExtractTextFromUrlAsync(string wikiUrl)
    {
        string pageTitle = ExtractPageTitleFromUrl(wikiUrl);
        if (string.IsNullOrEmpty(pageTitle))
        {
            throw new ArgumentException("Could not extract page title from URL", nameof(wikiUrl));
        }

        return await ExtractTextFromTitleAsync(pageTitle);
    }

    /// <summary>
    /// Extracts content from a Wikipedia page using the page title
    /// </summary>
    /// <param name="pageTitle">The title of the Wikipedia page</param>
    /// <returns>The extracted content as text</returns>
    public async Task<string> ExtractTextFromTitleAsync(string pageTitle)
    {
        try
        {
            // First try to get the content using the REST API
            var apiContent = await GetWikiContentFromApiAsync(pageTitle);
            if (!string.IsNullOrEmpty(apiContent))
            {
                return apiContent;
            }
        }
        catch (Exception)
        {
            // If the API fails, we'll fall back to scraping
        }

        // Fall back to scraping the HTML page if the API fails
        return await ScrapeWikiPageAsync(pageTitle);
    }

    /// <summary>
    /// Extracts the page title from a Wikipedia URL
    /// </summary>
    /// <param name="wikiUrl">The Wikipedia URL</param>
    /// <returns>The page title</returns>
    private string ExtractPageTitleFromUrl(string wikiUrl)
    {
        if (string.IsNullOrEmpty(wikiUrl))
        {
            return string.Empty;
        }

        // Check if it's a Wikipedia URL
        if (!wikiUrl.Contains("wikipedia.org"))
        {
            throw new ArgumentException("Not a valid Wikipedia URL", nameof(wikiUrl));
        }

        // Extract the page title from the URL
        int wikiIndex = wikiUrl.LastIndexOf("/wiki/");
        if (wikiIndex == -1)
        {
            return string.Empty;
        }

        string pageTitle = wikiUrl.Substring(wikiIndex + 6); // +6 to skip "/wiki/"
        
        // Remove any URL fragments
        int fragmentIndex = pageTitle.IndexOf('#');
        if (fragmentIndex != -1)
        {
            pageTitle = pageTitle.Substring(0, fragmentIndex);
        }
        
        // URL decode the title
        return Uri.UnescapeDataString(pageTitle);
    }

    /// <summary>
    /// Gets the wiki content using the REST API
    /// </summary>
    /// <param name="pageTitle">The page title</param>
    /// <returns>The processed content</returns>
    private async Task<string> GetWikiContentFromApiAsync(string pageTitle)
    {
        // Construct the API URL
        string apiUrl = $"{WikiApiUrl}{Uri.EscapeDataString(pageTitle)}";
        
        // Call the API
        var response = await _httpClient.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();
        
        // Parse the JSON response
        var jsonString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonString);
        
        // Extract the content
        var root = doc.RootElement;
        
        if (root.TryGetProperty("source", out var source))
        {
            // This is wikitext format that needs to be processed
            string wikiText = source.GetString() ?? string.Empty;
            return ProcessWikiText(wikiText);
        }
        else if (root.TryGetProperty("html", out var html))
        {
            // If we get HTML directly, parse it
            return ExtractTextFromHtml(html.GetString() ?? string.Empty);
        }
        else
        {
            // If we can't find the content, throw an exception
            throw new InvalidOperationException("Could not find content in API response");
        }
    }

    /// <summary>
    /// Processes the wikitext format (very simplified parsing)
    /// </summary>
    /// <param name="wikiText">The wikitext content</param>
    /// <returns>Processed text</returns>
    private string ProcessWikiText(string wikiText)
    {
        if (string.IsNullOrEmpty(wikiText))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        
        // Very basic wikitext processing - could be extended
        
        // Remove references
        wikiText = Regex.Replace(wikiText, @"<ref[^>]*>.*?</ref>", string.Empty, RegexOptions.Singleline);
        
        // Remove HTML comments
        wikiText = Regex.Replace(wikiText, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
        
        // Handle headers
        wikiText = Regex.Replace(wikiText, @"======(.*?)======", "$1", RegexOptions.Multiline);
        wikiText = Regex.Replace(wikiText, @"=====(.*?)=====", "$1", RegexOptions.Multiline);
        wikiText = Regex.Replace(wikiText, @"====(.*?)====", "$1", RegexOptions.Multiline);
        wikiText = Regex.Replace(wikiText, @"===(.*?)===", "$1", RegexOptions.Multiline);
        wikiText = Regex.Replace(wikiText, @"==(.*?)==", "$1", RegexOptions.Multiline);
        
        // Handle lists
        wikiText = Regex.Replace(wikiText, @"^\* ", "• ", RegexOptions.Multiline);
        wikiText = Regex.Replace(wikiText, @"^\# ", "1. ", RegexOptions.Multiline);
        
        // Handle bold/italic
        wikiText = Regex.Replace(wikiText, @"'''''(.*?)'''''", "$1", RegexOptions.Singleline);
        wikiText = Regex.Replace(wikiText, @"'''(.*?)'''", "$1", RegexOptions.Singleline);
        wikiText = Regex.Replace(wikiText, @"''(.*?)''", "$1", RegexOptions.Singleline);
        
        // Handle simple links
        wikiText = Regex.Replace(wikiText, @"\[\[(.*?)\]\]", m => {
            var link = m.Groups[1].Value;
            if (link.Contains("|"))
            {
                return link.Split('|')[1]; // Use the display text
            }
            return link; // Use the link itself
        });
        
        // Handle external links
        wikiText = Regex.Replace(wikiText, @"\[(https?://[^\s\]]+)\s+([^\]]+)\]", "$2");
        
        // Handle templates (very simplified)
        wikiText = Regex.Replace(wikiText, @"\{\{[^}]*\}\}", string.Empty);
        
        // Add the processed text to the StringBuilder
        sb.Append(wikiText);
        
        return sb.ToString();
    }

    /// <summary>
    /// Scrapes the HTML page as a fallback method
    /// </summary>
    /// <param name="pageTitle">The page title</param>
    /// <returns>The extracted content</returns>
    private async Task<string> ScrapeWikiPageAsync(string pageTitle)
    {
        // Construct the URL for the Wikipedia page
        string url = $"{WikiBaseUrl}{Uri.EscapeDataString(pageTitle)}";
        
        // Get the HTML
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        string html = await response.Content.ReadAsStringAsync();
        
        // Extract text from HTML
        return ExtractTextFromHtml(html);
    }

    /// <summary>
    /// Extracts text from HTML content using HtmlAgilityPack
    /// </summary>
    /// <param name="html">The HTML content</param>
    /// <returns>The extracted text</returns>
    private string ExtractTextFromHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        
        var sb = new StringBuilder();
        
        // Find the content div
        var contentNode = doc.DocumentNode.SelectSingleNode("//div[@id='mw-content-text']") ?? 
                           doc.DocumentNode.SelectSingleNode("//div[@id='bodyContent']") ??
                           doc.DocumentNode;
        
        // Remove unwanted elements
        foreach (var node in contentNode.SelectNodes("//div[contains(@class, 'toc')]|//table[contains(@class, 'infobox')]|//div[contains(@class, 'reflist')]|//div[contains(@class, 'navbox')]|//div[contains(@class, 'metadata')]") ?? new HtmlNodeCollection(null))
        {
            node.Remove();
        }
        
        // Process the nodes
        ProcessHtmlNode(contentNode, sb);
        
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Recursively processes HTML nodes to extract text (similar to EpubReader implementation)
    /// </summary>
    private void ProcessHtmlNode(HtmlNode node, StringBuilder sb)
    {
        if (node == null) return;
        
        // Skip comment nodes and certain elements
        if (node.NodeType == HtmlNodeType.Comment ||
            node.Name.Equals("script", StringComparison.OrdinalIgnoreCase) ||
            node.Name.Equals("style", StringComparison.OrdinalIgnoreCase) ||
            node.Name.Equals("noscript", StringComparison.OrdinalIgnoreCase) ||
            node.GetAttributeValue("class", "").Contains("reference") ||
            node.GetAttributeValue("class", "").Contains("mw-editsection"))
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
        
        // Special handling for headings
        if (node.Name.StartsWith("h") && node.Name.Length == 2 && char.IsDigit(node.Name[1]))
        {
            // Process children
            foreach (var child in node.ChildNodes)
            {
                ProcessHtmlNode(child, sb);
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
            ProcessHtmlNode(child, sb);
        }
        
        // Add newlines after certain block elements
        if (isBlockElement)
        {
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Determines if an HTML element is a block element
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
