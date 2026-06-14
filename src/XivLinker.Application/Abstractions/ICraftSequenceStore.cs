using XivLinker.Domain.Models;

namespace XivLinker.Application.Abstractions;

public interface ICraftSequenceStore
{
    IReadOnlyList<CraftSequence> GetAll();

    CraftSequence? Find(Guid sequenceId);

    void Save(CraftSequence sequence);

    void Delete(Guid sequenceId);
}
