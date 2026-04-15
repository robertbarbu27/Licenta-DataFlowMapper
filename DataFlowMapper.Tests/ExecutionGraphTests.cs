using System.Data;
using DataFlowMapper.Core.Models;
using DataFlowMapper.Executor;
using DataFlowMapper.Transforms;
using Xunit;

namespace DataFlowMapper.Tests;

public class ExecutionGraphTests
{
    // ── Subgraph partitioning ──────────────────────────────────────────────

    [Fact]
    public void TwoUnrelatedBranches_ProduceTwoSubgraphs()
    {
        var pipeline = new Pipeline
        {
            Sources = [
                new() { Id = "src-a", Type = "postgresql", ConnectionString = "", Table = "a" },
                new() { Id = "src-b", Type = "postgresql", ConnectionString = "", Table = "b" }
            ],
            Targets = [
                new() { Id = "tgt-a", ConnectorId = "src-a", Table = "out_a" },
                new() { Id = "tgt-b", ConnectorId = "src-b", Table = "out_b" }
            ],
            Transforms = []
        };

        var subgraphs = ExecutionGraph.Build(pipeline);

        Assert.Equal(2, subgraphs.Count);
    }

    [Fact]
    public void TwoTargetsSameSource_ProducesOneSubgraph()
    {
        // Both targets reference the same source → same component
        var pipeline = new Pipeline
        {
            Sources = [
                new() { Id = "src-a", Type = "postgresql", ConnectionString = "", Table = "a" }
            ],
            Targets = [
                new() { Id = "tgt-a", ConnectorId = "src-a", Table = "out_1" },
                new() { Id = "tgt-b", ConnectorId = "src-a", Table = "out_2" }
            ],
            Transforms = []
        };

        var subgraphs = ExecutionGraph.Build(pipeline);

        Assert.Single(subgraphs);
        Assert.Equal(2, subgraphs[0].Targets.Count);
    }

    [Fact]
    public void SingleSourceAndTarget_ProducesOneSubgraph()
    {
        var pipeline = new Pipeline
        {
            Sources = [new() { Id = "src-a", Type = "postgresql", ConnectionString = "", Table = "a" }],
            Targets = [new() { Id = "tgt-a", ConnectorId = "src-a", Table = "out_a" }],
            Transforms = []
        };

        var subgraphs = ExecutionGraph.Build(pipeline);

        Assert.Single(subgraphs);
        Assert.Single(subgraphs[0].Sources);
        Assert.Single(subgraphs[0].Targets);
    }

    // ── Transform stage ordering (Kahn's BFS) ─────────────────────────────

    [Fact]
    public void TransformsWithNoDeps_AllInStageZero()
    {
        var transforms = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim",   DependsOn = [] },
            new() { Id = "t2", Type = "rename", DependsOn = [] }
        };

        var stages = ExecutionGraph.BuildTransformStages(transforms);

        Assert.Single(stages);
        Assert.Equal(2, stages[0].Count);
    }

    [Fact]
    public void ChainedDeps_ProduceOrderedStages()
    {
        var transforms = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim",   DependsOn = [] },
            new() { Id = "t2", Type = "concat", DependsOn = ["t1"] },
            new() { Id = "t3", Type = "filter", DependsOn = ["t2"] }
        };

        var stages = ExecutionGraph.BuildTransformStages(transforms);

        Assert.Equal(3, stages.Count);
        Assert.Equal("t1", stages[0][0].Id);
        Assert.Equal("t2", stages[1][0].Id);
        Assert.Equal("t3", stages[2][0].Id);
    }

    [Fact]
    public void ParallelThenMerge_CorrectStages()
    {
        // t1 and t2 are independent, t3 depends on both
        var transforms = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim",   DependsOn = [] },
            new() { Id = "t2", Type = "rename", DependsOn = [] },
            new() { Id = "t3", Type = "concat", DependsOn = ["t1", "t2"] }
        };

        var stages = ExecutionGraph.BuildTransformStages(transforms);

        Assert.Equal(2, stages.Count);
        Assert.Equal(2, stages[0].Count); // t1 and t2 in parallel
        Assert.Equal("t3", stages[1][0].Id);
    }

    [Fact]
    public void CircularDeps_DoNotHangAndReturnEmpty()
    {
        var transforms = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim",   DependsOn = ["t2"] },
            new() { Id = "t2", Type = "rename", DependsOn = ["t1"] }
        };

        var stages = ExecutionGraph.BuildTransformStages(transforms);

        Assert.Empty(stages);
    }

    [Fact]
    public void EmptyTransforms_ReturnsEmptyStages()
    {
        var stages = ExecutionGraph.BuildTransformStages([]);

        Assert.Empty(stages);
    }

    // ── ApplyStage column merge (data integrity) ──────────────────────────

    [Fact]
    public void ApplyStage_SingleTransform_ReturnsDirectResult()
    {
        var factory = new TransformFactory();
        var input = MakeTable(("name", "  hello  "));

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim", Inputs = ["name"], Outputs = ["name"], DependsOn = [] }
        };

        var result = ExecutionGraph.ApplyStage(input, stage, factory);

        Assert.Equal("hello", (string)result.Rows[0]["name"]);
    }

    [Fact]
    public void ApplyStage_TwoTransformsDifferentColumns_BothChangesSurvive()
    {
        var factory = new TransformFactory();
        var input = MakeTable(("col_a", "  hello  "), ("col_b", "world"));

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim",   Inputs = ["col_a"], Outputs = ["col_a"], DependsOn = [] },
            new() { Id = "t2", Type = "rename", Inputs = ["col_b"], Outputs = ["col_b2"],
                    Params = new() { ["col_b"] = "col_b2" }, DependsOn = [] }
        };

        var result = ExecutionGraph.ApplyStage(input, stage, factory);

        // Trim applied to col_a
        Assert.Equal("hello", (string)result.Rows[0]["col_a"]);
        // Rename produced col_b2
        Assert.True(result.Columns.Contains("col_b2"));
    }

    [Fact]
    public void ApplyStage_RowCountPreserved()
    {
        var factory = new TransformFactory();
        var input = new DataTable();
        input.Columns.Add("name");
        for (var i = 0; i < 100; i++)
        {
            var row = input.NewRow();
            row["name"] = $"  value{i}  ";
            input.Rows.Add(row);
        }

        var stage = new List<TransformDefinition>
        {
            new() { Id = "t1", Type = "trim", Inputs = ["name"], Outputs = ["name"], DependsOn = [] }
        };

        var result = ExecutionGraph.ApplyStage(input, stage, factory);

        Assert.Equal(100, result.Rows.Count);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static DataTable MakeTable(params (string col, string val)[] columns)
    {
        var table = new DataTable();
        foreach (var (col, _) in columns)
            table.Columns.Add(col);

        var row = table.NewRow();
        foreach (var (col, val) in columns)
            row[col] = val;

        table.Rows.Add(row);
        return table;
    }
}
