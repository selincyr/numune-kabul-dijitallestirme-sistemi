using System.Text;
using System.Xml;
using System.Xml.Linq;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;

namespace NumuneKabul.Infrastructure.Services.Xml;

public class XmlGenerationService : IXmlGenerationService
{
    public string GenerateXml(
        PdfDocument document,
        List<ExtractedField> fields,
        List<OcrResult> ocrResults)
    {
        var mappedFields = fields
            .GroupBy(x => x.FieldName)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(f => f.Confidence)
                    .First());

        var root = new XElement("NumuneKabulBelgesi",
            new XAttribute("versiyon", "1.0"),

            new XElement("BelgeBilgileri",
                new XElement("BelgeId", document.Id),
                new XElement("DosyaAdi", SafeXmlText(document.FileName)),
                new XElement("Kurum", SafeXmlText(document.Institution?.Name)),
                new XElement("YuklemeTarihi", document.UploadDate.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XElement("BelgeDurumu", document.Status.ToString())
            ),

            new XElement("OcrMetinleri",
                ocrResults
                    .OrderBy(x => x.PageNo)
                    .Select(ocr =>
                        new XElement("Sayfa",
                            new XAttribute("no", ocr.PageNo),
                            new XElement("HamMetin", SafeXmlText(ocr.RawText)),
                            new XElement("HataMesaji", SafeXmlText(ocr.ErrorMessage)),
                            new XElement("OlusturulmaTarihi", ocr.CreatedDate.ToString("yyyy-MM-ddTHH:mm:ss"))
                        )
                    )
            ),

            new XElement("HastaBilgileri",
                CreateMappedElement(mappedFields, "HastaAdiSoyadi", "Hasta Adı Soyadı"),
                CreateMappedElement(mappedFields, "TcKimlikNo", "T.C. Kimlik No"),
                CreateMappedElement(mappedFields, "DogumTarihi", "Doğum Tarihi"),
                CreateMappedElement(mappedFields, "Cinsiyet", "Cinsiyet")
            ),

            new XElement("KurumBilgileri",
                CreateMappedElement(mappedFields, "KurumAdi", "Kurum"),
                CreateMappedElement(mappedFields, "Doktor", "Doktor"),
                CreateMappedElement(mappedFields, "ProtokolNo", "Protokol No")
            ),

            new XElement("NumuneBilgileri",
                CreateMappedElement(mappedFields, "NumuneBarkodu", "Numune Barkodu"),
                CreateMappedElement(mappedFields, "NumuneTuru", "Numune Türü"),
                CreateMappedElement(mappedFields, "NumuneKabulTarihi", "Numune Kabul Tarihi")
            ),

            new XElement("TestBilgileri",
                CreateMappedElement(mappedFields, "TestAdi", "Test Adı"),
                CreateMappedElement(mappedFields, "Aciklama", "Açıklama")
            ),

            new XElement("AlanDetaylari",
                fields
                    .OrderBy(x => x.PageNo)
                    .ThenBy(x => x.FieldName)
                    .Select(field =>
                        new XElement("Alan",
                            new XAttribute("adi", SafeXmlText(field.FieldName)),
                            new XElement("OcrDegeri", SafeXmlText(field.RawValue)),
                            new XElement("DuzeltilmisDeger", SafeXmlText(GetFinalValue(field))),
                            new XElement("GuvenSkoru", field.Confidence),
                            new XElement("SayfaNo", field.PageNo),
                            new XElement("Durum", field.Status.ToString()),
                            new XElement("Koordinat",
                                new XElement("X", field.X),
                                new XElement("Y", field.Y),
                                new XElement("Width", field.Width),
                                new XElement("Height", field.Height)
                            )
                        )
                    )
            )
        );

        var documentXml = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root
        );

        return documentXml.ToString();
    }

    private static XElement CreateMappedElement(
        Dictionary<string, ExtractedField> mappedFields,
        string xmlElementName,
        string fieldName)
    {
        if (!mappedFields.TryGetValue(fieldName, out var field))
        {
            return new XElement(xmlElementName,
                new XAttribute("durum", "NotFound"),
                new XAttribute("guven", "0"),
                string.Empty);
        }

        return new XElement(xmlElementName,
            new XAttribute("durum", field.Status.ToString()),
            new XAttribute("guven", field.Confidence),
            SafeXmlText(GetFinalValue(field)));
    }

    private static string GetFinalValue(ExtractedField field)
    {
        if (!string.IsNullOrWhiteSpace(field.CorrectedValue))
        {
            return field.CorrectedValue.Trim();
        }

        return field.RawValue?.Trim() ?? string.Empty;
    }

    private static string SafeXmlText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (XmlConvert.IsXmlChar(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}