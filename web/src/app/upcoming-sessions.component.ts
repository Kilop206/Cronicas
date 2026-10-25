import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UpcomingSession } from './api';

@Component({ selector: 'app-upcoming-sessions', standalone: true, imports: [CommonModule, RouterLink], template: `
  <section *ngIf="sessions.length" class="upcoming-section"><div class="section-heading"><div><span class="overline">AGENDA DA MESA</span><h2>Próximas sessões</h2></div></div>
    <div class="upcoming-list"><a *ngFor="let item of sessions" [routerLink]="['/campaigns', item.campaignId, 'sessions']" [queryParams]="{ entry: item.id }" class="upcoming-row"><time [attr.datetime]="item.eventAt"><strong>{{ item.eventAt | date:'dd' }}</strong><small>{{ item.eventAt | date:'MMM' }}</small></time><span><strong>{{ item.title }}</strong><small>{{ item.campaignName }} · {{ item.eventAt | date:'HH:mm' }}</small></span><b aria-hidden="true">↗</b></a></div>
  </section>` })
export class UpcomingSessionsComponent { @Input({ required: true }) sessions: UpcomingSession[] = []; }
