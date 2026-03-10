namespace DataFlowMapper.Core.Interfaces;

public interface ITransformFactory
{
    ITransform Get(string type);
}
