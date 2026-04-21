# Data Integrity Plan

## Problem

A migration pipeline can silently lose, corrupt, or partially write data at three points:

```
[Source DB] ──read──► [Transform Chain] ──write──► [Target DB]
     ↑                       ↑                          ↑
 schema drift           silent data loss           partial write
 bad query              wrong column merge          type mismatch
 missing table          null propagation            duplicate rows
```

None of these are currently caught — the pipeline either crashes mid-run or
silently produces wrong results.

---

## Layers

### Layer 1 — Pre-execution (schema validation)

Runs before any data moves. Checks that the pipeline is structurally sound.

**Checks:**
- Every column in `TransformDefinition.Inputs` exists in the source table schema
- Every `FieldMapping.From` column exists in the source schema
- Every target table exists (warn if it needs to be created)
- No circular `DependsOn` in the transform graph

**Output:** `List<ValidationError> { NodeId, Message }` — one entry per failing node.

**Integration:** `ExecutionController` calls `PipelineValidator.Validate()` before
`PipelineRunner.ExecuteAsync()`. If any errors exist, return HTTP 422 with the
error list. The frontend shows a red badge on each failing node in the graph.

```
POST /api/pipelines/{id}/execute
  → PipelineValidator.Validate(pipeline, connectorSchemas)
      → errors? return 422 { errors: [{ nodeId, message }] }
      → ok?     PipelineRunner.ExecuteAsync(...)
```

---

### Layer 2 — During execution (per-chunk invariants)

Runs inside `PipelineRunner` as each chunk flows through the pipeline.

**Row balance per chunk:**
```
rowsIn == rowsOut + rowsSkipped
```
- `rowsSkipped` = rows dropped by a `Filter` transform
- If the balance breaks, emit a `Warn` log with the delta and continue
- Accumulate into `ExecutionStats.RowsSkipped`

**Null guard before write:**
- Before calling `connector.WriteAsync`, check for null values in columns
  that are marked non-nullable in the target schema
- Emit a `Warn` per null violation with row index and column name

---

### Layer 3 — Post-execution (reconciliation)

Runs after `Task.WhenAll(branchTasks)` in `PipelineRunner`.

**Global row reconciliation:**
```
RowsRead == RowsWritten + RowsSkipped
```
If this does not hold → emit `Warn` with delta.
Indicates a bug in the merge logic, a connector drop, or an untracked filter.

**Target count verification:**
- Query `SELECT COUNT(*) FROM target_table` after writing
- Compare with `RowsWritten` for that target
- Delta > 0 → emit `Warn "Target row count mismatch: expected X, found Y"`
- Catches partial writes, connector buffering issues, or duplicate inserts

---

## New components

### `PipelineValidator` (DataFlowMapper.Executor)

```
PipelineValidator
  + Validate(pipeline, schemas) → List<ValidationError>
      - CheckTransformInputColumns
      - CheckMappingColumns
      - CheckTargetTablesExist
      - CheckCircularDependencies   ← reuses ExecutionGraph.BuildTransformStages
```

### `ValidationError` (DataFlowMapper.Core/Results)

```
ValidationError
  + NodeId:   string   (source/transform/target id)
  + NodeKind: string   ("source" | "transform" | "target")
  + Message:  string
```

### `ExecutionStats` additions

```
+ RowsSkipped:  long
+ IntegrityWarnings: List<string>
```

---

## Test cases

| Test | Layer | Catches |
|---|---|---|
| Transform references missing column → ValidationError | 1 | Schema drift |
| Mapping.From references missing column → ValidationError | 1 | Bad mapping |
| Circular DependsOn → ValidationError | 1 | Infinite loop |
| Filter "1=0" on 100 rows → RowsSkipped=100, RowsWritten=0 | 2 | Filter accounting |
| 100 rows in, 100 rows out (no filter) → balance holds | 2 | Silent drop |
| RowsRead != RowsWritten + RowsSkipped → Warn emitted | 3 | Untracked loss |
| Target COUNT(*) != RowsWritten → Warn emitted | 3 | Partial write |

---

## Implementation order

1. `ValidationError` model in `DataFlowMapper.Core/Results`
2. `PipelineValidator` in `DataFlowMapper.Executor`
3. Wire validator into `ExecutionController` (HTTP 422 on failure)
4. `RowsSkipped` tracking in `PipelineRunner` + per-chunk balance check
5. Post-execution reconciliation in `PipelineRunner`
6. Tests for all of the above in `DataFlowMapper.Tests`
