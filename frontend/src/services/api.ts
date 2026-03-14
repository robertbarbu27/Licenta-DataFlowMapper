import type { FieldInfo, Pipeline, TableInfo } from '../types/pipeline';

const BASE = 'http://localhost:5001/api';

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

// Pipelines
export const getPipelines = () => request<Pipeline[]>('/pipelines');
export const getPipeline = (id: string) => request<Pipeline>(`/pipelines/${id}`);
export const createPipeline = (p: Omit<Pipeline, 'id' | 'createdAt' | 'version'>) =>
  request<Pipeline>('/pipelines', { method: 'POST', body: JSON.stringify(p) });
export const updatePipeline = (id: string, p: Pipeline) =>
  request<Pipeline>(`/pipelines/${id}`, { method: 'PUT', body: JSON.stringify(p) });

export const deletePipeline = (id: string) =>
  request<void>(`/pipelines/${id}`, { method: 'DELETE' });

// Connectors
export interface ConnectorTestRequest {
  type: string;
  connectionString: string;
}
export interface ConnectorTestResult {
  success: boolean;
  tables: TableInfo[];
  error?: string;
}
export const testConnector = (req: ConnectorTestRequest) =>
  request<ConnectorTestResult>('/connectors/test', { method: 'POST', body: JSON.stringify(req) });

export const getSchema = (id: string, table: string) =>
  request<FieldInfo[]>(`/connectors/${id}/schema/${table}`);

// Execution
export const executePipeline = (id: string) =>
  request<{ executionId: string }>(`/pipelines/${id}/execute`, { method: 'POST' });

export const cancelExecution = (id: string) =>
  request<void>(`/pipelines/${id}/cancel`, { method: 'POST' });
