import { useState } from 'react';
import { testConnector } from '../../services/api';
import type { FieldMapping, SourceConfig, TableInfo, TargetConfig, TransformDefinition, WriteMode } from '../../types/pipeline';

interface Props {
  nodeType: 'source' | 'transform' | 'target';
  subtype: string;
  initialData?: Partial<SourceConfig & TargetConfig & TransformDefinition>;
  onSave: (data: SourceConfig | TargetConfig | TransformDefinition) => void;
  onClose: () => void;
}

const WRITE_MODES: WriteMode[] = ['Insert', 'Upsert', 'Overwrite'];

const labelStyle: React.CSSProperties = {
  display: 'block', fontSize: 11, color: '#94a3b8',
  textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 4,
};

const inputStyle: React.CSSProperties = {
  width: '100%', padding: '7px 10px', background: '#1e293b',
  border: '1px solid #334155', borderRadius: 6, color: '#e2e8f0',
  fontSize: 13, outline: 'none', boxSizing: 'border-box',
};

const btnStyle = (color: string): React.CSSProperties => ({
  padding: '7px 16px', borderRadius: 6, border: 'none',
  background: color, color: '#fff', fontWeight: 600,
  fontSize: 13, cursor: 'pointer',
});

export function ConnectorConfigPanel({ nodeType, subtype, initialData, onSave, onClose }: Props) {
  const [connectionString, setConnectionString] = useState(
    (initialData as SourceConfig)?.connectionString ?? ''
  );
  const [table, setTable] = useState((initialData as SourceConfig & TargetConfig)?.table ?? '');
  const [query, setQuery] = useState((initialData as SourceConfig)?.query ?? '');
  const [mode, setMode] = useState<WriteMode>((initialData as TargetConfig)?.mode ?? 'Insert');
  const [mappings, setMappings] = useState<FieldMapping[]>(
    (initialData as TargetConfig)?.mappings ?? []
  );
  const [transformType] = useState(subtype);
  const [params, setParams] = useState<Record<string, string>>(
    (initialData as TransformDefinition)?.params ?? {}
  );
  const [inputs, setInputs] = useState(
    ((initialData as TransformDefinition)?.inputs ?? []).join(', ')
  );
  const [outputs, setOutputs] = useState(
    ((initialData as TransformDefinition)?.outputs ?? []).join(', ')
  );
  const [outputField, setOutputField] = useState(
    (initialData as TransformDefinition)?.output ?? ''
  );

  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; tables: TableInfo[]; error?: string } | null>(null);

  async function handleTest() {
    setTesting(true);
    setTestResult(null);
    try {
      const res = await testConnector({ type: subtype, connectionString });
      setTestResult(res);
    } catch (e: unknown) {
      setTestResult({ success: false, tables: [], error: String(e) });
    } finally {
      setTesting(false);
    }
  }

  function handleSave() {
    if (nodeType === 'source') {
      onSave({
        id: (initialData as SourceConfig)?.id ?? crypto.randomUUID(),
        type: subtype,
        connectionString,
        table,
        query: query || undefined,
      } as SourceConfig);
    } else if (nodeType === 'target') {
      onSave({
        id: (initialData as TargetConfig)?.id ?? crypto.randomUUID(),
        table,
        connectorId: subtype,
        mode,
        mappings,
      } as TargetConfig);
    } else {
      onSave({
        id: (initialData as TransformDefinition)?.id ?? crypto.randomUUID(),
        type: transformType,
        inputs: inputs.split(',').map((s) => s.trim()).filter(Boolean),
        outputs: outputs.split(',').map((s) => s.trim()).filter(Boolean),
        output: outputField || undefined,
        params,
        dependsOn: [],
      } as TransformDefinition);
    }
  }

  function addMapping() {
    setMappings([...mappings, { from: '', to: '' }]);
  }

  function updateMapping(i: number, field: 'from' | 'to', value: string) {
    const updated = [...mappings];
    updated[i] = { ...updated[i], [field]: value };
    setMappings(updated);
  }

  function removeMapping(i: number) {
    setMappings(mappings.filter((_, idx) => idx !== i));
  }

  return (
    <div
      style={{
        position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.6)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100,
      }}
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div style={{
        background: '#0f172a', border: '1px solid #1e293b', borderRadius: 12,
        padding: 24, width: 480, maxHeight: '85vh', overflowY: 'auto',
        color: '#e2e8f0',
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
          <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>
            Configurare {nodeType} · {subtype}
          </h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: '#64748b', cursor: 'pointer', fontSize: 20 }}>×</button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {/* SOURCE */}
          {nodeType === 'source' && (
            <>
              <div>
                <label style={labelStyle}>Connection String</label>
                <input
                  style={inputStyle}
                  value={connectionString}
                  onChange={(e) => setConnectionString(e.target.value)}
                  placeholder={
                    subtype === 'postgresql' ? 'Host=localhost;Database=mydb;Username=postgres;Password=...' :
                    subtype === 'mysql' ? 'Server=localhost;Database=mydb;Uid=root;Pwd=...;' :
                    'mongodb://localhost:27017/mydb'
                  }
                />
              </div>
              <div>
                <label style={labelStyle}>Table / Collection</label>
                <input style={inputStyle} value={table} onChange={(e) => setTable(e.target.value)} placeholder="orders" />
              </div>
              <div>
                <label style={labelStyle}>Query (optional)</label>
                <input style={inputStyle} value={query} onChange={(e) => setQuery(e.target.value)} placeholder="SELECT * FROM orders WHERE status = 'active'" />
              </div>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <button style={btnStyle('#0284c7')} onClick={handleTest} disabled={testing}>
                  {testing ? 'Se testeaza...' : 'Testeaza conexiunea'}
                </button>
                {testResult && (
                  <span style={{ fontSize: 12, color: testResult.success ? '#22c55e' : '#ef4444' }}>
                    {testResult.success ? `✓ ${testResult.tables.length} tabele gasite` : `✗ ${testResult.error}`}
                  </span>
                )}
              </div>
              {testResult?.success && testResult.tables.length > 0 && (
                <div>
                  <label style={labelStyle}>Alege tabelul</label>
                  <select
                    style={{ ...inputStyle }}
                    value={table}
                    onChange={(e) => setTable(e.target.value)}
                  >
                    <option value="">-- alege --</option>
                    {testResult.tables.map((t) => (
                      <option key={t.name} value={t.name}>{t.schema ? `${t.schema}.${t.name}` : t.name}</option>
                    ))}
                  </select>
                </div>
              )}
            </>
          )}

          {/* TARGET */}
          {nodeType === 'target' && (
            <>
              <div>
                <label style={labelStyle}>Table / Collection</label>
                <input style={inputStyle} value={table} onChange={(e) => setTable(e.target.value)} placeholder="output_table" />
              </div>
              <div>
                <label style={labelStyle}>Write Mode</label>
                <select style={{ ...inputStyle }} value={mode} onChange={(e) => setMode(e.target.value as WriteMode)}>
                  {WRITE_MODES.map((m) => <option key={m} value={m}>{m}</option>)}
                </select>
              </div>
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                  <label style={{ ...labelStyle, marginBottom: 0 }}>Field Mappings</label>
                  <button onClick={addMapping} style={{ ...btnStyle('#334155'), padding: '3px 10px', fontSize: 12 }}>+ Adauga</button>
                </div>
                {mappings.map((m, i) => (
                  <div key={i} style={{ display: 'flex', gap: 6, marginBottom: 6, alignItems: 'center' }}>
                    <input style={{ ...inputStyle, flex: 1 }} placeholder="from" value={m.from} onChange={(e) => updateMapping(i, 'from', e.target.value)} />
                    <span style={{ color: '#64748b' }}>→</span>
                    <input style={{ ...inputStyle, flex: 1 }} placeholder="to" value={m.to} onChange={(e) => updateMapping(i, 'to', e.target.value)} />
                    <button onClick={() => removeMapping(i)} style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', fontSize: 16 }}>×</button>
                  </div>
                ))}
              </div>
            </>
          )}

          {/* TRANSFORM */}
          {nodeType === 'transform' && (
            <>
              <div>
                <label style={labelStyle}>Input columns (virgula-separat)</label>
                <input style={inputStyle} value={inputs} onChange={(e) => setInputs(e.target.value)} placeholder="col1, col2" />
              </div>
              {(subtype === 'concat') && (
                <div>
                  <label style={labelStyle}>Output column</label>
                  <input style={inputStyle} value={outputField} onChange={(e) => setOutputField(e.target.value)} placeholder="full_name" />
                </div>
              )}
              {(subtype === 'split') && (
                <div>
                  <label style={labelStyle}>Output columns (virgula-separat)</label>
                  <input style={inputStyle} value={outputs} onChange={(e) => setOutputs(e.target.value)} placeholder="part1, part2" />
                </div>
              )}
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                  <label style={{ ...labelStyle, marginBottom: 0 }}>Params</label>
                  <button
                    onClick={() => setParams({ ...params, '': '' })}
                    style={{ ...btnStyle('#334155'), padding: '3px 10px', fontSize: 12 }}
                  >+ Adauga</button>
                </div>
                {Object.entries(params).map(([k, v], i) => (
                  <div key={i} style={{ display: 'flex', gap: 6, marginBottom: 6 }}>
                    <input
                      style={{ ...inputStyle, flex: 1 }}
                      placeholder={subtype === 'concat' ? 'separator' : subtype === 'split' ? 'delimiter' : 'key'}
                      value={k}
                      onChange={(e) => {
                        const newP = Object.fromEntries(Object.entries(params).map(([ok, ov], j) => j === i ? [e.target.value, ov] : [ok, ov]));
                        setParams(newP);
                      }}
                    />
                    <input
                      style={{ ...inputStyle, flex: 1 }}
                      placeholder="value"
                      value={v}
                      onChange={(e) => setParams({ ...params, [k]: e.target.value })}
                    />
                  </div>
                ))}
              </div>
            </>
          )}
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 20 }}>
          <button style={btnStyle('#1e293b')} onClick={onClose}>Anuleaza</button>
          <button style={btnStyle('#2563eb')} onClick={handleSave}>Salveaza</button>
        </div>
      </div>
    </div>
  );
}
