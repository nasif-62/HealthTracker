using UglyToad.PdfPig;

namespace HealthTracker.Services;

public class PdfTextExtractorService
{
    public string ExtractText(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        try
        {
            using var pdf = PdfDocument.Open(filePath);
            var sb = new System.Text.StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PDF text extraction failed: {ex.Message}");
            return string.Empty;
        }
    }
}
