import { Injectable, signal } from '@angular/core';

export interface Toast { id: number; kind: 'success' | 'danger' | 'warning'; text: string; }

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  success(text: string) { this.push('success', text); }
  error(text: string) { this.push('danger', text); }
  warn(text: string) { this.push('warning', text); }
  dismiss(id: number) { this.toasts.update(list => list.filter(t => t.id !== id)); }

  private push(kind: Toast['kind'], text: string) {
    const id = this.nextId++;
    this.toasts.update(list => [...list, { id, kind, text }]);
    setTimeout(() => this.dismiss(id), 5000);
  }
}
