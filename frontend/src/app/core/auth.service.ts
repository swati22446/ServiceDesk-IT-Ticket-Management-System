import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, Role } from './models';

const STORAGE_KEY = 'servicedesk.session';

/**
 * Holds the signed-in session as a signal (service-based state).
 * The JWT lives in sessionStorage: it disappears when the tab closes. (localStorage would survive
 * restarts but is easier to steal through XSS - a trade-off worth mentioning in the review.)
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private readonly session = signal<AuthResponse | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isStaff = computed(() => this.hasRole('Agent', 'Admin'));
  readonly isAdmin = computed(() => this.hasRole('Admin'));

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap(res => this.store(res)));
  }

  register(payload: { fullName: string; email: string; password: string; departmentId: number | null }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/register`, payload)
      .pipe(tap(res => this.store(res)));
  }

  logout(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
    this.router.navigate(['/login']);
  }

  isLoggedIn(): boolean {
    const s = this.session();
    if (!s) return false;
    if (new Date(s.expiresAt).getTime() <= Date.now()) {
      sessionStorage.removeItem(STORAGE_KEY);
      this.session.set(null);
      return false;
    }
    return true;
  }

  hasRole(...roles: Role[]): boolean {
    const role = this.user()?.role;
    return !!role && roles.includes(role);
  }

  private store(res: AuthResponse): void {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(res));
    this.session.set(res);
  }

  private restore(): AuthResponse | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as AuthResponse) : null;
    } catch {
      return null;
    }
  }
}
