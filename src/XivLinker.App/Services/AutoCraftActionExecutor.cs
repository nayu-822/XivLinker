using XivLinker.Domain.Models;

namespace XivLinker.App.Services;

public sealed class AutoCraftActionExecutor : IAutoCraftActionExecutor
{
    public async Task ExecuteAsync(
        CraftSequence sequence,
        int runCount,
        Action<string>? reportStatus,
        CancellationToken cancellationToken = default)
    {
        if (sequence.Steps.Count == 0)
        {
            reportStatus?.Invoke("手順が空のため実行できません。");
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            return;
        }

        reportStatus?.Invoke("テスト実行中: 実際のキー送信は未実装です。停止ボタンで終了できます。");

        for (int cycle = 0; cycle < runCount; cycle++)
        {
            for (int stepIndex = 0; stepIndex < sequence.Steps.Count; stepIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                reportStatus?.Invoke(
                    $"テスト実行中 {cycle + 1}/{runCount} - 手順 {stepIndex + 1}/{sequence.Steps.Count}");
                await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);
            }
        }
    }
}
