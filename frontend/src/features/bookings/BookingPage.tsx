import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { Ban, Calendar, Check, CircleAlert, CircleX, Clock, Hourglass, MapPin, Pencil, Users } from 'lucide-react';
import { api } from '../../api/client';
import { invalidateBookings, useBooking, useHistory } from '../../api/hooks';
import type { Booking } from '../../api/types';
import { useAuth } from '../../app/auth';
import { useHideTabs } from '../../app/Shell';
import { useToast } from '../../app/toast';
import { effectiveStatus, hasEnded, hasStarted, isActive, isLive, StatusChip } from '../../lib/status';
import { agoLabel, duration, initials, people } from '../../lib/text';
import { dayLabel, interval, local, minutesBetween } from '../../lib/time';
import { useNow } from '../../lib/useNow';
import { Banner, Bar, Empty } from '../../ui/controls';
import { BookingFlow, type Draft } from '../booking/BookingFlow';
import { RejectSheet } from '../admin/RejectSheet';
import { bookingTitle, bookingWhere } from './BookingCard';
import { CancelSheet, editDraft } from './BookingSheets';
import { Timeline } from './history';

export function BookingPage() {
  useHideTabs();
  const id = Number(useParams().id);
  const { data: b, isLoading, error } = useBooking(id);
  if (isLoading) return <div className="page page-narrow"><Bar back="/bookings" title="Бронь" small /><div className="pad"><div className="rv-skel" style={{ height: 220, borderRadius: 14 }} /></div></div>;
  if (error || !b) return <div className="page page-narrow"><Bar back="/bookings" title="Бронь" small /><Empty icon={CircleAlert} title="Бронь не найдена" text="Её нет или она принадлежит другому пользователю." /></div>;
  return <BookingView b={b} />;
}

function BookingView({ b }: { b: Booking }) {
  const { user, isAdmin } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const now = useNow();
  const history = useHistory(b.id).data ?? [];
  const [draft, setDraft] = useState<Draft | null>(null);
  const [sheet, setSheet] = useState<'cancel' | 'reject' | null>(null);
  const [busy, setBusy] = useState(false);

  const mine = b.user.id === user?.id;
  const st = effectiveStatus(b, now);
  const live = isLive(b, now);
  const canEdit = mine && isActive(b) && !hasStarted(b, now);
  const canApprove = isAdmin && b.status === 'Pending' && !hasStarted(b, now);
  const canAdminCancel = isAdmin && !mine && isActive(b) && !hasEnded(b, now);

  const approve = async () => {
    setBusy(true);
    try {
      await api(`/admin/bookings/${b.id}/approve`, { method: 'POST' });
      await invalidateBookings(qc);
      toast('success', 'Заявка одобрена', `${b.user.fullName} получит уведомление.`);
    } finally {
      setBusy(false);
    }
  };

  const reasonBanner = b.statusReason && (st === 'Cancelled' || st === 'Rejected') && (
    <Banner icon={st === 'Rejected' ? CircleX : Ban} tone={st === 'Rejected' ? 'danger' : undefined} title={st === 'Rejected' ? 'Заявка отклонена' : 'Бронь отменена'}>{b.statusReason}</Banner>
  );

  return (
    <div className="page page-narrow">
      <Bar back="/bookings" title="Бронь" small />
      <div className="pad" style={{ display: 'flex', flexDirection: 'column', gap: 16, paddingBottom: 28 }}>
        <div className="rv-row" style={{ gap: 6 }}>
          {live && <span className="rv-status rv-status-live"><span className="dot" />Идёт сейчас · ещё {duration(minutesBetween(new Date(now), b.endAt))}</span>}
          <StatusChip status={st} />
        </div>
        <div>
          <h2 className="rv-h2">{bookingTitle(b)}</h2>
          <Link to={`/resources/${b.resource.id}?day=${local(b.startAt).key}`} className="lnk" style={{ display: 'inline-block', marginTop: 6 }}>Открыть расписание ресурса</Link>
        </div>
        <div className="rv-card" style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div className="mrow"><Calendar className="rv-ic" aria-hidden /><div>{dayLabel(local(b.startAt).key)}</div></div>
          <div className="mrow"><Clock className="rv-ic" aria-hidden /><div className="rv-tnum">{interval(b.startAt, b.endAt)} · {duration(minutesBetween(b.startAt, b.endAt))}</div></div>
          {b.title && <div className="mrow"><Pencil className="rv-ic" aria-hidden /><div>{b.title}</div></div>}
          {b.attendees != null && <div className="mrow"><Users className="rv-ic" aria-hidden /><div>{people(b.attendees)}</div></div>}
          <div className="mrow"><MapPin className="rv-ic" aria-hidden /><div>{bookingWhere(b)}{b.resource.spaceName && <div className="rv-sm rv-muted">{b.resource.locationName}{b.resource.floor != null ? `, ${b.resource.floor} этаж` : ''}</div>}</div></div>
          {b.resource.pickupInfo && <div className="mrow"><Clock className="rv-ic" aria-hidden /><div className="rv-sm rv-muted">{b.resource.pickupInfo}</div></div>}
        </div>

        {reasonBanner}
        {b.status === 'Pending' && !hasStarted(b, now) && (
          <Banner icon={Hourglass} tone="warn">Ждёт решения администратора. Если заявку не рассмотрят до начала, она отклонится автоматически.</Banner>
        )}

        {isAdmin && !mine && (
          <div className="rv-card" style={{ padding: '12px 14px', display: 'flex', gap: 12, alignItems: 'center' }}>
            <span className="rv-avatar">{initials(b.user.fullName)}</span>
            <div style={{ minWidth: 0 }}><b className="ell">{b.user.fullName}</b><span className="rv-sm rv-muted ell">{b.user.email}</span></div>
            <span className="rv-cap" style={{ marginLeft: 'auto' }}>Владелец</span>
          </div>
        )}

        {canEdit && (
          <div className="two">
            <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={async () => setDraft(await editDraft(b))}><Pencil className="rv-ic rv-ic-sm" aria-hidden />Изменить</button>
            <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={() => setSheet('cancel')}>Отменить бронь</button>
          </div>
        )}
        {canApprove && (
          <div className="two">
            <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={() => setSheet('reject')}>Отклонить</button>
            <button type="button" className="rv-btn rv-btn-primary rv-btn-lg" aria-busy={busy} onClick={approve}><Check className="rv-ic rv-ic-sm" aria-hidden />Одобрить</button>
          </div>
        )}
        {canAdminCancel && <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg rv-btn-block" onClick={() => setSheet('cancel')}>Отменить с причиной</button>}

        <p className="rv-sm rv-muted" style={{ margin: 0 }}>Создана {agoLabel(b.createdAt)}</p>
        <h2 className="h2m" style={{ marginTop: 4 }}>История</h2>
        <Timeline logs={history} meId={user?.id} />
      </div>

      {draft && <BookingFlow draft={draft} onClose={() => setDraft(null)} />}
      {sheet === 'cancel' && <CancelSheet b={b} onClose={() => setSheet(null)} />}
      {sheet === 'reject' && <RejectSheet b={b} onClose={() => setSheet(null)} />}
    </div>
  );
}
