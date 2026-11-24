import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/utils';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="row justify-content-center">
      <div class="col-md-5">
        <div class="card p-4">
          <h2 class="h4 mb-3">Sign in</h2>
          @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }
          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <div class="mb-3">
              <label class="form-label" for="email">Email</label>
              <input id="email" type="email" class="form-control" formControlName="email" autocomplete="username"
                     [class.is-invalid]="invalid('email')">
              <div class="invalid-feedback">Enter a valid email address.</div>
            </div>
            <div class="mb-3">
              <label class="form-label" for="password">Password</label>
              <input id="password" type="password" class="form-control" formControlName="password" autocomplete="current-password"
                     [class.is-invalid]="invalid('password')">
              <div class="invalid-feedback">Password is required.</div>
            </div>
            <button class="btn btn-primary w-100" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>
          </form>
          <p class="mt-3 mb-0 small">No account? <a routerLink="/register">Register</a></p>
          <hr>
          <p class="small text-muted mb-0">Demo accounts (seeded): admin&#64;servicedesk.local / Admin&#64;123,
            agent&#64;servicedesk.local / Agent&#64;123, user&#64;servicedesk.local / User&#64;1234</p>
        </div>
      </div>
    </div>
  `
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  busy = signal(false);
  error = signal('');
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  invalid(name: 'email' | 'password') {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  submit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    this.error.set('');
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigate([this.auth.isStaff() ? '/dashboard' : '/tickets']),
      error: err => { this.error.set(errorMessage(err)); this.busy.set(false); }
    });
  }
}
