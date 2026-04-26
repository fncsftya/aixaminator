using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Importers;

public class PDFReader
{
    public bool IsPdfFile(Stream stream)
    {
        try
        {
            byte[] buffer = new byte[5];
            stream.ReadExactly(buffer);
            string header = Encoding.ASCII.GetString(buffer);
            return header.StartsWith("%PDF-");
        }
        catch
        {
            return false;
        }
        finally
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
    }

    public string ExtractText(Stream pdfStream)
    {
        // TODO could also use NearestNeighbourWordExtractor.Instance
        // see PdfPIG readme for info
        var result = new StringBuilder();

        using (var document = PdfDocument.Open(pdfStream))
        {
            foreach (var page in document.GetPages())
            {
                var text = ContentOrderTextExtractor.GetText(page, new ContentOrderTextExtractor.Options
                {
                    ReplaceWhitespaceWithSpace = true,
                    // TODO can we use this to render stuff as markdown?
                    SeparateParagraphsWithDoubleNewline = true,
                });
                result.Append(text);
                result.AppendLine();
            }
        }

        return result.ToString();
    }

    public bool IsCapitalised(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return char.IsUpper(text[0]);
    }

    public bool IsLikelyTitle(string text)
    {
        var re = new Regex(@"\s+");
        var tre1 = new Regex(@"^\D+\d+\s*$");
        var tre2 = new Regex(@"^\s*\d+[^\.]\D+$");
        var words = re.Split(text);
        if (words.Where(IsCapitalised).Count() > words.Length / 2 && (tre1.IsMatch(text) || tre2.IsMatch(text)))
        {
            return true;
        }
        return false;
    }
}
