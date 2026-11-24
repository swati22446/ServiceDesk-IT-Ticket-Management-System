import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../core/toast.service';
import { Category, PRIORITIES, TicketPriority } from '../../core/models';
import { errorMessage } from '../../core/utils';

/** Used for both "new ticket" (no id in the route) and "edit ticket" (/tickets/:id/edit). */
@Component({
  selector: 'app-ticket-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="row justify-content-center">
      <div class="col-lg-8">
        <div class="card p-4">
          <h1 class="h4 mb-3">{{ id() ? 'Edit ticket #' + id() : 'New ticket' }}</h1>
          @if (error()) { <div class="alert alert-danger py-2">{{ error() }}</div> }
          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <div class="mb-3">
              <label class="form-label" for="title">Title</label>
              <input id="title" class="form-control" formControlName="title" maxlength="150" [class.is-invalid]="invalid('title')">
              <div class="invalid-feedback">Title must be 5–150 characters.</div>
            </div>
            <div class="mb-3">
              <label class="form-label" for="description">Description</label>
              <textarea id="description" rows="6" class="form-control" formControlName="description" maxlength="4000"
                        [class.is-invalid]="invalid('description')"></textarea>
              <div class="invalid-feedback">Describe the problem in at least 10 characters.</div>
            </div>
            <div class="row">
              <div class="col-md-6 mb-3">
                <label class="form-label" for="category">Category</label>
                <select id="category" class="form-select" formControlName="categoryId" [class.is-invalid]="invalid('categoryId')">
                  <option [ngValue]="0">Select a category…</option>
                  @for (c of categories(); track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
                </select>
                <div class="invalid-feedback">Please choose a category.</div>
              </div>
              <div class="col-md-6 mb-3">
                <label class="form-label" for="priority">Priority</label>
                <select id="priority" class="form-select" formControlName="priority">
                  @for (p of priorities; track p) { <option [value]="p">{{ p }}</option> }
                </select>
                <div class="form-text">Critical: 4h · High: 24h · Medium: 48h · Low: 72h target resolution.</div>
              </div>
            </div>
            <div class="d-flex gap-2">
              <button class="btn btn-primary" [disabled]="busy()">{{ busy() ? 'Saving…' : 'Save' }}</button>
              <a class="btn btn-outline-secondary" [routerLink]="id() ? ['/tickets', id()] : ['/tickets']">Cancel</a>
            </div>
          </form>
        </div>
      </div>
    </div>
  `
})
export class TicketFormComponent implements OnInit {
  /** Bound from the route parameter thanks to withComponentInputBinding(). */
  id = input<string>();

  private fb = inject(FormBuilder);
  private api = inject(ApiService);
  private router = inject(Router);
  private toast = inject(ToastService);

  priorities = PRIORITIES;
  categories = signal<Category[]>([]);
  busy = signal(false);
  error = signal('');
  private rowVersion = '';

  form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(150)]],
    description: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(4000)]],
    categoryId: [0, [Validators.required, Validators.min(1)]],
    priority: ['Medium' as TicketPriority, Validators.required]
  });

  ngOnInit() {
    this.api.categories().subscribe(c => this.categories.set(c));
    this.loadIfEditing();
  }

  private loadIfEditing() {
    const id = this.id();
    if (!id) return;
    this.api.getTicket(+id).subscribe({
      next: t => {
        this.rowVersion = t.rowVersion;
        this.form.patchValue({ title: t.title, description: t.description, categoryId: t.categoryId, priority: t.priority });
      },
      error: err => this.error.set(errorMessage(err))
    });
  }

  invalid(name: 'title' | 'description' | 'categoryId') {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  submit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    this.error.set('');
    const v = this.form.getRawValue();
    const id = this.id();
    const request$ = id
      ? this.api.updateTicket(+id, { ...v, rowVersion: this.rowVersion })
      : this.api.createTicket(v);
    request$.subscribe({
      next: t => {
        this.toast.success(id ? 'Ticket updated.' : 'Ticket created.');
        this.router.navigate(['/tickets', t.id]);
      },
      error: err => { this.error.set(errorMessage(err)); this.busy.set(false); }
    });
  }
}
