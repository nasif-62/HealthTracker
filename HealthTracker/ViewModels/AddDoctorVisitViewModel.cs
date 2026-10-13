using System.Windows.Input;
using HealthTracker.Helpers;
using HealthTracker.Services;
using Plugin.Maui.OCR;

namespace HealthTracker.ViewModels;

public class AddDoctorVisitViewModel : BaseViewModel
{
    private readonly DoctorService _service;
    private readonly IOcrService _ocrService;
    private readonly AiParserService _aiParser;

    private string _doctorName = string.Empty;
    private string? _hospital;
    private DateTime _visitDate = DateTime.Today;
    private string? _diagnosis;
    private string? _notes;
    private DateTime _followUpDate = DateTime.Today;
    private bool _hasFollowUp;
    private string? _prescriptionImagePath;
    private bool _hasImage;
    private bool _isScanning;

    public string DoctorName { get => _doctorName; set { _doctorName = value; OnPropertyChanged(); } }
    public string? Hospital { get => _hospital; set { _hospital = value; OnPropertyChanged(); } }
    public DateTime VisitDate { get => _visitDate; set { _visitDate = value; OnPropertyChanged(); } }
    public string? Diagnosis { get => _diagnosis; set { _diagnosis = value; OnPropertyChanged(); } }
    public string? Notes { get => _notes; set { _notes = value; OnPropertyChanged(); } }
    // Non-nullable: DatePicker.Date cannot bind a null, which previously made the picker's
    // displayed value never reach the ViewModel unless the user changed it.
    public DateTime FollowUpDate { get => _followUpDate; set { _followUpDate = value; OnPropertyChanged(); } }
    public bool HasFollowUp { get => _hasFollowUp; set { _hasFollowUp = value; OnPropertyChanged(); } }
    public string? PrescriptionImagePath { get => _prescriptionImagePath; set { _prescriptionImagePath = value; OnPropertyChanged(); } }
    public bool HasImage { get => _hasImage; set { _hasImage = value; OnPropertyChanged(); } }
    public bool IsScanning { get => _isScanning; set { _isScanning = value; OnPropertyChanged(); } }

    public ICommand SaveCommand { get; }
    public ICommand PickImageCommand { get; }
    public ICommand CaptureImageCommand { get; }
    public ICommand RemoveImageCommand { get; }
    public event EventHandler? SavedSuccessfully;

    public AddDoctorVisitViewModel(DoctorService service, IOcrService ocrService, AiParserService aiParser)
    {
        _service = service;
        _ocrService = ocrService;
        _aiParser = aiParser;
        SaveCommand = new Command(async () => await Save());
        PickImageCommand = new Command(async () => await PickImage());
        CaptureImageCommand = new Command(async () => await CaptureImage());
        RemoveImageCommand = new Command(() => { PrescriptionImagePath = null; HasImage = false; });
    }

    private async Task PickImage()
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync();
            if (result != null) await ProcessImage(result);
        }
        catch (Exception ex) { SetError("Failed to pick image. " + ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
    }

    private async Task CaptureImage()
    {
        try
        {
            var result = await MediaPicker.CapturePhotoAsync();
            if (result != null) await ProcessImage(result);
        }
        catch (Exception ex) { SetError("Failed to capture photo. " + ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
    }

    private async Task ProcessImage(FileResult imageFile)
    {
        IsScanning = true; ClearError();
        try
        {
            PrescriptionImagePath = imageFile.FullPath;
            HasImage = true;

            // OCR extract text
            using var stream = await imageFile.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var imageData = ms.ToArray();

            var ocrResult = await _ocrService.RecognizeTextAsync(imageData);
            var ocrText = ocrResult.Success ? ocrResult.AllText : string.Empty;

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                SetError("Could not read text from image. Fill in fields manually.");
                return;
            }

            // Try AI parsing if configured
            if (AiParserService.IsConfigured)
            {
                var parsed = await _aiParser.ParseDoctorVisitAsync(ocrText);
                if (parsed != null)
                {
                    if (string.IsNullOrWhiteSpace(DoctorName) && !string.IsNullOrWhiteSpace(parsed.DoctorName))
                        DoctorName = parsed.DoctorName;
                    if (string.IsNullOrWhiteSpace(Hospital) && !string.IsNullOrWhiteSpace(parsed.Hospital))
                        Hospital = parsed.Hospital;
                    if (!string.IsNullOrWhiteSpace(parsed.VisitDate) && DateTime.TryParse(parsed.VisitDate, out var date))
                        VisitDate = date;
                    if (string.IsNullOrWhiteSpace(Diagnosis) && !string.IsNullOrWhiteSpace(parsed.Diagnosis))
                        Diagnosis = parsed.Diagnosis;
                    if (string.IsNullOrWhiteSpace(Notes) && !string.IsNullOrWhiteSpace(parsed.Notes))
                        Notes = parsed.Notes;
                    return;
                }
            }

            // Fallback: basic regex extraction from OCR text
            ApplyBasicParsing(ocrText);
        }
        catch (Exception ex) { SetError("Scan failed: " + ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsScanning = false; }
    }

    private void ApplyBasicParsing(string text)
    {
        // Try to extract doctor name (Dr. Something)
        var drMatch = System.Text.RegularExpressions.Regex.Match(text, @"(?:Dr\.?\s+)([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (drMatch.Success && string.IsNullOrWhiteSpace(DoctorName))
            DoctorName = "Dr. " + drMatch.Groups[1].Value.Trim();

        // Try to extract hospital/clinic
        var hospMatch = System.Text.RegularExpressions.Regex.Match(text, @"(?:hospital|clinic|medical\s+center|health\s+center)[:\s]*(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (hospMatch.Success && string.IsNullOrWhiteSpace(Hospital))
            Hospital = hospMatch.Groups[1].Value.Trim().TrimEnd('.', ',');

        // Try to extract date
        var dateMatch = System.Text.RegularExpressions.Regex.Match(text, @"\b(\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{2,4})\b");
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
            VisitDate = parsedDate;
    }

    private async Task Save()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            if (string.IsNullOrWhiteSpace(DoctorName)) { SetError("Doctor name is required."); return; }
            if (HasFollowUp && FollowUpDate.Date < DateTime.Today) { SetError("Follow-up date must be today or a future date."); return; }

            var fuDate = HasFollowUp ? DateTimeHelper.ToDateString(FollowUpDate) : null;
            await _service.AddVisit(DoctorName, Hospital, DateTimeHelper.ToDateString(VisitDate),
                Diagnosis, Notes, fuDate, PrescriptionImagePath);
            SavedSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { SetError(ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
