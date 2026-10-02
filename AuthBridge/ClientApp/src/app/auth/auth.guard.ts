import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * The JWT lives in an httpOnly cookie the client can't read directly, so "is logged in"
 * is determined by asking the server (`/api/auth/me`, which validates the cookie) rather
 * than checking local/session storage.
 */
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.fetchCurrentUser().pipe(
    map((user) => (user ? true : router.createUrlTree(['/login'])))
  );
};

/**
 * Keeps already-authenticated users off the login/register pages: if a valid session cookie
 * still exists (e.g. the user navigates back to /login, or hits it directly after a previous
 * sign-in), they're sent straight to /my-applications instead of seeing the login form again.
 */
export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.fetchCurrentUser().pipe(
    map((user) => (user ? router.createUrlTree(['/my-applications']) : true))
  );
};

/** Restricts a route to signed-in users holding the "ApplicationAdmin" role (e.g. Application Search). */
export const applicationAdminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.fetchCurrentUser().pipe(
    map((user) => {
      if (!user) {
        return router.createUrlTree(['/login']);
      }

      return authService.isApplicationAdmin() ? true : router.createUrlTree(['/my-applications']);
    })
  );
};
