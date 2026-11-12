import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({ selector: 'app-site-footer', standalone: true, imports: [RouterLink], template: `
  <footer class="site-footer"><span>© {{ year }} Crônicas</span><nav aria-label="Informações legais"><a routerLink="/privacy">Privacidade</a><a routerLink="/terms">Termos de Uso</a><a routerLink="/cookies">Cookies e privacidade</a></nav></footer>
` })
export class SiteFooterComponent { year = new Date().getFullYear(); }
