using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Repositories;
using HealthTracker.Services;

namespace HealthTracker.Tests.Services;

public class MedicineServiceTests
{
    private static MedicineService CreateService(out DatabaseService db)
    {
        db = new DatabaseService();
        var medicineRepo = new MedicineRepository(db);
        var scheduleRepo = new MedicineScheduleRepository(db);
        var logRepo = new MedicineLogRepository(db);
        var courseRepo = new MedicineCourseRepository(db);
        return new MedicineService(medicineRepo, scheduleRepo, logRepo, courseRepo, new NullReminderService());
    }

    private static MedicineService CreateService() => CreateService(out _);

    private static string Today => DateTimeHelper.ToDateString(DateTime.Today);

    private static Task<Medicine> AddDefault(MedicineService svc, string name = "Aspirin",
        double stock = 30, int prescribedDays = 10, bool isOngoing = false,
        string? startDate = null, string? endDate = null, List<string>? schedules = null)
        => svc.AddMedicine(name, stock, "Tablet", startDate ?? Today, endDate, null, null,
            prescribedDays, isOngoing, schedules ?? ["08:00", "20:00"]);

    // ── AddMedicine ──────────────────────────────────────────────────────────
    [Fact]
    public async Task AddMedicine_WithValidData_StoresMedicineSchedulesAndCourse()
    {
        var svc = CreateService();

        var medicine = await AddDefault(svc);

        Assert.True(medicine.Id > 0);
        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.NotNull(stored);
        Assert.Equal("Aspirin", stored.Name);
        Assert.Equal(1, stored.IsActive);

        var storedSchedules = await svc.GetSchedulesForMedicine(medicine.Id);
        Assert.Equal(2, storedSchedules.Count);

        var courses = await svc.GetCoursesForMedicine(medicine.Id);
        var course = Assert.Single(courses);
        Assert.Equal("Active", course.Status);
        Assert.Equal(2, course.SchedulesPerDay);
        Assert.Equal(20, course.TotalScheduled);
        Assert.Equal(0, course.TotalTaken);
    }

