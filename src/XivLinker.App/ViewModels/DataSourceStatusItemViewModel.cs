namespace XivLinker.App.ViewModels;

public sealed class DataSourceStatusItemViewModel
{
    public DataSourceStatusItemViewModel(string name, string status, string detail)
    {
        Name = name;
        Status = status;
        Detail = detail;
    }

    public string Name { get; }

    public string Status { get; }

    public string Detail { get; }
}
