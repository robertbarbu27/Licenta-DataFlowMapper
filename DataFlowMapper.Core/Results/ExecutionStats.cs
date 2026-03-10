namespace DataFlowMapper.Core.Results;

public class ExecutionStats
{
    public long RowsRead { get; set; }
    public long RowsWritten { get; set; }
    public long RowsSkipped { get; set; }
    public int ChunksTotal { get; set; }
    public int ChunksDone { get; set; }
    public long ElapsedMs { get; set; }
    public double ProgressPercent => ChunksTotal == 0 ? 0 : (double)ChunksDone / ChunksTotal * 100;
}
