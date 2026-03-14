import * as signalR from '@microsoft/signalr';
import type { ExecutionStats, LogMessage } from '../types/pipeline';

const HUB_URL = 'http://localhost:5001/hubs/execution';

let connection: signalR.HubConnection | null = null;

function getConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .build();
  }
  return connection;
}

export async function startConnection() {
  const conn = getConnection();
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    await conn.start();
  }
  return conn;
}

export async function stopConnection() {
  await connection?.stop();
}

export async function joinExecution(executionId: string) {
  const conn = await startConnection();
  await conn.invoke('JoinExecution', executionId);
}

export async function leaveExecution(executionId: string) {
  const conn = getConnection();
  await conn.invoke('LeaveExecution', executionId);
}

export function onLog(handler: (msg: LogMessage) => void) {
  getConnection().on('ReceiveLog', handler);
}

export function offLog(handler: (msg: LogMessage) => void) {
  getConnection().off('ReceiveLog', handler);
}

export function onProgress(handler: (stats: ExecutionStats) => void) {
  getConnection().on('ReceiveProgress', handler);
}

export function offProgress(handler: (stats: ExecutionStats) => void) {
  getConnection().off('ReceiveProgress', handler);
}
