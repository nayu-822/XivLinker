using XivLinker.Domain.Models;

namespace XivLinker.App.Services;

public interface IAutoCraftSequenceEditorDialogService
{
    Task ShowEditorAsync(CraftSequence? sequence, Action<CraftSequence> save);
}
