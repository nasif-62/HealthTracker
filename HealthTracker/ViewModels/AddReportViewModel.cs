using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class ReportPageItem : BaseViewModel
{
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;

    private string _fileName = string.Empty;
    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(); }
    }

    public int PageNumber { get; set; }
}

public class AddReportViewModel : BaseViewModel
{
    private readonly ReportService _service;
    private readonly ReportTextParserService _textParser;

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    private string _reportType = "Lab";
    public string ReportType
    {
        get => _reportType;
        set { _reportType = value; OnPropertyChanged(); }
    }

    private string? _doctorName;
    public string? DoctorName
    {
        get => _doctorName;
        set { _doctorName = value; OnPropertyChanged(); }
    }

    private string? _hospital;
    public string? Hospital
    {
        get => _hospital;
        set { _hospital = value; OnPropertyChanged(); }
    }

    private DateTime _reportDate = DateTime.Today;
    public DateTime ReportDate
    {
        get => _reportDate;
        set { _reportDate = value; OnPropertyChanged(); }
    }

    private string? _notes;
    public string? Notes
    {
        get => _notes;
        set { _notes = value; OnPropertyChanged(); }
    }

    public int? DoctorVisitId { get; set; }

    public ObservableCollection<ReportPageItem> Pages { get; } = new();

    private bool _hasPages;
    public bool HasPages
    {
        get => _hasPages;
        set { _hasPages = value; OnPropertyChanged(); }
    }

    private string _ocrStatus = string.Empty;
    public string OcrStatus
    {
        get => _ocrStatus;
        set { _ocrStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasOcrStatus)); }
    }
    public bool HasOcrStatus => !string.IsNullOrEmpty(_ocrStatus);

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public string[] ReportTypes { get; } = ["Lab", "Prescription", "Xray", "Scan", "Other"];

    public ICommand PickFileCommand { get; }
    public ICommand CapturePhotoCommand { get; }
    public ICommand RemovePageCommand { get; }
    public ICommand SaveCommand { get; }
    public event EventHandler? SavedSuccessfully;

    private readonly StringBuilder _accumulatedOcrText = new();

    private readonly AiParserService _aiParser;

    public AddReportViewModel(ReportService service, ReportTextParserService textParser, AiParserService aiParser)
    {
        _service = service;
        _textParser = textParser;
        _aiParser = aiParser;
        PickFileCommand = new Command(async () => await PickFile());
        CapturePhotoCommand = new Command(async () => await CapturePhoto());
        RemovePageCommand = new Command<ReportPageItem>(RemovePage);
        SaveCommand = new Command(async () => await Save());
    }

    private async Task PickFile()
    {
        ClearError();
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "image/*", "application/pdf" } }
                })
            });
            if (result is null) return;
            var ext = Path.GetExtension(result.FileName).TrimStart('.').ToLower();
            if (!Helpers.ValidationHelper.IsValidFileType(ext, out var err)) { SetError(err); return; }

            AddPage(result.FullPath, ext, result.FileName);
            await RunOcrOnFile(result.FullPath, ext);
        }
        catch (Exception ex) { SetError("Failed to pick file."); System.Diagnostics.Debug.WriteLine(ex); }
    }

    private async Task CapturePhoto()
    {
        ClearError();
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                SetError("Camera permission is required.");
                return;
            }

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                SetError("Camera is not available on this device.");
                return;
            }

            FileResult? photo;
