import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, Input, OnChanges, ViewChild } from '@angular/core';

@Component({
  selector: 'app-media-stream-view', standalone: true, imports: [CommonModule],
  template: `<div class="stream-view"><video #video autoplay playsinline [muted]="muted"></video><audio #audio autoplay></audio><span *ngIf="!stream" class="stream-placeholder">{{ placeholder }}</span></div>`,
  styles: [`
    .stream-view { position: relative; width: 100%; height: 100%; min-height: 190px; background: #070b10; overflow: hidden; }
    video { display: block; width: 100%; height: 100%; object-fit: contain; }
    .stream-placeholder { position: absolute; inset: 0; display: grid; place-items: center; color: var(--muted); padding: 20px; text-align: center; }
  `]
})
export class MediaStreamViewComponent implements AfterViewInit, OnChanges {
  @Input() stream: MediaStream | null = null;
  @Input() audioStream: MediaStream | null = null;
  @Input() muted = false;
  @Input() placeholder = 'Aguardando vídeo...';
  @ViewChild('video') video?: ElementRef<HTMLVideoElement>;
  @ViewChild('audio') audio?: ElementRef<HTMLAudioElement>;
  ngAfterViewInit() { this.attach(); }
  ngOnChanges() { queueMicrotask(() => this.attach()); }
  private attach() {
    const video = this.video?.nativeElement, audio = this.audio?.nativeElement;
    if (video && video.srcObject !== this.stream) { video.srcObject = this.stream; if (this.stream) void video.play().catch(() => {}); }
    if (audio && audio.srcObject !== this.audioStream) { audio.srcObject = this.audioStream; if (this.audioStream) void audio.play().catch(() => {}); }
  }
}
