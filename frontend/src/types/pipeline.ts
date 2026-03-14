export type WriteMode = 'Insert' | 'Upsert' | 'Overwrite';
export type ExecutionStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled' | 'Interrupted';
export type LogLevel = 'Info' | 'Ok' | 'Warn' | 'Error';

export interface FieldMapping {
  from: string;
  to: string;
}

export interface SourceConfig {
  id: string;
  type: string;
  connectionString: string;
  table: string;
  query?: string;
}

export interface TargetConfig {
  id: string;
  table: string;
  connectorId: string;
  mode: WriteMode;
  mappings: FieldMapping[];
}

export interface TransformDefinition {
  id: string;
  type: string;
  inputs: string[];
  outputs: string[];
  output?: string;
  params: Record<string, string>;
  dependsOn: string[];
}

export interface JoinDefinition {
  left: string;
  right: string;
  on: string;
  joinType: string;
}

export interface Pipeline {
  id: string;
  name: string;
  version: number;
  createdAt: string;
  sources: SourceConfig[];
  targets: TargetConfig[];
  transforms: TransformDefinition[];
  joins: JoinDefinition[];
}

export interface TableInfo {
  name: string;
  schema: string;
}

export interface FieldInfo {
  name: string;
  type: string;
  nullable: boolean;
}

export interface LogMessage {
  level: LogLevel;
  message: string;
  timestamp: string;
  meta: Record<string, string>;
}

export interface ExecutionStats {
  rowsRead: number;
  rowsWritten: number;
  rowsSkipped: number;
  chunksTotal: number;
  chunksDone: number;
  elapsedMs: number;
  progressPercent: number;
}
