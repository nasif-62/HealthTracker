using HealthTracker.Helpers;
using HealthTracker.Models;
using SQLite;

namespace HealthTracker.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static async Task MigrateAsync(SQLiteAsyncConnection db)
    {
        // Remove DoseAmount from MedicineSchedule if present
        var schedCols = await db.QueryAsync<ColumnInfo>("PRAGMA table_info(MedicineSchedule)");
        if (schedCols.Any(c => c.name == "DoseAmount"))
        {
            await db.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS MedicineSchedule_new (
                Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                MedicineId INTEGER NOT NULL,
                ScheduleTime TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1)");
            await db.ExecuteAsync(@"INSERT INTO MedicineSchedule_new (Id, MedicineId, ScheduleTime, IsActive)
                SELECT Id, MedicineId, ScheduleTime, IsActive FROM MedicineSchedule");
            await db.ExecuteAsync("DROP TABLE MedicineSchedule");
            await db.ExecuteAsync("ALTER TABLE MedicineSchedule_new RENAME TO MedicineSchedule");
        }

        // Remove Dosage from Medicine if present. The rebuilt table must carry every column
        // the Medicine model maps, otherwise inserts fail until the next launch re-adds them.
        var medCols = await db.QueryAsync<ColumnInfo>("PRAGMA table_info(Medicine)");
        if (medCols.Any(c => c.name == "Dosage"))
        {
            var hasSortOrder = medCols.Any(c => c.name == "SortOrder");
            await db.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS Medicine_new (
                Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                Name TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                StockCount REAL NOT NULL DEFAULT 0,
                StockUnit TEXT NOT NULL DEFAULT '',
                PrescribedDays INTEGER NOT NULL DEFAULT 0,
                IsOngoing INTEGER DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                StartDate TEXT,
                EndDate TEXT,
                Instructions TEXT,
                Notes TEXT,
                CreatedAt TEXT NOT NULL DEFAULT '')");
            var sortOrderExpr = hasSortOrder ? "COALESCE(SortOrder, 0)" : "0";
            await db.ExecuteAsync($@"INSERT INTO Medicine_new (Id, Name, IsActive, StockCount, StockUnit, PrescribedDays, IsOngoing, SortOrder, StartDate, EndDate, Instructions, Notes, CreatedAt)
                SELECT Id, Name, IsActive, StockCount, StockUnit,
                       COALESCE(PrescribedDays, 0), COALESCE(IsOngoing, 0), {sortOrderExpr},
                       StartDate, EndDate, Instructions, Notes, COALESCE(CreatedAt, '')
                FROM Medicine");
            await db.ExecuteAsync("DROP TABLE Medicine");
            await db.ExecuteAsync("ALTER TABLE Medicine_new RENAME TO Medicine");
        }

        // Ensure columns added after the first release exist (handles any prior broken migration)
        var medColsFinal = await db.QueryAsync<ColumnInfo>("PRAGMA table_info(Medicine)");
        if (!medColsFinal.Any(c => c.name == "IsOngoing"))
            await db.ExecuteAsync("ALTER TABLE Medicine ADD COLUMN IsOngoing INTEGER DEFAULT 0");
        if (!medColsFinal.Any(c => c.name == "SortOrder"))
            await db.ExecuteAsync("ALTER TABLE Medicine ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0");
    }

    private class ColumnInfo { public string name { get; set; } = ""; }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
            return _connection;

        // App.OnStart (RescheduleAll) and the first page load both hit this concurrently at launch;
        // serialize initialization so CreateTable/Migrate run exactly once.
        await _initLock.WaitAsync();
        try
        {
            if (_connection is not null)
                return _connection;

            var dbPath = FileStorageHelper.GetDatabasePath();
            var conn = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create);

            await conn.CreateTableAsync<Medicine>();
            await conn.CreateTableAsync<MedicineSchedule>();
            await conn.CreateTableAsync<MedicineLog>();
            await conn.CreateTableAsync<MedicineCourse>();
            await conn.CreateTableAsync<HealthMetric>();
            await conn.CreateTableAsync<MedicalReport>();
            await conn.CreateTableAsync<DoctorVisit>();
            await conn.CreateTableAsync<PendingMedicine>();
            await conn.CreateTableAsync<ReportPage>();

            await MigrateAsync(conn);

            _connection = conn;
            return _connection;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task DeleteAllDataAsync()
    {
        var conn = await GetConnectionAsync();
        await conn.DeleteAllAsync<MedicineLog>();
        await conn.DeleteAllAsync<MedicineSchedule>();
        await conn.DeleteAllAsync<MedicineCourse>();
        await conn.DeleteAllAsync<Medicine>();
        await conn.DeleteAllAsync<HealthMetric>();
        await conn.DeleteAllAsync<MedicalReport>();
        await conn.DeleteAllAsync<DoctorVisit>();
        await conn.DeleteAllAsync<PendingMedicine>();
        await conn.DeleteAllAsync<ReportPage>();
    }
}
