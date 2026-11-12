import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { pageMetadata, PUBLIC_ORIGIN } from './page-metadata';

@Injectable({ providedIn: 'root' })
export class PageMetadataService {
  private document = inject(DOCUMENT);
  private router = inject(Router);
  private meta = inject(Meta);
  private title = inject(Title);

  constructor() {
    this.router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(event => {
      const path = this.router.parseUrl(event.urlAfterRedirects).root.children['primary']?.segments.map(segment => segment.path).join('/') || '';
      const metadata = pageMetadata('/' + path);
      this.title.setTitle(metadata.title);
      this.meta.updateTag({ name: 'description', content: metadata.description });
      const indexable = metadata.public && this.document.location.hostname === new URL(PUBLIC_ORIGIN).hostname;
      this.meta.updateTag({ name: 'robots', content: indexable ? 'index, follow' : 'noindex, nofollow, noarchive' });
      for (const property of ['og:title', 'og:description']) this.meta.updateTag({ property, content: property === 'og:title' ? metadata.title : metadata.description });
      for (const name of ['twitter:title', 'twitter:description']) this.meta.updateTag({ name, content: name === 'twitter:title' ? metadata.title : metadata.description });
      this.document.querySelector('link[rel="canonical"]')?.remove();
      this.meta.removeTag('property="og:url"');
      if (metadata.public) {
        const canonical = this.document.createElement('link'); canonical.rel = 'canonical'; canonical.href = PUBLIC_ORIGIN + '/'; this.document.head.append(canonical);
        this.meta.updateTag({ property: 'og:url', content: PUBLIC_ORIGIN + '/' });
      }
      if (!metadata.public) this.document.querySelector('script[type="application/ld+json"]')?.remove();
      // Keep keyboard/reader navigation in the newly rendered view without changing scroll restoration.
      this.document.defaultView?.requestAnimationFrame(() => {
        const main = this.document.querySelector<HTMLElement>('main');
        if (main) { main.id = 'main-content'; main.tabIndex = -1; main.focus({ preventScroll: true }); }
      });
    });
  }
}
