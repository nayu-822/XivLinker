using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XivLinker.App.ViewModels;

public partial class AutoCraftRunOptionsViewModel : ObservableObject
{
    private const int MinimumRunCount = 1;
    private const int MaximumRunCount = 999;
    private readonly Action<int> confirm;
    private readonly Action cancel;

    [ObservableProperty]
    private string runCountText = "1";

    public AutoCraftRunOptionsViewModel(
        string sequenceName,
        Action<int> confirm,
        Action cancel)
    {
        SequenceName = sequenceName;
        this.confirm = confirm;
        this.cancel = cancel;
        ConfirmCommand = new RelayCommand(Confirm, CanConfirm);
        CancelCommand = new RelayCommand(Cancel);
    }

    public string SequenceName { get; }

    public string CountHelpText => $"1 から {MaximumRunCount} 回まで指定できます。";

    public string ValidationMessage
    {
        get
        {
            return TryGetRunCount(out _)
                ? string.Empty
                : $"回数は {MinimumRunCount} から {MaximumRunCount} の範囲で入力してください。";
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public IRelayCommand ConfirmCommand { get; }

    public IRelayCommand CancelCommand { get; }

    partial void OnRunCountTextChanged(string value)
    {
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationMessage));
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    private bool CanConfirm()
    {
        return TryGetRunCount(out _);
    }

    private void Confirm()
    {
        if (!TryGetRunCount(out int runCount))
        {
            return;
        }

        confirm(runCount);
    }

    private void Cancel()
    {
        cancel();
    }

    private bool TryGetRunCount(out int runCount)
    {
        if (!int.TryParse(RunCountText, out runCount))
        {
            return false;
        }

        return runCount is >= MinimumRunCount and <= MaximumRunCount;
    }
}
