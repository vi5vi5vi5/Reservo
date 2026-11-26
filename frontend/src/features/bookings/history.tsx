import { Ban, Bell, CircleCheck, CircleX, Flag, Hourglass, Pencil, Plus, RefreshCw, Users, Wrench, type LucideIcon } from 'lucide-react';
import type { AuditLog } from '../../api/types';
import { tone } from '../../lib/status';
import { agoLabel } from '../../lib/text';
import { interval, local, dayLabel } from '../../lib/time';

interface Line { icon: LucideIcon; tone: string; text: string }

type Change<T> = { old: T; new: T } | undefined;

/** Запись журнала → строка истории по-человечески. Подробности (старые и новые значения) лежат в details. */
export function describe(a: AuditLog): Line[] {
  const d = (a.details ?? {}) as Record<string, unknown>;
  switch (a.action) {
    case 'BookingCreated':
      return [{ icon: Plus, tone: 'neutral', text: d.status === 'Pending' ? 'Создана, отправлена на согласование' : 'Создана и подтверждена' }];
    case 'BookingUpdated': {
      const lines: Line[] = [];
      const start = d.startAt as Change<string>;
      const end = d.endAt as Change<string>;
      if (start || end) {
        const oldS = start?.old ?? null;
        const newS = start?.new ?? null;
        const oldE = end?.old ?? null;
        const newE = end?.new ?? null;
        const before = oldS && oldE ? interval(oldS, oldE) : '';
        const after = newS && newE ? interval(newS, newE) : '';
        const dayChanged = oldS && newS && local(oldS).key !== local(newS).key;
        lines.push({
          icon: Pencil,
          tone: 'accent',
          text: before && after
            ? `Изменено время: ${dayChanged ? `${dayLabel(local(oldS!).key)}, ` : ''}${before} → ${dayChanged ? `${dayLabel(local(newS!).key)}, ` : ''}${after}`
            : 'Изменено время брони',
        });
      }
      const title = d.title as Change<string | null>;
      if (title) lines.push({ icon: Pencil, tone: 'accent', text: `Изменена тема: «${title.old ?? '—'}» → «${title.new ?? '—'}»` });
      const att = d.attendees as Change<number | null>;
      if (att) lines.push({ icon: Users, tone: 'accent', text: `Изменено число участников: ${att.old ?? '—'} → ${att.new ?? '—'}` });
      const status = d.status as Change<string>;
      if (status?.new === 'Pending') lines.push({ icon: Hourglass, tone: 'pending', text: 'Снова на согласовании' });
      return lines.length ? lines : [{ icon: Pencil, tone: 'accent', text: 'Бронь изменена' }];
    }
    case 'BookingApproved':
      return [{ icon: CircleCheck, tone: 'confirmed', text: 'Одобрена администратором' }];
    case 'BookingRejected':
      return [{ icon: CircleX, tone: 'rejected', text: a.actorId ? `Отклонена${d.reason ? `: ${d.reason}` : ''}` : 'Отклонена автоматически: не успели согласовать до начала' }];
    case 'BookingCancelled':
      return [{ icon: Ban, tone: 'cancelled', text: `Отменена${d.reason ? `: ${d.reason}` : ''}` }];
    case 'BookingCompleted':
      return [{ icon: Flag, tone: 'completed', text: 'Завершена' }];
    case 'BookingReminderSent':
      return [{ icon: Bell, tone: 'info', text: 'Напоминание отправлено' }];
    case 'ResourceDeactivated':
      return [{ icon: Wrench, tone: 'cancelled', text: `Выведен из эксплуатации${d.reason ? `: ${d.reason}` : ''}` }];
    case 'ResourceActivated':
      return [{ icon: RefreshCw, tone: 'confirmed', text: 'Возвращён в эксплуатацию' }];
    default:
      return [{ icon: Pencil, tone: 'neutral', text: a.action }];
  }
}

export function who(a: AuditLog, meId?: number) {
  if (a.actorId == null) return 'Система';
  return a.actorId === meId ? 'Вы' : (a.actorName ?? 'Администратор');
}

export function Timeline({ logs, meId, withEntity }: { logs: AuditLog[]; meId?: number; withEntity?: boolean }) {
  return (
    <ol className="rv-timeline">
      {logs.flatMap((a) => describe(a).map((line, i) => {
        const t = tone(line.tone);
        return (
          <li key={`${a.id}-${i}`}>
            <span className="node" style={{ background: t.background, color: t.color }}><line.icon className="rv-ic" aria-hidden /></span>
            <div>
              <div style={{ fontWeight: 700, fontSize: 14, lineHeight: '20px' }}>{withEntity && a.entityName ? `${a.entityName}: ${line.text.charAt(0).toLowerCase()}${line.text.slice(1)}` : line.text}</div>
              <div className="rv-sm rv-muted">{who(a, meId)} · {agoLabel(a.createdAt)}</div>
            </div>
          </li>
        );
      }))}
    </ol>
  );
}
