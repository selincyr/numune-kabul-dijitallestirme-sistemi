namespace NumuneKabul.Domain.Enums;

public enum PdfStatus
{
    Uploaded = 1,
    OcrProcessing = 2,
    OcrCompleted = 3,
    WaitingForValidation = 4,
    XmlCreated = 5,
    SentToIntegration = 6,
    Failed = 7
}
