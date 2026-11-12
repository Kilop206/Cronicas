import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({ standalone: true, imports: [RouterLink], template: `
  <main class="not-found"><a routerLink="/" class="brand"><span class="brand-mark" aria-hidden="true">C</span><span>crônicas</span></a><span class="overline">ERRO AO ABRIR O ARQUIVO</span><h1>Esta página precisa de uma pausa.</h1><p>Não foi possível concluir o carregamento. Tente novamente em instantes.</p><a routerLink="/dashboard" class="button">Voltar ao arquivo</a></main>
` })
export class ErrorPage {}
