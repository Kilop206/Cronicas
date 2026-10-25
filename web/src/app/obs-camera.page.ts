import { CommonModule } from '@angular/common';
import { Component, ElementRef, inject, OnDestroy, signal, ViewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import RealtimeKitClient from '@cloudflare/realtimekit';
import { combineLatest, Subscription, timer } from 'rxjs';
import { ApiService, apiError, Character } from './api';
import { OverlayCardComponent } from './studio-pages';

type Meeting = Awaited<ReturnType<typeof RealtimeKitClient.init>>;
type Peer = ReturnType<Meeting['participants']['joined']['toArray']>[number];

@Component({ standalone: true, imports: [CommonModule, OverlayCardComponent], template: `
  <main class="obs-camera-page"><div class="obs-video-stage"><video #video autoplay playsinline muted></video><div *ngIf="!videoReady()" class="obs-video-empty">{{ waiting() }}</div></div>
    <app-overlay-card *ngIf="character() as item" [character]="item" />
    <p *ngIf="error()" class="obs-camera-error" role="alert">{{ error() }}</p>
  </main>` })
export class ObsCameraPage implements OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  @ViewChild('video') video?: ElementRef<HTMLVideoElement>;
  private campaignId = this.route.snapshot.paramMap.get('campaignId')!;
  private characterId = this.route.snapshot.paramMap.get('characterId')!;
  private meeting?: Meeting; private peer?: Peer; private destroyed = false;
  private polling?: Subscription; private refresh?: ReturnType<typeof setInterval>;
  character = signal<Character | null>(null); videoReady = signal(false); waiting = signal('Aguardando a câmera do participante...'); error = signal('');

  constructor() {
    combineLatest([this.api.character(this.characterId), this.api.joinRoom(this.campaignId)]).subscribe({
      next: async ([character, room]) => {
        if (character.campaignId !== this.campaignId) { this.error.set('Personagem fora desta campanha.'); return; }
        this.character.set(character);
        this.polling = timer(3000, 3000).subscribe(() => this.api.character(this.characterId).subscribe({ next: item => this.character.set(item) }));
        try {
          const meeting = await RealtimeKitClient.init({ authToken: room.authToken, defaults: { audio: false, video: false } });
          if (this.destroyed) return;
          this.meeting = meeting;
          await meeting.join();
          if (this.destroyed) { await meeting.leave(); return; }
          meeting.participants.joined.on('participantJoined', this.pickPeer);
          meeting.participants.joined.on('participantLeft', this.pickPeer);
          this.refresh = setInterval(this.pickPeer, 2000);
          this.pickPeer();
        } catch { if (!this.destroyed) this.error.set('Não foi possível conectar a câmera à sala.'); }
      },
      error: error => this.error.set(apiError(error))
    });
  }

  private pickPeer = () => {
    const character = this.character();
    if (!this.meeting || !character || this.destroyed) return;
    const peers = this.meeting.participants.joined.toArray().filter(peer => peer.customParticipantId === character.ownerId);
    const chosen = peers.find(peer => peer.videoEnabled && peer.videoTrack) ?? peers[0];
    if (chosen !== this.peer) {
      this.peer?.off('videoUpdate', this.onVideoUpdate);
      this.peer = chosen;
      chosen?.on('videoUpdate', this.onVideoUpdate);
      if (chosen) void this.meeting.participants.subscribe([chosen.id], ['video']).catch(() => {});
    }
    this.attachVideo(chosen?.videoEnabled ? chosen.videoTrack : undefined);
  };
  private onVideoUpdate = (event: { videoEnabled: boolean; videoTrack: MediaStreamTrack }) => this.attachVideo(event.videoEnabled ? event.videoTrack : undefined);
  private attachVideo(track?: MediaStreamTrack) {
    const element = this.video?.nativeElement;
    if (!element) return;
    if (track?.readyState === 'live') {
      if ((element.srcObject as MediaStream | null)?.getVideoTracks()[0] !== track) element.srcObject = new MediaStream([track]);
      void element.play().catch(() => { this.waiting.set('Clique em Interagir no OBS para iniciar a reprodução.'); });
      this.videoReady.set(true);
    } else { element.srcObject = null; this.videoReady.set(false); }
  }
  ngOnDestroy() {
    this.destroyed = true;
    this.polling?.unsubscribe();
    if (this.refresh) clearInterval(this.refresh);
    this.peer?.off('videoUpdate', this.onVideoUpdate);
    if (this.meeting) {
      this.meeting.participants.joined.off('participantJoined', this.pickPeer);
      this.meeting.participants.joined.off('participantLeft', this.pickPeer);
      void this.meeting.leave();
    }
  }
}
