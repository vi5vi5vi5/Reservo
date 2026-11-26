import { Ban, Bell, CircleCheck, CircleX, Flag, Hourglass, Inbox, type LucideIcon } from 'lucide-react';
import type { Booking, BookingStatus, NotificationType } from '../api/types';

/** Статус всегда показывается цветом, иконкой и текстом сразу — цвет не единственный признак. */
export const statusMeta: Record<BookingStatus, { label: string; cls: string; icon: LucideIcon }> = {
  Pending: { label: 'На согласовании', cls: 'rv-status rv-status-pending', icon: Hourglass },
  Confirmed: { label: 'Подтверждена', cls: 'rv-status rv-status-confirmed', icon: CircleCheck },
  Rejected: { label: 'Отклонена', cls: 'rv-status rv-status-rejected', icon: CircleX },
  Cancelled: { label: 'Отменена', cls: 'rv-status rv-status-cancelled', icon: Ban },
  Completed: { label: 'Завершена', cls: 'rv-status rv-status-completed', icon: Flag },
};

export function StatusChip({ status }: { status: BookingStatus }) {
  const m = statusMeta[status];
  const Icon = m.icon;
  return (
    <span className={m.cls}>
      <Icon className="rv-ic" aria-hidden />
      {m.label}
    </span>
  );
}

export const isActive = (b: Booking) => b.status === 'Pending' || b.status === 'Confirmed';
export const hasEnded = (b: Booking, now = Date.now()) => new Date(b.endAt).getTime() <= now;
export const hasStarted = (b: Booking, now = Date.now()) => new Date(b.startAt).getTime() <= now;

/** Подтверждённая бронь, у которой время прошло, показывается завершённой, даже если фоновая задача ещё не успела. */
export function effectiveStatus(b: Booking, now = Date.now()): BookingStatus {
  return b.status === 'Confirmed' && hasEnded(b, now) ? 'Completed' : b.status;
}

export function isLive(b: Booking, now = Date.now()) {
  return b.status === 'Confirmed' && hasStarted(b, now) && !hasEnded(b, now);
}

export const notificationMeta: Record<NotificationType, { icon: LucideIcon; tone: string }> = {
  BookingReminder: { icon: Bell, tone: 'info' },
  BookingConfirmed: { icon: CircleCheck, tone: 'confirmed' },
  BookingApproved: { icon: CircleCheck, tone: 'confirmed' },
  BookingPendingApproval: { icon: Hourglass, tone: 'pending' },
  BookingRejected: { icon: CircleX, tone: 'rejected' },
  BookingCancelled: { icon: Ban, tone: 'cancelled' },
  ApprovalRequested: { icon: Inbox, tone: 'accent' },
};

/** Цвета подложки и значка по тону. */
export function tone(t: string): { background: string; color: string } {
  if (t === 'neutral') return { background: 'var(--surface-muted)', color: 'var(--ink-muted)' };
  if (t === 'accent') return { background: 'var(--accent-soft)', color: 'var(--accent-text)' };
  if (t === 'info') return { background: 'var(--info-bg)', color: 'var(--info)' };
  return { background: `var(--status-${t}-bg)`, color: `var(--status-${t})` };
}