#if ANDROID
            // Mute the shutter sound while capturing; always restore it, even if capture throws.
            var audioManager = (Android.Media.AudioManager?)Android.App.Application.Context
                .GetSystemService(Android.Content.Context.AudioService);
            int originalVolume = -1;
            try
            {
                originalVolume = audioManager?.GetStreamVolume(Android.Media.Stream.System) ?? -1;
                audioManager?.SetStreamVolume(Android.Media.Stream.System, 0,
                    Android.Media.VolumeNotificationFlags.RemoveSoundAndVibrate);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Volume mute failed: {ex.Message}"); }
            try
            {
                photo = await MediaPicker.Default.CapturePhotoAsync();
            }
            finally
            {
                if (originalVolume >= 0)
                {
                    try { audioManager?.SetStreamVolume(Android.Media.Stream.System, originalVolume, 0); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Volume restore failed: {ex.Message}"); }
                }
            }
#else
            photo = await MediaPicker.Default.CapturePhotoAsync();
#endif

            if (photo is null) return;

            AddPage(photo.FullPath, "jpg", photo.FileName);
            await RunOcrOnFile(photo.FullPath, "jpg");
        }
        catch (Exception ex) { SetError("Failed to capture photo."); System.Diagnostics.Debug.WriteLine(ex); }
    }

    private void AddPage(string filePath, string fileType, string fileName)
    {
        var pageNum = Pages.Count + 1;
        Pages.Add(new ReportPageItem
        {
            FilePath = filePath,
            FileType = fileType,
            FileName = $"Page {pageNum}: {fileName}",
            PageNumber = pageNum
        });
        HasPages = true;
    }

    private void RemovePage(ReportPageItem page)
    {
        Pages.Remove(page);
        for (int i = 0; i < Pages.Count; i++)
        {
            Pages[i].PageNumber = i + 1;
            var baseName = Pages[i].FileName;
            var colonIdx = baseName.IndexOf(':');
            if (colonIdx > 0)
                baseName = baseName[(colonIdx + 1)..].Trim();
            Pages[i].FileName = $"Page {i + 1}: {baseName}";
        }
        HasPages = Pages.Count > 0;
    }

    private async Task RunOcrOnFile(string filePath, string fileType)
    {
        // Only OCR image files, not PDFs
        if (fileType is "pdf") return;

        IsScanning = true;
        OcrStatus = "Scanning page for text...";
        try
        {
            using var stream = File.OpenRead(filePath);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var imageData = ms.ToArray();

            var text = await _textParser.ExtractTextFromImage(imageData);
            if (string.IsNullOrWhiteSpace(text))
            {
                OcrStatus = "No text detected on this page.";
                return;
            }

            _accumulatedOcrText.AppendLine(text);
            var allText = _accumulatedOcrText.ToString();

            // Try AI parsing first
            if (AiParserService.IsConfigured)
            {
                OcrStatus = "AI analyzing report...";
                var aiResult = await _aiParser.ParseReportAsync(allText);
                if (aiResult != null)
                {
                    ApplyAiFields(aiResult);
                    OcrStatus = "Fields pre-filled by AI.";
                    return;
                }
            }

            // Fall back to regex parsing
            OcrStatus = "Extracting fields from text...";
            var fields = _textParser.ParseReportFields(allText);
            ApplyParsedFields(fields);
            OcrStatus = "Fields pre-filled from scanned text.";
        }
        catch (Exception ex)
        {
            OcrStatus = "OCR failed: " + ex.Message;
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally { IsScanning = false; }
    }

    private void ApplyAiFields(AiParsedReport fields)
    {
        if (string.IsNullOrWhiteSpace(Title) && !string.IsNullOrEmpty(fields.Title))
            Title = fields.Title;

        if (string.IsNullOrWhiteSpace(DoctorName) && !string.IsNullOrEmpty(fields.DoctorName))
            DoctorName = fields.DoctorName;

        if (string.IsNullOrWhiteSpace(Hospital) && !string.IsNullOrEmpty(fields.Hospital))
            Hospital = fields.Hospital;

        if (!string.IsNullOrEmpty(fields.ReportType))
            ReportType = fields.ReportType;

        if (!string.IsNullOrEmpty(fields.ReportDate) &&
            DateTime.TryParse(fields.ReportDate, out var parsed))
            ReportDate = parsed;
    }

    private void ApplyParsedFields(ParsedReportFields fields)
    {
        if (string.IsNullOrWhiteSpace(Title) && !string.IsNullOrEmpty(fields.Title))
            Title = fields.Title;

        if (string.IsNullOrWhiteSpace(DoctorName) && !string.IsNullOrEmpty(fields.DoctorName))
            DoctorName = fields.DoctorName;

        if (string.IsNullOrWhiteSpace(Hospital) && !string.IsNullOrEmpty(fields.Hospital))
            Hospital = fields.Hospital;

        if (!string.IsNullOrEmpty(fields.ReportType))
            ReportType = fields.ReportType;

        if (!string.IsNullOrEmpty(fields.ReportDate) &&
            DateTime.TryParse(fields.ReportDate, out var parsed))
            ReportDate = parsed;
    }

    private async Task Save()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        try
        {
            if (string.IsNullOrWhiteSpace(Title)) { SetError("Title is required."); return; }
            if (Pages.Count == 0) { SetError("Please add at least one page."); return; }

            var pageList = Pages.Select(p => (p.FilePath, p.FileType)).ToList();
            await _service.SaveMultiPageReport(pageList, ReportType, Title, DoctorName,
                Hospital, ReportDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), Notes, DoctorVisitId);

            SavedSuccessfully?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { SetError(ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
