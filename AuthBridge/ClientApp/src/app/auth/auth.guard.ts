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
