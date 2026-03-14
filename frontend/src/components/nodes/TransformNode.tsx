import { Handle, Position, type NodeProps } from '@xyflow/react';
import type { TransformDefinition } from '../../types/pipeline';

const TRANSFORM_ICONS: Record<string, string> = {
  concat: '🔗',
  split: '✂️',
  rename: '✏️',
  mapvalues: '🗺️',
  filter: '🔍',
  trim: '🧹',
};

export interface TransformNodeData extends Record<string, unknown> {
  config: TransformDefinition;
  label: string;
}

export function TransformNode({ data, selected }: NodeProps) {
  const d = data as TransformNodeData;
  const type = d.config?.type?.toLowerCase() ?? '';
  const icon = TRANSFORM_ICONS[type] ?? '⚙️';

  return (
    <div
      style={{
        background: selected ? '#2d1f47' : '#231840',
        border: `2px solid ${selected ? '#a855f7' : '#7c3aed'}`,
        borderRadius: 10,
        padding: '10px 16px',
        minWidth: 150,
        color: '#e2e8f0',
        fontFamily: 'monospace',
        fontSize: 13,
        boxShadow: selected ? '0 0 0 3px rgba(168,85,247,0.3)' : '0 2px 8px rgba(0,0,0,0.4)',
      }}
    >
      <Handle
        type="target"
        position={Position.Left}
        style={{ background: '#a855f7', width: 10, height: 10 }}
      />
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
        <span style={{ fontSize: 18 }}>{icon}</span>
        <span style={{ fontWeight: 700, color: '#c4b5fd', fontSize: 11, textTransform: 'uppercase', letterSpacing: 1 }}>
          Transform
        </span>
      </div>
      <div style={{ fontWeight: 600, fontSize: 14, marginBottom: 2 }}>{d.label}</div>
      <div style={{ fontSize: 11, color: '#94a3b8' }}>{d.config?.type}</div>
      <Handle
        type="source"
        position={Position.Right}
        style={{ background: '#a855f7', width: 10, height: 10 }}
      />
    </div>
  );
}
