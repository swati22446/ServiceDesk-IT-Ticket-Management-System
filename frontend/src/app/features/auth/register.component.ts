import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Department } from '../../core/models';
import { errorMessage } from '../../core/utils';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="row justify-content-center">
      <div class="col-md-6">
        <div class="card p-4">
          <h2 class="h4 mb-3">Create an account</h2>
          @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }
          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <div class="mb-3">
              <label class="form-label" for="fullName">Full name</label>
              <input id="fullName" class="form-control" formControlName="fullName" [class.is-invalid]="invalid('fullName')">
              <div class="invalid-feedback">Name must be 2–100 characters.</div>
            </div>
            <div class="mb-3">
              <label class="form-label" for="email">Email</label>
              <input id="email" type="email" class="form-control" formControlName="email" [class.is-invalid]="invalid('email')">
              <div class="invalid-feedback">Enter a valid email address.</div>
            </div>
            <div class="mb-3">
              <label class="form-label" for="password">Password</label>
              <input id="password" type="password" class="form-control" formControlName="password" autocomplete="new-password"
                     [class.is-invalid]="invalid('password')">
              <div class="invalid-feedback">At least 8 characters with upper-case, lower-case and a digit.</div>
            </div>
            <div class="mb-3">
              <label class="form-label" for="dept">Department (optional)</label>
              <select id="dept" class="form-select" formControlName="departmentId">
                <option [ngValue]="null">— none —</option>
                @for (d of departments(); track d.id) { <option [ngValue]="d.id">{{ d.name }}</option> }
              </select>
            </div>
            <button class="btn btn-primary w-100" [disabled]="busy()">{{ busy() ? 'Creating…' : 'Register' }}</button>
          </form>
          <p class="mt-3 mb-0 small">Already registered? <a routerLink="/login">Sign in</a></p>
        </div>
      </div>
    </div>
  `
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private api = inject(ApiService);
  private router = inject(Router);

  busy = signal(false);
  error = signal('');
  departments = signal<Department[]>([]);

  form = this.fb.group({
    fullName: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    email: this.fb.nonNullable.control('', [Validators.required, Validators.email]),
    password: this.fb.nonNullable.control('', [
      Validators.required, Validators.minLength(8), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/)
    ]),
    departmentId: this.fb.control<number | null>(null)
  });

  constructor() {
    this.api.departments().subscribe({ next: d => this.departments.set(d), error: () => undefined });
  }

  invalid(name: 'fullName' | 'email' | 'password') {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  submit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    this.error.set('');
    const v = this.form.getRawValue();
    this.auth.register({
      fullName: v.fullName, email: v.email, password: v.password, departmentId: v.departmentId
    }).subscribe({
      next: () => this.router.navigate(['/tickets']),
      error: err => { this.error.set(errorMessage(err)); this.busy.set(false); }
    });
  }
}
