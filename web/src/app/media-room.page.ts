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
    <section *ngIf="joined()" class="mesh-stage"><div class="mesh-toolbar"><div><strong>{{ feeds().length + 1 }} na sala</strong><span role="status">{{ status() }}</span></div><div class="mesh-controls" *ngIf="role() !== 'spectator'"><button type="button" class="button button-small secondary" (click)="toggleCamera()" [attr.aria-pressed]="cameraOn()">{{ cameraOn() ? 'Desligar câmera' : 'Ligar câmera' }}</button><button type="button" class="button button-small secondary" (click)="toggleMic()" [attr.aria-pressed]="micOn()">{{ micOn() ? 'Desligar microfone' : 'Ligar microfone' }}</button><button type="button" class="button button-small secondary" (click)="toggleScreen()" [attr.aria-pressed]="screenOn()">{{ screenOn() ? 'Parar compartilhamento' : 'Compartilhar tela' }}</button></div></div>
      <p class="room-note">A conexão é direta entre navegadores. Em redes que bloqueiam WebRTC, vídeo e áudio podem não conectar sem um serviço TURN.</p>
      <div class="mesh-grid"><div class="mesh-tile"><header>Você · câmera</header><app-media-stream-view [stream]="localCamera()" [muted]="true" placeholder="Câmera desligada" /></div><div *ngIf="localScreen()" class="mesh-tile"><header>Você · tela</header><app-media-stream-view [stream]="localScreen()" [muted]="true" /></div>
        <ng-container *ngFor="let feed of feeds()"><div class="mesh-tile"><header>{{ feed.peer.displayName }} · câmera</header><app-media-stream-view [stream]="feed.peer.cameraEnabled ? feed.camera : null" [audioStream]="feed.audio" placeholder="Câmera desligada" /></div><div *ngIf="feed.peer.screenEnabled" class="mesh-tile"><header>{{ feed.peer.displayName }} · tela</header><app-media-stream-view [stream]="feed.screen" placeholder="Aguardando tela..." /></div></ng-container>
      </div>
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
    .mesh-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 300px), 1fr)); gap: 14px; margin-top: 18px; }
    .mesh-tile { border: 1px solid var(--border); background: var(--surface); min-width: 0; }
    .mesh-tile header { padding: 10px 14px; font-size: 12px; color: var(--muted); border-bottom: 1px solid var(--border); }
    @media (max-width: 620px) { .mesh-controls { width: 100%; } .mesh-controls button { flex: 1 1 auto; } }
  `]
})
export class MediaRoomPage implements OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  campaignId = this.route.snapshot.paramMap.get('id')!;
  role = signal<string | null>(null); joined = signal(false); connecting = signal(false); status = signal(''); error = signal('');
  feeds = signal<Feed[]>([]); cameraOn = signal(false); micOn = signal(false); screenOn = signal(false);
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
      revoked: () => { this.joined.set(false); this.error.set('Seu acesso à sala mudou. Recarregue a página para entrar novamente.'); }
    });
    try { await this.room.connect(); if (!this.destroyed) this.joined.set(true); }
    catch { if (!this.destroyed) this.error.set('Não foi possível entrar na sala.'); }
    finally { this.connecting.set(false); }
  }
  private syncFeeds() { this.feeds.set(this.peers.filter(peer => peer.mode === 'room').map(peer => {
    const streams = this.streams.get(peer.connectionId) ?? {};
    return { peer, camera: streams.camera ?? null, screen: streams.screen ?? null, audio: streams.audio ?? null };
  })); }
  async toggleCamera() {
    if (!this.room) return;
    try {
      if (this.cameraOn()) { await this.room.setTrack('camera', null); this.localCamera.set(null); this.cameraOn.set(false); }
      else { const stream = await navigator.mediaDevices.getUserMedia({ video: true }); await this.room.setTrack('camera', stream.getVideoTracks()[0]); this.localCamera.set(stream); this.cameraOn.set(true); }
    } catch { this.error.set('Não foi possível acessar a câmera. Verifique a permissão do navegador.'); }
  }
  async toggleMic() {
    if (!this.room) return;
    try {
      if (this.micOn()) { await this.room.setTrack('audio', null); this.micOn.set(false); }
      else { const stream = await navigator.mediaDevices.getUserMedia({ audio: true }); await this.room.setTrack('audio', stream.getAudioTracks()[0]); this.micOn.set(true); }
    } catch { this.error.set('Não foi possível acessar o microfone. Verifique a permissão do navegador.'); }
  }
  async toggleScreen() {
    if (!this.room) return;
    try {
      if (this.screenOn()) { await this.room.setTrack('screen', null); this.localScreen.set(null); this.screenOn.set(false); }
      else { const stream = await navigator.mediaDevices.getDisplayMedia({ video: true }); const track = stream.getVideoTracks()[0]; track.onended = () => { if (this.screenOn()) void this.toggleScreen(); }; await this.room.setTrack('screen', track); this.localScreen.set(stream); this.screenOn.set(true); }
    } catch { this.error.set('Não foi possível compartilhar a tela neste navegador.'); }
  }
  ngOnDestroy() { this.destroyed = true; void this.room?.close(); }
}
