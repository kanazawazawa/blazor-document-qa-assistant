using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace ResponseAgent.Services;

public interface IFileProcessingService
{
    Task<string> ExtractTextFromFileAsync(Stream fileStream, string fileName);
}

public class FileProcessingService : IFileProcessingService
{
    private readonly ILogger<FileProcessingService> _logger;

    public FileProcessingService(ILogger<FileProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextFromFileAsync(Stream fileStream, string fileName)
    {
        try
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".txt" => await ExtractFromTextFileAsync(fileStream),
                ".docx" => await ExtractFromWordFileAsync(fileStream),
                _ => throw new NotSupportedException($"サポートされていないファイル形式です: {extension}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ファイル処理中にエラーが発生しました: {FileName}", fileName);
            throw;
        }
    }

    private static async Task<string> ExtractFromTextFileAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static Task<string> ExtractFromWordFileAsync(Stream stream)
    {
        var sb = new StringBuilder();

        using (var doc = WordprocessingDocument.Open(stream, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            
            if (body != null)
            {
                foreach (var paragraph in body.Descendants<Paragraph>())
                {
                    var text = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }
        }

        return Task.FromResult(sb.ToString());
    }
}
