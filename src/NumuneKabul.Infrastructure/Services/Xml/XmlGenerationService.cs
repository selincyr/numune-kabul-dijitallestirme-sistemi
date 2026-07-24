using System.Xml.Linq;
using NumuneKabul.Application.Interfaces;
using NumuneKabul.Domain.Entities;

namespace NumuneKabul.Infrastructure.Services.Xml;

public class XmlGenerationService : IXmlGenerationService
{
    public string GenerateXml(PdfDocument document, List<ExtractedField> fields)
    {
        var root = new XElement("NumuneKabulBelgesi",
            new XElement("BelgeBilgileri",
                new XElement("BelgeId", document.Id),
                new XElement("DosyaAdi", document.FileName),
                new XElement("YuklemeTarihi", document.UploadDate.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XElement("Kurum", document.Institution?.Name ?? string.Empty)
            ),
            new XElement("Alanlar",
                fields.Select(field =>
                    new XElement("Alan",
                        new XElement("AlanAdi", field.FieldName),
                        new XElement("OcrDegeri", field.RawValue ?? string.Empty),
                        new XElement("DuzeltilmisDeger", field.CorrectedValue ?? field.RawValue ?? string.Empty),
                        new XElement("GuvenSkoru", field.Confidence),
                        new XElement("SayfaNo", field.PageNo),
                        new XElement("Durum", field.Status.ToString())
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
}
