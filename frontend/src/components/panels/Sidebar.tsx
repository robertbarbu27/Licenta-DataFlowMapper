import React from 'react';

interface NodeType {
  type: string;
  label: string;
  icon: string;
  color: string;
  subtype?: string;
}

const SOURCE_NODES: NodeType[] = [
  { type: 'source', label: 'PostgreSQL', icon: '🐘', color: '#2563eb', subtype: 'postgresql' },
  { type: 'source', label: 'MySQL', icon: '🐬', color: '#2563eb', subtype: 'mysql' },
  { type: 'source', label: 'MongoDB', icon: '🍃', color: '#2563eb', subtype: 'mongodb' },
];

const TRANSFORM_NODES: NodeType[] = [
  { type: 'transform', label: 'Concat', icon: '🔗', color: '#7c3aed', subtype: 'concat' },
  { type: 'transform', label: 'Split', icon: '✂️', color: '#7c3aed', subtype: 'split' },
  { type: 'transform', label: 'Rename', icon: '✏️', color: '#7c3aed', subtype: 'rename' },
  { type: 'transform', label: 'Map Values', icon: '🗺️', color: '#7c3aed', subtype: 'mapvalues' },
  { type: 'transform', label: 'Filter', icon: '🔍', color: '#7c3aed', subtype: 'filter' },
  { type: 'transform', label: 'Trim', icon: '🧹', color: '#7c3aed', subtype: 'trim' },
];

const TARGET_NODES: NodeType[] = [
  { type: 'target', label: 'PostgreSQL', icon: '🐘', color: '#16a34a', subtype: 'postgresql' },
  { type: 'target', label: 'MySQL', icon: '🐬', color: '#16a34a', subtype: 'mysql' },
  { type: 'target', label: 'MongoDB', icon: '🍃', color: '#16a34a', subtype: 'mongodb' },
];

interface SidebarProps {
  onDragStart?: (event: React.DragEvent, type: string, subtype: string) => void;
}

function NodeItem({ node, onDragStart }: { node: NodeType; onDragStart?: SidebarProps['onDragStart'] }) {
  return (
    <div
      draggable
      onDragStart={(e) => onDragStart?.(e, node.type, node.subtype ?? node.type)}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '7px 10px',
        borderRadius: 7,
        cursor: 'grab',
        border: `1px solid ${node.color}44`,
        background: `${node.color}11`,
        color: '#e2e8f0',
        fontSize: 13,
        userSelect: 'none',
        transition: 'background 0.15s',
      }}
      onMouseEnter={(e) => {
        (e.currentTarget as HTMLDivElement).style.background = `${node.color}22`;
      }}
      onMouseLeave={(e) => {
        (e.currentTarget as HTMLDivElement).style.background = `${node.color}11`;
      }}
    >
      <span style={{ fontSize: 16 }}>{node.icon}</span>
      <span>{node.label}</span>
    </div>
  );
}

function Section({ title, color, nodes, onDragStart }: {
  title: string;
  color: string;
  nodes: NodeType[];
  onDragStart?: SidebarProps['onDragStart'];
}) {
  return (
    <div style={{ marginBottom: 20 }}>
      <div style={{ color, fontWeight: 700, fontSize: 11, textTransform: 'uppercase', letterSpacing: 1, marginBottom: 8 }}>
        {title}
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
        {nodes.map((n) => (
          <NodeItem key={`${n.type}-${n.subtype}`} node={n} onDragStart={onDragStart} />
        ))}
      </div>
    </div>
  );
}

export function Sidebar({ onDragStart }: SidebarProps) {
  return (
    <div
      style={{
        width: 200,
        background: '#0f172a',
        borderRight: '1px solid #1e293b',
        padding: '16px 12px',
        overflowY: 'auto',
        flexShrink: 0,
      }}
    >
      <div style={{ color: '#64748b', fontSize: 11, marginBottom: 16, lineHeight: 1.5 }}>
        Trage noduri pe canvas pentru a construi pipeline-ul
      </div>
      <Section title="Surse" color="#3b82f6" nodes={SOURCE_NODES} onDragStart={onDragStart} />
      <Section title="Transformari" color="#a855f7" nodes={TRANSFORM_NODES} onDragStart={onDragStart} />
      <Section title="Targeturi" color="#22c55e" nodes={TARGET_NODES} onDragStart={onDragStart} />
    </div>
  );
}
