using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Models;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class TimelineViewModel : BaseViewModel
{
    private readonly TimelineService _service;
    private int _offset = 0;
    private const int PageSize = 20;
    private bool _hasMore = true;

    public ObservableCollection<TimelineEntry> Entries { get; } = new();
    public ICommand LoadMoreCommand { get; }

    public bool HasMore
    {
        get => _hasMore;
        private set { _hasMore = value; OnPropertyChanged(); }
    }

    public TimelineViewModel(TimelineService service)
    {
        _service = service;
        LoadMoreCommand = new Command(async () => await LoadMore());
    }

    public async Task Load()
    {
        if (IsBusy) return;
        _offset = 0;
        IsBusy = true; ClearError();
        try
        {
            var list = await _service.GetTimeline(PageSize, 0);
            Entries.Clear();
            string? lastGroup = null;
            foreach (var e in list)
            {
                if (e.MonthYearGroup == lastGroup) e.MonthYearGroup = null;
                else lastGroup = e.MonthYearGroup;
                Entries.Add(e);
            }
            _offset = list.Count;
            HasMore = list.Count == PageSize;
        }
        catch (Exception ex) { SetError("Failed to load timeline."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }

    private async Task LoadMore()
    {
        if (IsBusy) return;
        IsBusy = true; ClearError();
        try
        {
            var list = await _service.GetTimeline(PageSize, _offset);
            // track last group from existing entries to avoid duplicate headers across pages
            string? lastGroup = Entries.LastOrDefault(e => e.MonthYearGroup != null)?.MonthYearGroup;
            foreach (var e in list)
            {
                if (e.MonthYearGroup == lastGroup) e.MonthYearGroup = null;
                else lastGroup = e.MonthYearGroup;
                Entries.Add(e);
            }
            _offset += list.Count;
            HasMore = list.Count == PageSize;
        }
        catch (Exception ex) { SetError("Failed to load more."); System.Diagnostics.Debug.WriteLine(ex); }
        finally { IsBusy = false; }
    }
}
