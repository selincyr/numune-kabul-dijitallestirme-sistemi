using System.Diagnostics;
using System.Globalization;
using System.Text;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Application.Models;

namespace NumuneKabul.Infrastructure.Services.Ocr;

public class TesseractOcrService : IOcrService
{
    public async Task<string> ExtractTextAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("OCR için görsel dosyası bulunamadı.", imagePath);
        }

        var output = await RunTesseractAsync(
            imagePath,
            outputFormat: null,
            cancellationToken);

        return output;
    }

    public async Task<List<OcrWordResult>> ExtractWordsAsync(
        string imagePath,
        int pageNo,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("OCR için görsel dosyası bulunamadı.", imagePath);
        }

        var tsvOutput = await RunTesseractAsync(
            imagePath,
            outputFormat: "tsv",
            cancellationToken);

        return ParseTsv(tsvOutput, pageNo);
    }

    private static async Task<string> RunTesseractAsync(
        string imagePath,
        string? outputFormat,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "tesseract",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add(imagePath);
        startInfo.ArgumentList.Add("stdout");
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("tur+eng");

        if (!string.IsNullOrWhiteSpace(outputFormat))
        {
            startInfo.ArgumentList.Add(outputFormat);
        }

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Tesseract OCR işlemi başarısız oldu: {error}");
        }

        return output;
    }

    private static List<OcrWordResult> ParseTsv(string tsvOutput, int pageNo)
    {
        var words = new List<OcrWordResult>();

        if (string.IsNullOrWhiteSpace(tsvOutput))
        {
            return words;
        }

        var lines = tsvOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1);

        foreach (var line in lines)
        {
            var columns = line.Split('\t');

            if (columns.Length < 12)
            {
                continue;
            }

            var level = columns[0];

            if (level != "5")
            {
                continue;
            }

            var text = columns[11].Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!int.TryParse(columns[6], out var x))
            {
                continue;
            }

            if (!int.TryParse(columns[7], out var y))
            {
                continue;
            }

            if (!int.TryParse(columns[8], out var width))
            {
                continue;
            }

            if (!int.TryParse(columns[9], out var height))
            {
                continue;
            }

            if (!decimal.TryParse(
                    columns[10],
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var confidence))
            {
                confidence = 0;
            }

            if (confidence < 0)
            {
                confidence = 0;
            }

            words.Add(new OcrWordResult
            {
                PageNo = pageNo,
                Text = text,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Confidence = confidence
            });
        }

        return words;
    }
}