using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Repositories;

namespace HealthTracker.Services;

public class MedicineService
{
    /// <summary>How far back AutoLogMissedDoses will look when the app has not been opened for a while.</summary>
    private const int MaxAutoLogLookbackDays = 30;

    private readonly MedicineRepository _medicineRepo;
    private readonly MedicineScheduleRepository _scheduleRepo;
    private readonly MedicineLogRepository _logRepo;
    private readonly MedicineCourseRepository _courseRepo;
    private readonly IReminderService _reminderService;

    public MedicineService(MedicineRepository medicineRepo, MedicineScheduleRepository scheduleRepo,
        MedicineLogRepository logRepo, MedicineCourseRepository courseRepo, IReminderService reminderService)
    {
        _medicineRepo = medicineRepo;
        _scheduleRepo = scheduleRepo;
        _logRepo = logRepo;
        _courseRepo = courseRepo;
        _reminderService = reminderService;
    }

    private static string? ComputeEndDate(string startDate, int prescribedDays, bool isOngoing)
    {
        if (isOngoing || prescribedDays <= 0) return null;
        return DateTimeHelper.TryParse(startDate, out var sd)
            ? DateTimeHelper.ToDateString(sd.Date.AddDays(prescribedDays - 1))
            : null;
    }

    public async Task<Medicine> AddMedicine(string name,
        double stock, string stockUnit, string startDate, string? endDate,
        string? instructions, string? notes, int prescribedDays, bool isOngoing, List<string> schedules)
    {
        if (schedules.Count == 0)
            throw new InvalidOperationException("At least one schedule time is required.");

        schedules = schedules.Distinct().ToList();

        // Compute end date from prescribed days if not explicitly provided
        var computedEndDate = isOngoing ? null : endDate ?? ComputeEndDate(startDate, prescribedDays, isOngoing);

        var medicine = new Medicine
        {
            Name = name,
            StockCount = stock,
            StockUnit = stockUnit,
            StartDate = startDate,
            EndDate = computedEndDate,
            Instructions = instructions,
            Notes = notes,
            PrescribedDays = prescribedDays,
            IsOngoing = isOngoing ? 1 : 0,
            IsActive = 1,
            CreatedAt = DateTimeHelper.ToDateTimeString(DateTime.Now)
        };

        await _medicineRepo.InsertAsync(medicine);

        // New medicine gets highest SortOrder so it appears first
        var all = await _medicineRepo.GetAllActiveAsync();
        medicine.SortOrder = all.Count > 0 ? all.Max(m => m.SortOrder) + 1 : 1;
        await _medicineRepo.UpdateAsync(medicine);

        foreach (var time in schedules)
        {
            await _scheduleRepo.InsertAsync(new MedicineSchedule
            {
                MedicineId = medicine.Id,
                ScheduleTime = time,
                IsActive = 1
            });
        }

        // Create first course
        await _courseRepo.InsertAsync(new MedicineCourse
        {
            MedicineId = medicine.Id,
            StartDate = startDate,
            EndDate = computedEndDate,
            PrescribedDays = isOngoing ? 0 : prescribedDays,
            SchedulesPerDay = schedules.Count,
            TotalTaken = 0,
            TotalScheduled = isOngoing ? 0 : prescribedDays * schedules.Count,
            Status = "Active",
            CreatedAt = DateTimeHelper.ToDateTimeString(DateTime.Now)
        });

        await _reminderService.ScheduleReminders(medicine.Id);
        return medicine;
    }

    public Task<List<Medicine>> GetAllActiveMedicines() =>
        _medicineRepo.GetAllActiveAsync();

    public Task<List<Medicine>> GetAllInactiveMedicines() =>
        _medicineRepo.GetAllInactiveAsync();

    public Task<Medicine?> GetMedicineById(int id) =>
        _medicineRepo.GetByIdAsync(id);

    public Task<List<MedicineSchedule>> GetSchedulesForMedicine(int medicineId) =>
        _scheduleRepo.GetByMedicineIdAsync(medicineId);

    public Task<List<MedicineCourse>> GetCoursesForMedicine(int medicineId) =>
        _courseRepo.GetByMedicineIdAsync(medicineId);

    public async Task CheckAndCompleteCourses()
    {
        var activeCourses = await _courseRepo.GetAllActiveCoursesAsync();
        foreach (var course in activeCourses)
        {
            bool endedByDate = DateTimeHelper.TryParse(course.EndDate, out var endDate)
                && endDate.Date < DateTime.Today;

            // Also complete if all scheduled doses are taken (handles missing EndDate)
            bool endedByDoses = course.TotalScheduled > 0
                && course.TotalTaken >= course.TotalScheduled;

            if (!endedByDate && !endedByDoses) continue;

            // Course has ended — complete it and deactivate medicine
            course.Status = "Completed";
            await _courseRepo.UpdateAsync(course);

            var medicine = await _medicineRepo.GetByIdAsync(course.MedicineId);
            if (medicine is null || medicine.IsActive == 0) continue;
            medicine.IsActive = 0;
            await _medicineRepo.UpdateAsync(medicine);
            await _reminderService.CancelReminders(medicine.Id);
        }
    }

