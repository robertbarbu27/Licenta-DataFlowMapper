import { useEffect, useRef, useState } from 'react';
import { cancelExecution, executePipeline } from '../../services/api';
import { joinExecution, leaveExecution, offLog, offProgress, onLog, onProgress } from '../../services/signalr';
import type { ExecutionStats, LogMessage } from '../../types/pipeline';

interface Props {
  pipelineId: string;
  onClose: () => void;
}

const LOG_COLORS: Record<string, string> = {
  Info: '#94a3b8',
  Ok: '#22c55e',
  Warn: '#f59e0b',
  Error: '#ef4444',
};

export function ExecutionLogPanel({ pipelineId, onClose }: Props) {
  const [executionId, setExecutionId] = useState<string | null>(null);
  const [logs, setLogs] = useState<LogMessage[]>([]);
  const [stats, setStats] = useState<ExecutionStats | null>(null);
  const [running, setRunning] = useState(false);
  const logEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [logs]);

  useEffect(() => {
    const logHandler = (msg: LogMessage) => setLogs((prev) => [...prev, msg]);
    const progressHandler = (s: ExecutionStats) => {
      setStats(s);
      if (s.progressPercent >= 100) setRunning(false);
    };
    onLog(logHandler);
    onProgress(progressHandler);
    return () => {
      offLog(logHandler);
      offProgress(progressHandler);
    };
  }, []);

  async function handleRun() {
    setLogs([]);
    setStats(null);
    setRunning(true);
    try {
      const { executionId: eid } = await executePipeline(pipelineId);
      setExecutionId(eid);
      await joinExecution(eid);
    } catch (e) {
      setLogs([{ level: 'Error', message: String(e), timestamp: new Date().toISOString(), meta: {} }]);
      setRunning(false);
    }
  }

  async function handleCancel() {
    if (executionId) await leaveExecution(executionId);
    await cancelExecution(pipelineId);
    setRunning(false);
  }

  return (
    <div style={{
      position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.7)',
      display: 'flex', alignItems: 'flex-end', justifyContent: 'center', zIndex: 100,
    }}
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div style={{
        background: '#0f172a', border: '1px solid #1e293b', borderTopLeftRadius: 12, borderTopRightRadius: 12,
        width: '100%', maxWidth: 900, height: '55vh', display: 'flex', flexDirection: 'column',
      }}>
        {/* Header */}
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '12px 16px', borderBottom: '1px solid #1e293b',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ color: '#e2e8f0', fontWeight: 700 }}>Executie Pipeline</span>
            {running && (
              <span style={{ fontSize: 11, background: '#16a34a22', color: '#22c55e', padding: '2px 8px', borderRadius: 999 }}>
                ● Running
              </span>
            )}
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            {!running ? (
              <button
                onClick={handleRun}
                style={{ padding: '6px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6, fontWeight: 600, cursor: 'pointer' }}
              >
                ▶ Ruleaza
              </button>
            ) : (
              <button
                onClick={handleCancel}
                style={{ padding: '6px 16px', background: '#dc2626', color: '#fff', border: 'none', borderRadius: 6, fontWeight: 600, cursor: 'pointer' }}
              >
                ■ Stop
              </button>
            )}
            <button onClick={onClose} style={{ background: 'none', border: 'none', color: '#64748b', cursor: 'pointer', fontSize: 20 }}>×</button>
          </div>
        </div>

        {/* Progress */}
        {stats && (
          <div style={{ padding: '8px 16px', borderBottom: '1px solid #1e293b' }}>
            <div style={{ display: 'flex', gap: 20, fontSize: 12, color: '#94a3b8', marginBottom: 6 }}>
              <span>Citite: <strong style={{ color: '#e2e8f0' }}>{stats.rowsRead}</strong></span>
              <span>Scrise: <strong style={{ color: '#22c55e' }}>{stats.rowsWritten}</strong></span>
              <span>Sarite: <strong style={{ color: '#f59e0b' }}>{stats.rowsSkipped}</strong></span>
              <span>Chunks: <strong style={{ color: '#e2e8f0' }}>{stats.chunksDone}/{stats.chunksTotal}</strong></span>
              <span>Timp: <strong style={{ color: '#e2e8f0' }}>{(stats.elapsedMs / 1000).toFixed(1)}s</strong></span>
            </div>
            <div style={{ background: '#1e293b', borderRadius: 999, height: 6, overflow: 'hidden' }}>
              <div style={{
                background: '#2563eb', height: '100%',
                width: `${stats.progressPercent}%`, transition: 'width 0.3s',
              }} />
            </div>
          </div>
        )}

        {/* Logs */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '10px 16px', fontFamily: 'monospace', fontSize: 12 }}>
          {logs.length === 0 && (
            <div style={{ color: '#475569', textAlign: 'center', marginTop: 40 }}>
              Apasa Ruleaza pentru a porni executia...
            </div>
          )}
          {logs.map((log, i) => (
            <div key={i} style={{ display: 'flex', gap: 10, marginBottom: 3, alignItems: 'flex-start' }}>
              <span style={{ color: '#475569', flexShrink: 0 }}>
                {new Date(log.timestamp).toLocaleTimeString()}
              </span>
              <span style={{ color: LOG_COLORS[log.level] ?? '#94a3b8', flexShrink: 0, fontWeight: 700, width: 40 }}>
                {log.level.toUpperCase()}
              </span>
              <span style={{ color: '#cbd5e1' }}>{log.message}</span>
            </div>
          ))}
          <div ref={logEndRef} />
        </div>
      </div>
    </div>
  );
}
