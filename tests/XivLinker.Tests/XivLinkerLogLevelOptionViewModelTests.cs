using XivLinker.App.ViewModels;
using XivLinker.Application.Logging;

namespace XivLinker.Tests;

public sealed class XivLinkerLogLevelOptionViewModelTests
{
    [Fact]
    public void LogLevelOption_ToString_ReturnsDisplayName()
    {
        XivLinkerLogLevelOptionViewModel option = new(
            XivLinkerLogLevel.Info,
            "INFO",
            "通常の動作確認に必要な主要ログを出力します。");

        Assert.Equal("INFO", option.ToString());
    }
}