    public async Task StartNewCourse(int medicineId, int prescribedDays, string startDate)
    {
        // Close current active course
        var current = await _courseRepo.GetActiveCourseAsync(medicineId);
        if (current != null)
        {
            current.Status = "Completed";
            current.EndDate = DateTimeHelper.ToDateString(DateTime.Today.AddDays(-1));
            await _courseRepo.UpdateAsync(current);
        }

        var medicine = await _medicineRepo.GetByIdAsync(medicineId);
        if (medicine is null) return;

        var schedules = await _scheduleRepo.GetByMedicineIdAsync(medicineId);

        // Update medicine
        medicine.IsActive = 1;
        medicine.PrescribedDays = prescribedDays;
        medicine.StartDate = startDate;
        medicine.EndDate = ComputeEndDate(startDate, prescribedDays, isOngoing: false);
        await _medicineRepo.UpdateAsync(medicine);

        // Create new course
        await _courseRepo.InsertAsync(new MedicineCourse
        {
            MedicineId = medicineId,
            StartDate = startDate,
            EndDate = medicine.EndDate,
            PrescribedDays = prescribedDays,
            SchedulesPerDay = schedules.Count,
            TotalTaken = 0,
            TotalScheduled = prescribedDays * schedules.Count,
            Status = "Active",
            CreatedAt = DateTimeHelper.ToDateTimeString(DateTime.Now)
        });

        await _reminderService.ScheduleReminders(medicineId);
    }

    public async Task DeactivateMedicine(int id)
    {
        var medicine = await _medicineRepo.GetByIdAsync(id);
        if (medicine is null) return;
        medicine.IsActive = 0;
        await _medicineRepo.UpdateAsync(medicine);
        await _reminderService.CancelReminders(id);
    }

    public async Task DeleteMedicine(int id)
    {
        await _reminderService.CancelReminders(id);
        var schedules = await _scheduleRepo.GetByMedicineIdAsync(id);
        foreach (var s in schedules)
            await _scheduleRepo.DeleteAsync(s);
        var courses = await _courseRepo.GetByMedicineIdAsync(id);
        foreach (var c in courses)
            await _courseRepo.DeleteAsync(c);
        await _logRepo.DeleteByMedicineIdAsync(id);
        var medicine = await _medicineRepo.GetByIdAsync(id);
        if (medicine is null) return;
        await _medicineRepo.DeleteAsync(medicine);
    }

    public async Task UpdateMedicine(Medicine medicine, List<string> schedules)
    {
        schedules = schedules.Distinct().ToList();

        // Cancel reminders while the old schedule rows still exist, so their notification ids are known.
        await _reminderService.CancelReminders(medicine.Id);

        // Recompute EndDate only when the course definition changed, so an extension added by
        // AutoLogMissedDoses survives an unrelated edit (e.g. changing the notes).
        var previous = await _medicineRepo.GetByIdAsync(medicine.Id);
        bool courseDefinitionChanged = previous is null
            || previous.StartDate != medicine.StartDate
            || previous.PrescribedDays != medicine.PrescribedDays
            || previous.IsOngoing != medicine.IsOngoing;
        if (courseDefinitionChanged)
            medicine.EndDate = ComputeEndDate(medicine.StartDate, medicine.PrescribedDays, medicine.IsOngoing == 1);

        await _medicineRepo.UpdateAsync(medicine);

        // Diff schedules by time so unchanged rows keep their ids (dose logs reference ScheduleId).
        var existing = await _scheduleRepo.GetByMedicineIdAsync(medicine.Id);
        foreach (var s in existing.Where(s => !schedules.Contains(s.ScheduleTime)))
            await _scheduleRepo.DeleteAsync(s);
        foreach (var time in schedules.Where(t => existing.All(s => s.ScheduleTime != t)))
        {
            await _scheduleRepo.InsertAsync(new MedicineSchedule
            {
                MedicineId = medicine.Id,
                ScheduleTime = time,
                IsActive = 1
            });
        }

        // Update active course schedule info
        var course = await _courseRepo.GetActiveCourseAsync(medicine.Id);
        if (course != null)
        {
            course.PrescribedDays = medicine.IsOngoing == 1 ? 0 : medicine.PrescribedDays;
            course.SchedulesPerDay = schedules.Count;
            course.TotalScheduled = medicine.IsOngoing == 1 ? 0 : medicine.PrescribedDays * schedules.Count;
            course.EndDate = medicine.IsOngoing == 1 ? null : medicine.EndDate;
            await _courseRepo.UpdateAsync(course);
        }

        await _reminderService.ScheduleReminders(medicine.Id);
    }

