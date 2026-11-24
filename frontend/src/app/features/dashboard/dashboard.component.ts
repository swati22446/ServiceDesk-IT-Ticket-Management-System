import { Component, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { CountItem, Dashboard } from '../../core/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <h1 class="h3 mb-3">Dashboard</h1>
    @if (data(); as d) {
      <div class="row g-3 mb-3">
        @for (k of kpis(d); track k.label) {
          <div class="col-6 col-lg-3">
            <div class="card p-3 h-100">
              <div class="text-muted small">{{ k.label }}</div>
              <div class="display-6 fw-semibold" [class]="k.cls">{{ k.value }}</div>
            </div>
          </div>
        }
      </div>

      <div class="row g-3">
        @for (block of blocks(d); track block.title) {
          <div class="col-lg-6">
            <div class="card p-3 h-100">
              <h2 class="h6 text-muted">{{ block.title }}</h2>
              @for (item of block.items; track item.label) {
                <div class="mb-2">
                  <div class="d-flex justify-content-between small"><span>{{ item.label }}</span><strong>{{ item.count }}</strong></div>
                  <div class="progress" role="progressbar" [attr.aria-label]="item.label" style="height: 8px">
                    <div class="progress-bar" [style.width.%]="percent(item, block.items)"></div>
                  </div>
                </div>
              }
            </div>
          </div>
        }
      </div>
    } @else {
      <p class="text-muted">Loading…</p>
    }
  `
})
export class DashboardComponent {
  private api = inject(ApiService);
  data = signal<Dashboard | null>(null);

  constructor() {
    this.api.dashboard().subscribe(d => this.data.set(d));
  }

  kpis(d: Dashboard) {
    return [
      { label: 'Total tickets', value: d.total, cls: '' },
      { label: 'Open', value: d.open, cls: 'text-primary' },
      { label: 'In progress', value: d.inProgress, cls: 'text-info' },
      { label: 'Resolved / closed', value: d.resolved, cls: 'text-success' },
      { label: 'Overdue (SLA)', value: d.overdue, cls: d.overdue > 0 ? 'text-danger' : '' },
      { label: 'Unassigned', value: d.unassigned, cls: d.unassigned > 0 ? 'text-warning' : '' },
      { label: 'Avg. resolution (h)', value: d.avgResolutionHours, cls: '' }
    ];
  }

  blocks(d: Dashboard) {
    return [
      { title: 'By status', items: d.byStatus },
      { title: 'By priority', items: d.byPriority },
      { title: 'By category', items: d.byCategory },
      { title: 'Created in the last 7 days', items: d.last7Days }
    ];
  }

  percent(item: CountItem, all: CountItem[]) {
    const max = Math.max(...all.map(i => i.count), 1);
    return Math.round((item.count / max) * 100);
  }
}
