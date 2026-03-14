import { Handle, Position, type NodeProps } from '@xyflow/react';
import type { TargetConfig } from '../../types/pipeline';

export interface TargetNodeData extends Record<string, unknown> {
  config: TargetConfig;
  label: string;
}

export function TargetNode({ data, selected }: NodeProps) {
  const d = data as TargetNodeData;

  const modeColor: Record<string, string> = {
    Insert: '#22c55e',
    Upsert: '#f59e0b',
    Overwrite: '#ef4444',
  };
  const color = modeColor[d.config?.mode] ?? '#22c55e';

  return (
    <div
      style={{
        background: selected ? '#1a3325' : '#152a1e',
        border: `2px solid ${selected ? '#22c55e' : '#16a34a'}`,
        borderRadius: 10,
        padding: '10px 16px',
        minWidth: 160,
        color: '#e2e8f0',
        fontFamily: 'monospace',
        fontSize: 13,
        boxShadow: selected ? '0 0 0 3px rgba(34,197,94,0.3)' : '0 2px 8px rgba(0,0,0,0.4)',
      }}
    >
      <Handle
        type="target"
        position={Position.Left}
        style={{ background: '#22c55e', width: 10, height: 10 }}
      />
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
        <span style={{ fontSize: 18 }}>🎯</span>
        <span style={{ fontWeight: 700, color: '#86efac', fontSize: 11, textTransform: 'uppercase', letterSpacing: 1 }}>
          Target
        </span>
      </div>
      <div style={{ fontWeight: 600, fontSize: 14, marginBottom: 2 }}>{d.label}</div>
      {d.config?.table && (
        <div style={{ fontSize: 11, color: '#94a3b8' }}>table: {d.config.table}</div>
      )}
      {d.config?.mode && (
        <div style={{ fontSize: 11, color, marginTop: 2 }}>mode: {d.config.mode}</div>
      )}
    </div>
  );
}