    public async Task UpdateStock(int medicineId, double newStock)
    {
        var medicine = await _medicineRepo.GetByIdAsync(medicineId);
        if (medicine is null) return;
        medicine.StockCount = newStock;
        await _medicineRepo.UpdateAsync(medicine);
    }

    /// <summary>
    /// Writes a "missed" log for every past schedule slot (up to yesterday) that has no log,
    /// covering every day since the app was last opened. For fixed-length courses the end date
    /// is pushed back by the number of whole days' worth of doses that were missed.
    /// </summary>
    public async Task AutoLogMissedDoses()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        var medicines = await _medicineRepo.GetAllActiveAsync();

        foreach (var med in medicines)
        {
            if (!DateTimeHelper.TryParse(med.StartDate, out var startDateTime)) continue;
            if (yesterday < startDateTime.Date) continue;

            var isOngoing = med.IsOngoing == 1;
            var hasEnd = DateTimeHelper.TryParse(med.EndDate, out var endDate);
            if (!isOngoing && !hasEnd) continue;

            var schedules = await _scheduleRepo.GetByMedicineIdAsync(med.Id);
            if (schedules.Count == 0) continue;

            var from = startDateTime.Date;
            var lookbackLimit = yesterday.AddDays(-(MaxAutoLogLookbackDays - 1));
            if (from < lookbackLimit) from = lookbackLimit;

            var to = yesterday;
            if (hasEnd && endDate.Date < to) to = endDate.Date;
            if (from > to) continue;

            var existingLogs = await _logRepo.GetLogsForMedicineAsync(med.Id,
                DateTimeHelper.ToDateTimeString(from),
                DateTimeHelper.ToDateTimeString(to.AddDays(1).AddSeconds(-1)));
            var loggedSlots = existingLogs
                .Select(l => (l.ScheduleId, Day: l.ScheduledFor.Length >= 10 ? l.ScheduledFor[..10] : l.ScheduledFor))
                .ToHashSet();

            int missedCount = 0;
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var dayKey = DateTimeHelper.ToDateString(day);
                foreach (var sched in schedules)
                {
                    if (!TimeSpan.TryParse(sched.ScheduleTime, out var time)) continue;
                    var slot = day.Add(time);
                    if (slot < startDateTime) continue; // slot passed before the medicine was started
                    if (loggedSlots.Contains((sched.Id, dayKey))) continue;

                    await _logRepo.InsertAsync(new MedicineLog
                    {
                        MedicineId = med.Id,
                        ScheduleId = sched.Id,
                        ScheduledFor = DateTimeHelper.ToDateTimeString(slot),
                        TakenAt = null,
                        WasTaken = 0,
                        StockBefore = med.StockCount,
                        StockAfter = med.StockCount
                    });
                    missedCount++;
                }
            }

            if (missedCount == 0 || isOngoing || !hasEnd) continue;

            // Extend the course by whole days: each day's worth of missed doses adds one day.
            var extraDays = (int)Math.Ceiling(missedCount / (double)schedules.Count);
            if (extraDays <= 0) continue;

            med.EndDate = DateTimeHelper.ToDateString(endDate.Date.AddDays(extraDays));
            await _medicineRepo.UpdateAsync(med);

