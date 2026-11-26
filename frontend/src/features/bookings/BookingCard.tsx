import { Link } from 'react-router';
import { Pencil, X } from 'lucide-react';
import type { Booking } from '../../api/types';
import { effectiveStatus, hasStarted, isActive, isLive, StatusChip } from '../../lib/status';
import { duration, untilLabel, where } from '../../lib/text';
import { DOW, dow, interval, local, minutesBetween } from '../../lib/time';
import { useNow } from '../../lib/useNow';

/** Название ресурса брони: у места — «Место 7 · Коворкинг «Точка»». */
export function bookingTitle(b: Booking) {
  return b.resource.seatNumber != null ? `Место ${b.resource.seatNumber}` : b.resource.name;
}

export function bookingWhere(b: Booking) {
  return b.resource.spaceName ?? where(b.resource.locationName, b.resource.floor);
}

/** Карточка брони. compact — на главной (без действий), иначе — в «Моих бронях». */
export function BookingCard({ b, compact, onEdit, onCancel }: { b: Booking; compact?: boolean; onEdit?: () => void; onCancel?: () => void }) {
  const now = useNow();
  const l = local(b.startAt);
  const live = isLive(b, now);
  const st = effectiveStatus(b, now);
  const muted = st === 'Cancelled' || st === 'Rejected' || st === 'Completed';
  const left = minutesBetween(new Date(now), b.endAt);
  const pct = Math.round((minutesBetween(b.startAt, new Date(now)) / Math.max(1, minutesBetween(b.startAt, b.endAt))) * 100);
  const rel = live ? `Идёт сейчас · ещё ${duration(left)}` : isActive(b) && !hasStarted(b, now) ? untilLabel(b.startAt, new Date(now)) : '';
  const canEdit = !compact && isActive(b) && !hasStarted(b, now);
  const cls = `rv-card ${compact ? 'upc' : 'bkc'}${live ? ' rv-booking-live' : muted ? ' rv-booking-muted' : ''}`;

  const body = (
    <>
      <span className="rv-date"><b>{l.d}</b><small>{DOW[dow(l.key)]}</small></span>
      <span className={compact ? 'upc-b' : 'bkc-b'}>
        <span className="upc-n">{bookingTitle(b)}</span>
        <span className="rv-tnum" style={{ fontWeight: 700 }}>
          {interval(b.startAt, b.endAt)}
          <span className="rv-muted" style={{ fontWeight: 600 }}> · {compact ? bookingWhere(b) : duration(minutesBetween(b.startAt, b.endAt))}</span>
        </span>
        {!compact && <span className="rv-sm rv-muted">{[b.resource.spaceName, b.title].filter(Boolean).join(' · ') || bookingWhere(b)}</span>}
        {compact ? (
          live ? (
            <>
              <span className="rv-status rv-status-live" style={{ alignSelf: 'flex-start', marginTop: 2 }}><span className="dot" />{rel}</span>
              <span className="rv-progress" style={{ display: 'block' }}><i style={{ width: `${pct}%` }} /></span>
            </>
          ) : (
            <span className="relx">{b.status === 'Pending' ? `На согласовании · ${rel}` : rel}</span>
          )
        ) : (
          <>
            <span className="bkc-chips">
              {live ? <span className="rv-status rv-status-live"><span className="dot" />{rel}</span> : <StatusChip status={st} />}
              {!live && rel && <span className="relx">{rel}</span>}
            </span>
            {live && <span className="rv-progress" style={{ display: 'block' }}><i style={{ width: `${pct}%` }} /></span>}
            {b.statusReason && (st === 'Cancelled' || st === 'Rejected') && <span className="rv-sm rv-muted">{b.statusReason}</span>}
          </>
        )}
      </span>
    </>
  );

  if (compact) {
    return <Link to={`/bookings/${b.id}`} className={cls}>{body}</Link>;
  }

  return (
    <article className={cls}>
      <Link to={`/bookings/${b.id}`} className="bkc-hit">{body}</Link>
      {canEdit && (
        <div className="bkc-a">
          <button type="button" className="rv-btn rv-btn-secondary" onClick={onEdit}><Pencil className="rv-ic rv-ic-sm" aria-hidden />Изменить</button>
          <button type="button" className="rv-btn rv-btn-secondary" onClick={onCancel}><X className="rv-ic rv-ic-sm" aria-hidden />Отменить</button>
        </div>
      )}
    </article>
  );
}
