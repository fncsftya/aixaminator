using System.IO.Compression;
using System.Text;
using Aixaminator.Services;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Aixaminator.Tests.Services;

public class DocumentImporterTests
{
    private readonly FakeWikipediaClient _wikipedia = new();
    private readonly DocumentImporter _importer;

    public DocumentImporterTests()
    {
        _importer = new DocumentImporter(_wikipedia);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Text_files_are_read_and_named_after_the_file()
    {
        var result = await _importer.ReadFileAsync("My Notes.txt", Utf8("Hello there."), Token);

        Assert.Equal("My Notes", result.SuggestedName);
        Assert.Equal("Hello there.", result.Text);
    }

    [Fact]
    public async Task Binary_files_are_rejected()
    {
        var error = await Assert.ThrowsAsync<DocumentImportException>(
            () => _importer.ReadFileAsync("image.png", new MemoryStream([0x89, 0x50, 0x00, 0x00, 0x01]), Token));

        Assert.Contains("binary", error.Message);
    }

    [Fact]
    public async Task Files_without_text_are_rejected()
    {
        var error = await Assert.ThrowsAsync<DocumentImportException>(
            () => _importer.ReadFileAsync("empty.txt", Utf8("  \n "), Token));

        Assert.Contains("No text", error.Message);
    }

    [Fact]
    public async Task Pdf_files_are_converted_to_text()
    {
        var result = await _importer.ReadFileAsync("Paper.pdf", CreatePdf("Hello from a PDF"), Token);

        Assert.Equal("Paper", result.SuggestedName);
        Assert.Contains("Hello from a PDF", result.Text);
    }

    [Fact]
    public async Task Pdf_files_are_detected_by_content_regardless_of_extension()
    {
        var result = await _importer.ReadFileAsync("download", CreatePdf("Detected by header"), Token);

        Assert.Contains("Detected by header", result.Text);
    }

    [Fact]
    public async Task Epub_files_are_converted_to_text()
    {
        var result = await _importer.ReadFileAsync("Novel.epub", CreateEpub("<p>It was a dark and stormy night.</p>"), Token);

        Assert.Equal("Novel", result.SuggestedName);
        Assert.Contains("It was a dark and stormy night.", result.Text);
    }

    [Fact]
    public async Task Invalid_epub_files_are_rejected()
    {
        var error = await Assert.ThrowsAsync<DocumentImportException>(
            () => _importer.ReadFileAsync("broken.epub", Utf8("not a zip"), Token));

        Assert.Contains("not a valid EPUB", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://en.wikipedia.org/wiki/Example")]
    public async Task Invalid_urls_are_rejected(string url)
    {
        await Assert.ThrowsAsync<DocumentImportException>(() => _importer.ReadUrlAsync(url, Token));
    }

    [Fact]
    public async Task Only_wikipedia_urls_are_supported()
    {
        var error = await Assert.ThrowsAsync<DocumentImportException>(
            () => _importer.ReadUrlAsync("https://example.com/article", Token));

        Assert.Contains("Wikipedia", error.Message);
    }

    [Fact]
    public async Task Wikipedia_pages_are_named_after_their_title()
    {
        _wikipedia.Text = "Article text.";

        var result = await _importer.ReadUrlAsync("https://en.wikipedia.org/wiki/Ada_Lovelace%27s_notes", Token);

        Assert.Equal("Ada Lovelace's notes", result.SuggestedName);
        Assert.Equal("Article text.", result.Text);
        Assert.Equal("https://en.wikipedia.org/wiki/Ada_Lovelace%27s_notes", _wikipedia.RequestedUrl);
    }

    [Theory]
    [InlineData("https://en.wikipedia.org/wiki/Example", true)]
    [InlineData("https://de.m.wikipedia.org/wiki/Beispiel", true)]
    [InlineData("https://wikipedia.org.evil.com/wiki/Example", false)]
    [InlineData("https://example.com/?q=wikipedia.org", false)]
    public void Wikipedia_urls_are_recognised_by_host(string url, bool expected)
    {
        Assert.Equal(expected, DocumentImporter.IsWikipediaUrl(url));
    }

    private static MemoryStream CreatePdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        page.AddText(text, 12, new PdfPoint(50, 700), font);
        return new MemoryStream(builder.Build());
    }

    private static MemoryStream CreateEpub(string bodyHtml)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content, CompressionLevel level = CompressionLevel.Optimal)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name, level).Open());
                writer.Write(content);
            }

            Add("mimetype", "application/epub+zip", CompressionLevel.NoCompression);
            Add("META-INF/container.xml", """
                <?xml version="1.0"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                </container>
                """);
            Add("OEBPS/content.opf", """
                <?xml version="1.0" encoding="UTF-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:identifier id="id">test-book</dc:identifier>
                    <dc:title>Test Book</dc:title>
                    <dc:language>en</dc:language>
                  </metadata>
                  <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="ch1" href="ch1.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine><itemref idref="ch1"/></spine>
                </package>
                """);
            Add("OEBPS/nav.xhtml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Nav</title></head>
                <body><nav epub:type="toc"><ol><li><a href="ch1.xhtml">Chapter 1</a></li></ol></nav></body></html>
                """);
            Add("OEBPS/ch1.xhtml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Chapter 1</title></head><body>{bodyHtml}</body></html>
                """);
        }
        stream.Position = 0;
        return stream;
    }

    private sealed class FakeWikipediaClient : IWikipediaClient
    {
        public string Text { get; set; } = string.Empty;

        public string? RequestedUrl { get; private set; }

        public Task<string> GetTextAsync(string url, CancellationToken cancellationToken)
        {
            RequestedUrl = url;
            return Task.FromResult(Text);
        }
    }
}
