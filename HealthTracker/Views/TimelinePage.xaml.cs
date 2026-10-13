using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class TimelinePage : ContentPage
{
    private readonly TimelineViewModel _vm;

    public TimelinePage(TimelineViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Load();
    }

    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        var choice = await DisplayActionSheet("History", "Cancel", null,
            "Medicine History", "Doctor Visits", "Reports");

        if (choice == null || choice == "Cancel") return;

        var category = choice switch
        {
            "Medicine History" => "Medicine",
            "Doctor Visits" => "Doctor",
            "Reports" => "Reports",
            _ => null
        };

        if (category == null) return;

        var page = App.Services.GetService<HistoryPage>()!;
        page.SetCategory(category);
        await Navigation.PushAsync(page);
    }
}
