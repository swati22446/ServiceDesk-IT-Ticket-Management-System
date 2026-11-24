import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ToastService } from '../../core/toast.service';
import { Category } from '../../core/models';
import { errorMessage } from '../../core/utils';

@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <h1 class="h3 mb-3">Ticket categories</h1>
    <div class="row g-3">
      <div class="col-lg-7">
        <div class="card">
          <table class="table align-middle mb-0">
            <thead class="table-light"><tr><th>Name</th><th>Description</th><th class="text-end">Actions</th></tr></thead>
            <tbody>
              @for (c of categories(); track c.id) {
                <tr>
                  <td>{{ c.name }}</td>
                  <td class="text-muted">{{ c.description }}</td>
                  <td class="text-end">
                    <button class="btn btn-sm btn-outline-primary me-1" (click)="edit(c)">Edit</button>
                    <button class="btn btn-sm btn-outline-danger" (click)="remove(c)">Delete</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
      <div class="col-lg-5">
        <div class="card p-3">
          <h2 class="h6">{{ editingId() ? 'Edit category' : 'Add category' }}</h2>
          <form [formGroup]="form" (ngSubmit)="save()" novalidate>
            <div class="mb-2">
              <label class="form-label small" for="name">Name</label>
              <input id="name" class="form-control" formControlName="name" [class.is-invalid]="form.controls.name.invalid && form.controls.name.touched">
              <div class="invalid-feedback">Name must be 2–80 characters.</div>
            </div>
            <div class="mb-3">
              <label class="form-label small" for="desc">Description</label>
              <input id="desc" class="form-control" formControlName="description" maxlength="250">
            </div>
            <div class="d-flex gap-2">
              <button class="btn btn-primary btn-sm">{{ editingId() ? 'Update' : 'Add' }}</button>
              @if (editingId()) { <button type="button" class="btn btn-outline-secondary btn-sm" (click)="reset()">Cancel</button> }
            </div>
          </form>
        </div>
      </div>
    </div>
  `
})
export class CategoriesComponent {
  private api = inject(ApiService);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);

  categories = signal<Category[]>([]);
  editingId = signal<number | null>(null);

  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    description: ['']
  });

  constructor() { this.load(); }

  private load() { this.api.categories().subscribe(c => this.categories.set(c)); }

  edit(c: Category) {
    this.editingId.set(c.id);
    this.form.setValue({ name: c.name, description: c.description ?? '' });
  }

  reset() {
    this.editingId.set(null);
    this.form.reset({ name: '', description: '' });
  }

  save() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const { name, description } = this.form.getRawValue();
    const body = { name, description: description || null };
    const id = this.editingId();
    const request$ = id ? this.api.updateCategory(id, body) : this.api.createCategory(body);
    request$.subscribe({
      next: () => { this.toast.success(id ? 'Category updated.' : 'Category added.'); this.reset(); this.load(); },
      error: err => this.toast.error(errorMessage(err))
    });
  }

  remove(c: Category) {
    if (!confirm(`Delete category "${c.name}"?`)) return;
    this.api.deleteCategory(c.id).subscribe({
      next: () => { this.toast.success('Category deleted.'); this.load(); },
      error: err => this.toast.error(errorMessage(err))
    });
  }
}
