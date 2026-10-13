using HealthTracker.Helpers;
using HealthTracker.Models;
using HealthTracker.Repositories;
using Plugin.LocalNotification;

namespace HealthTracker.Services;

public class ReminderService : IReminderService
{
    // MedicineSchedule.Id is a single global AUTOINCREMENT, so it is already unique on its own.
    // Early reminders live in a separate, non-overlapping ID band.
    private const int EarlyIdOffset = 1_000_000;

    private readonly MedicineRepository _medicineRepo;
    private readonly MedicineScheduleRepository _scheduleRepo;

    public ReminderService(MedicineRepository medicineRepo, MedicineScheduleRepository scheduleRepo)
    {
        _medicineRepo = medicineRepo;
        _scheduleRepo = scheduleRepo;
    }

    public async Task ScheduleReminders(int medicineId)
    {
        var medicine = await _medicineRepo.GetByIdAsync(medicineId);
        if (medicine is null || medicine.IsActive == 0) return;

        var schedules = await _scheduleRepo.GetByMedicineIdAsync(medicineId);
        foreach (var schedule in schedules)
        {
            await ScheduleNotificationForSchedule(medicine, schedule);
        }
    }

    public async Task CancelReminders(int medicineId)
    {
        var schedules = await _scheduleRepo.GetByMedicineIdAsync(medicineId);
        foreach (var schedule in schedules)
        {
            LocalNotificationCenter.Current.Cancel(GetNotificationId(schedule.Id));
            LocalNotificationCenter.Current.Cancel(GetEarlyNotificationId(schedule.Id));
        }
    }

    public Task CancelAll()
    {
        LocalNotificationCenter.Current.CancelAll();
        return Task.CompletedTask;
    }

    public async Task CancelDoseReminders(int medicineId, int scheduleId)
    {
        LocalNotificationCenter.Current.Cancel(GetNotificationId(scheduleId));
        LocalNotificationCenter.Current.Cancel(GetEarlyNotificationId(scheduleId));

        var medicine = await _medicineRepo.GetByIdAsync(medicineId);
        if (medicine is null) return;
        var schedule = await _scheduleRepo.GetScheduleByIdAsync(scheduleId);
        if (schedule is null || !TimeSpan.TryParse(schedule.ScheduleTime, out var time)) return;

        var onTime = DateTime.Today.AddDays(1).Add(time);
        var earlyTime = onTime.AddMinutes(-15);
        var timeAmPm = DateTime.Today.Add(time).ToString("h:mm tt");

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = GetEarlyNotificationId(scheduleId),
            Title = $"Reminder: {medicine.Name} in 15 minutes",
            Description = $"Scheduled at {timeAmPm}",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = earlyTime,
                RepeatType = NotificationRepeat.Daily
            }
        });

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = GetNotificationId(scheduleId),
            Title = $"Time to take {medicine.Name}",
            Description = $"Scheduled at {timeAmPm}",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = onTime,
                RepeatType = NotificationRepeat.Daily
            }
        });
    }

    public async Task RescheduleAll()
    {
        LocalNotificationCenter.Current.CancelAll();
        var medicines = await _medicineRepo.GetAllActiveAsync();
        foreach (var medicine in medicines)
        {
            if (DateTimeHelper.TryParse(medicine.EndDate, out var end) && end.Date < DateTime.Today)
                continue;

            var schedules = await _scheduleRepo.GetByMedicineIdAsync(medicine.Id);
            foreach (var schedule in schedules)
            {
                await ScheduleNotificationForSchedule(medicine, schedule);
            }
        }
    }

    private static async Task ScheduleNotificationForSchedule(Medicine medicine, MedicineSchedule schedule)
    {
        if (!TimeSpan.TryParse(schedule.ScheduleTime, out var time)) return;

        var onTime = DateTime.Today.Add(time);
        if (onTime < DateTime.Now) onTime = onTime.AddDays(1);

        var earlyTime = onTime.AddMinutes(-15);
        if (earlyTime < DateTime.Now) earlyTime = earlyTime.AddDays(1);

        var timeAmPm = DateTime.Today.Add(time).ToString("h:mm tt");

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = GetEarlyNotificationId(schedule.Id),
            Title = $"Reminder: {medicine.Name} in 15 minutes",
            Description = $"Scheduled at {timeAmPm}",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = earlyTime,
                RepeatType = NotificationRepeat.Daily
            }
        });

        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = GetNotificationId(schedule.Id),
            Title = $"Time to take {medicine.Name}",
            Description = $"Scheduled at {timeAmPm}",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = onTime,
                RepeatType = NotificationRepeat.Daily
            }
        });
    }

    private static int GetNotificationId(int scheduleId) => scheduleId;

    private static int GetEarlyNotificationId(int scheduleId) => EarlyIdOffset + scheduleId;
}
