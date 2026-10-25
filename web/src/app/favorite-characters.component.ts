import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FavoriteCharacter } from './api';

@Component({ selector: 'app-favorite-characters', standalone: true, imports: [CommonModule, RouterLink], template: `
  <section *ngIf="characters.length" class="favorite-section"><div class="section-heading"><div><span class="overline">ACESSO RÁPIDO</span><h2>Personagens favoritos</h2></div></div>
    <div class="favorite-list"><a *ngFor="let item of characters" [routerLink]="['/characters', item.characterId]" class="favorite-row"><span class="character-thumb"><img *ngIf="item.portraitUrl" [src]="item.portraitUrl" alt=""></span><span class="favorite-name"><strong>{{ item.name }}</strong><small>{{ item.campaignName }}</small></span><span class="favorite-health">Vida {{ item.health }}/{{ item.maxHealth }}</span><span aria-hidden="true">↗</span></a></div>
  </section>` })
export class FavoriteCharactersComponent { @Input({ required: true }) characters: FavoriteCharacter[] = []; }
