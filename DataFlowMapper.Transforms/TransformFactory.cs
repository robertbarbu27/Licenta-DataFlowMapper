using DataFlowMapper.Core.Interfaces;

namespace DataFlowMapper.Transforms;

public class TransformFactory : ITransformFactory
{
    private readonly Dictionary<string, ITransform> _transforms;

    public TransformFactory()
    {
        _transforms = new Dictionary<string, ITransform>(StringComparer.OrdinalIgnoreCase)
        {
            ["concat"] = new ConcatTransform(),
            ["split"] = new SplitTransform(),
            ["rename"] = new RenameTransform(),
            ["mapvalues"] = new MapValuesTransform(),
            ["filter"] = new FilterTransform(),
            ["trim"] = new TrimTransform()
        };
    }

    public ITransform Get(string type)
    {
        if (_transforms.TryGetValue(type, out var transform))
            return transform;
        throw new NotSupportedException($"Transform type '{type}' is not supported.");
    }
}
