// Типы ответов API — один в один с DTO бэкенда (Reservo.Application). Время приходит строками ISO 8601 в UTC.

export type UserRole = 'User' | 'Admin';
export type ResourceKind = 'Room' | 'Seat' | 'Equipment';
export type BookingStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'Cancelled' | 'Completed';
export type SeatStatus = 'Free' | 'Busy' | 'Mine' | 'Unavailable';
export type NotificationType =
  | 'BookingConfirmed'
  | 'BookingPendingApproval'
  | 'ApprovalRequested'
  | 'BookingApproved'
  | 'BookingRejected'
  | 'BookingCancelled'
  | 'BookingReminder';
export type AuditAction =
  | 'UserRegistered' | 'UserRoleChanged' | 'UserBlocked' | 'UserUnblocked'
  | 'ResourceCreated' | 'ResourceUpdated' | 'ResourceDeactivated' | 'ResourceActivated'
  | 'BookingCreated' | 'BookingUpdated' | 'BookingApproved' | 'BookingRejected'
  | 'BookingCancelled' | 'BookingCompleted' | 'BookingReminderSent';

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface User {
  id: number;
  email: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: User;
}

export interface Category {
  id: number;
  code: string;
  name: string;
  description: string | null;
  kind: ResourceKind;
}

export interface Location {
  id: number;
  name: string;
  address: string | null;
}

export interface Feature {
  id: number;
  code: string;
  name: string;
}

export interface ResourceSeat {
  spaceId: number;
  spaceName: string;
  number: number;
  row: number;
  column: number;
}

export interface Resource {
  id: number;
  name: string;
  description: string | null;
  category: Category;
  location: Location;
  floor: number | null;
  capacity: number | null;
  imageUrl: string | null;
  requiresApproval: boolean;
  /** Местное время площадки, «08:00:00». */
  openTime: string;
  closeTime: string;
  maxDurationMinutes: number | null;
  allowsOvernight: boolean;
  pickupInfo: string | null;
  seat: ResourceSeat | null;
  isActive: boolean;
  deactivationReason: string | null;
  features: Feature[];
  createdAt: string;
  updatedAt: string;
}

export interface AvailableResource {
  resource: Resource;
  freeUntil: string | null;
}

export interface ScheduleItem {
  startAt: string;
  endAt: string;
  status: BookingStatus;
  isMine: boolean;
  bookingId: number | null;
  title: string | null;
  ownerName: string | null;
}

export interface ResourceSchedule {
  resourceId: number;
  resourceName: string;
  from: string;
  to: string;
  timeZone: string;
  openTime: string;
  closeTime: string;
  items: ScheduleItem[];
}

export interface BookingResource {
  id: number;
  name: string;
  categoryCode: string;
  kind: ResourceKind;
  locationName: string;
  floor: number | null;
  requiresApproval: boolean;
  allowsOvernight: boolean;
  pickupInfo: string | null;
  spaceId: number | null;
  spaceName: string | null;
  seatNumber: number | null;
}

export interface Booking {
  id: number;
  resource: BookingResource;
  user: { id: number; fullName: string; email: string };
  startAt: string;
  endAt: string;
  title: string | null;
  attendees: number | null;
  status: BookingStatus;
  statusReason: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AuditLog {
  id: number;
  action: AuditAction;
  entityType: 'Booking' | 'Resource' | 'User';
  entityId: number;
  actorId: number | null;
  actorName: string | null;
  entityName: string | null;
  details: Record<string, unknown> | null;
  createdAt: string;
}

export interface Notification {
  id: number;
  type: NotificationType;
  title: string;
  message: string;
  bookingId: number | null;
  isRead: boolean;
  createdAt: string;
}

export interface Seat {
  resourceId: number;
  number: number;
  row: number;
  column: number;
  featureCodes: string[];
  isActive: boolean;
  status: SeatStatus | null;
  freeUntil: string | null;
}

export interface Space {
  id: number;
  name: string;
  description: string | null;
  location: Location;
  floor: number | null;
  isQuiet: boolean;
  rows: number;
  columns: number;
  aisleAfterColumn: number | null;
  openTime: string | null;
  closeTime: string | null;
  totalSeats: number;
  freeSeats: number | null;
  seats: Seat[];
}

export interface Suggestion {
  resourceId: number;
  resourceName: string;
  locationName: string;
  startAt: string;
  endAt: string;
  reason: 'Earlier' | 'Later' | 'SimilarResource';
}

export interface HeatmapRow {
  type: 'resource' | 'space';
  id: number;
  name: string;
  categoryCode: string | null;
  busyPercent: (number | null)[];
}

export interface AdminStats {
  date: string;
  timeZone: string;
  generatedAt: string;
  bookingsOnDate: number;
  inProgressNow: number;
  pendingApprovals: number;
  urgentApprovals: number;
  urgentHours: number;
  activeResources: number;
  inactiveResources: number;
  hours: number[];
  heatmap: HeatmapRow[];
}

/** Ошибка API в формате ProblemDetails (RFC 9457) с машиночитаемым кодом. */
export interface Problem {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
}
