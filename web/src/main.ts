import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { requestLifecycleInterceptor } from './app/core/api/request-lifecycle.interceptor';
import { AppComponent } from './app/app.component';
import { routes } from './app/routing/routes';

bootstrapApplication(AppComponent, {
  providers: [provideHttpClient(withInterceptors([requestLifecycleInterceptor])), provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' }))]
}).catch(console.error);
