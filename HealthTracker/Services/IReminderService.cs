namespace HealthTracker.Services;

public interface IReminderService
{
    Task ScheduleReminders(int medicineId);
    Task CancelReminders(int medicineId);
    Task CancelDoseReminders(int medicineId, int scheduleId);
    Task RescheduleAll();
    Task CancelAll();
}
