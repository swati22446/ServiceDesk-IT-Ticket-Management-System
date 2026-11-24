import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';
import { ToastService } from './toast.service';

/** Adds "Authorization: Bearer <jwt>" to every call to our own API. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).token();
  if (token && req.url.startsWith(environment.apiUrl)) {
    return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
  }
  return next(req);
};

/** Global error handling: expired sessions and server failures; validation errors are shown by components. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const toast = inject(ToastService);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const isLogin = req.url.includes('/auth/login');
      if (err.status === 401 && !isLogin) {
        toast.warn('Your session has expired. Please sign in again.');
        auth.logout();
      } else if (err.status === 0 || err.status >= 500) {
        toast.error(err.status === 0 ? 'Cannot reach the server.' : 'The server had a problem. Please try again.');
      }
      return throwError(() => err);
    })
  );
};
