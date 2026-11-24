import { Component, inject, signal } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { Router, RouterLink } from "@angular/router";
import { AuthService } from "../../core/auth.service";
import { errorMessage } from "../../core/utils";

@Component({
  selector: "app-login",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="auth-shell">
      <div class="row g-0">
        <div class="col-md-5">
          <div class="auth-brand">
            <h1 class="mb-3">Report it once. Track it to done.</h1>
            <p>
              Raise an IT request and follow it until it is resolved. Every
              ticket gets a deadline based on its priority:
            </p>
            <ul class="sla-list">
              <li><span>Critical</span><strong>4 hours</strong></li>
              <li><span>High</span><strong>24 hours</strong></li>
              <li><span>Medium</span><strong>48 hours</strong></li>
              <li><span>Low</span><strong>72 hours</strong></li>
            </ul>
          </div>
        </div>
        <div class="col-md-7">
          <div class="auth-form">
            <h2 class="h4 mb-1">Sign in</h2>
            <p class="text-muted small mb-4">Use your service desk account.</p>
            @if (error()) {
            <div class="alert alert-danger py-2">{{ error() }}</div>
            }
            <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
              <div class="mb-3">
                <label class="form-label" for="email">Email</label>
                <input
                  id="email"
                  type="email"
                  class="form-control"
                  formControlName="email"
                  autocomplete="username"
                  [class.is-invalid]="invalid('email')"
                />
                <div class="invalid-feedback">Enter a valid email address.</div>
              </div>
              <div class="mb-3">
                <label class="form-label" for="password">Password</label>
                <input
                  id="password"
                  type="password"
                  class="form-control"
                  formControlName="password"
                  autocomplete="current-password"
                  [class.is-invalid]="invalid('password')"
                />
                <div class="invalid-feedback">Password is required.</div>
              </div>
              <button class="btn btn-primary w-100" [disabled]="busy()">
                {{ busy() ? "Signing in…" : "Sign in" }}
              </button>
            </form>
            <p class="mt-3 mb-4 small">
              No account? <a routerLink="/register">Register</a>
            </p>
            <div class="demo-box">
              Demo accounts (seeded):<br />
              admin&#64;servicedesk.local / Admin&#64;123<br />
              agent&#64;servicedesk.local / Agent&#64;123<br />
              user&#64;servicedesk.local / User&#64;1234
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  busy = signal(false);
  error = signal("");
  form = this.fb.nonNullable.group({
    email: ["", [Validators.required, Validators.email]],
    password: ["", Validators.required],
  });

  invalid(name: "email" | "password") {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set("");
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () =>
        this.router.navigate([this.auth.isStaff() ? "/dashboard" : "/tickets"]),
      error: (err) => {
        this.error.set(errorMessage(err));
        this.busy.set(false);
      },
    });
  }
}
