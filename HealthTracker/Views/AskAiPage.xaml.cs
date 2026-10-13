using HealthTracker.ViewModels;

namespace HealthTracker.Views;

public partial class AskAiPage : ContentPage
{
    private readonly AskAiViewModel _vm;

    public AskAiPage(AskAiViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Scroll to last message when page appears
        if (_vm.Messages.Count > 0)
            MessagesView.ScrollTo(_vm.Messages[^1], ScrollToPosition.End, animate: false);
    }
}
