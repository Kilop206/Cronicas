import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';

export interface SheetInvalidation { eventId: string; type: string; characterId: string; revision: number | null; }

// Per-editor connection; no shared session/game state. Reconnection always asks the API for current state.
export class SheetUpdates {
  private connection?: HubConnection;
  private stopped = false;
  private retry?: ReturnType<typeof setTimeout>;
  private seen = new Set<string>();

  watch(id: string, invalidate: (event: SheetInvalidation | null) => void, unavailable: () => void) {
    this.stopped = false;
    const connection = new HubConnectionBuilder().withUrl('/api/hubs/sheets', { withCredentials: true })
      .withAutomaticReconnect().configureLogging(LogLevel.Error).build();
    this.connection = connection;
    connection.on('SheetInvalidated', (event: SheetInvalidation) => {
      if (event.characterId !== id || this.seen.has(event.eventId)) return;
      this.seen.add(event.eventId);
      if (this.seen.size > 200) this.seen.delete(this.seen.values().next().value!);
      invalidate(event);
    });
    const subscribe = async () => {
      await connection.invoke('Subscribe', id);
      if (!this.stopped) invalidate(null);
    };
    connection.onreconnecting(unavailable);
    const scheduleRetry = () => { if (!this.stopped) { clearTimeout(this.retry); this.retry = setTimeout(() => void start(), 15000); } };
    connection.onreconnected(() => { void subscribe().catch(async () => { unavailable(); await connection.stop(); scheduleRetry(); }); });
    let starting = false;
    const start = async () => {
      if (this.stopped || starting || connection.state !== HubConnectionState.Disconnected) return;
      starting = true;
      try { await connection.start(); if (this.stopped) { await connection.stop(); return; } await subscribe(); }
      catch { if (!this.stopped) { unavailable(); await connection.stop(); scheduleRetry(); } }
      finally { starting = false; }
    };
    connection.onclose(() => { if (!this.stopped) { unavailable(); scheduleRetry(); } });
    void start();
  }

  stop() { this.stopped = true; clearTimeout(this.retry); void this.connection?.stop(); this.connection = undefined; this.seen.clear(); }
}
