using System.Collections.ObjectModel;

namespace XivLinker.App.ViewModels;

public sealed class CraftActionPaletteCategoryViewModel
{
    public CraftActionPaletteCategoryViewModel(
        string categoryName,
        IEnumerable<CraftActionPaletteItemViewModel> actions)
    {
        CategoryName = categoryName;
        Actions = new ObservableCollection<CraftActionPaletteItemViewModel>(actions);
    }

    public string CategoryName
    {
        get;
    }

    public ObservableCollection<CraftActionPaletteItemViewModel> Actions
    {
        get;
    }
}
