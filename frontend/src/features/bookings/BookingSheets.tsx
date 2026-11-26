import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, ApiError } from '../../api/client';
import { invalidateBookings } from '../../api/hooks';
import type { Booking, Resource } from '../../api/types';
import { useAuth } from '../../app/auth';
import { useToast } from '../../app/toast';
import { dayLabel, interval, local } from '../../lib/time';
import { Sheet } from '../../ui/Sheet';
import type { Draft } from '../booking/BookingFlow';
import { bookingTitle } from './BookingCard';

/** Отмена брони. Админ, отменяя чужую, обязан объяснить причину — владелец увидит её в уведомлении. */
export function CancelSheet({ b, onClose, onDone }: { b: Booking; onClose: () => void; onDone?: () => void }) {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const foreign = b.user.id !== user?.id;

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await api(`/bookings/${b.id}/cancel`, { method: 'POST', body: { reason: reason.trim() || null } });
      await invalidateBookings(qc);
      toast('success', 'Бронь отменена', foreign ? 'Владелец получит уведомление с причиной.' : 'Время освободилось.');
      onDone?.();
      onClose();
    } catch (e) {
      setError((e as ApiError).detail ?? (e as ApiError).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet title="Отменить бронь?" onClose={onClose}>
      <p style={{ margin: 0, fontSize: 16, lineHeight: '24px' }}>
        {bookingTitle(b)}{b.resource.spaceName ? `, ${b.resource.spaceName}` : ''} — {dayLabel(local(b.startAt).key)}, {interval(b.startAt, b.endAt)}.
      </p>
      {foreign ? (
        <label className="rv-field">
          <span className="rv-label">Причина</span>
          <span className="rv-control rv-control-area"><textarea maxLength={500} placeholder="Например, аудитория нужна для экзамена" value={reason} onChange={(e) => setReason(e.target.value)} /></span>
          <span className="rv-hint">{b.user.fullName} увидит причину в уведомлении.</span>
        </label>
      ) : (
        <p className="rv-hint" style={{ margin: 0 }}>Время освободится сразу, и его смогут занять другие.</p>
      )}
      {error && <p className="rv-error-text" style={{ margin: 0 }}>{error}</p>}
      <div className="two">
        <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={onClose}>Не отменять</button>
        <button type="button" className="rv-btn rv-btn-danger rv-btn-lg" disabled={foreign && !reason.trim()} aria-busy={busy} onClick={submit}>Отменить бронь</button>
      </div>
    </Sheet>
  );
}

/** Черновик переноса: ресурс подгружается, чтобы форма знала его часы и лимиты. */
export async function editDraft(b: Booking): Promise<Draft> {
  const resource = await api<Resource>(`/resources/${b.resource.id}`);
  return { resource, startAt: b.startAt, endAt: b.endAt, title: b.title, attendees: b.attendees, bookingId: b.id, status: b.status };
}
