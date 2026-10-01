import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  succeeded: boolean;
  token?: string;
  expiresAtUtc?: string;
  userId?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
  requiresMfa: boolean;
  errors: string[];
}

export interface AuthOptions {
  ssoEnabled: boolean;
}

export interface AuthUser {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
}

/**
 * The JWT itself is never exposed to JavaScript: the backend issues it as an httpOnly,
 * Secure cookie (see AuthController), so the browser attaches it automatically and no
 * client-side code can read or exfiltrate it (mitigates XSS token theft). This service
 * only keeps the non-sensitive, display-only user profile in memory - never in
 * localStorage/sessionStorage - so it's cleared on tab close/refresh and is re-fetched
 * from `/api/auth/me` (which reads the cookie) whenever the app needs to re-establish it.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly baseUrl = '/api/auth';
  private currentUser: AuthUser | null = null;

  constructor(private http: HttpClient) {}

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/register`, request, { withCredentials: true })
      .pipe(tap((res) => this.cacheUser(res)));
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.baseUrl}/login`, request, { withCredentials: true })
      .pipe(tap((res) => this.cacheUser(res)));
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => (this.currentUser = null))
    );
  }

  /**
   * Exchanges the httpOnly refresh-token cookie for a new access token (silent renewal).
   * Called by the auth interceptor when an API call fails with 401 (expired access token),
   * so the user isn't logged out just because the short-lived access token expired.
   */
  refresh(): Observable<AuthUser | null> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/refresh`, {}, { withCredentials: true }).pipe(
      map((res) => {
        this.cacheUser(res);
        return this.currentUser;
      }),
      catchError(() => {
        this.currentUser = null;
        return of(null);
      })
    );
  }

  getAuthOptions(): Observable<AuthOptions> {
    return this.http.get<AuthOptions>(`${this.baseUrl}/options`);
  }

  /**
   * Standalone SSO login: full-page redirect into the Microsoft Entra ID Authorization Code
   * flow. The optional email is passed through as a `login_hint` so Microsoft's login page
   * targets that account directly.
   */
  ssoLoginUrl(email?: string): string {
    const params = email ? `?email=${encodeURIComponent(email)}` : '';
    return `${this.baseUrl}/sso-login${params}`;
  }

  /** Returns the cached user if known, otherwise asks the server (cookie-based). */
  fetchCurrentUser(): Observable<AuthUser | null> {
    if (this.currentUser) {
      return of(this.currentUser);
    }

    return this.http.get<AuthResponse>(`${this.baseUrl}/me`, { withCredentials: true }).pipe(
      map((res) => {
        this.cacheUser(res);
        return this.currentUser;
      }),
      catchError(() => of(null))
    );
  }

  getUser(): AuthUser | null {
    return this.currentUser;
  }

  private cacheUser(res: AuthResponse): void {
    if (res.succeeded) {
      this.currentUser = {
        userId: res.userId ?? '',
        email: res.email ?? '',
        firstName: res.firstName ?? '',
        lastName: res.lastName ?? ''
      };
    }
  }
}
