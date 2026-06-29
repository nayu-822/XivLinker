using System.Windows;

namespace XivLinker.App.Services;

public sealed class ConfirmationDialogService : IConfirmationDialogService
{
    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
