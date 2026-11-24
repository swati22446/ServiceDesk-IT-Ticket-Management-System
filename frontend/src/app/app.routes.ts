import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tickets' },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent)
  },
  {
    path: 'tickets',
    canActivate: [authGuard],
    loadComponent: () => import('./features/tickets/ticket-list.component').then(m => m.TicketListComponent)
  },
  {
    path: 'tickets/new',
    canActivate: [authGuard],
    loadComponent: () => import('./features/tickets/ticket-form.component').then(m => m.TicketFormComponent)
  },
  {
    path: 'tickets/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/tickets/ticket-detail.component').then(m => m.TicketDetailComponent)
  },
  {
    path: 'tickets/:id/edit',
    canActivate: [authGuard],
    loadComponent: () => import('./features/tickets/ticket-form.component').then(m => m.TicketFormComponent)
  },
  {
    path: 'dashboard',
    canActivate: [authGuard, roleGuard('Agent', 'Admin')],
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
  },
  {
    path: 'admin/users',
    canActivate: [authGuard, roleGuard('Admin')],
    loadComponent: () => import('./features/admin/users.component').then(m => m.UsersComponent)
  },
  {
    path: 'admin/categories',
    canActivate: [authGuard, roleGuard('Admin')],
    loadComponent: () => import('./features/admin/categories.component').then(m => m.CategoriesComponent)
  },
  { path: '**', redirectTo: 'tickets' }
];
