# Execution Graph

```
                    PIPELINE EXECUTION DAG
                    ──────────────────────

 [Source A] ──────────────────────────────────────────► [Target A]
      │
      ▼
 [Trim]  [Rename]          ← Stage 0 (parallel, no deps)
      │       │
      └───┬───┘
          ▼
      [Concat]              ← Stage 1 (depends on both)
          │
          ▼
      [Filter]              ← Stage 2 (depends on Concat)
          │
          ▼
      [Target B]


 [Source C] ──────────────────────────────────────────► [Target C]


 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

 Component 1: Source A → Trim/Rename → Concat → Filter → Target B
 Component 2: Source A → Target A    (direct)
 Component 3: Source C → Target C    (direct)

 Task 1 ║ Task 2 ║ Task 3   ← all run via Task.WhenAll
```
