using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace HealthTracker;

[BroadcastReceiver(Name = "com.healthapp.healthtracker.HealthWidget", Label = "Health Tracker Widget", Exported = true)]
[IntentFilter(new[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_info")]
public class HealthWidget : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null) return;

        // GoAsync keeps the BroadcastReceiver alive while we do background work
        var pending = GoAsync();
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                foreach (var id in appWidgetIds)
                    UpdateWidget(context, appWidgetManager, id);
            }
            finally
            {
                pending.Finish();
            }
        });
    }

    public static void UpdateAllWidgets(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context);
        if (manager == null) return;
        var ids = manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(HealthWidget))));
        if (ids == null || ids.Length == 0) return;

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            foreach (var id in ids)
                UpdateWidget(context, manager, id);
        });
    }

    public static void UpdateWidget(Context context, AppWidgetManager manager, int widgetId)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_layout);

        // Tap to open app
        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        if (launchIntent != null)
        {
            var pendingIntent = PendingIntent.GetActivity(context, 0, launchIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            views.SetOnClickPendingIntent(Resource.Id.widget_root, pendingIntent);
        }

        views.SetTextViewText(Resource.Id.widget_date, DateTime.Today.ToString("MMM d"));

        try
        {
            var data = WidgetDataHelper.GetWidgetData(context);
            views.SetTextViewText(Resource.Id.widget_progress, data.ProgressText);
            views.SetTextViewText(Resource.Id.widget_next_dose, data.NextDoseText);
            views.SetTextViewText(Resource.Id.widget_heart_rate, data.HeartRateText);
            views.SetTextViewText(Resource.Id.widget_blood_pressure, data.BloodPressureText);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("HealthWidget", $"UpdateWidget error: {ex.Message}");
            views.SetTextViewText(Resource.Id.widget_progress, "Tap to open app");
            views.SetTextViewText(Resource.Id.widget_next_dose, "");
        }

        manager.UpdateAppWidget(widgetId, views);
    }
}
