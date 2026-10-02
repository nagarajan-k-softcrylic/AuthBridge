import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

// Endpoints that must never trigger a silent-refresh retry (avoids infinite refresh loops
// and refreshing on the auth pages themselves, where a 401 is an expected outcome).
// "/api/auth/me" is included because authGuard/guestGuard call it on every route change to
// check "is there a session"; a 401 there just means "not logged in" and is already handled
// by AuthService.fetchCurrentUser()'s own catchError. Without this exclusion, an unauthenticated
// visit to /login would trigger: me (401) -> interceptor refresh (401) -> navigateByUrl('/login')
// -> guestGuard re-runs -> me (401) -> ... an infinite request loop (most visible when starting
// with zero cookies, e.g. a private browser window or an external OIDC client's login redirect).
const EXCLUDED_PATHS = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh', '/api/auth/logout', '/api/auth/me'];

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
