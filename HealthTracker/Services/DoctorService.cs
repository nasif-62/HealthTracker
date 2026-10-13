using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Repositories;

namespace HealthTracker.Services;

public class DoctorService
{
    private readonly DoctorVisitRepository _repo;

    public DoctorService(DoctorVisitRepository repo)
    {
        _repo = repo;
    }

    public async Task<DoctorVisit> AddVisit(string doctorName, string? hospital, string visitDate,
        string? diagnosis, string? notes, string? followUpDate, string? prescriptionImageSourcePath = null)
    {
        // RULE-06: follow-up date must be today or future
        if (followUpDate != null)
        {
            if (!DateTimeHelper.TryParse(followUpDate, out var fuDate))
                throw new ArgumentException("Follow-up date is not a valid date.");
            if (fuDate.Date < DateTime.Today)
                throw new ArgumentException("Follow-up date must be today or a future date.");
        }

        string? savedImagePath = null;
        if (!string.IsNullOrEmpty(prescriptionImageSourcePath) && File.Exists(prescriptionImageSourcePath))
        {
            FileStorageHelper.EnsureFoldersExist();
            var folder = FileStorageHelper.GetSubFolder("Prescription");
            var ext = Path.GetExtension(prescriptionImageSourcePath).TrimStart('.');
            var fileName = FileStorageHelper.BuildFileName("Rx", visitDate, ext);
            savedImagePath = Path.Combine(folder, fileName);
            File.Copy(prescriptionImageSourcePath, savedImagePath, overwrite: false);
        }

        var visit = new DoctorVisit
        {
            DoctorName = doctorName,
            Hospital = hospital,
            VisitDate = visitDate,
            Diagnosis = diagnosis,
            Notes = notes,
            FollowUpDate = followUpDate,
            PrescriptionImagePath = savedImagePath,
            CreatedAt = DateTimeHelper.ToDateTimeString(DateTime.Now)
        };

        await _repo.InsertAsync(visit);
        return visit;
    }

    public async Task UpdateVisit(DoctorVisit visit)
    {
        await _repo.UpdateAsync(visit);
    }

    public async Task DeleteVisit(int id)
    {
        var visit = await _repo.GetByIdAsync(id);
        if (visit is null) return;
        if (!string.IsNullOrEmpty(visit.PrescriptionImagePath) && File.Exists(visit.PrescriptionImagePath))
            File.Delete(visit.PrescriptionImagePath);
        await _repo.DeleteAsync(visit);
    }

    public Task<List<DoctorVisit>> GetAllVisits() => _repo.GetAllOrderedAsync();

    public Task<DoctorVisit?> GetVisitById(int id) => _repo.GetByIdAsync(id);
}
