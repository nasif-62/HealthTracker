using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Repositories;

namespace HealthTracker.Services;

public class ReportService
{
    private readonly MedicalReportRepository _repo;
    private readonly ReportPageRepository _pageRepo;

    public ReportService(MedicalReportRepository repo, ReportPageRepository pageRepo)
    {
        _repo = repo;
        _pageRepo = pageRepo;
    }

    public async Task<MedicalReport> SaveReport(string sourcePath, string type, string title,
        string fileType, string? doctorName, string? hospital, string reportDate,
        string? notes, int? visitId)
    {
        return await SaveMultiPageReport([(sourcePath, fileType)], type, title, doctorName,
            hospital, reportDate, notes, visitId);
    }

    public async Task<MedicalReport> SaveMultiPageReport(List<(string Path, string FileType)> pages,
        string type, string title, string? doctorName, string? hospital, string reportDate,
        string? notes, int? visitId)
    {
        if (pages.Count == 0)
            throw new ArgumentException("At least one page is required.");

        FileStorageHelper.EnsureFoldersExist();
        var destFolder = FileStorageHelper.GetSubFolder(type);

        // Copy first page as the primary file on MedicalReport
        var firstExt = pages[0].FileType;
        if (!ValidationHelper.IsValidFileType(firstExt, out var error))
            throw new ArgumentException(error);

        var firstName = FileStorageHelper.BuildFileName(type, reportDate, firstExt);
        var firstDest = Path.Combine(destFolder, firstName);
        File.Copy(pages[0].Path, firstDest, overwrite: false);
        if (!File.Exists(firstDest))
            throw new IOException("File copy failed. Report not saved.");

        var report = new MedicalReport
        {
            DoctorVisitId = visitId,
            Title = title,
            ReportType = type,
            FilePath = firstDest,
            FileType = firstExt.ToLowerInvariant().TrimStart('.'),
            ReportDate = reportDate,
            DoctorName = doctorName,
            Hospital = hospital,
            Notes = notes,
            CreatedAt = DateTimeHelper.ToDateTimeString(DateTime.Now)
        };

        await _repo.InsertAsync(report);

        // Save all pages (including first) to ReportPage table
        for (int i = 0; i < pages.Count; i++)
        {
            var (srcPath, ext) = pages[i];
            string destPath;
            if (i == 0)
            {
                destPath = firstDest;
            }
            else
            {
                if (!ValidationHelper.IsValidFileType(ext, out _))
                    continue;
                var fn = FileStorageHelper.BuildFileName(type, reportDate, ext);
                destPath = Path.Combine(destFolder, fn);
                File.Copy(srcPath, destPath, overwrite: false);
            }

            await _pageRepo.InsertAsync(new ReportPage
            {
                ReportId = report.Id,
                FilePath = destPath,
                FileType = ext.ToLowerInvariant().TrimStart('.'),
                PageNumber = i + 1
            });
        }

        return report;
    }

    public async Task DeleteReport(int id)
    {
        var report = await _repo.GetByIdAsync(id);
        if (report is null) return;

        // Delete page files
        var pages = await _pageRepo.GetByReportIdAsync(id);
        foreach (var page in pages)
        {
            if (File.Exists(page.FilePath))
                File.Delete(page.FilePath);
        }
        await _pageRepo.DeleteByReportIdAsync(id);

        // Delete primary file if not already deleted via pages
        if (File.Exists(report.FilePath))
            File.Delete(report.FilePath);

        await _repo.DeleteAsync(report);
    }

    public Task<List<MedicalReport>> GetAllReports() => _repo.GetAllOrderedAsync();

    public Task<List<MedicalReport>> GetReportsByVisit(int visitId) => _repo.GetByVisitIdAsync(visitId);

    public Task<List<ReportPage>> GetReportPages(int reportId) => _pageRepo.GetByReportIdAsync(reportId);
}
