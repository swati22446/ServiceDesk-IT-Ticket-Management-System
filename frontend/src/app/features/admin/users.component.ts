import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { Role, UserSummary } from '../../core/models';
import { errorMessage } from '../../core/utils';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <h1 class="h3 mb-3">User management</h1>
    <div class="card">
      <div class="table-responsive">
        <table class="table align-middle mb-0">
          <thead class="table-light">
            <tr><th>Name</th><th>Email</th><th>Department</th><th>Role</th><th>Joined</th><th>Active</th></tr>
          </thead>
          <tbody>
            @for (u of users(); track u.id) {
              <tr [class.text-muted]="!u.isActive">
                <td>{{ u.fullName }}</td>
                <td>{{ u.email }}</td>
                <td>{{ u.department ?? '—' }}</td>
                <td>
                  <select class="form-select form-select-sm" style="width: 120px" [ngModel]="u.role"
                          (ngModelChange)="changeRole(u, $event)" [disabled]="u.id === me()?.id" [attr.aria-label]="'Role of ' + u.fullName">
                    @for (r of roles; track r) { <option [value]="r">{{ r }}</option> }
                  </select>
                </td>
                <td>{{ u.createdAt | date: 'dd MMM yyyy' }}</td>
                <td>
                  <div class="form-check form-switch">
                    <input class="form-check-input" type="checkbox" role="switch" [checked]="u.isActive"
                           [disabled]="u.id === me()?.id" (change)="toggle(u)" [attr.aria-label]="'Active: ' + u.fullName">
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class UsersComponent {
  private api = inject(ApiService);
  private toast = inject(ToastService);
  me = inject(AuthService).user;

  roles: Role[] = ['User', 'Agent', 'Admin'];
  users = signal<UserSummary[]>([]);

  constructor() { this.load(); }

  private load() { this.api.users().subscribe(u => this.users.set(u)); }

  changeRole(u: UserSummary, role: Role) {
    this.api.changeRole(u.id, role).subscribe({
      next: () => { this.toast.success(`${u.fullName} is now ${role}.`); this.load(); },
      error: err => { this.toast.error(errorMessage(err)); this.load(); }
    });
  }

  toggle(u: UserSummary) {
    this.api.setActive(u.id, !u.isActive).subscribe({
      next: () => { this.toast.success(`${u.fullName} ${u.isActive ? 'deactivated' : 'activated'}.`); this.load(); },
      error: err => { this.toast.error(errorMessage(err)); this.load(); }
    });
  }
}
