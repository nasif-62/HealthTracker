using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.Repositories;

public class MedicalReportRepository : BaseRepository<MedicalReport>
{
    public MedicalReportRepository(DatabaseService db) : base(db) { }

    public async Task<List<MedicalReport>> GetAllOrderedAsync()
    {
        var db = await GetDbAsync();
        return await db.Table<MedicalReport>().OrderByDescending(r => r.ReportDate).ToListAsync();
    }

    public async Task<List<MedicalReport>> GetByVisitIdAsync(int visitId)
    {
        var db = await GetDbAsync();
        return await db.Table<MedicalReport>().Where(r => r.DoctorVisitId == visitId).ToListAsync();
    }

    public new async Task<MedicalReport?> GetByIdAsync(int id)
    {
        var db = await GetDbAsync();
        return await db.Table<MedicalReport>().Where(r => r.Id == id).FirstOrDefaultAsync();
    }
}
