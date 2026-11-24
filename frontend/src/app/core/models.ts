export type Role = 'User' | 'Agent' | 'Admin';
export type TicketStatus = 'Open' | 'InProgress' | 'OnHold' | 'Resolved' | 'Closed';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const STATUSES: TicketStatus[] = ['Open', 'InProgress', 'OnHold', 'Resolved', 'Closed'];
export const PRIORITIES: TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

export interface UserInfo { id: number; fullName: string; email: string; role: Role; department: string | null; }
export interface AuthResponse { token: string; expiresAt: string; user: UserInfo; }
export interface Category { id: number; name: string; description: string | null; }
export interface Department { id: number; name: string; }
export interface UserSummary { id: number; fullName: string; email: string; role: Role; department: string | null; isActive: boolean; createdAt: string; }

export interface Ticket {
  id: number; title: string; status: TicketStatus; priority: TicketPriority; category: string;
  createdBy: string; assignedTo: string | null; createdAt: string; updatedAt: string; dueAt: string; isOverdue: boolean;
}
export interface TicketComment { id: number; author: string; authorRole: Role; body: string; isInternal: boolean; createdAt: string; }
export interface Attachment { id: number; fileName: string; contentType: string; sizeBytes: number; uploadedBy: string; uploadedAt: string; }
export interface HistoryItem { action: string; oldValue: string | null; newValue: string | null; changedBy: string; changedAt: string; }
export interface TicketDetail {
  id: number; title: string; description: string; status: TicketStatus; priority: TicketPriority;
  categoryId: number; category: string; createdById: number; createdBy: string; assignedToId: number | null; assignedTo: string | null;
  createdAt: string; updatedAt: string; dueAt: string; resolvedAt: string | null; isOverdue: boolean; rowVersion: string;
  comments: TicketComment[]; attachments: Attachment[]; history: HistoryItem[];
}

export interface PagedResult<T> { items: T[]; total: number; page: number; pageSize: number; totalPages: number; }
export interface CountItem { label: string; count: number; }
export interface Dashboard {
  total: number; open: number; inProgress: number; resolved: number; overdue: number; unassigned: number; avgResolutionHours: number;
  byStatus: CountItem[]; byPriority: CountItem[]; byCategory: CountItem[]; last7Days: CountItem[];
}

export interface TicketQuery {
  search?: string; status?: string; priority?: string; categoryId?: number | null; overdueOnly?: boolean;
  sortBy: string; desc: boolean; page: number; pageSize: number;
}
