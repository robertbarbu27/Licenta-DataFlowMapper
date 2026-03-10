using DataFlowMapper.Core.Models;

namespace DataFlowMapper.API.Services;

public class PipelineStore
{
    private readonly Dictionary<Guid, Pipeline> _store = new();

    public IEnumerable<Pipeline> GetAll() => _store.Values;

    public Pipeline? GetById(Guid id) => _store.TryGetValue(id, out var p) ? p : null;

    public Pipeline Add(Pipeline pipeline)
    {
        pipeline.Id = Guid.NewGuid();
        pipeline.CreatedAt = DateTime.UtcNow;
        _store[pipeline.Id] = pipeline;
        return pipeline;
    }

    public bool Remove(Guid id) => _store.Remove(id);
}
