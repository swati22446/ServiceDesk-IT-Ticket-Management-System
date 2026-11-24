namespace ServiceDesk.Api.Models;

public enum UserRole { User = 0, Agent = 1, Admin = 2 }

public enum TicketStatus { Open = 0, InProgress = 1, OnHold = 2, Resolved = 3, Closed = 4 }

public enum TicketPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }
