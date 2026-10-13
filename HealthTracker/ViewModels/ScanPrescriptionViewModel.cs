using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Repositories;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class ScannedMedicineItem : BaseViewModel
{
    private bool _isSelected = true;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public string Name { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Instructions { get; set; }
    public string DisplayText => string.Join(" · ", new[] { Name, Dosage, Frequency, Instructions }
        .Where(s => !string.IsNullOrEmpty(s)));
}

public class ScanPrescriptionViewModel : BaseViewModel
{
    private readonly PrescriptionParserService _parser;
    private readonly PendingMedicineRepository _pendingRepo;
    private string _ocrText = string.Empty;
    private bool _hasResults;
    private bool _hasScanned;
    private bool _savedSuccessfully;

    public ObservableCollection<ScannedMedicineItem> ScannedMedicines { get; } = new();

    public string OcrText
    {
        get => _ocrText;
        set { _ocrText = value; OnPropertyChanged(); }
    }

    public bool HasResults
    {
        get => _hasResults;
        set { _hasResults = value; OnPropertyChanged(); }
    }

    public bool HasScanned
    {
        get => _hasScanned;
        set { _hasScanned = value; OnPropertyChanged(); }
    }

    public bool SavedSuccessfully
    {
        get => _savedSuccessfully;
        set { _savedSuccessfully = value; OnPropertyChanged(); }
    }

    private string _resultMessage = string.Empty;
    public string ResultMessage
    {
        get => _resultMessage;
        set { _resultMessage = value; OnPropertyChanged(); }
    }

    public ICommand PickImageCommand { get; }
    public ICommand CaptureImageCommand { get; }
    public ICommand SaveToPendingCommand { get; }

    public ScanPrescriptionViewModel(PrescriptionParserService parser, PendingMedicineRepository pendingRepo)
    {
        _parser = parser;
        _pendingRepo = pendingRepo;
        PickImageCommand = new Command(async () => await ScanFromGallery());
        CaptureImageCommand = new Command(async () => await ScanFromCamera());
        SaveToPendingCommand = new Command(async () => await SaveToPending());
    }

    private async Task ScanFromGallery()
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync();
            if (result != null)
                await ProcessImage(result);
        }
        catch (Exception ex)
        {
            SetError("Failed to pick image. " + ex.Message);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async Task ScanFromCamera()
    {
        try
        {
            var result = await MediaPicker.CapturePhotoAsync();
            if (result != null)
                await ProcessImage(result);
        }
        catch (Exception ex)
        {
            SetError("Failed to capture photo. " + ex.Message);
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private async Task ProcessImage(FileResult imageFile)
    {
        IsBusy = true; ClearError();
        HasScanned = false;
        HasResults = false;
        SavedSuccessfully = false;
        try
        {
            using var stream = await imageFile.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var imageData = ms.ToArray();

            var text = await _parser.ExtractTextFromImage(imageData);
            OcrText = text;
            HasScanned = true;

            if (string.IsNullOrWhiteSpace(text))
            {
                SetError("Could not read any text from the image. Try a clearer photo.");
                return;
            }

            ScannedMedicines.Clear();
            var parsed = _parser.ParseMedicines(text);
            foreach (var m in parsed)
            {
                ScannedMedicines.Add(new ScannedMedicineItem
                {
                    Name = m.Name,
                    Dosage = m.Dosage,
                    Frequency = m.Frequency,
                    Instructions = m.Instructions
                });
            }

            HasResults = ScannedMedicines.Count > 0;
            if (!HasResults)
                SetError("No medicines detected. You can read the text above and add manually.");
        }
        catch (Exception ex) { SetError("OCR failed: " + ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task SaveToPending()
    {
        var selected = ScannedMedicines.Where(m => m.IsSelected).ToList();
        if (selected.Count == 0)
        {
            SetError("Select at least one medicine.");
            return;
        }

        IsBusy = true; ClearError();
        try
        {
            var now = DateTimeHelper.ToDateTimeString(DateTime.Now);
            foreach (var item in selected)
            {
                await _pendingRepo.InsertAsync(new PendingMedicine
                {
                    Name = item.Name,
                    Dosage = item.Dosage,
                    Frequency = item.Frequency,
                    Form = item.Instructions,
                    ScannedAt = now
                });
            }

            SavedSuccessfully = true;
            HasResults = false;
            ResultMessage = $"{selected.Count} medicine(s) saved to pending review:\n{string.Join("\n", selected.Select(m => $"  - {m.Name}"))}";

            await Shell.Current.DisplayAlert(
                "Saved to Pending",
                $"{selected.Count} medicine(s) added to pending list.\n\nGo to Medicines to review and approve each one.",
                "OK");
        }
        catch (Exception ex) { SetError("Failed to save: " + ex.Message); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
