using System.Text;
using System.Text.Json;

namespace HealthTracker.Services;

public class AiParsedDoctorVisit
{
    public string? DoctorName { get; set; }
    public string? Hospital { get; set; }
    public string? VisitDate { get; set; }
    public string? Diagnosis { get; set; }
    public string? Notes { get; set; }
}

public class AiParsedReport
{
    public string? Title { get; set; }
    public string? DoctorName { get; set; }
    public string? Hospital { get; set; }
    public string? ReportType { get; set; }
    public string? ReportDate { get; set; }
}

public class AiParserService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string PrefsKey = "gemini_api_key";

    public static string? GetApiKey() => Preferences.Get(PrefsKey, null);
    public static void SetApiKey(string? key) => Preferences.Set(PrefsKey, key ?? string.Empty);
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(GetApiKey());

    public async Task<AiParsedReport?> ParseReportAsync(string ocrText)
    {
        var key = GetApiKey();
        if (string.IsNullOrWhiteSpace(key)) return null;

        var prompt = "You are a medical report parser. Extract report details from the following text.\n" +
            "Provide: title (brief description of the report), doctorName, hospital, reportType (one of: Lab, Prescription, Xray, Scan, Other), reportDate (in yyyy-MM-dd format).\n\n" +
            "Return ONLY a JSON object, no markdown, no explanation. Example:\n" +
            "{\"title\":\"Complete Blood Count\",\"doctorName\":\"Dr. Smith\",\"hospital\":\"City Hospital\",\"reportType\":\"Lab\",\"reportDate\":\"2024-03-15\"}\n\n" +
            "Use null for fields you cannot determine.\n\n" +
            "Report text:\n" + ocrText;

        var json = await CallGemini(key, prompt);
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<AiParsedReport>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI parse report failed: {ex.Message}\nRaw: {json}");
            return null;
        }
    }

    public async Task<AiParsedDoctorVisit?> ParseDoctorVisitAsync(string ocrText)
    {
        var key = GetApiKey();
        if (string.IsNullOrWhiteSpace(key)) return null;

        var prompt = "You are a medical prescription parser. Extract doctor visit details from the following prescription text.\n" +
            "Provide: doctorName, hospital (clinic or hospital name), visitDate (in yyyy-MM-dd format), diagnosis (medical condition or reason for visit), notes (any additional relevant info like medicines prescribed).\n\n" +
            "Return ONLY a JSON object, no markdown, no explanation. Example:\n" +
            "{\"doctorName\":\"Dr. Smith\",\"hospital\":\"City Hospital\",\"visitDate\":\"2024-03-15\",\"diagnosis\":\"Upper respiratory infection\",\"notes\":\"Prescribed amoxicillin 500mg\"}\n\n" +
            "Use null for fields you cannot determine.\n\n" +
            "Prescription text:\n" + ocrText;

        var json = await CallGemini(key, prompt);
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<AiParsedDoctorVisit>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI parse doctor visit failed: {ex.Message}\nRaw: {json}");
            return null;
        }
    }

    public static async Task<string?> CallGemini(string apiKey, string prompt, int maxOutputTokens = 1024, double temperature = 0.1)
    {
        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature,
                    maxOutputTokens
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await Http.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"Gemini API error: {response.StatusCode} - {responseText}");
                return null;
            }

            // Extract the text from Gemini response
            using var doc = JsonDocument.Parse(responseText);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(text)) return null;

            // Strip markdown code fences if present
            text = text.Trim();
            if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                text = text[7..];
            else if (text.StartsWith("```"))
                text = text[3..];
            if (text.EndsWith("```"))
                text = text[..^3];

            return text.Trim();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Gemini call failed: {ex}");
            return $"__ERROR__: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
