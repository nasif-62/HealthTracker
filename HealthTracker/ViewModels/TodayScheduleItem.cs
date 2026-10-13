namespace HealthTracker.ViewModels;

public class TodayScheduleItem : BaseViewModel
{
    public int MedicineId { get; set; }
    public int ScheduleId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string StockUnit { get; set; } = string.Empty;
    public string StockUnitIcon => StockUnit switch
    {
        "Tablet"    => "💊",
        "Capsule"   => "💊",
        "Drop"      => "💧",
        "Syrup"     => "🧴",
        "Injection" => "💉",
        "Spray"     => "🚿",
        "Inhaler"   => "🫁",
        _           => "📦"
    };
    public string ScheduleTime { get; set; } = string.Empty;
    public string ScheduleTimeDisplay => TimeSpan.TryParse(ScheduleTime, out var t)
        ? DateTime.Today.Add(t).ToString("h:mm tt")
        : ScheduleTime;

    private bool _isLogged;
    private bool _wasTaken;

    public bool IsLogged
    {
        get => _isLogged;
        set
        {
            _isLogged = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanActNow));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(TimeUntilText));
        }
    }

    public bool WasTaken
    {
        get => _wasTaken;
        set
        {
            _wasTaken = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(TimeUntilText));
        }
    }

    public bool CanActNow
    {
        get
        {
            if (IsLogged) return false;
            if (!TimeSpan.TryParse(ScheduleTime, out var scheduled)) return false;
            var now = DateTime.Now.TimeOfDay;
            return now >= scheduled.Subtract(TimeSpan.FromHours(1))
                && now <= scheduled.Add(TimeSpan.FromHours(2));
        }
    }

    public string StatusText
    {
        get
        {
            if (IsLogged) return WasTaken ? "Taken" : "Missed";
            if (!TimeSpan.TryParse(ScheduleTime, out var scheduled)) return "Upcoming";
            var now = DateTime.Now.TimeOfDay;
            if (now > scheduled.Add(TimeSpan.FromHours(2))) return "Missed";
            if (now >= scheduled.Subtract(TimeSpan.FromHours(1))) return "Take Now";
            return "Upcoming";
        }
    }

    public string StatusIcon
    {
        get
        {
            if (IsLogged) return WasTaken ? "\u2705" : "\u274c";
            if (!TimeSpan.TryParse(ScheduleTime, out var scheduled)) return "\U0001f550";
            var now = DateTime.Now.TimeOfDay;
            if (now > scheduled.Add(TimeSpan.FromHours(2))) return "\u274c";
            if (now >= scheduled.Subtract(TimeSpan.FromHours(1))) return StockUnitIcon;
            return "\U0001f550";
        }
    }

    public string StatusColor
    {
        get
        {
            if (IsLogged) return WasTaken ? "#1A8A4A" : "#C0392B";
            if (!TimeSpan.TryParse(ScheduleTime, out var scheduled)) return "#7F8C8D";
            var now = DateTime.Now.TimeOfDay;
            if (now > scheduled.Add(TimeSpan.FromHours(2))) return "#C0392B";
            if (now >= scheduled.Subtract(TimeSpan.FromHours(1))) return "#E67E22";
            return "#5A7A9A";
        }
    }

    public string TimeUntilText
    {
        get
        {
            if (IsLogged) return WasTaken ? "Taken" : "Missed";
            if (!TimeSpan.TryParse(ScheduleTime, out var scheduled)) return ScheduleTime;
            var diff = scheduled - DateTime.Now.TimeOfDay;
            var mins = diff.TotalMinutes;
            if (mins > 120) return $"in {(int)diff.TotalHours}h {diff.Minutes}m";
            if (mins > 60)  return $"in {(int)diff.TotalHours}h";
            if (mins > 5)   return $"in {(int)mins} min";
            if (mins >= -5) return "Take now!";
            if (mins >= -60)  return $"{(int)Math.Abs(mins)} min ago";
            if (mins >= -120) return $"{(int)Math.Abs(diff.TotalHours)}h ago";
            return $"Missed ({(int)Math.Abs(diff.TotalHours)}h ago)";
        }
    }

    public string TimeUntilLabel
    {
        get
        {
            var t = TimeUntilText;
            if (string.IsNullOrEmpty(t) || t == "Take now!") return string.Empty;
            return $"({t})";
        }
    }

    public void RefreshWindow()
    {
        OnPropertyChanged(nameof(CanActNow));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(TimeUntilText));
        OnPropertyChanged(nameof(TimeUntilLabel));
    }
}
