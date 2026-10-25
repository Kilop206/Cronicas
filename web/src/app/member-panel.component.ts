import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService, CampaignMember, apiError } from './api';

@Component({ selector: 'app-member-panel', standalone: true, imports: [CommonModule, FormsModule], template: `
  <section class="editor-panel member-panel"><div class="panel-heading"><span>MESA</span><h2>Participantes</h2></div>
    <p *ngIf="error()" class="form-error" role="alert">{{ error() }}</p>
    <div *ngFor="let member of members()" class="member-row"><span class="avatar">{{ member.displayName.slice(0, 2).toUpperCase() }}</span>
      <div class="member-identity"><strong>{{ member.displayName }}</strong><small>Desde {{ member.joinedAt | date:'dd/MM/yyyy' }}</small></div>
      <span *ngIf="role !== 'owner' || member.role === 'owner'" class="role-badge">{{ roleLabel(member.role) }}</span>
      <select *ngIf="role === 'owner' && member.role !== 'owner'" [name]="'role-' + member.userId" [ngModel]="member.role" (ngModelChange)="setRole(member, $event)" [disabled]="busy()" [attr.aria-label]="'Papel de ' + member.displayName"><option value="gm">Mestre</option><option value="co-gm">Co-mestre</option><option value="player">Jogador</option><option value="spectator">Espectador</option></select>
      <button *ngIf="role === 'owner' && member.role !== 'owner'" type="button" class="text-link danger" [disabled]="busy()" (click)="remove(member)">Remover</button>
      <button *ngIf="role !== 'owner' && member.userId === api.user()?.id" type="button" class="text-link danger" [disabled]="busy()" (click)="remove(member)">Sair</button>
    </div>
  </section>` })
export class MemberPanelComponent implements OnInit {
  @Input({ required: true }) campaignId = ''; @Input() role = 'player';
  api = inject(ApiService); private router = inject(Router); members = signal<CampaignMember[]>([]); busy = signal(false); error = signal('');
  ngOnInit() { this.load(); }
  load() { this.api.members(this.campaignId).subscribe({ next: items => this.members.set(items), error: e => this.error.set(apiError(e)) }); }
  roleLabel(role: string) { return ({ owner: 'Proprietário', gm: 'Mestre', 'co-gm': 'Co-mestre', player: 'Jogador', spectator: 'Espectador' } as Record<string, string>)[role] || role; }
  setRole(member: CampaignMember, role: string) { if (role === member.role) return; this.busy.set(true); this.error.set(''); this.api.changeMemberRole(this.campaignId, member.userId, role).subscribe({ next: () => { this.busy.set(false); this.load(); }, error: e => { this.busy.set(false); this.error.set(apiError(e)); this.load(); } }); }
  remove(member: CampaignMember) { if (!confirm(`Remover ${member.displayName} da campanha?`)) return; this.busy.set(true); this.error.set(''); this.api.removeMember(this.campaignId, member.userId).subscribe({ next: () => { this.busy.set(false); if (member.userId === this.api.user()?.id) void this.router.navigateByUrl('/campaigns'); else this.load(); }, error: e => { this.busy.set(false); this.error.set(apiError(e)); } }); }
}
