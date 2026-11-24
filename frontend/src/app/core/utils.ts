import { HttpErrorResponse } from '@angular/common/http';

/** Turns an API error (ProblemDetails / ValidationProblemDetails) into one readable message. */
export function errorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (body?.errors && typeof body.errors === 'object') {
      return Object.values(body.errors as Record<string, string[]>).flat().join(' ');
    }
    if (body?.detail) return body.detail;
    if (body?.title) return body.title;
    if (err.status === 0) return 'Cannot reach the server. Check your connection.';
    if (err.status === 429) return 'Too many attempts. Please wait a minute and try again.';
  }
  return 'Something went wrong. Please try again.';
}

export function statusBadge(status: string): string {
  switch (status) {
    case 'Open': return 'text-bg-primary';
    case 'InProgress': return 'text-bg-info';
    case 'OnHold': return 'text-bg-warning';
    case 'Resolved': return 'text-bg-success';
    default: return 'text-bg-secondary';
  }
}

export function priorityBadge(priority: string): string {
  switch (priority) {
    case 'Critical': return 'text-bg-danger';
    case 'High': return 'text-bg-warning';
    case 'Medium': return 'text-bg-primary';
    default: return 'text-bg-secondary';
  }
}

export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** Saves a Blob (e.g. from an authenticated download) through a temporary link. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}
