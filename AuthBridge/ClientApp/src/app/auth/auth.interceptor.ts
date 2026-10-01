import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// Endpoints that must never trigger a silent-refresh retry (avoids infinite refresh loops
// and refreshing on the auth pages themselves, where a 401 is an expected outcome).
const EXCLUDED_PATHS = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh', '/api/auth/logout'];

/**
 * When an API call fails with 401 (the short-lived access token cookie has expired), this
 * silently calls `/api/auth/refresh` (which reads the longer-lived refresh token cookie) to
 * obtain a new access token, then retries the original request once. If the refresh itself
 * fails, the user is redirected to /login - this is the only place a failed refresh results
 * in a logout, so the user isn't bounced out just because a 60-minute access token expired.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isExcluded = EXCLUDED_PATHS.some((path) => req.url.includes(path));

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isExcluded) {
        return authService.refresh().pipe(
          switchMap((user) => {
            if (!user) {
              router.navigateByUrl('/login');
              return throwError(() => error);
            }
            return next(req.clone({ withCredentials: true }));
          })
        );
      }

      return throwError(() => error);
    })
  );
};
