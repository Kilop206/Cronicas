import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError, timeout } from 'rxjs';
import { ApiService } from './api.service';

export const requestLifecycleInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) return next(request);
  const api = inject(ApiService);
  // Render Free may take a minute to wake. Never retry a mutation automatically.
  return next(request).pipe(timeout(90000), catchError(error => {
    if (error instanceof HttpErrorResponse && error.status === 401 && !/^\/api\/auth\/(?:login|register|password-reset\/)/.test(request.url) && api.user()) api.expireSession();
    return throwError(() => error);
  }));
};
