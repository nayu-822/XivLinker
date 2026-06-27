using XivLinker.Domain.Models;

namespace XivLinker.App.Services;

public interface IAutoCraftActionExecutor
{
    Task ExecuteAsync(
        CraftSequence sequence,
        int runCount,
        Action<string>? reportStatus,
        CancellationToken cancellationToken = default);
}
