using HealthTracker.Models;
using SQLite;

namespace HealthTracker.Services;

/// <summary>In-memory SQLite replacement for DatabaseService used in tests.</summary>
public class DatabaseService
{
    private SQLiteAsyncConnection? _conn;

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_conn != null) return _conn;
        _conn = new SQLiteAsyncConnection(":memory:");
        await _conn.CreateTableAsync<Medicine>();
        await _conn.CreateTableAsync<MedicineSchedule>();
        await _conn.CreateTableAsync<MedicineLog>();
        await _conn.CreateTableAsync<MedicineCourse>();
        await _conn.CreateTableAsync<HealthMetric>();
        await _conn.CreateTableAsync<MedicalReport>();
        await _conn.CreateTableAsync<DoctorVisit>();
        return _conn;
    }
}
