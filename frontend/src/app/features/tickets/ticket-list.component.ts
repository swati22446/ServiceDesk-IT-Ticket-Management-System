import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { Category, PRIORITIES, PagedResult, STATUSES, Ticket, TicketQuery } from '../../core/models';
import { priorityBadge, saveBlob, statusBadge } from '../../core/utils';

@Component({
  selector: 'app-ticket-list',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe],
  template: `
    <div class="d-flex justify-content-between align-items-center mb-3">
      <h1 class="h3 mb-0">{{ auth.isStaff() ? 'All tickets' : 'My tickets' }}</h1>
      <div class="d-flex gap-2">
        @if (auth.isStaff()) {
          <button class="btn btn-outline-success" (click)="exportExcel()" [disabled]="exporting()">
            {{ exporting() ? 'Exporting…' : 'Export Excel' }}
          </button>
        }
        <a class="btn btn-primary" routerLink="/tickets/new">+ New ticket</a>
      </div>
    </div>

    <div class="card p-3 mb-3">
      <div class="row g-2">
        <div class="col-md-4">
          <input class="form-control" placeholder="Search title or description…" [(ngModel)]="query.search" (keyup.enter)="reload(true)">
        </div>
        <div class="col-md-2">
          <select class="form-select" [(ngModel)]="query.status" (change)="reload(true)" aria-label="Status">
            <option value="">All statuses</option>
            @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
          </select>
        </div>
        <div class="col-md-2">
          <select class="form-select" [(ngModel)]="query.priority" (change)="reload(true)" aria-label="Priority">
            <option value="">All priorities</option>
            @for (p of priorities; track p) { <option [value]="p">{{ p }}</option> }
          </select>
        </div>
        <div class="col-md-2">
          <select class="form-select" [(ngModel)]="query.categoryId" (change)="reload(true)" aria-label="Category">
            <option [ngValue]="null">All categories</option>
            @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
          </select>
        </div>
        <div class="col-md-2 d-flex gap-2">
          <button class="btn btn-secondary flex-fill" (click)="reload(true)">Search</button>
          <button class="btn btn-outline-secondary" (click)="clear()" title="Clear filters">✕</button>
        </div>
      </div>
      @if (auth.isStaff()) {
        <div class="form-check mt-2">
          <input class="form-check-input" type="checkbox" id="overdue" [(ngModel)]="overdue" (change)="reload(true)">
          <label class="form-check-label" for="overdue">Overdue only</label>
        </div>
      }
    </div>

    <div class="card">
      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th class="sortable" (click)="sort('id')">#</th>
              <th class="sortable" (click)="sort('title')">Title {{ arrow('title') }}</th>
              <th class="sortable" (click)="sort('status')">Status {{ arrow('status') }}</th>
              <th class="sortable" (click)="sort('priority')">Priority {{ arrow('priority') }}</th>
              <th>Category</th>
              @if (auth.isStaff()) { <th>Requester</th> }
              <th>Assigned</th>
              <th class="sortable" (click)="sort('dueAt')">Due {{ arrow('dueAt') }}</th>
            </tr>
          </thead>
          <tbody>
            @for (t of result()?.items ?? []; track t.id) {
              <tr>
                <td>{{ t.id }}</td>
                <td><a [routerLink]="['/tickets', t.id]">{{ t.title }}</a></td>
                <td><span class="badge" [class]="badge(t.status)">{{ t.status }}</span></td>
                <td><span class="badge" [class]="pBadge(t.priority)">{{ t.priority }}</span></td>
                <td>{{ t.category }}</td>
                @if (auth.isStaff()) { <td>{{ t.createdBy }}</td> }
                <td>{{ t.assignedTo ?? '—' }}</td>
                <td [class.text-danger]="t.isOverdue" [class.fw-bold]="t.isOverdue">
                  {{ t.dueAt | date: 'dd MMM, HH:mm' }} @if (t.isOverdue) { <small>(overdue)</small> }
                </td>
              </tr>
            } @empty {
              <tr><td [attr.colspan]="auth.isStaff() ? 8 : 7" class="text-center text-muted py-4">
                {{ loading() ? 'Loading…' : 'No tickets found.' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (result(); as r) {
        <div class="d-flex justify-content-between align-items-center p-3">
          <small class="text-muted">{{ r.total }} ticket(s) · page {{ r.page }} of {{ r.totalPages || 1 }}</small>
          <div class="btn-group">
            <button class="btn btn-outline-secondary btn-sm" [disabled]="r.page <= 1" (click)="go(r.page - 1)">Previous</button>
            <button class="btn btn-outline-secondary btn-sm" [disabled]="r.page >= r.totalPages" (click)="go(r.page + 1)">Next</button>
          </div>
        </div>
      }
    </div>
  `
})
export class TicketListComponent {
  private api = inject(ApiService);
  private toast = inject(ToastService);
  auth = inject(AuthService);

  statuses = STATUSES;
  priorities = PRIORITIES;
  categories = signal<Category[]>([]);
  result = signal<PagedResult<Ticket> | null>(null);
  loading = signal(false);
  exporting = signal(false);
  overdue = false;

  query: TicketQuery = { search: '', status: '', priority: '', categoryId: null, sortBy: 'createdAt', desc: true, page: 1, pageSize: 10 };

  badge = statusBadge;
  pBadge = priorityBadge;

  constructor() {
    this.api.categories().subscribe(c => this.categories.set(c));
    this.reload();
  }

  reload(resetPage = false) {
    if (resetPage) this.query.page = 1;
    this.loading.set(true);
    this.api.listTickets(this.currentQuery()).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  clear() {
    this.query = { ...this.query, search: '', status: '', priority: '', categoryId: null, page: 1 };
    this.overdue = false;
    this.reload();
  }

  sort(column: string) {
    if (column === 'id') column = 'createdAt';
    this.query.desc = this.query.sortBy === column ? !this.query.desc : false;
    this.query.sortBy = column;
    this.reload(true);
  }

  arrow(column: string) { return this.query.sortBy === column ? (this.query.desc ? '▼' : '▲') : ''; }

  go(page: number) { this.query.page = page; this.reload(); }

  exportExcel() {
    this.exporting.set(true);
    this.api.exportExcel(this.currentQuery()).subscribe({
      next: blob => { saveBlob(blob, 'tickets.xlsx'); this.exporting.set(false); },
      error: () => { this.toast.error('Export failed.'); this.exporting.set(false); }
    });
  }

  private currentQuery(): TicketQuery {
    return { ...this.query, overdueOnly: this.overdue || undefined };
  }
}