    [Fact]
    public async Task AddMedicine_WithNoSchedules_ThrowsInvalidOperationException()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AddDefault(svc, schedules: []));
    }

    [Fact]
    public async Task AddMedicine_ComputesEndDateFromPrescribedDays()
    {
        var svc = CreateService();

        var medicine = await AddDefault(svc, prescribedDays: 10, startDate: "2024-01-01");

        Assert.Equal("2024-01-10", medicine.EndDate);
    }

    [Fact]
    public async Task AddMedicine_WhenOngoing_HasNoEndDateAndZeroTotalScheduled()
    {
        var svc = CreateService();

        var medicine = await AddDefault(svc, isOngoing: true, prescribedDays: 0);

        Assert.Null(medicine.EndDate);
        Assert.Equal(1, medicine.IsOngoing);
        var course = Assert.Single(await svc.GetCoursesForMedicine(medicine.Id));
        Assert.Equal(0, course.TotalScheduled);
    }

    [Fact]
    public async Task AddMedicine_AssignsIncreasingSortOrder()
    {
        var svc = CreateService();

        var first = await AddDefault(svc, "First");
        var second = await AddDefault(svc, "Second");

        Assert.True(second.SortOrder > first.SortOrder);
    }

    // ── GetAllActiveMedicines ────────────────────────────────────────────────
    [Fact]
    public async Task GetAllActiveMedicines_ReturnsOnlyActiveMedicines()
    {
        var svc = CreateService();

        await AddDefault(svc, "Active");
        var inactive = await AddDefault(svc, "Inactive");
        await svc.DeactivateMedicine(inactive.Id);

        var active = await svc.GetAllActiveMedicines();
        var inactiveList = await svc.GetAllInactiveMedicines();

        Assert.Contains(active, m => m.Name == "Active");
        Assert.DoesNotContain(active, m => m.Name == "Inactive");
        Assert.Contains(inactiveList, m => m.Name == "Inactive");
    }

    // ── DeleteMedicine ───────────────────────────────────────────────────────
    [Fact]
    public async Task DeleteMedicine_RemovesMedicineSchedulesAndCourses()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc);

        await svc.DeleteMedicine(medicine.Id);

        Assert.Null(await svc.GetMedicineById(medicine.Id));
        Assert.Empty(await svc.GetSchedulesForMedicine(medicine.Id));
        Assert.Empty(await svc.GetCoursesForMedicine(medicine.Id));
    }

    // ── UpdateMedicine ───────────────────────────────────────────────────────
    [Fact]
    public async Task UpdateMedicine_ReplacesSchedulesAndUpdatesActiveCourse()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, prescribedDays: 10, schedules: ["08:00"]);

        medicine.Name = "Aspirin Updated";
        await svc.UpdateMedicine(medicine, ["07:00", "12:00", "19:00"]);

        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.Equal("Aspirin Updated", stored!.Name);

        var schedules = await svc.GetSchedulesForMedicine(medicine.Id);
        Assert.Equal(3, schedules.Count);

        var course = Assert.Single(await svc.GetCoursesForMedicine(medicine.Id));
        Assert.Equal(3, course.SchedulesPerDay);
        Assert.Equal(30, course.TotalScheduled);
    }

    // ── Stock helpers ────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.5, true)]
    [InlineData(1.0, false)]
    [InlineData(5.0, false)]
    public void HasInsufficientStock_ReturnsTrueOnlyWhenStockBelowOne(double stock, bool expected)
    {
        var svc = CreateService();
        Assert.Equal(expected, svc.HasInsufficientStock(new Medicine { StockCount = stock }));
    }

    [Fact]
    public void GetStockDaysRemaining_DividesStockBySchedulesPerDay()
    {
        var svc = CreateService();
        Assert.Equal(5.0, svc.GetStockDaysRemaining(new Medicine { StockCount = 10 }, 2));
    }

    [Fact]
    public void GetStockDaysRemaining_WhenOngoingOrNoSchedules_ReturnsMax()
    {
        var svc = CreateService();
        Assert.Equal(double.MaxValue, svc.GetStockDaysRemaining(new Medicine { StockCount = 10, IsOngoing = 1 }, 2));
        Assert.Equal(double.MaxValue, svc.GetStockDaysRemaining(new Medicine { StockCount = 10 }, 0));
    }

    // ── LogDoseTaken ─────────────────────────────────────────────────────────
    [Fact]
    public async Task LogDoseTaken_InsertsLogDecrementsStockAndIncrementsCourse()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, stock: 10, schedules: ["08:00"]);
        var scheduleId = (await svc.GetSchedulesForMedicine(medicine.Id))[0].Id;

        var log = await svc.LogDoseTaken(medicine.Id, scheduleId);

        Assert.Equal(1, log.WasTaken);
        Assert.Equal(10.0, log.StockBefore);
        Assert.Equal(9.0, log.StockAfter);

        var updated = await svc.GetMedicineById(medicine.Id);
        Assert.Equal(9.0, updated!.StockCount);

        var course = Assert.Single(await svc.GetCoursesForMedicine(medicine.Id));
        Assert.Equal(1, course.TotalTaken);
    }

    [Fact]
    public async Task LogDoseTaken_WhenStockIsZero_ClampsToZero()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, stock: 0, schedules: ["08:00"]);
        var scheduleId = (await svc.GetSchedulesForMedicine(medicine.Id))[0].Id;

        var log = await svc.LogDoseTaken(medicine.Id, scheduleId);

        Assert.Equal(0.0, log.StockAfter);
    }

    [Fact]
    public async Task LogDoseTaken_WithUnknownMedicine_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.LogDoseTaken(999, 1));
    }

    // ── CheckAndCompleteCourses ──────────────────────────────────────────────
    [Fact]
    public async Task CheckAndCompleteCourses_WhenEndDatePassed_CompletesCourseAndDeactivates()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, prescribedDays: 3, startDate: "2024-01-01");

        await svc.CheckAndCompleteCourses();

        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.Equal(0, stored!.IsActive);
        var course = Assert.Single(await svc.GetCoursesForMedicine(medicine.Id));
        Assert.Equal("Completed", course.Status);
    }

    [Fact]
    public async Task CheckAndCompleteCourses_WhenAllDosesTaken_CompletesCourse()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, prescribedDays: 1, schedules: ["08:00"]);
        var scheduleId = (await svc.GetSchedulesForMedicine(medicine.Id))[0].Id;
        await svc.LogDoseTaken(medicine.Id, scheduleId);

        await svc.CheckAndCompleteCourses();

        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.Equal(0, stored!.IsActive);
    }

    [Fact]
    public async Task CheckAndCompleteCourses_WhenCourseStillRunning_LeavesActive()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, prescribedDays: 10);

        await svc.CheckAndCompleteCourses();

        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.Equal(1, stored!.IsActive);
    }

    // ── StartNewCourse ───────────────────────────────────────────────────────
    [Fact]
    public async Task StartNewCourse_ClosesOldCourseAndReactivatesMedicine()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, prescribedDays: 3, startDate: "2024-01-01");
        await svc.CheckAndCompleteCourses();

        await svc.StartNewCourse(medicine.Id, 5, Today);

        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.Equal(1, stored!.IsActive);
        Assert.Equal(5, stored.PrescribedDays);
        Assert.Equal(DateTimeHelper.ToDateString(DateTime.Today.AddDays(4)), stored.EndDate);

        var courses = await svc.GetCoursesForMedicine(medicine.Id);
        Assert.Equal(2, courses.Count);
        Assert.Single(courses, c => c.Status == "Active");
    }

    // ── AutoLogMissedDoses ───────────────────────────────────────────────────
    [Fact]
    public async Task AutoLogMissedDoses_LogsEveryUnloggedPastDayAndExtendsEndDateByWholeDays()
    {
        var svc = CreateService();
        var start = DateTimeHelper.ToDateString(DateTime.Today.AddDays(-2));
        var medicine = await AddDefault(svc, prescribedDays: 10, startDate: start, schedules: ["08:00", "20:00"]);
        var originalEnd = DateTimeHelper.ParseDate(medicine.EndDate!);

        await svc.AutoLogMissedDoses();

        // Two whole past days (day -2 and day -1), two slots each → 4 missed logs, none for today
        var allPast = await svc.GetLogsForMedicine(medicine.Id, DateTime.Today.AddDays(-2), DateTime.Today.AddSeconds(-1));
        Assert.Equal(4, allPast.Count);
        Assert.All(allPast, l => { Assert.Equal(0, l.WasTaken); Assert.Null(l.TakenAt); });
        Assert.Empty(await svc.GetLogsForMedicine(medicine.Id, DateTime.Today, DateTime.Today.AddDays(1)));

        // 4 missed doses / 2 per day = 2 extra days, and EndDate stays a plain date string
        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.Equal(DateTimeHelper.ToDateString(originalEnd.AddDays(2)), stored!.EndDate);

        var course = Assert.Single(await svc.GetCoursesForMedicine(medicine.Id));
        Assert.Equal(stored.EndDate, course.EndDate);
    }

    [Fact]
    public async Task AutoLogMissedDoses_SkipsSlotsBeforeTheMedicineWasStarted()
    {
        var svc = CreateService();
        // Started yesterday at 15:00 → only the 20:00 slot of yesterday was actually due
        var start = DateTimeHelper.ToDateTimeString(DateTime.Today.AddDays(-1).AddHours(15));
        var medicine = await AddDefault(svc, prescribedDays: 10, startDate: start, schedules: ["08:00", "20:00"]);

        await svc.AutoLogMissedDoses();

        var yesterday = DateTime.Today.AddDays(-1);
        var logs = await svc.GetLogsForMedicine(medicine.Id, yesterday, yesterday.AddDays(1).AddSeconds(-1));
        var log = Assert.Single(logs);
        Assert.EndsWith("T20:00:00", log.ScheduledFor);
    }

    [Fact]
    public async Task AutoLogMissedDoses_IsIdempotent()
    {
        var svc = CreateService();
        var start = DateTimeHelper.ToDateString(DateTime.Today.AddDays(-2));
        var medicine = await AddDefault(svc, prescribedDays: 10, startDate: start, schedules: ["08:00"]);

        await svc.AutoLogMissedDoses();
        await svc.AutoLogMissedDoses();

        var yesterday = DateTime.Today.AddDays(-1);
        var logs = await svc.GetLogsForMedicine(medicine.Id, yesterday, yesterday.AddDays(1).AddSeconds(-1));
        Assert.Single(logs);
    }

    [Fact]
    public async Task AutoLogMissedDoses_LogsOngoingMedicinesWithoutEndDateButSkipsNotYetStarted()
    {
        var svc = CreateService();
        var ongoing = await AddDefault(svc, "Ongoing", isOngoing: true, prescribedDays: 0,
            startDate: DateTimeHelper.ToDateString(DateTime.Today.AddDays(-1)), schedules: ["08:00"]);
        var future = await AddDefault(svc, "Future", prescribedDays: 5,
            startDate: DateTimeHelper.ToDateString(DateTime.Today.AddDays(2)));

        await svc.AutoLogMissedDoses();

        var yesterday = DateTime.Today.AddDays(-1);
        Assert.Single(await svc.GetLogsForMedicine(ongoing.Id, yesterday, yesterday.AddDays(1)));
        Assert.Null((await svc.GetMedicineById(ongoing.Id))!.EndDate);
        Assert.Empty(await svc.GetLogsForMedicine(future.Id, yesterday, yesterday.AddDays(1)));
    }

    [Fact]
    public async Task AutoLogMissedDoses_DoesNotLogDaysAfterTheCourseEnded()
    {
        var svc = CreateService();
        // 2-day course that ended 3 days ago
        var start = DateTimeHelper.ToDateString(DateTime.Today.AddDays(-4));
        var medicine = await AddDefault(svc, prescribedDays: 2, startDate: start, schedules: ["08:00"]);

        await svc.AutoLogMissedDoses();

        var logs = await svc.GetLogsForMedicine(medicine.Id, DateTime.Today.AddDays(-10), DateTime.Today);
        Assert.Equal(2, logs.Count);
        Assert.All(logs, l => Assert.True(DateTimeHelper.ParseDateTime(l.ScheduledFor).Date <= DateTime.Today.AddDays(-3)));
    }

    // ── UpdateMedicine keeps schedule ids and recomputes EndDate ─────────────
    [Fact]
    public async Task UpdateMedicine_KeepsUnchangedScheduleIdsSoLogsStayLinked()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, schedules: ["08:00", "20:00"]);
        var before = await svc.GetSchedulesForMedicine(medicine.Id);
        var morningId = before.Single(s => s.ScheduleTime == "08:00").Id;
        await svc.LogDoseTaken(medicine.Id, morningId);

        medicine.Notes = "edited";
        await svc.UpdateMedicine(medicine, ["08:00", "14:00"]);

        var after = await svc.GetSchedulesForMedicine(medicine.Id);
        Assert.Equal(2, after.Count);
        Assert.Equal(morningId, after.Single(s => s.ScheduleTime == "08:00").Id);
        Assert.DoesNotContain(after, s => s.ScheduleTime == "20:00");

        var todayLogs = await svc.GetLogsForMedicine(medicine.Id, DateTime.Today, DateTime.Today.AddDays(1));
        Assert.Contains(todayLogs, l => l.ScheduleId == morningId);
    }

    [Fact]
    public async Task UpdateMedicine_RecomputesEndDateWhenPrescribedDaysChange()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, prescribedDays: 7, startDate: "2024-01-01");
        Assert.Equal("2024-01-07", medicine.EndDate);

        medicine.PrescribedDays = 14;
        await svc.UpdateMedicine(medicine, ["08:00", "20:00"]);

        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.Equal("2024-01-14", stored!.EndDate);
        var course = Assert.Single(await svc.GetCoursesForMedicine(medicine.Id));
        Assert.Equal("2024-01-14", course.EndDate);
        Assert.Equal(28, course.TotalScheduled);
    }

    [Fact]
    public async Task DeleteMedicine_AlsoRemovesDoseLogs()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, schedules: ["08:00"]);
        var scheduleId = (await svc.GetSchedulesForMedicine(medicine.Id))[0].Id;
        await svc.LogDoseTaken(medicine.Id, scheduleId);

        await svc.DeleteMedicine(medicine.Id);

        Assert.Empty(await svc.GetLogsForMedicine(medicine.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1)));
    }

    // ── BackfillPastDoses ────────────────────────────────────────────────────
    [Fact]
    public async Task BackfillPastDoses_InsertsTakenLogsAndReducesStock()
    {
        var svc = CreateService();
        var start = DateTime.Today.AddDays(-3);
        var medicine = await AddDefault(svc, stock: 20, prescribedDays: 10,
            startDate: DateTimeHelper.ToDateString(start), schedules: ["08:00", "20:00"]);

        var count = await svc.BackfillPastDoses(medicine.Id, start, DateTime.Today.AddDays(-1));

        Assert.Equal(6, count);
        var stored = await svc.GetMedicineById(medicine.Id);
        Assert.Equal(14.0, stored!.StockCount);
        var course = Assert.Single(await svc.GetCoursesForMedicine(medicine.Id));
        Assert.Equal(6, course.TotalTaken);
    }

    [Fact]
    public async Task BackfillPastDoses_DoesNotDuplicateExistingLogs()
    {
        var svc = CreateService();
        var start = DateTime.Today.AddDays(-1);
        var medicine = await AddDefault(svc, stock: 20, prescribedDays: 10,
            startDate: DateTimeHelper.ToDateString(start), schedules: ["08:00"]);

        var first = await svc.BackfillPastDoses(medicine.Id, start, start);
        var second = await svc.BackfillPastDoses(medicine.Id, start, start);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    // ── GetAdherenceStats ────────────────────────────────────────────────────
    [Fact]
    public async Task GetAdherenceStats_WithNoLogs_ReturnsZeroStreakAndZeroMissed()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, schedules: ["08:00"]);

        var (streak, missedThisWeek) = await svc.GetAdherenceStats(medicine.Id);

        Assert.Equal(0, streak);
        Assert.Equal(0, missedThisWeek);
    }

    [Fact]
    public async Task GetAdherenceStats_WithTodayTaken_ReturnsStreak1()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, schedules: ["08:00"]);
        var scheduleId = (await svc.GetSchedulesForMedicine(medicine.Id))[0].Id;

        await svc.LogDoseTaken(medicine.Id, scheduleId);

        var (streak, missedThisWeek) = await svc.GetAdherenceStats(medicine.Id);
        Assert.Equal(1, streak);
        Assert.Equal(0, missedThisWeek);
    }

    [Fact]
    public async Task GetAdherenceStats_WithMissedLog_CountsMissedThisWeek()
    {
        var svc = CreateService(out var db);
        var medicine = await AddDefault(svc, schedules: ["08:00"]);
        var scheduleId = (await svc.GetSchedulesForMedicine(medicine.Id))[0].Id;

        var conn = await db.GetConnectionAsync();
        await conn.InsertAsync(new MedicineLog
        {
            MedicineId = medicine.Id,
            ScheduleId = scheduleId,
            ScheduledFor = DateTimeHelper.ToDateTimeString(DateTime.Today.AddDays(-1).AddHours(8)),
            TakenAt = DateTimeHelper.ToDateTimeString(DateTime.Today.AddDays(-1).AddHours(8)),
            WasTaken = 0,
            StockBefore = 10,
            StockAfter = 10
        });

        var (_, missedThisWeek) = await svc.GetAdherenceStats(medicine.Id);
        Assert.Equal(1, missedThisWeek);
    }

    [Fact]
    public async Task GetAdherenceStats_WithTwoDayStreak_ReturnsStreak2()
    {
        var svc = CreateService(out var db);
        var medicine = await AddDefault(svc, schedules: ["08:00"]);
        var scheduleId = (await svc.GetSchedulesForMedicine(medicine.Id))[0].Id;

        await svc.LogDoseTaken(medicine.Id, scheduleId);

        var conn = await db.GetConnectionAsync();
        await conn.InsertAsync(new MedicineLog
        {
            MedicineId = medicine.Id,
            ScheduleId = scheduleId,
            ScheduledFor = DateTimeHelper.ToDateTimeString(DateTime.Today.AddDays(-1).AddHours(8)),
            TakenAt = DateTimeHelper.ToDateTimeString(DateTime.Today.AddDays(-1).AddHours(8).AddMinutes(5)),
            WasTaken = 1,
            StockBefore = 9,
            StockAfter = 8
        });

        var (streak, _) = await svc.GetAdherenceStats(medicine.Id);
        Assert.Equal(2, streak);
    }

    [Fact]
    public async Task GetAdherenceStats_StreakRequiresAllDailyDoses()
    {
        var svc = CreateService();
        var medicine = await AddDefault(svc, schedules: ["08:00", "20:00"]);
        var scheduleId = (await svc.GetSchedulesForMedicine(medicine.Id))[0].Id;

        await svc.LogDoseTaken(medicine.Id, scheduleId);

        var (streak, _) = await svc.GetAdherenceStats(medicine.Id);
        Assert.Equal(0, streak);
    }
}
