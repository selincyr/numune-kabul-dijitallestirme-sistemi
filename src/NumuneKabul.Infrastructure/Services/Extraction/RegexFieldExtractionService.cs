using System.Text.RegularExpressions;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Application.Models;
using NumuneKabul.Domain.Entities;
using NumuneKabul.Domain.Enums;

namespace NumuneKabul.Infrastructure.Services.Extraction;

public class RegexFieldExtractionService : IFieldExtractionService
{
    public List<ExtractedFieldResult> ExtractFields(
        string rawText,
        int pageNo,
        IReadOnlyCollection<TemplateField> templateFields)
    {
        var results = new List<ExtractedFieldResult>();

        foreach (var templateField in templateFields.OrderBy(x => x.OrderNo))
        {
            if (string.IsNullOrWhiteSpace(templateField.Regex))
            {
                results.Add(CreateNotFoundResult(templateField, pageNo));
                continue;
            }

            Match match;

            try
            {
                match = Regex.Match(
                    rawText,
                    templateField.Regex,
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            catch
            {
                results.Add(new ExtractedFieldResult
                {
                    FieldName = templateField.FieldName,
                    RawValue = string.Empty,
                    Confidence = 0,
                    PageNo = pageNo,
                    Status = FieldStatus.NeedsReview
                });

                continue;
            }

            if (!match.Success)
            {
                results.Add(CreateNotFoundResult(templateField, pageNo));
                continue;
            }

            var value = match.Groups.Count > 1
                ? match.Groups[1].Value
                : match.Value;

            value = CleanValue(value);

            if (string.IsNullOrWhiteSpace(value))
            {
                results.Add(CreateNotFoundResult(templateField, pageNo));
                continue;
            }

            var confidence = CalculateConfidence(templateField, rawText, value);

            var status = confidence >= 85
                ? FieldStatus.Verified
                : FieldStatus.NeedsReview;

            results.Add(new ExtractedFieldResult
            {
                FieldName = templateField.FieldName,
                RawValue = value,
                Confidence = confidence,
                PageNo = pageNo,
                Status = status
            });
        }

        return results;
    }

    private static ExtractedFieldResult CreateNotFoundResult(
        TemplateField templateField,
        int pageNo)
    {
        return new ExtractedFieldResult
        {
            FieldName = templateField.FieldName,
            RawValue = string.Empty,
            Confidence = 0,
            PageNo = pageNo,
            Status = FieldStatus.NotFound
        };
    }

    private static decimal CalculateConfidence(
        TemplateField templateField,
        string rawText,
        string value)
    {
        decimal confidence = 75;

        if (!string.IsNullOrWhiteSpace(templateField.Keyword) &&
            rawText.Contains(templateField.Keyword, StringComparison.OrdinalIgnoreCase))
        {
            confidence += 10;
        }

        if (IsValueCompatibleWithDataType(templateField.DataType, value))
        {
            confidence += 10;
        }
        else
        {
            confidence -= 20;
        }

        if (templateField.Required)
        {
            confidence += 5;
        }

        if (confidence > 95)
        {
            confidence = 95;
        }

        if (confidence < 0)
        {
            confidence = 0;
        }

        return confidence;
    }

    private static bool IsValueCompatibleWithDataType(string dataType, string value)
    {
        var normalizedDataType = dataType.Trim().ToLowerInvariant();

        return normalizedDataType switch
        {
            "tckn" => Regex.IsMatch(value, @"^[1-9][0-9]{10}$"),
            "tc" => Regex.IsMatch(value, @"^[1-9][0-9]{10}$"),
            "tc kimlik no" => Regex.IsMatch(value, @"^[1-9][0-9]{10}$"),
            "date" => Regex.IsMatch(value, @"^[0-9]{1,2}[./-][0-9]{1,2}[./-][0-9]{2,4}$"),
            "tarih" => Regex.IsMatch(value, @"^[0-9]{1,2}[./-][0-9]{1,2}[./-][0-9]{2,4}$"),
            "number" => Regex.IsMatch(value, @"^[0-9]+$"),
            "numeric" => Regex.IsMatch(value, @"^[0-9]+$"),
            "text" => !string.IsNullOrWhiteSpace(value),
            _ => true
        };
    }

    private static string CleanValue(string value)
    {
        return value
            .Replace("|", "")
            .Replace(";", "")
            .Replace("\r", "")
            .Replace("\n", " ")
            .Trim();
    }
}