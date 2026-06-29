namespace XivLinker.App.Services;

public interface IConfirmationDialogService
{
    bool Confirm(string title, string message);
}
