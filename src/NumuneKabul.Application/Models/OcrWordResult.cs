namespace NumuneKabul.Application.Models;

public class OcrWordResult
{
    public int PageNo { get; set; }

    public string Text { get; set; } = string.Empty;

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public decimal Confidence { get; set; }
}