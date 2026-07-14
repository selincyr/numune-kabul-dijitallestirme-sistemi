using System.Text.RegularExpressions;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Application.Models;

namespace NumuneKabul.Infrastructure.Services.Extraction;

public class RegexFieldExtractionService : IFieldExtractionService
{
    public List<ExtractedFieldResult> ExtractFields(string rawText, int pageNo)
    {
        var results = new List<ExtractedFieldResult>();

        AddIfFound(
            results,
            "T.C. Kimlik No",
            rawText,
            @"\b[1-9][0-9]{10}\b",
            pageNo,
            95);

        AddLineBasedField(
            results,
            "Hasta Adı Soyadı",
            rawText,
            @"(?:Hasta\s+Adı\s+Soyadı|Hasta\s+Adı|Adı\s+Soyadı|Ad\s+Soyad)\s*[:\-]?\s*(.+)",
            pageNo,
            80);

        AddLineBasedField(
            results,
            "Doğum Tarihi",
            rawText,
            @"(?:Doğum\s+Tarihi|Dogum\s+Tarihi|Doğum\s+Tar\.?)\s*[:\-]?\s*([0-9]{1,2}[./-][0-9]{1,2}[./-][0-9]{2,4})",
            pageNo,
            85);

        AddLineBasedField(
            results,
            "Cinsiyet",
            rawText,
            @"(?:Cinsiyet|Cinsiyeti)\s*[:\-]?\s*(Kadın|Erkek|Kadin|E|K)",
            pageNo,
            80);

        AddLineBasedField(
            results,
            "Kurum",
            rawText,
            @"(?:Kurum|Kurumu|Hastane)\s*[:\-]?\s*(.+)",
            pageNo,
            70);

        AddLineBasedField(
            results,
            "Doktor",
            rawText,
            @"(?:Doktor|Hekim|İstemi\s+Yapan\s+Doktor|Istemi\s+Yapan\s+Doktor)\s*[:\-]?\s*(.+)",
            pageNo,
            75);

        AddLineBasedField(
            results,
            "Protokol No",
            rawText,
            @"(?:Protokol\s+No|İşlem\s+No|Islem\s+No|Dosya\s+No)\s*[:\-]?\s*([A-Za-z0-9\-\/]+)",
            pageNo,
            80);

        return results;
    }

    private static void AddIfFound(
        List<ExtractedFieldResult> results,
        string fieldName,
        string text,
        string pattern,
        int pageNo,
        decimal confidence)
    {
        var match = Regex.Match(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        if (!match.Success)
        {
            return;
        }

        results.Add(new ExtractedFieldResult
        {
            FieldName = fieldName,
            RawValue = CleanValue(match.Value),
            Confidence = confidence,
            PageNo = pageNo
        });
    }

    private static void AddLineBasedField(
        List<ExtractedFieldResult> results,
        string fieldName,
        string text,
        string pattern,
        int pageNo,
        decimal confidence)
    {
        var match = Regex.Match(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        if (!match.Success || match.Groups.Count < 2)
        {
            return;
        }

        results.Add(new ExtractedFieldResult
        {
            FieldName = fieldName,
            RawValue = CleanValue(match.Groups[1].Value),
            Confidence = confidence,
            PageNo = pageNo
        });
    }

    private static string CleanValue(string value)
    {
        return value
            .Replace("|", "")
            .Replace(";", "")
            .Trim();
    }
}