            var course = await _courseRepo.GetActiveCourseAsync(med.Id);
            if (course != null && DateTimeHelper.TryParse(course.EndDate, out var courseEnd))
            {
                course.EndDate = DateTimeHelper.ToDateString(courseEnd.Date.AddDays(extraDays));
                await _courseRepo.UpdateAsync(course);
            }
        }
    }

    public async Task UpdateSortOrder(int medicineId, int order)
    {
        var medicine = await _medicineRepo.GetByIdAsync(medicineId);
        if (medicine is null) return;
        medicine.SortOrder = order;
        await _medicineRepo.UpdateAsync(medicine);
    }

    public async Task<MedicineLog> LogDoseTaken(int medicineId, int scheduleId)
    {
        var medicine = await _medicineRepo.GetByIdAsync(medicineId)
            ?? throw new InvalidOperationException("Medicine not found.");
        var schedule = await _scheduleRepo.GetScheduleByIdAsync(scheduleId)
            ?? throw new InvalidOperationException("Schedule not found.");

        var stockBefore = medicine.StockCount;
        var stockAfter = Math.Max(0, stockBefore - 1);
        var scheduledFor = TimeSpan.TryParse(schedule.ScheduleTime, out var time)
            ? DateTime.Today.Add(time)
            : DateTime.Now;

        var log = new MedicineLog
        {
            MedicineId = medicineId,
            ScheduleId = scheduleId,
            ScheduledFor = DateTimeHelper.ToDateTimeString(scheduledFor),
            TakenAt = DateTimeHelper.ToDateTimeString(DateTime.Now),
            WasTaken = 1,
            StockBefore = stockBefore,
            StockAfter = stockAfter
        };

        await _logRepo.InsertAsync(log);
        medicine.StockCount = stockAfter;
        await _medicineRepo.UpdateAsync(medicine);

        // Update active course TotalTaken
        var course = await _courseRepo.GetActiveCourseAsync(medicineId);
        if (course != null)
        {
            course.TotalTaken++;
            await _courseRepo.UpdateAsync(course);
        }

        return log;
    }

    public async Task<List<MedicineLog>> GetLogsForMedicine(int medicineId, DateTime from, DateTime to)
    {
        return await _logRepo.GetLogsForMedicineAsync(medicineId,
            DateTimeHelper.ToDateTimeString(from),
            DateTimeHelper.ToDateTimeString(to));
    }

    public async Task<(int streak, int missedThisWeek)> GetAdherenceStats(int medicineId)
    {
        var schedules = await GetSchedulesForMedicine(medicineId);
        var schedulesPerDay = schedules.Count;

        // Last 7 calendar days including today
        var logs7d = await GetLogsForMedicine(medicineId, DateTime.Today.AddDays(-6), DateTime.Today.AddDays(1).AddSeconds(-1));
        var missedThisWeek = logs7d.Count(l => l.WasTaken == 0);

        var logs30d = await GetLogsForMedicine(medicineId, DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1).AddSeconds(-1));

        var takenPerDay = logs30d
            .Where(l => l.WasTaken == 1)
            .GroupBy(l => l.ScheduledFor.Length >= 10 ? l.ScheduledFor[..10] : l.ScheduledFor)
            .ToDictionary(g => g.Key, g => g.Count());

        bool AllTaken(string date) =>
            schedulesPerDay > 0 && takenPerDay.TryGetValue(date, out var count) && count >= schedulesPerDay;

        var today = DateTimeHelper.ToDateString(DateTime.Today);
        int streak = AllTaken(today) ? 1 : 0;
        for (int i = 1; i <= 30; i++)
        {
            if (AllTaken(DateTimeHelper.ToDateString(DateTime.Today.AddDays(-i))))
                streak++;
            else
                break;
        }

        return (streak, missedThisWeek);
    }

    public double GetStockDaysRemaining(Medicine medicine, int schedulesPerDay)
    {
        if (medicine.IsOngoing == 1 || schedulesPerDay <= 0) return double.MaxValue;
        return medicine.StockCount / schedulesPerDay;
    }

    public bool HasInsufficientStock(Medicine medicine)
    {
        return medicine.StockCount < 1;
    }

    public async Task<int> BackfillPastDoses(int medicineId, DateTime fromDate, DateTime toDate)
    {
        var medicine = await _medicineRepo.GetByIdAsync(medicineId)
            ?? throw new InvalidOperationException("Medicine not found.");
        var schedules = await _scheduleRepo.GetByMedicineIdAsync(medicineId);
        if (schedules.Count == 0) return 0;

        int count = 0;
        for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
        {
            var existingLogs = await GetLogsForMedicine(medicineId, date, date.AddDays(1).AddSeconds(-1));

            foreach (var sched in schedules)
            {
                if (existingLogs.Any(l => l.ScheduleId == sched.Id)) continue;
                if (!TimeSpan.TryParse(sched.ScheduleTime, out var time)) continue;

                var scheduledDateTime = date.Add(time);
                // Skip future slots (today's doses not yet due)
                if (scheduledDateTime > DateTime.Now) continue;
                var stockBefore = medicine.StockCount;
                var stockAfter = Math.Max(0, stockBefore - 1);

                var log = new MedicineLog
                {
                    MedicineId = medicineId,
                    ScheduleId = sched.Id,
                    ScheduledFor = DateTimeHelper.ToDateTimeString(scheduledDateTime),
                    TakenAt = DateTimeHelper.ToDateTimeString(scheduledDateTime),
                    WasTaken = 1,
                    StockBefore = stockBefore,
                    StockAfter = stockAfter
                };

                await _logRepo.InsertAsync(log);
                medicine.StockCount = stockAfter;
                count++;
            }
        }

        await _medicineRepo.UpdateAsync(medicine);

        var course = await _courseRepo.GetActiveCourseAsync(medicineId);
        if (course != null)
        {
            course.TotalTaken += count;
            await _courseRepo.UpdateAsync(course);
        }

        return count;
    }
}
