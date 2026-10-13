using System.Text.RegularExpressions;
using Plugin.Maui.OCR;

namespace HealthTracker.Services;

public class ParsedMedicine
{
    public string Name { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Instructions { get; set; }
}

public partial class PrescriptionParserService
{
    private readonly IOcrService _ocrService;

    public PrescriptionParserService(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task<string> ExtractTextFromImage(byte[] imageData)
    {
        var result = await _ocrService.RecognizeTextAsync(imageData);
        return result.Success ? result.AllText : string.Empty;
    }

    public List<ParsedMedicine> ParseMedicines(string ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText)) return [];

        var medicines = new List<ParsedMedicine>();
        var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.Length < 3) continue;

            var parsed = TryParseMedicineLine(line);
            if (parsed != null)
                medicines.Add(parsed);
        }

        return medicines;
    }

    private static ParsedMedicine? TryParseMedicineLine(string line)
    {
        // Match lines starting with common prefixes: "Tab.", "Cap.", "Syp.", "Inj.", numbered lines, or "Rx"
        var prefixMatch = PrefixPattern().Match(line);
        if (prefixMatch.Success)
        {
            var remainder = line[prefixMatch.Length..].Trim();
            return ParseRemainderAsMedicine(remainder, prefixMatch.Value.Trim());
        }

        // Match lines starting with a number like "1. Metformin 500mg" or "1) Amoxicillin"
        var numberedMatch = NumberedPattern().Match(line);
        if (numberedMatch.Success)
        {
            var remainder = line[numberedMatch.Length..].Trim();
            return ParseRemainderAsMedicine(remainder, null);
        }

        // Match lines containing known drug form keywords anywhere
        if (DrugFormKeyword().IsMatch(line))
        {
            return ParseRemainderAsMedicine(line, null);
        }

        return null;
    }

    private static ParsedMedicine ParseRemainderAsMedicine(string text, string? prefix)
    {
        var medicine = new ParsedMedicine();

        // Extract frequency hints
        string? frequency = null;
        var freqMatch = FrequencyPattern().Match(text);
        if (freqMatch.Success)
        {
            frequency = freqMatch.Value.Trim();
            text = text[..freqMatch.Index].Trim().TrimEnd('-', ',', '–');
        }

        // Extract dosage (e.g. "500mg", "250 mg", "10ml")
        string? dosage = null;
        var dosageMatch = DosagePattern().Match(text);
        if (dosageMatch.Success)
        {
            dosage = dosageMatch.Value.Trim();
            text = (text[..dosageMatch.Index] + text[(dosageMatch.Index + dosageMatch.Length)..]).Trim();
        }

        // Clean up the medicine name
        var name = text.Trim().TrimEnd('-', ',', '–', '.').Trim();

        // Prepend prefix form to instructions if present
        string? instructions = null;
        if (!string.IsNullOrEmpty(prefix))
        {
            var form = prefix.TrimEnd('.', ' ').ToUpperInvariant() switch
            {
                "TAB" => "Tablet",
                "CAP" => "Capsule",
                "SYP" or "SYRUP" => "Syrup",
                "INJ" => "Injection",
                "DROPS" or "DROP" => "Drops",
                _ => prefix.TrimEnd('.', ' ')
            };
            instructions = form;
        }

        if (string.IsNullOrWhiteSpace(name)) return new ParsedMedicine { Name = "Unknown" };

        medicine.Name = name;
        medicine.Dosage = dosage;
        medicine.Frequency = frequency;
        medicine.Instructions = instructions;

        return medicine;
    }

    [GeneratedRegex(@"^(?:Tab\.?|Cap\.?|Syp\.?|Syrup\.?|Inj\.?|Drops?\.?|Rx\.?)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixPattern();

    [GeneratedRegex(@"^\d+[\.\)\-]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex NumberedPattern();

    [GeneratedRegex(@"\b(?:tablet|capsule|syrup|injection|drops|inhaler|ointment|cream|gel|patch)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DrugFormKeyword();

    [GeneratedRegex(@"\b(?:once|twice|thrice|\d+\s*(?:time|x)\s*(?:a|per)?\s*day|(?:od|bd|tds|qid|sos|prn|hs|ac|pc|bid|tid))\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex FrequencyPattern();

    [GeneratedRegex(@"\b\d+\s*(?:mg|ml|mcg|g|iu|units?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DosagePattern();
}
