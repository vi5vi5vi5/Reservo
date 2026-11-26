import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/client';
import { invalidateBookings } from '../../api/hooks';
import type { Booking } from '../../api/types';
import { useToast } from '../../app/toast';
import { dayLabel, interval, local } from '../../lib/time';
import { Sheet } from '../../ui/Sheet';

export function RejectSheet({ b, onClose }: { b: Booking; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      await api(`/admin/bookings/${b.id}/reject`, { method: 'POST', body: { reason: reason.trim() } });
      await invalidateBookings(qc);
      toast('success', 'Заявка отклонена', `${b.user.fullName} увидит причину в уведомлении.`);
      onClose();
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet title="Отклонить заявку" onClose={onClose}>
      <div className="fbox">
        <b>{b.resource.name}</b>
        <span className="rv-tnum" style={{ fontWeight: 700 }}>{dayLabel(local(b.startAt).key)}, {interval(b.startAt, b.endAt)}</span>
        <span className="rv-sm rv-muted">{b.user.fullName}{b.title ? ` · ${b.title}` : ''}</span>
      </div>
      <label className="rv-field">
        <span className="rv-label">Причина</span>
        <span className="rv-control rv-control-area"><textarea maxLength={500} placeholder="Например, в это время плановое обслуживание стендов" value={reason} onChange={(e) => setReason(e.target.value)} /></span>
        <span className="rv-hint">Владелец увидит причину в уведомлении.</span>
      </label>
      <div className="two">
        <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={onClose}>Не отклонять</button>
        <button type="button" className="rv-btn rv-btn-danger rv-btn-lg" disabled={!reason.trim()} aria-busy={busy} onClick={submit}>Отклонить</button>
      </div>
    </Sheet>
  );
}
