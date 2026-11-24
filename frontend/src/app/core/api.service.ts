import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Attachment, Category, Dashboard, Department, PagedResult, Role, Ticket, TicketComment, TicketDetail, TicketQuery, TicketStatus,
  UserSummary
} from './models';

/** One place for every HTTP call, so components stay free of URLs. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  // ----- tickets -----
  listTickets(q: TicketQuery): Observable<PagedResult<Ticket>> {
    return this.http.get<PagedResult<Ticket>>(`${this.base}/tickets`, { params: this.toParams(q) });
  }
  getTicket(id: number) { return this.http.get<TicketDetail>(`${this.base}/tickets/${id}`); }
  createTicket(body: { title: string; description: string; categoryId: number; priority: string }) {
    return this.http.post<TicketDetail>(`${this.base}/tickets`, body);
  }
  updateTicket(id: number, body: { title: string; description: string; categoryId: number; priority: string; rowVersion: string }) {
    return this.http.put<TicketDetail>(`${this.base}/tickets/${id}`, body);
  }
  changeStatus(id: number, status: TicketStatus) {
    return this.http.patch<TicketDetail>(`${this.base}/tickets/${id}/status`, { status });
  }
  assign(id: number, agentId: number) {
    return this.http.patch<TicketDetail>(`${this.base}/tickets/${id}/assign`, { agentId });
  }
  addComment(id: number, body: string, isInternal: boolean) {
    return this.http.post<TicketComment>(`${this.base}/tickets/${id}/comments`, { body, isInternal });
  }
  upload(id: number, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<Attachment>(`${this.base}/tickets/${id}/attachments`, form);
  }
  downloadAttachment(id: number) {
    return this.http.get(`${this.base}/attachments/${id}`, { responseType: 'blob' });
  }
  deleteTicket(id: number) { return this.http.delete<void>(`${this.base}/tickets/${id}`); }
  exportExcel(q: TicketQuery) {
    return this.http.get(`${this.base}/reports/tickets/excel`, { params: this.toParams({ ...q, page: 1 }), responseType: 'blob' });
  }

  // ----- lookups & dashboard -----
  categories() { return this.http.get<Category[]>(`${this.base}/lookups/categories`); }
  departments() { return this.http.get<Department[]>(`${this.base}/lookups/departments`); }
  agents() { return this.http.get<UserSummary[]>(`${this.base}/lookups/agents`); }
  dashboard() { return this.http.get<Dashboard>(`${this.base}/dashboard`); }

  // ----- admin -----
  users() { return this.http.get<UserSummary[]>(`${this.base}/admin/users`); }
  changeRole(id: number, role: Role) { return this.http.patch<void>(`${this.base}/admin/users/${id}/role`, { role }); }
  setActive(id: number, isActive: boolean) { return this.http.patch<void>(`${this.base}/admin/users/${id}/active`, { isActive }); }
  createCategory(body: { name: string; description: string | null }) { return this.http.post<Category>(`${this.base}/admin/categories`, body); }
  updateCategory(id: number, body: { name: string; description: string | null }) { return this.http.put<Category>(`${this.base}/admin/categories/${id}`, body); }
  deleteCategory(id: number) { return this.http.delete<void>(`${this.base}/admin/categories/${id}`); }

  private toParams(q: TicketQuery): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(q)) {
      if (value !== undefined && value !== null && value !== '') params = params.set(key, String(value));
    }
    return params;
  }
}
