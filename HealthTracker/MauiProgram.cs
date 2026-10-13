using HealthTracker.Repositories;
using HealthTracker.Services;
using HealthTracker.ViewModels;
using HealthTracker.Views;
using Microcharts.Maui;
using Plugin.LocalNotification;
using Plugin.Maui.OCR;

namespace HealthTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLocalNotification()
            .UseMicrocharts()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var services = builder.Services;

        // Database
        services.AddSingleton<DatabaseService>();

        // Repositories
        services.AddSingleton<MedicineRepository>();
        services.AddSingleton<MedicineScheduleRepository>();
        services.AddSingleton<MedicineLogRepository>();
        services.AddSingleton<MedicineCourseRepository>();
        services.AddSingleton<HealthMetricRepository>();
        services.AddSingleton<MedicalReportRepository>();
        services.AddSingleton<DoctorVisitRepository>();
        services.AddSingleton<PendingMedicineRepository>();
        services.AddSingleton<ReportPageRepository>();

        // Services
        services.AddSingleton<IReminderService, ReminderService>();
        services.AddSingleton<MedicineService>();
        services.AddSingleton<MetricsService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<DoctorService>();
        services.AddSingleton<TimelineService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<IOcrService>(OcrPlugin.Default);
        services.AddSingleton<PrescriptionParserService>();
        services.AddSingleton<ReportTextParserService>();
        services.AddSingleton<AiParserService>();
        services.AddSingleton<PdfTextExtractorService>();
        services.AddSingleton<HealthReportService>();
        services.AddSingleton<AiChatService>();

        // ViewModels
        services.AddTransient<MedicineListViewModel>();
        services.AddTransient<AddMedicineViewModel>();
        services.AddTransient<MedicineDetailViewModel>();
        services.AddTransient<MetricsDashboardViewModel>();
        services.AddTransient<AddMetricViewModel>();
        services.AddTransient<MetricHistoryViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<AddReportViewModel>();
        services.AddTransient<DoctorVisitsViewModel>();
        services.AddTransient<AddDoctorVisitViewModel>();
        services.AddTransient<DoctorVisitDetailViewModel>();
        services.AddTransient<EditDoctorVisitViewModel>();
        services.AddTransient<EditMedicineViewModel>();
        services.AddTransient<TodayViewModel>();
        services.AddTransient<TimelineViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PinLockViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<ScanPrescriptionViewModel>();
        services.AddTransient<AskAiViewModel>();
        services.AddTransient<EditMetricViewModel>();

        // Views
        services.AddTransient<MedicineListPage>();
        services.AddTransient<AddMedicinePage>();
        services.AddTransient<MedicineDetailPage>();
        services.AddTransient<MetricsDashboardPage>();
        services.AddTransient<AddMetricPage>();
        services.AddTransient<MetricHistoryPage>();
        services.AddTransient<ReportsPage>();
        services.AddTransient<AddReportPage>();
        services.AddTransient<DoctorVisitsPage>();
        services.AddTransient<AddDoctorVisitPage>();
        services.AddTransient<DoctorVisitDetailPage>();
        services.AddTransient<EditDoctorVisitPage>();
        services.AddTransient<EditMedicinePage>();
        services.AddTransient<EditMetricPage>();
        services.AddTransient<TodayPage>();
        services.AddTransient<TimelinePage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<PinLockPage>();
        services.AddTransient<HistoryPage>();
        services.AddTransient<ScanPrescriptionPage>();
        services.AddTransient<AskAiPage>();

        return builder.Build();
    }
}
