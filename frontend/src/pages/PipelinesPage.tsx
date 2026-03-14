import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { createPipeline, deletePipeline, getPipelines } from '../services/api';
import type { Pipeline } from '../types/pipeline';

export function PipelinesPage() {
  const [pipelines, setPipelines] = useState<Pipeline[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    load();
  }, []);

  async function load() {
    setLoading(true);
    try {
      setPipelines(await getPipelines());
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  }

  async function handleCreate() {
    if (!newName.trim()) return;
    try {
      const p = await createPipeline({ name: newName.trim(), sources: [], targets: [], transforms: [], joins: [] });
      setNewName('');
      setCreating(false);
      navigate(`/pipelines/${p.id}`);
    } catch (e) {
      setError(String(e));
    }
  }

  async function handleDelete(id: string, e: React.MouseEvent) {
    e.stopPropagation();
    if (!confirm('Stergi pipeline-ul?')) return;
    await deletePipeline(id);
    setPipelines((prev) => prev.filter((p) => p.id !== id));
  }

  return (
    <div style={{ minHeight: '100vh', background: '#0a0f1e', color: '#e2e8f0', fontFamily: 'monospace' }}>
      {/* Nav */}
      <div style={{ background: '#0f172a', borderBottom: '1px solid #1e293b', padding: '14px 28px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <span style={{ fontSize: 22 }}>⚡</span>
          <span style={{ fontWeight: 800, fontSize: 18, color: '#3b82f6' }}>DataFlowMapper</span>
        </div>
      </div>

      <div style={{ maxWidth: 800, margin: '0 auto', padding: '40px 24px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 28 }}>
          <h1 style={{ margin: 0, fontSize: 24, fontWeight: 700 }}>Pipelines</h1>
          <button
            onClick={() => setCreating(true)}
            style={{ padding: '8px 18px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 8, fontWeight: 700, cursor: 'pointer', fontSize: 14 }}
          >
            + Pipeline nou
          </button>
        </div>

        {creating && (
          <div style={{ background: '#0f172a', border: '1px solid #1e293b', borderRadius: 10, padding: 20, marginBottom: 20 }}>
            <div style={{ marginBottom: 10, fontWeight: 600 }}>Nume pipeline</div>
            <div style={{ display: 'flex', gap: 8 }}>
              <input
                autoFocus
                style={{ flex: 1, padding: '8px 12px', background: '#1e293b', border: '1px solid #334155', borderRadius: 6, color: '#e2e8f0', fontSize: 14, outline: 'none' }}
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
                placeholder="ex: ETL Orders Daily"
              />
              <button onClick={handleCreate} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: 6, fontWeight: 600, cursor: 'pointer' }}>
                Creeaza
              </button>
              <button onClick={() => setCreating(false)} style={{ padding: '8px 16px', background: '#1e293b', color: '#94a3b8', border: 'none', borderRadius: 6, cursor: 'pointer' }}>
                Anuleaza
              </button>
            </div>
          </div>
        )}

        {error && (
          <div style={{ background: '#ef444422', border: '1px solid #ef4444', borderRadius: 8, padding: '10px 14px', marginBottom: 16, color: '#fca5a5', fontSize: 13 }}>
            {error}
          </div>
        )}

        {loading ? (
          <div style={{ color: '#475569', textAlign: 'center', padding: 60 }}>Se incarca...</div>
        ) : pipelines.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 80, color: '#475569' }}>
            <div style={{ fontSize: 48, marginBottom: 12 }}>📭</div>
            <div style={{ fontSize: 16 }}>Niciun pipeline. Creeaza primul.</div>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {pipelines.map((p) => (
              <div
                key={p.id}
                onClick={() => navigate(`/pipelines/${p.id}`)}
                style={{
                  background: '#0f172a', border: '1px solid #1e293b', borderRadius: 10,
                  padding: '16px 20px', cursor: 'pointer', display: 'flex',
                  justifyContent: 'space-between', alignItems: 'center',
                  transition: 'border-color 0.15s',
                }}
                onMouseEnter={(e) => (e.currentTarget.style.borderColor = '#3b82f6')}
                onMouseLeave={(e) => (e.currentTarget.style.borderColor = '#1e293b')}
              >
                <div>
                  <div style={{ fontWeight: 700, fontSize: 15 }}>{p.name}</div>
                  <div style={{ fontSize: 12, color: '#475569', marginTop: 4 }}>
                    v{p.version} · {p.sources.length} surse · {p.transforms.length} transformari · {p.targets.length} targeturi
                  </div>
                  <div style={{ fontSize: 11, color: '#334155', marginTop: 2 }}>
                    {new Date(p.createdAt).toLocaleString()}
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <span style={{ fontSize: 12, color: '#3b82f6', background: '#1e3a5f', padding: '3px 10px', borderRadius: 999 }}>
                    Editeaza →
                  </span>
                  <button
                    onClick={(e) => handleDelete(p.id, e)}
                    style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', fontSize: 18, padding: '0 4px' }}
                  >
                    🗑
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
