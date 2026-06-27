namespace XivLinker.App.ViewModels;

public sealed class AppEventLogItemViewModel
{
    public AppEventLogItemViewModel(string level, string message)
    {
        Level = level;
        Message = message;
        Timestamp = DateTime.Now;
    }

    public DateTime Timestamp
    {
        get;
    }

    public string TimestampText => Timestamp.ToString("HH:mm:ss");

    public string Level
    {
        get;
    }

    public string Message
    {
        get;
    }
}
