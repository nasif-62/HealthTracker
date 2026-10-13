using System.Collections.ObjectModel;
using System.Windows.Input;
using HealthTracker.Services;

namespace HealthTracker.ViewModels;

public class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public string BubbleColor => IsUser ? "#1B5FA8" : "#F0F4F8";
    public string TextColor => IsUser ? "#FFFFFF" : "#0D1B2A";
    public LayoutOptions Alignment => IsUser ? LayoutOptions.End : LayoutOptions.Start;
}

public class AskAiViewModel : BaseViewModel
{
    private readonly AiChatService _chatService;
    private string _inputText = string.Empty;

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public bool IsConfigured => AiParserService.IsConfigured;
    public bool IsNotConfigured => !IsConfigured;

    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); }
    }

    public ICommand SendCommand { get; }
    public ICommand ClearCommand { get; }

    // Conversation history for multi-turn context (role, text)
    private readonly List<(string Role, string Text)> _history = new();

    public AskAiViewModel(AiChatService chatService)
    {
        _chatService = chatService;
        SendCommand = new Command(async () => await Send(), () => !IsBusy);
        ClearCommand = new Command(Clear);

        if (IsConfigured)
            Messages.Add(new ChatMessage
            {
                Text = "Hi! Ask me anything about your medicines, reports, doctor visits, or health metrics.",
                IsUser = false
            });
        else
            Messages.Add(new ChatMessage
            {
                Text = "Gemini API key is not configured. Please go to Settings → AI Assistant and enter your API key.",
                IsUser = false
            });
    }

    private async Task Send()
    {
        var question = InputText.Trim();
        if (string.IsNullOrWhiteSpace(question)) return;
        if (!IsConfigured) return;

        Messages.Add(new ChatMessage { Text = question, IsUser = true });
        _history.Add(("user", question));
        InputText = string.Empty;

        IsBusy = true;
        ((Command)SendCommand).ChangeCanExecute();
        try
        {
            // Pass history minus the last user message (already included in AskAsync)
            var historyForApi = _history.Count > 1
                ? _history.Take(_history.Count - 1).ToList()
                : new List<(string, string)>();

            var reply = await _chatService.AskAsync(question, historyForApi);
            Messages.Add(new ChatMessage { Text = reply, IsUser = false });
            _history.Add(("model", reply));
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage { Text = "Something went wrong. Please try again.", IsUser = false });
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            ((Command)SendCommand).ChangeCanExecute();
        }
    }

    private void Clear()
    {
        _history.Clear();
        Messages.Clear();
        Messages.Add(new ChatMessage
        {
            Text = "Conversation cleared. Ask me anything about your health data.",
            IsUser = false
        });
    }
}
