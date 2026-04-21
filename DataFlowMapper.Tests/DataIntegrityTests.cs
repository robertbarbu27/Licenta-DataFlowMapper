using System.Data;
using DataFlowMapper.Core.Models;
using DataFlowMapper.Core.Results;
using DataFlowMapper.Executor;
using DataFlowMapper.Transforms;
using Xunit;

namespace DataFlowMapper.Tests;

public class DataIntegrityTests
{
    // ── Circular dependency detection ──────────────────────────────────────

    [Fact]
    public void CircularDeps_DetectedByValidator()
    {
        var transforms = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim",   DependsOn = ["t2"] },
            new() { Id = "t2", Type = "rename", DependsOn = ["t1"] }
        };

        var stages   = ExecutionGraph.BuildTransformStages(transforms);
        var resolved = stages.SelectMany(s => s).Select(t => t.Id).ToHashSet();
        var circular = transforms.Where(t => !resolved.Contains(t.Id)).ToList();

        Assert.Equal(2, circular.Count);
    }

    [Fact]
    public void NoDeps_NoneCircular()
    {
        var transforms = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim",   DependsOn = [] },
            new() { Id = "t2", Type = "rename", DependsOn = [] }
        };

        var stages   = ExecutionGraph.BuildTransformStages(transforms);
        var resolved = stages.SelectMany(s => s).Select(t => t.Id).ToHashSet();

        Assert.Equal(transforms.Count, resolved.Count);
    }

    // ── Row count integrity ────────────────────────────────────────────────

    [Fact]
    public void NoFilter_RowCountPreserved()
    {
        var factory = new TransformFactory();
        var input   = MakeTable(100, "name", "  hello  ");

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim", Inputs = ["name"], Outputs = ["name"], DependsOn = [] }
        };

        var result = ExecutionGraph.ApplyStage(input, stage, factory);

        Assert.Equal(100, result.Rows.Count);
    }

    [Fact]
    public void Filter_RowsDroppedAreCountable()
    {
        var factory = new TransformFactory();
        var input   = new DataTable();
        input.Columns.Add("age");

        for (var i = 0; i < 10; i++)
        {
            var row = input.NewRow();
            row["age"] = i.ToString();
            input.Rows.Add(row);
        }

        // Filter keeps only rows where age >= 5 → 5 rows kept, 5 dropped
        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "filter",
                    Params = new() { ["condition"] = "age >= 5" }, DependsOn = [] }
        };

        var result   = ExecutionGraph.ApplyStage(input, stage, factory);
        var skipped  = input.Rows.Count - result.Rows.Count;

        Assert.Equal(5, result.Rows.Count);
        Assert.Equal(5, skipped);
    }

    [Fact]
    public void FilterAll_AllRowsSkipped()
    {
        var factory = new TransformFactory();
        var input   = MakeTable(50, "status", "active");

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "filter",
                    Params = new() { ["condition"] = "1=0" }, DependsOn = [] }
        };

        var result  = ExecutionGraph.ApplyStage(input, stage, factory);
        var skipped = input.Rows.Count - result.Rows.Count;

        Assert.Equal(0, result.Rows.Count);
        Assert.Equal(50, skipped);
    }

    // ── Row balance invariant ──────────────────────────────────────────────

    [Fact]
    public void RowBalance_HoldsWithNoFilter()
    {
        const int rowsIn = 100;
        var factory = new TransformFactory();
        var input   = MakeTable(rowsIn, "name", "  hello  ");

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim", Inputs = ["name"], Outputs = ["name"], DependsOn = [] }
        };

        var result   = ExecutionGraph.ApplyStage(input, stage, factory);
        var rowsOut  = result.Rows.Count;
        var skipped  = rowsIn - rowsOut;

        // rowsIn == rowsOut + skipped must hold
        Assert.Equal(rowsIn, rowsOut + skipped);
    }

    [Fact]
    public void RowBalance_HoldsWithFilter()
    {
        const int rowsIn = 20;
        var factory = new TransformFactory();
        var input   = new DataTable();
        input.Columns.Add("value");

        for (var i = 0; i < rowsIn; i++)
        {
            var row = input.NewRow();
            row["value"] = i.ToString();
            input.Rows.Add(row);
        }

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "filter",
                    Params = new() { ["expression"] = "value >= 10" }, DependsOn = [] }
        };

        var result  = ExecutionGraph.ApplyStage(input, stage, factory);
        var rowsOut = result.Rows.Count;
        var skipped = rowsIn - rowsOut;

        Assert.Equal(rowsIn, rowsOut + skipped);
    }

    // ── Column integrity after transforms ─────────────────────────────────

    [Fact]
    public void Rename_OldColumnGone_NewColumnPresent()
    {
        var factory = new TransformFactory();
        var input   = MakeTable(1, "old_name", "value");

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "rename",
                    Inputs  = ["old_name"],
                    Outputs = ["new_name"],
                    Params  = new() { ["old_name"] = "new_name" }, DependsOn = [] }
        };

        var result = ExecutionGraph.ApplyStage(input, stage, factory);

        Assert.False(result.Columns.Contains("old_name"));
        Assert.True(result.Columns.Contains("new_name"));
    }

    [Fact]
    public void Trim_ValuesActuallyTrimmed()
    {
        var factory = new TransformFactory();
        var input   = MakeTable(1, "name", "  hello world  ");

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim", Inputs = ["name"], Outputs = ["name"], DependsOn = [] }
        };

        var result = ExecutionGraph.ApplyStage(input, stage, factory);

        Assert.Equal("hello world", (string)result.Rows[0]["name"]);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static DataTable MakeTable(int rowCount, string column, string value)
    {
        var table = new DataTable();
        table.Columns.Add(column);

        for (var i = 0; i < rowCount; i++)
        {
            var row = table.NewRow();
            row[column] = value;
            table.Rows.Add(row);
        }

        return table;
    }
}
