namespace XivLinker.App.Services;

public interface IFolderPickerService
{
    string? PickFolder(string? initialDirectory = null);
}
