import {
  addEdge,
  Background,
  BackgroundVariant,
  Controls,
  MiniMap,
  ReactFlow,
  useEdgesState,
  useNodesState,
  type Connection,
  type Edge,
  type Node,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import * as yaml from 'js-yaml';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { SourceNode } from '../components/nodes/SourceNode';
import { TargetNode } from '../components/nodes/TargetNode';
import { TransformNode } from '../components/nodes/TransformNode';
import { ConnectorConfigPanel } from '../components/panels/ConnectorConfigPanel';
import { ExecutionLogPanel } from '../components/panels/ExecutionLogPanel';
import { Sidebar } from '../components/panels/Sidebar';
import { getPipeline, updatePipeline } from '../services/api';
import type { Pipeline, SourceConfig, TargetConfig, TransformDefinition } from '../types/pipeline';

const NODE_TYPES = {
  source: SourceNode,
  transform: TransformNode,
  target: TargetNode,
};

type ConfigPanel = {
  nodeId: string;
  nodeType: 'source' | 'transform' | 'target';
  subtype: string;
  initialData?: Partial<SourceConfig & TargetConfig & TransformDefinition>;
};

export function EditorPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [pipeline, setPipeline] = useState<Pipeline | null>(null);
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const [configPanel, setConfigPanel] = useState<ConfigPanel | null>(null);
  const [showExecution, setShowExecution] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveStatus, setSaveStatus] = useState<'saved' | 'saving' | 'unsaved' | null>(null);
  const [yamlText, setYamlText] = useState('');
  const [showYaml, setShowYaml] = useState(false);
  const autoSaveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const pipelineRef = useRef<Pipeline | null>(null);

  useEffect(() => { pipelineRef.current = pipeline; }, [pipeline]);

  useEffect(() => {
    if (!id) return;
    getPipeline(id).then((p) => {
      setPipeline(p);
      pipelineRef.current = p;
      const initialNodes: Node[] = [
        ...p.sources.map((s, i) => ({
          id: `source-${s.id}`,
          type: 'source',
          position: { x: 80, y: 80 + i * 160 },
          data: { label: s.table || s.type, config: s },
        })),
        ...p.transforms.map((t, i) => ({
          id: `transform-${t.id}`,
          type: 'transform',
          position: { x: 380, y: 80 + i * 160 },
          data: { label: t.type, config: t },
        })),
        ...p.targets.map((t, i) => ({
          id: `target-${t.id}`,
          type: 'target',
          position: { x: 680, y: 80 + i * 160 },
          data: { label: t.table || t.connectorId, config: t },
        })),
      ];
      setNodes(initialNodes);
      setSaveStatus('saved');
      setYamlText(yaml.dump(p, { indent: 2 }));
    }).catch(() => navigate('/'));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function buildPipelineFromNodes(currentNodes: Node[]): Pipeline {
    const sources = currentNodes
      .filter((n) => n.type === 'source')
      .map((n) => (n.data as { config: SourceConfig }).config)
      .filter(Boolean) as SourceConfig[];

    const transforms = currentNodes
      .filter((n) => n.type === 'transform')
      .map((n) => (n.data as { config: TransformDefinition }).config)
      .filter(Boolean) as TransformDefinition[];

    const targets = currentNodes
      .filter((n) => n.type === 'target')
      .map((n) => (n.data as { config: TargetConfig }).config)
      .filter(Boolean) as TargetConfig[];

    return { ...pipelineRef.current!, sources, transforms, targets };
  }

  function triggerAutoSave(currentNodes: Node[]) {
    setSaveStatus('unsaved');
    if (autoSaveTimer.current) clearTimeout(autoSaveTimer.current);
    autoSaveTimer.current = setTimeout(async () => {
      if (!id || !pipelineRef.current) return;
      setSaveStatus('saving');
      try {
        const updated = buildPipelineFromNodes(currentNodes);
        const saved = await updatePipeline(id, updated);
        setPipeline(saved);
        pipelineRef.current = saved;
        setSaveStatus('saved');
        setYamlText(yaml.dump(saved, { indent: 2 }));
      } catch (e) {
        console.error('Auto-save failed', e);
        setSaveStatus('unsaved');
      }
    }, 1200);
  }

  async function handleExecute() {
    if (!id || !pipelineRef.current) return;
    setSaving(true);
    try {
      const updated = buildPipelineFromNodes(nodes);
      const saved = await updatePipeline(id, updated);
      setPipeline(saved);
      pipelineRef.current = saved;
      setSaveStatus('saved');
      setYamlText(yaml.dump(saved, { indent: 2 }));
    } finally {
      setSaving(false);
    }
    setShowExecution(true);
  }

  const onConnect = useCallback(
    (params: Connection) =>
      setEdges((eds) =>
        addEdge({ ...params, animated: true, style: { stroke: '#3b82f6' } } as Edge, eds)
      ),
    [setEdges]
  );

  function onDragOver(event: React.DragEvent) {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }

  function onDrop(event: React.DragEvent) {
    event.preventDefault();
    const type = event.dataTransfer.getData('nodeType') as 'source' | 'transform' | 'target';
    const subtype = event.dataTransfer.getData('nodeSubtype');
    if (!type) return;

    const bounds = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const position = { x: event.clientX - bounds.left - 80, y: event.clientY - bounds.top - 30 };
    const newId = crypto.randomUUID();

    const newNode: Node = {
      id: `${type}-${newId}`,
      type,
      position,
      data: { label: subtype, config: { id: newId, type: subtype } },
    };

    setNodes((nds) => {
      const updated = [...nds, newNode];
      triggerAutoSave(updated);
      return updated;
    });
    setConfigPanel({ nodeId: `${type}-${newId}`, nodeType: type, subtype, initialData: { id: newId } });
  }

  function onDragStart(event: React.DragEvent, type: string, subtype: string) {
    event.dataTransfer.setData('nodeType', type);
    event.dataTransfer.setData('nodeSubtype', subtype);
    event.dataTransfer.effectAllowed = 'move';
  }

  function onNodeDoubleClick(_: React.MouseEvent, node: Node) {
    const [type] = node.id.split('-');
    const data = node.data as { config: SourceConfig & TargetConfig & TransformDefinition };
    setConfigPanel({
      nodeId: node.id,
      nodeType: type as 'source' | 'transform' | 'target',
      subtype: data.config?.type ?? type,
      initialData: data.config,
    });
  }

  function handleConfigSave(data: SourceConfig | TargetConfig | TransformDefinition) {
    if (!configPanel) return;
    setNodes((nds) => {
      const updated = nds.map((n) =>
        n.id === configPanel.nodeId
          ? {
              ...n,
              data: {
                ...n.data,
                label:
                  (data as SourceConfig).table ||
                  (data as TargetConfig).table ||
                  (data as TransformDefinition).type || '',
                config: data,
              },
            }
          : n
      );
      triggerAutoSave(updated);
      return updated;
    });
    setConfigPanel(null);
  }

  const statusColor = saveStatus === 'saved' ? '#22c55e' : saveStatus === 'saving' ? '#3b82f6' : '#f59e0b';
  const statusLabel = saveStatus === 'saved' ? '✓ salvat' : saveStatus === 'saving' ? '⟳ salvare...' : '● modificat';

  return (
    <div style={{ height: '100vh', display: 'flex', flexDirection: 'column', background: '#0a0f1e', fontFamily: 'monospace' }}>
      {/* Topbar */}
      <div style={{
        background: '#0f172a', borderBottom: '1px solid #1e293b',
        padding: '10px 16px', display: 'flex', alignItems: 'center', gap: 12, flexShrink: 0,
      }}>
        <button onClick={() => navigate('/')} style={{ background: 'none', border: 'none', color: '#64748b', cursor: 'pointer', fontSize: 20, padding: 0 }}>←</button>
        <span style={{ fontSize: 18 }}>⚡</span>
        <span style={{ fontWeight: 800, fontSize: 16, color: '#3b82f6' }}>DataFlowMapper</span>
        <span style={{ color: '#334155' }}>|</span>
        <span style={{ color: '#e2e8f0', fontWeight: 600 }}>{pipeline?.name ?? '...'}</span>
        {saveStatus && (
          <span style={{ fontSize: 11, color: statusColor }}>{statusLabel}</span>
        )}
        <div style={{ flex: 1 }} />
        <button
          onClick={() => setShowYaml((v) => !v)}
          style={{ padding: '6px 14px', background: showYaml ? '#1e3a5f' : '#1e293b', color: '#93c5fd', border: '1px solid #2563eb44', borderRadius: 6, fontWeight: 600, cursor: 'pointer', fontSize: 12 }}
        >
          {'{ } YAML'}
        </button>
        <button
          onClick={handleExecute}
          disabled={saving}
          style={{ padding: '6px 18px', background: '#16a34a', color: '#fff', border: 'none', borderRadius: 6, fontWeight: 700, cursor: saving ? 'wait' : 'pointer', fontSize: 13, opacity: saving ? 0.7 : 1 }}
        >
          {saving ? '⟳ se pregateste...' : '▶ Executa'}
        </button>
      </div>

      {/* Main */}
      <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
        <Sidebar onDragStart={onDragStart} />

        <div style={{ flex: 1 }} onDragOver={onDragOver} onDrop={onDrop}>
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            onNodeDoubleClick={onNodeDoubleClick}
            nodeTypes={NODE_TYPES}
            fitView
            style={{ background: '#0a0f1e' }}
            defaultEdgeOptions={{ animated: true, style: { stroke: '#3b82f6', strokeWidth: 2 } }}
          >
            <Background color="#1e293b" variant={BackgroundVariant.Dots} gap={20} />
            <Controls style={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: 8 }} />
            <MiniMap
              style={{ background: '#0f172a', border: '1px solid #1e293b' }}
              nodeColor={(n) =>
                n.type === 'source' ? '#2563eb' :
                n.type === 'transform' ? '#7c3aed' : '#16a34a'
              }
            />
          </ReactFlow>
        </div>

        {/* YAML Panel */}
        {showYaml && (
          <div style={{
            width: 320, background: '#0f172a', borderLeft: '1px solid #1e293b',
            display: 'flex', flexDirection: 'column', flexShrink: 0,
          }}>
            <div style={{ padding: '10px 14px', borderBottom: '1px solid #1e293b', fontSize: 11, color: '#64748b', fontWeight: 700, textTransform: 'uppercase', letterSpacing: 1 }}>
              Pipeline YAML
            </div>
            <pre style={{
              flex: 1, overflowY: 'auto', margin: 0, padding: '12px 14px',
              fontSize: 11, color: '#94a3b8', lineHeight: 1.6,
              whiteSpace: 'pre-wrap', wordBreak: 'break-all',
            }}>
              {yamlText || '# pipeline gol'}
            </pre>
          </div>
        )}
      </div>

      {configPanel && (
        <ConnectorConfigPanel
          nodeType={configPanel.nodeType}
          subtype={configPanel.subtype}
          initialData={configPanel.initialData}
          onSave={handleConfigSave}
          onClose={() => setConfigPanel(null)}
        />
      )}

      {showExecution && id && (
        <ExecutionLogPanel pipelineId={id} onClose={() => setShowExecution(false)} />
      )}
    </div>
  );
}
