import { CommonModule } from '@angular/common';
import { Component, OnDestroy, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService, apiError } from './api';
import { ChatPanelComponent } from './chat-panel.component';
import { DicePanelComponent } from './dice-panel.component';
import { MediaStreamViewComponent } from './media-stream-view.component';
import { MediaKind, MeshRoom, RoomPeer } from './mesh-room';

interface Feed { peer: RoomPeer; camera: MediaStream | null; screen: MediaStream | null; audio: MediaStream | null; }

@Component({ standalone: true, imports: [CommonModule, RouterLink, MediaStreamViewComponent, DicePanelComponent, ChatPanelComponent], template: `
  <main class="page media-room"><a [routerLink]="['/campaigns', campaignId]" class="back-link">← Voltar à campanha</a>
    <div class="page-heading"><div><span class="overline">MESA AO VIVO</span><h1>Sala de transmissão</h1><p>Câmeras e telas compartilhadas diretamente entre os participantes, com portrait e recursos para OBS.</p></div></div>
    <p *ngIf="error()" class="notice error" role="alert">{{ error() }}</p>
    <section class="room-connect" *ngIf="!joined()"><p>Entre para ver os participantes. O navegador pedirá permissão apenas quando você ligar câmera, microfone ou tela.</p><button type="button" class="button" (click)="join()" [disabled]="connecting() || !role()">{{ connecting() ? 'Conectando...' : 'Entrar na sala' }}</button></section>
    <section *ngIf="joined()" class="mesh-stage"><div class="mesh-toolbar"><div><strong>{{ feeds().length + 1 }} na sala</strong><span role="status">{{ status() }}</span></div><div class="mesh-controls" *ngIf="role() !== 'spectator'"><label class="quality-select">Qualidade da câmera<select [value]="quality" (change)="changeQuality($any($event.target).value)"><option value="economy">Econômica · 360p</option><option value="balanced">Equilibrada · 480p</option><option value="high">Alta · 720p</option></select></label><button type="button" class="button button-small secondary" (click)="toggleCamera()" [attr.aria-pressed]="cameraOn()">{{ cameraOn() ? 'Desligar câmera' : 'Ligar câmera' }}</button><button type="button" class="button button-small secondary" (click)="toggleMic()" [attr.aria-pressed]="micOn()">{{ micOn() ? 'Desligar microfone' : 'Ligar microfone' }}</button><button type="button" class="button button-small secondary" (click)="toggleScreen()" [attr.aria-pressed]="screenOn()">{{ screenOn() ? 'Parar compartilhamento' : 'Compartilhar tela' }}</button></div></div>
      <p class="room-note">A conexão é direta entre navegadores. Em redes que bloqueiam WebRTC, vídeo e áudio podem não conectar sem um serviço TURN.</p>
      <div *ngIf="localScreen() || hasRemoteScreen()" class="screen-section"><h2>Telas compartilhadas</h2><div class="screen-grid"><div *ngIf="localScreen()" class="mesh-tile screen-tile"><header>Você · tela</header><app-media-stream-view [stream]="localScreen()" [muted]="true" /></div><ng-container *ngFor="let feed of feeds()"><div *ngIf="feed.peer.screenEnabled" class="mesh-tile screen-tile"><header>{{ feed.peer.displayName }} · tela</header><app-media-stream-view [stream]="feed.screen" placeholder="Aguardando tela..." /></div></ng-container></div></div>
      <div class="camera-section"><h2>Participantes</h2><div class="camera-grid"><div class="mesh-tile"><header>Você · câmera</header><app-media-stream-view [stream]="localCamera()" [muted]="true" placeholder="Câmera desligada" /></div><div *ngFor="let feed of feeds()" class="mesh-tile"><header>{{ feed.peer.displayName }} · câmera</header><app-media-stream-view [stream]="feed.peer.cameraEnabled ? feed.camera : null" [audioStream]="feed.audio" placeholder="Câmera desligada" /></div></div></div>
      <p *ngIf="feeds().length === 0" class="room-note">Você está sozinho na sala. Convide os outros participantes pela campanha.</p>
    </section>
    <app-dice-panel [campaignId]="campaignId" /><app-chat-panel [campaignId]="campaignId" />
  </main>`,
  styles: [`
    .room-connect { border: 1px solid var(--border); background: var(--surface); padding: 28px; margin: 24px 0; }
    .room-connect p { color: var(--muted); }
    .mesh-stage { margin: 24px 0 42px; }
    .mesh-toolbar { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 16px; border: 1px solid var(--border); background: var(--surface); padding: 16px; }
    .mesh-toolbar > div:first-child { display: grid; gap: 3px; }
    .mesh-toolbar span { color: var(--muted); font-size: 12px; }
    .mesh-controls { display: flex; flex-wrap: wrap; gap: 8px; }
    .quality-select { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 12px; color: var(--muted); }
    .quality-select select { margin: 0; min-width: 145px; padding: 8px; }
    .screen-section, .camera-section { margin-top: 22px; }
    .screen-section h2, .camera-section h2 { font-size: 19px; margin: 0 0 12px; }
    .screen-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 480px), 1fr)); gap: 14px; }
    .camera-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 230px), 1fr)); gap: 12px; }
    .screen-tile app-media-stream-view { display: block; height: 280px; }
    .camera-grid app-media-stream-view { display: block; height: 200px; }
    .mesh-tile { border: 1px solid var(--border); background: var(--surface); min-width: 0; }
    .mesh-tile header { padding: 10px 14px; font-size: 12px; color: var(--muted); border-bottom: 1px solid var(--border); }
    @media (max-width: 620px) { .mesh-controls { width: 100%; } .mesh-controls button { flex: 1 1 auto; } .quality-select { width: 100%; justify-content: space-between; } }
  `]
})
export class MediaRoomPage implements OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  campaignId = this.route.snapshot.paramMap.get('id')!;
  role = signal<string | null>(null); joined = signal(false); connecting = signal(false); status = signal(''); error = signal('');
  feeds = signal<Feed[]>([]); cameraOn = signal(false); micOn = signal(false); screenOn = signal(false);
  quality: 'economy' | 'balanced' | 'high' = 'balanced';
  localCamera = signal<MediaStream | null>(null); localScreen = signal<MediaStream | null>(null);
  private peers: RoomPeer[] = []; private streams = new Map<string, Partial<Record<MediaKind, MediaStream>>>();
  private room?: MeshRoom; private destroyed = false;
  constructor() { this.api.campaign(this.campaignId).subscribe({ next: campaign => this.role.set(campaign.role), error: e => this.error.set(apiError(e)) }); }
  async join() {
    if (this.connecting() || this.room) return;
    this.connecting.set(true); this.error.set('');
    this.room = new MeshRoom(this.campaignId, 'room', {
      peers: peers => { this.peers = peers; this.syncFeeds(); },
      track: (id, kind, stream) => { const current = this.streams.get(id) ?? {}; if (stream) current[kind] = stream; else delete current[kind]; this.streams.set(id, current); this.syncFeeds(); },
      status: message => this.status.set(message),
      revoked: () => { this.joined.set(false); this.role.set(null); this.error.set('Seu acesso à sala mudou. Recarregue a página para entrar novamente.'); }
    });
    try { await this.room.connect(); if (!this.destroyed) this.joined.set(true); }
    catch { if (!this.destroyed) this.error.set('Não foi possível entrar na sala.'); await this.room.close(); this.room = undefined; }
    finally { this.connecting.set(false); }
  }
  private syncFeeds() { this.feeds.set(this.peers.filter(peer => peer.mode === 'room').map(peer => {
    const streams = this.streams.get(peer.connectionId) ?? {};
    return { peer, camera: streams.camera ?? null, screen: streams.screen ?? null, audio: streams.audio ?? null };
  })); }
  hasRemoteScreen() { return this.feeds().some(feed => feed.peer.screenEnabled); }
  async toggleCamera() {
    if (!this.room) return;
    try {
      if (this.cameraOn()) { await this.room.setTrack('camera', null); this.localCamera.set(null); this.cameraOn.set(false); }
      else {
        const stream = await navigator.mediaDevices.getUserMedia({ video: this.cameraConstraints() });
        const track = stream.getVideoTracks()[0];
        track.onended = () => { if (this.cameraOn()) { void this.room?.setTrack('camera', null); this.localCamera.set(null); this.cameraOn.set(false); } };
        await this.room.setTrack('camera', track); this.localCamera.set(stream); this.cameraOn.set(true);
      }
    } catch { this.error.set('Não foi possível acessar a câmera. Verifique a permissão do navegador.'); }
  }
  async toggleMic() {
    if (!this.room) return;
    try {
      if (this.micOn()) { await this.room.setTrack('audio', null); this.micOn.set(false); }
      else {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } });
        const track = stream.getAudioTracks()[0];
        track.onended = () => { if (this.micOn()) { void this.room?.setTrack('audio', null); this.micOn.set(false); } };
        await this.room.setTrack('audio', track); this.micOn.set(true);
      }
    } catch { this.error.set('Não foi possível acessar o microfone. Verifique a permissão do navegador.'); }
  }
  async toggleScreen() {
    if (!this.room) return;
    try {
      if (this.screenOn()) { await this.room.setTrack('screen', null); this.localScreen.set(null); this.screenOn.set(false); }
      else { const stream = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: 15 }, audio: false }); const track = stream.getVideoTracks()[0]; track.onended = () => { if (this.screenOn()) void this.toggleScreen(); }; await this.room.setTrack('screen', track); this.localScreen.set(stream); this.screenOn.set(true); }
    } catch { this.error.set('Não foi possível compartilhar a tela neste navegador.'); }
  }
  async changeQuality(value: string) {
    if (value !== 'economy' && value !== 'balanced' && value !== 'high') return;
    const previous = this.quality;
    this.quality = value;
    const track = this.localCamera()?.getVideoTracks()[0];
    if (track) try { await track.applyConstraints(this.cameraConstraints()); }
    catch { this.quality = previous; this.error.set('Esta câmera não aceita a qualidade selecionada.'); }
  }
  private cameraConstraints(): MediaTrackConstraints {
    const presets = { economy: { width: 640, height: 360, frameRate: 15 }, balanced: { width: 854, height: 480, frameRate: 20 }, high: { width: 1280, height: 720, frameRate: 24 } };
    const preset = presets[this.quality];
    return { width: { ideal: preset.width }, height: { ideal: preset.height }, frameRate: { ideal: preset.frameRate, max: preset.frameRate } };
  }
  ngOnDestroy() { this.destroyed = true; void this.room?.close(); }
}
