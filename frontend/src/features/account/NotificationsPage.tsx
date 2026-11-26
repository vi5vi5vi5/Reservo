import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { Bell } from 'lucide-react';
import { api } from '../../api/client';
import { keys, useNotifications, useUnread } from '../../api/hooks';
import type { Notification } from '../../api/types';
import { useAuth } from '../../app/auth';
import { notificationMeta, tone } from '../../lib/status';
import { agoLabel } from '../../lib/text';
import { Bar, Empty, LoadMore, Seg } from '../../ui/controls';

export function NotificationsPage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [filter, setFilter] = useState<'all' | 'un'>('all');
  const unread = useUnread(!!user).data ?? 0;
  const list = useNotifications({ unreadOnly: filter === 'un' }, !!user);
  if (!user) return <Navigate to="/login" state={{ from: '/notifications' }} replace />;
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];

  const refresh = () => qc.invalidateQueries({ queryKey: ['notifications'] });

  const open = async (n: Notification) => {
    if (!n.isRead) {
      await api(`/notifications/${n.id}/read`, { method: 'POST' }).catch(() => undefined);
      qc.setQueryData(keys.unread, (c: { count: number } | undefined) => (c ? { count: Math.max(0, c.count - 1) } : c));
      refresh();
    }
    if (n.bookingId) navigate(n.type === 'ApprovalRequested' ? '/admin/queue' : `/bookings/${n.bookingId}`);
  };

  const readAll = async () => {
    await api('/notifications/read-all', { method: 'POST' });
    refresh();
  };

  return (
    <div className="page page-narrow">
      <Bar title="Уведомления">
        <button type="button" className="rv-btn rv-btn-ghost" style={{ marginLeft: 'auto', marginRight: 8 }} disabled={!unread} onClick={readAll}>Прочитать все</button>
      </Bar>
      <div className="pad" style={{ paddingBottom: 12 }}>
        <Seg value={filter} onChange={setFilter} label="Какие уведомления" options={[{ value: 'all', label: 'Все' }, { value: 'un', label: unread ? `Непрочитанные · ${unread}` : 'Непрочитанные' }]} />
      </div>
      {!list.isLoading && items.length === 0 && (
        <Empty icon={Bell} title={filter === 'un' ? 'Всё прочитано' : 'Уведомлений пока нет'} text="Здесь появятся подтверждения, решения по заявкам и напоминания — без перезагрузки." />
      )}
      <div className="pad nlist">
        {items.map((n) => {
          const m = notificationMeta[n.type];
          const t = tone(m.tone);
          return (
            <button key={n.id} type="button" className="rv-notif" data-unread={n.isRead ? undefined : ''} onClick={() => open(n)}>
              <span className="rv-notif-ic" style={t}><m.icon className="rv-ic" aria-hidden /></span>
              <span style={{ minWidth: 0 }}><b style={{ display: 'block' }}>{n.title}</b><span className="tx">{n.message}</span></span>
              <span style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 6 }}>
                <time dateTime={n.createdAt}>{agoLabel(n.createdAt)}</time>
                {!n.isRead && <span className="rv-unread-dot" role="img" aria-label="Не прочитано" />}
              </span>
            </button>
          );
        })}
      </div>
      <LoadMore hasNext={!!list.hasNextPage} loading={list.isFetchingNextPage} onLoad={() => list.fetchNextPage()} />
    </div>
  );
}
