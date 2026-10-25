import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subscription, timer } from 'rxjs';
import { ApiService, Character, apiError } from './api';
import { MeshRoom, RoomPeer } from './mesh-room';
import { OverlayCardComponent } from './studio-pages';

@Component({ standalone: true, imports: [CommonModule, OverlayCardComponent], template: `
  <main class="obs-camera-page"><div class="obs-video-stage"><video #video autoplay playsinline muted></video><div *ngIf="!videoReady()" class="obs-video-empty">{{ waiting() }}</div></div>
    <app-overlay-card *ngIf="character() as item" [character]="item" />
    <p *ngIf="error()" class="obs-camera-error" role="alert">{{ error() }}</p>
  </main>` })
export class ObsCameraPage implements AfterViewInit, OnDestroy {
  private api = inject(ApiService); private route = inject(ActivatedRoute);
  @ViewChild('video') video?: ElementRef<HTMLVideoElement>;
  private campaignId = this.route.snapshot.paramMap.get('campaignId')!;
  private characterId = this.route.snapshot.paramMap.get('characterId')!;
  private room?: MeshRoom; private destroyed = false; private polling?: Subscription;
  private peers: RoomPeer[] = []; private cameraStreams = new Map<string, MediaStream>();
  character = signal<Character | null>(null); videoReady = signal(false);
  waiting = signal('Aguardando a câmera do participante...'); error = signal('');

  constructor() {
    this.api.character(this.characterId).subscribe({
      next: character => {
        if (character.campaignId !== this.campaignId) { this.error.set('Personagem fora desta campanha.'); return; }
        this.character.set(character);
        this.polling = timer(3000, 3000).subscribe(() => this.api.character(this.characterId).subscribe({ next: item => this.character.set(item) }));
        this.room = new MeshRoom(this.campaignId, 'obs', {
          peers: peers => { this.peers = peers; this.pickCamera(); },
          track: (id, kind, stream) => { if (kind !== 'camera') return; if (stream) this.cameraStreams.set(id, stream); else this.cameraStreams.delete(id); this.pickCamera(); },
          status: message => this.waiting.set(message),
          revoked: () => this.error.set('O acesso à campanha mudou. Atualize esta fonte no OBS.')
        });
        void this.room.connect().catch(() => { if (!this.destroyed) this.error.set('Não foi possível conectar a fonte à sala.'); });
      },
      error: error => this.error.set(apiError(error))
    });
  }

  private pickCamera() {
    const ownerId = this.character()?.ownerId;
    const peer = this.peers.find(x => x.userId === ownerId && x.mode === 'room' && x.cameraEnabled && this.cameraStreams.has(x.connectionId));
    const stream = peer ? this.cameraStreams.get(peer.connectionId) ?? null : null;
    const element = this.video?.nativeElement;
    if (element && element.srcObject !== stream) { element.srcObject = stream; if (stream) void element.play().catch(() => this.waiting.set('Clique em Interagir no OBS para iniciar a reprodução.')); }
    this.videoReady.set(!!stream && !!element);
    if (!stream) this.waiting.set('Aguardando a câmera do participante...');
  }

  ngAfterViewInit() { this.pickCamera(); }
  ngOnDestroy() { this.destroyed = true; this.polling?.unsubscribe(); void this.room?.close(); }
}
