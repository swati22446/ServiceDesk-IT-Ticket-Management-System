import { Component, ElementRef, OnInit, ViewChild, computed, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { Attachment, STATUSES, TicketDetail, TicketStatus, UserSummary } from '../../core/models';
import { errorMessage, formatSize, priorityBadge, saveBlob, statusBadge } from '../../core/utils';

@Component({
  selector: 'app-ticket-detail',
  standalone: true,
  imports: [DatePipe, FormsModule, ReactiveFormsModule, RouterLink],
  template: `
    @if (ticket(); as t) {
      <div class="d-flex justify-content-between align-items-start mb-3">
        <div>
          <a routerLink="/tickets" class="small">← Back to tickets</a>
          <h1 class="h3 mt-1">#{{ t.id }} · {{ t.title }}</h1>
          <span class="badge me-1" [class]="badge(t.status)">{{ t.status }}</span>
          <span class="badge me-1" [class]="pBadge(t.priority)">{{ t.priority }}</span>
          @if (t.isOverdue) { <span class="badge text-bg-danger">Overdue</span> }
        </div>
        <div class="d-flex gap-2">
          @if (canEdit()) { <a class="btn btn-outline-primary btn-sm" [routerLink]="['/tickets', t.id, 'edit']">Edit</a> }
          @if (auth.isAdmin()) { <button class="btn btn-outline-danger btn-sm" (click)="remove()">Delete</button> }
        </div>
      </div>

      <div class="row g-3">
        <div class="col-lg-8">
          <div class="card p-3 mb-3">
            <h2 class="h6 text-muted">Description</h2>
            <p class="pre-wrap mb-0">{{ t.description }}</p>
          </div>

          <div class="card p-3 mb-3">
            <h2 class="h6 text-muted">Attachments</h2>
            <ul class="list-unstyled mb-2">
              @for (a of t.attachments; track a.id) {
                <li>
                  <a href="#" (click)="download($event, a)">{{ a.fileName }}</a>
                  <small class="text-muted"> · {{ size(a.sizeBytes) }} · {{ a.uploadedBy }}</small>
                </li>
              } @empty { <li class="text-muted">No attachments.</li> }
            </ul>
            <div class="d-flex gap-2 align-items-center">
              <input #file type="file" class="form-control form-control-sm" style="max-width: 320px"
                     accept=".png,.jpg,.jpeg,.pdf,.txt,.log,.docx,.xlsx,.csv">
              <button class="btn btn-sm btn-outline-secondary" (click)="upload(file.files)" [disabled]="uploading()">
                {{ uploading() ? 'Uploading…' : 'Upload' }}</button>
            </div>
            <small class="text-muted">Max 5 MB. png, jpg, pdf, txt, log, docx, xlsx, csv.</small>
          </div>

          <div class="card p-3">
            <h2 class="h6 text-muted">Conversation</h2>
            @for (c of t.comments; track c.id) {
              <div class="border rounded p-2 mb-2" [class.bg-warning-subtle]="c.isInternal">
                <div class="small text-muted">
                  <strong>{{ c.author }}</strong> ({{ c.authorRole }}) · {{ c.createdAt | date: 'dd MMM yyyy, HH:mm' }}
                  @if (c.isInternal) { <span class="badge text-bg-warning">Internal note</span> }
                </div>
                <div class="pre-wrap">{{ c.body }}</div>
              </div>
            } @empty { <p class="text-muted">No comments yet.</p> }

            <form [formGroup]="commentForm" (ngSubmit)="addComment()" class="mt-2">
              <textarea class="form-control mb-2" rows="3" placeholder="Write a reply…" formControlName="body" maxlength="2000"></textarea>
              <div class="d-flex justify-content-between align-items-center">
                @if (auth.isStaff()) {
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="internal" formControlName="isInternal">
                    <label class="form-check-label" for="internal">Internal note (hidden from requester)</label>
                  </div>
                } @else { <span></span> }
                <button class="btn btn-primary btn-sm" [disabled]="commentForm.invalid || posting()">Post reply</button>
              </div>
            </form>
          </div>
        </div>

        <div class="col-lg-4">
          <div class="card p-3 mb-3">
            <h2 class="h6 text-muted">Details</h2>
            <dl class="row small mb-0">
              <dt class="col-5">Category</dt><dd class="col-7">{{ t.category }}</dd>
              <dt class="col-5">Requester</dt><dd class="col-7">{{ t.createdBy }}</dd>
              <dt class="col-5">Assigned to</dt><dd class="col-7">{{ t.assignedTo ?? 'Unassigned' }}</dd>
              <dt class="col-5">Created</dt><dd class="col-7">{{ t.createdAt | date: 'dd MMM yyyy, HH:mm' }}</dd>
              <dt class="col-5">Due (SLA)</dt><dd class="col-7">{{ t.dueAt | date: 'dd MMM yyyy, HH:mm' }}</dd>
              @if (t.resolvedAt) { <dt class="col-5">Resolved</dt><dd class="col-7">{{ t.resolvedAt | date: 'dd MMM yyyy, HH:mm' }}</dd> }
            </dl>
          </div>

          @if (auth.isStaff()) {
            <div class="card p-3 mb-3">
              <h2 class="h6 text-muted">Manage</h2>
              <label class="form-label small mb-1" for="status">Status</label>
              <select id="status" class="form-select form-select-sm mb-2" [ngModel]="t.status" (ngModelChange)="setStatus($event)">
                @for (s of statuses; track s) { <option [value]="s">{{ s }}</option> }
              </select>
              <label class="form-label small mb-1" for="agent">Assign to</label>
              <select id="agent" class="form-select form-select-sm" [ngModel]="t.assignedToId" (ngModelChange)="assign($event)">
                <option [ngValue]="null" disabled>Choose agent…</option>
                @for (a of agents(); track a.id) { <option [ngValue]="a.id">{{ a.fullName }}</option> }
              </select>
            </div>
          } @else if (canRequesterAct()) {
            <div class="card p-3 mb-3">
              <h2 class="h6 text-muted">Is it fixed?</h2>
              <div class="d-flex gap-2">
                <button class="btn btn-success btn-sm" (click)="setStatus('Closed')">Yes, close it</button>
                <button class="btn btn-outline-warning btn-sm" (click)="setStatus('Open')">No, reopen</button>
              </div>
            </div>
          }

          <div class="card p-3">
            <h2 class="h6 text-muted">History</h2>
            <ul class="list-unstyled timeline small mb-0">
              @for (h of t.history; track $index) {
                <li>
                  <strong>{{ h.action }}</strong>
                  @if (h.oldValue || h.newValue) { : {{ h.oldValue ?? '—' }} → {{ h.newValue ?? '—' }} }
                  <div class="text-muted">{{ h.changedBy }} · {{ h.changedAt | date: 'dd MMM, HH:mm' }}</div>
                </li>
              }
            </ul>
          </div>
        </div>
      </div>
    } @else {
      @if (error()) { <div class="alert alert-danger">{{ error() }}</div> } @else { <p class="text-muted">Loading…</p> }
    }
  `
})
export class TicketDetailComponent implements OnInit {
  /** Route parameter, bound by withComponentInputBinding(). */
  id = input.required<string>();

  private api = inject(ApiService);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);
  private router = inject(Router);
  auth = inject(AuthService);

  @ViewChild('file') fileInput?: ElementRef<HTMLInputElement>;

  statuses = STATUSES;
  ticket = signal<TicketDetail | null>(null);
  agents = signal<UserSummary[]>([]);
  error = signal('');
  posting = signal(false);
  uploading = signal(false);

  badge = statusBadge;
  pBadge = priorityBadge;
  size = formatSize;

  commentForm = this.fb.nonNullable.group({
    body: ['', [Validators.required, Validators.maxLength(2000)]],
    isInternal: [false]
  });

  canEdit = computed(() => {
    const t = this.ticket();
    const user = this.auth.user();
    if (!t || !user) return false;
    return this.auth.isStaff() || (t.createdById === user.id && t.status === 'Open');
  });

  canRequesterAct = computed(() => this.ticket()?.status === 'Resolved');

  ngOnInit() {
    this.load();
    if (this.auth.isStaff()) this.api.agents().subscribe(a => this.agents.set(a));
  }

  private load() {
    this.api.getTicket(+this.id()).subscribe({
      next: t => this.ticket.set(t),
      error: err => this.error.set(errorMessage(err))
    });
  }

  setStatus(status: TicketStatus) {
    const t = this.ticket();
    if (!t || t.status === status) return;
    this.api.changeStatus(t.id, status).subscribe({
      next: updated => { this.ticket.set(updated); this.toast.success(`Status changed to ${status}.`); },
      error: err => { this.toast.error(errorMessage(err)); this.ticket.set({ ...t }); }
    });
  }

  assign(agentId: number | null) {
    const t = this.ticket();
    if (!t || agentId === null) return;
    this.api.assign(t.id, agentId).subscribe({
      next: updated => { this.ticket.set(updated); this.toast.success('Ticket assigned.'); },
      error: err => this.toast.error(errorMessage(err))
    });
  }

  addComment() {
    const t = this.ticket();
    if (!t || this.commentForm.invalid) return;
    this.posting.set(true);
    const { body, isInternal } = this.commentForm.getRawValue();
    this.api.addComment(t.id, body, isInternal).subscribe({
      next: () => { this.commentForm.reset({ body: '', isInternal: false }); this.posting.set(false); this.load(); },
      error: err => { this.toast.error(errorMessage(err)); this.posting.set(false); }
    });
  }

  upload(files: FileList | null) {
    const t = this.ticket();
    const file = files?.item(0);
    if (!t || !file) { this.toast.warn('Choose a file first.'); return; }
    this.uploading.set(true);
    this.api.upload(t.id, file).subscribe({
      next: () => {
        this.uploading.set(false);
        if (this.fileInput) this.fileInput.nativeElement.value = '';
        this.toast.success('File uploaded.');
        this.load();
      },
      error: err => { this.uploading.set(false); this.toast.error(errorMessage(err)); }
    });
  }

  download(event: Event, a: Attachment) {
    event.preventDefault();
    this.api.downloadAttachment(a.id).subscribe({
      next: blob => saveBlob(blob, a.fileName),
      error: () => this.toast.error('Could not download the file.')
    });
  }

  remove() {
    const t = this.ticket();
    if (!t || !confirm(`Delete ticket #${t.id} permanently?`)) return;
    this.api.deleteTicket(t.id).subscribe({
      next: () => { this.toast.success('Ticket deleted.'); this.router.navigate(['/tickets']); },
      error: err => this.toast.error(errorMessage(err))
    });
  }
}
