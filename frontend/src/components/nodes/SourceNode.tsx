import { Handle, Position, type NodeProps } from '@xyflow/react';
import type { SourceConfig } from '../../types/pipeline';

const DB_ICONS: Record<string, string> = {
  postgresql: '🐘',
  mysql: '🐬',
  mongodb: '🍃',
};

export interface SourceNodeData extends Record<string, unknown> {
  config: SourceConfig;
  label: string;
}

export function SourceNode({ data, selected }: NodeProps) {
  const d = data as SourceNodeData;
  const icon = DB_ICONS[d.config?.type?.toLowerCase() ?? ''] ?? '🗄️';

  return (
    <div
      style={{
        background: selected ? '#1e3a5f' : '#1a2f4e',
        border: `2px solid ${selected ? '#3b82f6' : '#2563eb'}`,
        borderRadius: 10,
        padding: '10px 16px',
        minWidth: 160,
        color: '#e2e8f0',
        fontFamily: 'monospace',
        fontSize: 13,
        boxShadow: selected ? '0 0 0 3px rgba(59,130,246,0.3)' : '0 2px 8px rgba(0,0,0,0.4)',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
        <span style={{ fontSize: 18 }}>{icon}</span>
        <span style={{ fontWeight: 700, color: '#93c5fd', fontSize: 11, textTransform: 'uppercase', letterSpacing: 1 }}>
          Source
        </span>
      </div>
      <div style={{ fontWeight: 600, fontSize: 14, marginBottom: 2 }}>{d.label}</div>
      {d.config?.table && (
        <div style={{ fontSize: 11, color: '#94a3b8' }}>table: {d.config.table}</div>
      )}
      <Handle
        type="source"
        position={Position.Right}
        style={{ background: '#3b82f6', width: 10, height: 10 }}
      />
    </div>
  );
}
