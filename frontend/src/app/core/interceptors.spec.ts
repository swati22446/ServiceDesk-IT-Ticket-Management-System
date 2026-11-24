import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor, errorInterceptor } from './interceptors';
import { AuthService } from './auth.service';

describe('HTTP interceptors', () => {
  let http: HttpClient;
  let ctrl: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    http = TestBed.inject(HttpClient);
    ctrl = TestBed.inject(HttpTestingController);
  });

  afterEach(() => ctrl.verify());

  it('adds the bearer token to API calls', () => {
    TestBed.inject(AuthService).login('a@b.c', 'x').subscribe();
    ctrl.expectOne('/api/v1/auth/login').flush({
      token: 'abc', expiresAt: new Date(Date.now() + 60000).toISOString(),
      user: { id: 1, fullName: 'A', email: 'a@b.c', role: 'User', department: null }
    });

    http.get('/api/v1/tickets').subscribe();
    const req = ctrl.expectOne('/api/v1/tickets');
    expect(req.request.headers.get('Authorization')).toBe('Bearer abc');
    req.flush([]);
  });

  it('does not add a token to anonymous calls', () => {
    http.get('/api/v1/lookups/departments').subscribe();
    const req = ctrl.expectOne('/api/v1/lookups/departments');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });
});
