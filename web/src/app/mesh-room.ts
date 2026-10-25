import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';

export interface RoomPeer { connectionId: string; campaignId: string; userId: string; displayName: string; role: string; mode: 'room' | 'obs'; cameraEnabled: boolean; screenEnabled: boolean; }
export type MediaKind = 'audio' | 'camera' | 'screen';
export interface RoomCallbacks {
  peers: (peers: RoomPeer[]) => void;
  track: (connectionId: string, kind: MediaKind, stream: MediaStream | null) => void;
  status: (message: string) => void;
  revoked: () => void;
}

interface RemoteConnection { info: RoomPeer; connection: RTCPeerConnection; senders: Record<MediaKind, RTCRtpSender>; candidates: RTCIceCandidateInit[]; }

export class MeshRoom {
  private hub: HubConnection;
  private peers = new Map<string, RemoteConnection>();
  private tracks: Partial<Record<MediaKind, MediaStreamTrack>> = {};
  private closed = false;
  private selfId = '';

  constructor(private campaignId: string, private mode: 'room' | 'obs', private callbacks: RoomCallbacks) {
    this.hub = new HubConnectionBuilder().withUrl('/api/hubs/campaign', { withCredentials: true })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 3000 }).build();
    this.hub.on('PeerJoined', (peer: RoomPeer) => { void this.addPeer(peer); });
    this.hub.on('PeerLeft', (id: string) => this.removePeer(id));
    this.hub.on('PeerState', (peer: RoomPeer) => {
      const existing = this.peers.get(peer.connectionId);
      if (existing) { existing.info = peer; this.emitPeers(); }
    });
    this.hub.on('Signal', (from: string, kind: string, payload: string) => { void this.receive(from, kind, payload); });
    this.hub.on('MembershipChanged', () => { this.callbacks.revoked(); void this.close(); });
    this.hub.onreconnecting(() => { this.clearPeers(); this.callbacks.status('Reconectando à sala...'); });
    this.hub.onreconnected(() => { void this.join().catch(() => { this.callbacks.revoked(); void this.close(); }); });
    this.hub.onclose(() => { if (!this.closed) this.callbacks.status('A conexão da sala foi encerrada.'); });
  }

  async connect() {
    while (!this.closed && this.hub.state === HubConnectionState.Disconnected) {
      try { await this.hub.start(); break; }
      catch { if (!this.closed) { this.callbacks.status('Conectando à sala. O servidor gratuito pode levar até um minuto para iniciar...'); await new Promise(resolve => setTimeout(resolve, 5000)); } }
    }
    if (!this.closed) await this.join();
  }

  private async join() {
    if (this.closed) return;
    this.clearPeers();
    const existing = await this.hub.invoke<RoomPeer[]>('JoinCampaign', this.campaignId, this.mode);
    this.selfId = this.hub.connectionId ?? '';
    for (const peer of existing) await this.addPeer(peer);
    this.callbacks.status('Conectado à sala.');
  }

  private async addPeer(info: RoomPeer) {
    if (this.closed || info.connectionId === this.selfId || this.peers.has(info.connectionId)) return;
    const connection = new RTCPeerConnection({ iceServers: [{ urls: 'stun:stun.cloudflare.com:3478' }] });
    const audio = connection.addTransceiver('audio', { direction: 'sendrecv' });
    const camera = connection.addTransceiver('video', { direction: 'sendrecv' });
    const screen = connection.addTransceiver('video', { direction: 'sendrecv' });
    const remote: RemoteConnection = { info, connection, senders: { audio: audio.sender, camera: camera.sender, screen: screen.sender }, candidates: [] };
    this.peers.set(info.connectionId, remote);
    connection.onicecandidate = event => { if (event.candidate) void this.signal(info.connectionId, 'ice', JSON.stringify(event.candidate.toJSON())); };
    connection.ontrack = event => {
      const kind: MediaKind = event.transceiver.mid === '0' ? 'audio' : event.transceiver.mid === '1' ? 'camera' : 'screen';
      this.callbacks.track(info.connectionId, kind, new MediaStream([event.track]));
      event.track.onended = () => this.callbacks.track(info.connectionId, kind, null);
    };
    connection.onconnectionstatechange = () => {
      if (connection.connectionState === 'failed') this.callbacks.status(`Conexão direta com ${info.displayName} falhou nesta rede.`);
    };
    for (const kind of ['audio', 'camera', 'screen'] as const) await remote.senders[kind].replaceTrack(this.tracks[kind] ?? null);
    this.emitPeers();
    if (this.selfId && this.selfId.localeCompare(info.connectionId) < 0) {
      try { await connection.setLocalDescription(await connection.createOffer()); await this.signal(info.connectionId, 'offer', JSON.stringify(connection.localDescription)); }
      catch { this.callbacks.status(`Não foi possível negociar mídia com ${info.displayName}.`); }
    }
  }

  private async receive(from: string, kind: string, payload: string) {
    const peer = this.peers.get(from);
    if (!peer || this.closed) return;
    try {
      if (kind === 'ice') {
        const candidate = JSON.parse(payload) as RTCIceCandidateInit;
        if (peer.connection.remoteDescription) await peer.connection.addIceCandidate(candidate);
        else peer.candidates.push(candidate);
      } else if (kind === 'offer' || kind === 'answer') {
        await peer.connection.setRemoteDescription(JSON.parse(payload) as RTCSessionDescriptionInit);
        for (const candidate of peer.candidates.splice(0)) await peer.connection.addIceCandidate(candidate);
        if (kind === 'offer') {
          await peer.connection.setLocalDescription(await peer.connection.createAnswer());
          await this.signal(from, 'answer', JSON.stringify(peer.connection.localDescription));
        }
      }
    } catch { this.callbacks.status(`Falha ao negociar mídia com ${peer.info.displayName}.`); }
  }

  private async signal(target: string, kind: string, payload: string) {
    if (this.hub.state === HubConnectionState.Connected && !this.closed)
      try { await this.hub.invoke('SendSignal', this.campaignId, target, kind, payload); } catch { /* retry on reconnect */ }
  }

  async setTrack(kind: MediaKind, track: MediaStreamTrack | null) {
    this.tracks[kind]?.stop();
    if (track) this.tracks[kind] = track; else delete this.tracks[kind];
    await Promise.all([...this.peers.values()].map(peer => peer.senders[kind].replaceTrack(track).catch(() => {})));
    if (kind !== 'audio' && this.hub.state === HubConnectionState.Connected)
      await this.hub.invoke('SetMediaState', !!this.tracks.camera, !!this.tracks.screen);
  }

  private removePeer(id: string) {
    const peer = this.peers.get(id);
    if (!peer) return;
    peer.connection.close(); this.peers.delete(id);
    for (const kind of ['audio', 'camera', 'screen'] as const) this.callbacks.track(id, kind, null);
    this.emitPeers();
  }
  private clearPeers() { for (const id of this.peers.keys()) this.removePeer(id); }
  private emitPeers() { this.callbacks.peers([...this.peers.values()].map(x => x.info)); }
  async close() { this.closed = true; this.clearPeers(); for (const track of Object.values(this.tracks)) track.stop(); await this.hub.stop(); }
}
