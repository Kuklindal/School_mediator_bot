using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using iTextSharp.text.pdf; // Для работы с PDF
using iTextSharp.text.pdf.parser;
using Path = System.IO.Path;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging; // Для парсинга PDF

public class FileSearchResult
{
    public string FileName { get; set; }
    public string Excerpt { get; set; }
    public int PageNumber { get; set; }
}

public class FileSearchService
{
    private readonly string _searchDirectory;
    private readonly Regex _sentenceRegex = new Regex(@"(?<=[.!?])\s+(?=[А-ЯA-Z])", RegexOptions.Compiled);
    public FileSearchService(string directoryPath)
    {
        _searchDirectory = directoryPath;
    }
    private string CleanText(string text)
    {
        // Удаляем URL
        text = Regex.Replace(text, @"https?:\/\/\S+", "");

        // Удаляем номера типа "N° п/п"
        text = Regex.Replace(text, @"N°\s*\n*\s*п/п", "");

        // Удаляем избыточные переносы строк
        text = Regex.Replace(text, @"(\n\s*){2,}", "\n");

        // Удаляем технические данные
        text = Regex.Replace(text, @"ISBN \d+[\d-]+", "");

        return text.Trim();
    }
    public async Task<List<FileSearchResult>> SearchInFilesAsync(string query)
    {
        var results = new List<FileSearchResult>();
        var files = Directory.GetFiles(_searchDirectory, "*.*", SearchOption.AllDirectories);
        int count = 0;
        foreach (var file in files)
        {
            try
            {
                if (count > 5)
                {
                    break;
                }
                var cleanedContent = await ProcessFileAsync(file, query);
                if (cleanedContent != null)
                {
                    var result = CleanText(cleanedContent.Excerpt);
                    cleanedContent.Excerpt = result;
                    results.Add(cleanedContent);
                    count++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка обработки файла {file}: {ex.Message}");
            }
        }
        return results;
    }

    private async Task<FileSearchResult> ProcessFileAsync(string filePath, string query)
    {
        return Path.GetExtension(filePath).ToLower() switch
        {
            ".pdf" => ProcessPdfFile(filePath, query),
            _ => null
        };
    }

    private FileSearchResult ProcessPdfFile(string filePath, string query)
    {
        using (var reader = new PdfReader(filePath))
        {
            for (int page = 1; page <= reader.NumberOfPages; page++)
            {
                var strategy = new SimpleTextExtractionStrategy();
                var content = PdfTextExtractor.GetTextFromPage(reader, page, strategy);
                var result = FindFirstMatch(content, query, Path.GetFileName(filePath), page);
                if (result != null) return result;
            }
        }
        return null;
    }


    private FileSearchResult FindFirstMatch(string content, string query, string fileName, int page = 0)
    {
        var sentences = _sentenceRegex.Split(content);

        for (int i = 0; i < sentences.Length; i++)
        {
            if (sentences[i].Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return new FileSearchResult
                {
                    FileName = fileName,
                    Excerpt = $"{sentences[i].Trim()}",
                    PageNumber = page
                };
            }
        }
        return null;
    }
}

