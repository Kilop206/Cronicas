import { CommonModule } from '@angular/common';
import { Component, inject, OnDestroy, signal, ViewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import RealtimeKitClient from '@cloudflare/realtimekit';
import { RealtimeKitComponentsModule, RtkMeeting } from '@cloudflare/realtimekit-angular-ui';
import { ApiService, apiError } from './api';
import { DicePanelComponent } from './dice-panel.component';
import { ChatPanelComponent } from './chat-panel.component';

type Meeting = Awaited<ReturnType<typeof RealtimeKitClient.init>>;

@Component({ standalone: true, imports: [CommonModule, RouterLink, RealtimeKitComponentsModule, DicePanelComponent, ChatPanelComponent], template: `
  <main class="page media-room"><a [routerLink]="['/campaigns', campaignId]" class="back-link">← Voltar à campanha</a>
    <div class="page-heading"><div><span class="overline">MESA AO VIVO</span><h1>Sala de transmissão</h1><p>Compartilhe telas e ligue sua câmera com o grupo. Use a fonte de portrait na cena do OBS.</p></div></div>
    <div *ngIf="error()" class="notice error" role="alert">{{ error() }}</div><div *ngIf="loading()" class="loading">Preparando a sala...</div>
    <div class="room-stage" [hidden]="!ready()"><rtk-meeting #meetingComponent [showSetupScreen]="false"></rtk-meeting></div>
    <p class="room-note">O navegador solicitará permissão antes de ativar câmera, microfone ou compartilhar a tela. Cada participante controla suas próprias fontes.</p>
    <app-dice-panel [campaignId]="campaignId" />
    <app-chat-panel [campaignId]="campaignId" />
  </main>` })
export class MediaRoomPage implements OnDestroy {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  @ViewChild('meetingComponent') meetingComponent?: RtkMeeting;
  campaignId = this.route.snapshot.paramMap.get('id')!;
  loading = signal(true); ready = signal(false); error = signal('');
  private meeting?: Meeting;
  private destroyed = false;

  constructor() {
    this.api.joinRoom(this.campaignId).subscribe({
      next: async result => {
        try {
          const meeting = await RealtimeKitClient.init({ authToken: result.authToken, defaults: { audio: false, video: false } });
          if (this.destroyed) return;
          this.meeting = meeting;
          await meeting.join();
          if (this.destroyed) { await meeting.leave(); return; }
          if (this.meetingComponent) this.meetingComponent.meeting = meeting;
          this.ready.set(true);
        } catch { if (!this.destroyed) this.error.set('Não foi possível abrir a sala de mídia. Tente novamente mais tarde.'); }
        finally { this.loading.set(false); }
      },
      error: error => { if (!this.destroyed) this.error.set(apiError(error)); this.loading.set(false); }
    });
  }
  ngOnDestroy() { this.destroyed = true; void this.meeting?.leave(); }
}
