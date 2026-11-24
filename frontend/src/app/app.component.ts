import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { ToastService } from './core/toast.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <nav class="navbar navbar-expand-md navbar-dark bg-dark">
      <div class="container">
        <a class="navbar-brand" routerLink="/">IT Service Desk</a>
        <button class="navbar-toggler" type="button" (click)="open = !open" aria-label="Toggle navigation">
          <span class="navbar-toggler-icon"></span>
        </button>
        <div class="collapse navbar-collapse" [class.show]="open">
          @if (auth.user(); as user) {
            <ul class="navbar-nav me-auto">
              <li class="nav-item"><a class="nav-link" routerLink="/tickets" routerLinkActive="active">Tickets</a></li>
              @if (auth.isStaff()) {
                <li class="nav-item"><a class="nav-link" routerLink="/dashboard" routerLinkActive="active">Dashboard</a></li>
              }
              @if (auth.isAdmin()) {
                <li class="nav-item"><a class="nav-link" routerLink="/admin/users" routerLinkActive="active">Users</a></li>
                <li class="nav-item"><a class="nav-link" routerLink="/admin/categories" routerLinkActive="active">Categories</a></li>
              }
            </ul>
            <span class="navbar-text me-3">{{ user.fullName }} <span class="badge text-bg-secondary">{{ user.role }}</span></span>
            <button class="btn btn-outline-light btn-sm" (click)="auth.logout()">Log out</button>
          } @else {
            <ul class="navbar-nav ms-auto">
              <li class="nav-item"><a class="nav-link" routerLink="/login">Log in</a></li>
              <li class="nav-item"><a class="nav-link" routerLink="/register">Register</a></li>
            </ul>
          }
        </div>
      </div>
    </nav>

    <div class="toast-area">
      @for (t of toast.toasts(); track t.id) {
        <div class="alert alert-{{ t.kind }} alert-dismissible shadow-sm" role="alert">
          {{ t.text }}
          <button type="button" class="btn-close" aria-label="Close" (click)="toast.dismiss(t.id)"></button>
        </div>
      }
    </div>

    <main class="container py-4">
      <router-outlet />
    </main>
  `
})
export class AppComponent {
  auth = inject(AuthService);
  toast = inject(ToastService);
  open = false;
}
