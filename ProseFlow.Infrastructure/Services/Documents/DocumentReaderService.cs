using System.IO.Compression;
using System.Text;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.XWPF.UserModel;
using ProseFlow.Application.Interfaces;
using ProseFlow.Core.Models;

namespace ProseFlow.Infrastructure.Services.Documents;

/// <summary>
/// Implements the service for extracting plain text from various document file types.
/// </summary>
public class DocumentReaderService : IDocumentReaderService
{
    /// <inheritdoc />
    public async Task<string> ReadTextAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file could not be found.", filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // NPOI methods are synchronous, so we run them on a background thread.
        return extension switch
        {
            // Plain text, markup, data, and code
            _ when Constants.PlainTextLikeExtensions.Contains(extension) =>
                await File.ReadAllTextAsync(filePath),
            
            // PDF Documents
            ".pdf" => await Task.Run(() => ReadPdfText(filePath)),
            
            // Microsoft Word Documents
            ".docx" => await Task.Run(() => ReadDocxText(filePath)),
            
            // Microsoft Excel Spreadsheets
            ".xlsx" => await Task.Run(() => ReadXlsxText(filePath)),
            ".xls" => await Task.Run(() => ReadXlsText(filePath)),
            
            // E-books
            ".epub" => await Task.Run(() => ReadEpubText(filePath)),
            
            _ => throw new NotSupportedException($"File type '{extension}' is not supported for text extraction.")
        };
    }

    #region Reader Implementations

    private static string ReadPdfText(string filePath)
    {
        var textBuilder = new StringBuilder();
        using var pdfReader = new PdfReader(filePath);
        using var pdfDocument = new PdfDocument(pdfReader);
        for (var pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
        {
            var strategy = new SimpleTextExtractionStrategy();
            textBuilder.Append(PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(pageNum), strategy));
            textBuilder.Append(' ');
        }
        return textBuilder.ToString();
    }

    private static string ReadDocxText(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var doc = new XWPFDocument(stream);
        return string.Join("\n", doc.Paragraphs.Select(p => p.Text));
    }

    private static string ReadXlsxText(string filePath)
    {
        var textBuilder = new StringBuilder();
        using var stream = File.OpenRead(filePath);
        using var workbook = new XSSFWorkbook(stream);
        for (var i = 0; i < workbook.NumberOfSheets; i++)
        {
            var sheet = workbook.GetSheetAt(i);
            for (var j = 0; j <= sheet.LastRowNum; j++)
            {
                var row = sheet.GetRow(j);
                if (row == null) continue;
                foreach (var cell in row.Cells)
                {
                    textBuilder.Append(cell);
                    textBuilder.Append('\t');
                }
                textBuilder.AppendLine();
            }
        }
        return textBuilder.ToString();
    }

    private static string ReadXlsText(string filePath)
    {
        var textBuilder = new StringBuilder();
        using var stream = File.OpenRead(filePath);
        using var workbook = new HSSFWorkbook(stream);
        for (var i = 0; i < workbook.NumberOfSheets; i++)
        {
            var sheet = workbook.GetSheetAt(i);
            for (var j = 0; j <= sheet.LastRowNum; j++)
            {
                var row = sheet.GetRow(j);
                if (row == null) continue;
                foreach (var cell in row.Cells)
                {
                    textBuilder.Append(cell);
                    textBuilder.Append('\t');
                }
                textBuilder.AppendLine();
            }
        }
        return textBuilder.ToString();
    }

    private static string ReadEpubText(string filePath)
    {
        var textBuilder = new StringBuilder();
        using var archive = ZipFile.OpenRead(filePath);
        
        // Find HTML/XHTML entries and read them in order
        var htmlEntries = archive.Entries
            .Where(e => e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName);

        foreach (var entry in htmlEntries)
        {
            using var stream = entry.Open();
            var doc = new HtmlDocument();
            doc.Load(stream);
            
            // Extract text from the body, ignoring script and style blocks
            var textNodes = doc.DocumentNode.SelectNodes("//body//text()[not(parent::script) and not(parent::style)]");
            if (textNodes.Count != 0)
            {
                foreach (var node in textNodes)
                {
                    textBuilder.Append(HtmlEntity.DeEntitize(node.InnerText.Trim()));
                    textBuilder.Append(' ');
                }
            }
            textBuilder.AppendLine();
        }
        return textBuilder.ToString();
    }

    #endregion
}