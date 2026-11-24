import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { AuthResponse } from './models';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const response = (role: 'User' | 'Agent' | 'Admin', minutes = 30): AuthResponse => ({
    token: 'jwt-token',
    expiresAt: new Date(Date.now() + minutes * 60000).toISOString(),
    user: { id: 1, fullName: 'Test User', email: 't@test.local', role, department: null }
  });

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('stores the session after login and exposes role helpers', () => {
    service.login('t@test.local', 'secret').subscribe();
    http.expectOne('/api/v1/auth/login').flush(response('Agent'));

    expect(service.isLoggedIn()).toBeTrue();
    expect(service.token()).toBe('jwt-token');
    expect(service.isStaff()).toBeTrue();
    expect(service.isAdmin()).toBeFalse();
  });

  it('treats an expired token as logged out', () => {
    service.login('t@test.local', 'secret').subscribe();
    http.expectOne('/api/v1/auth/login').flush(response('User', -5));

    expect(service.isLoggedIn()).toBeFalse();
  });

  it('clears the session on logout', () => {
    service.login('t@test.local', 'secret').subscribe();
    http.expectOne('/api/v1/auth/login').flush(response('Admin'));

    service.logout();

    expect(service.token()).toBeNull();
    expect(sessionStorage.getItem('servicedesk.session')).toBeNull();
  });
});
