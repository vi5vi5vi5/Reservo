import { useState } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router';
import { CalendarCheck } from 'lucide-react';
import { useMyBookings } from '../../api/hooks';
import type { Booking, BookingStatus } from '../../api/types';
import { useAuth } from '../../app/auth';
import { hourNow } from '../../app/Shell';
import { Bar, CardSkeletons, Empty, LoadMore, Seg } from '../../ui/controls';
import { BookingFlow, MAX_ACTIVE, type Draft } from '../booking/BookingFlow';
import { BookingCard } from './BookingCard';
import { CancelSheet, editDraft } from './BookingSheets';

type Tab = 'up' | 'past' | 'gone';

const tabs: Record<Tab, { status: BookingStatus[]; from?: boolean; to?: boolean; dir: 'Asc' | 'Desc'; empty: [string, string] }> = {
  up: { status: ['Pending', 'Confirmed'], from: true, dir: 'Asc', empty: ['У вас пока нет броней', 'Найдите свободную аудиторию, место или оборудование — это займёт полминуты.'] },
  past: { status: ['Completed', 'Confirmed'], to: true, dir: 'Desc', empty: ['Прошедших броней нет', 'Здесь появятся брони, время которых уже прошло.'] },
  gone: { status: ['Cancelled', 'Rejected'], dir: 'Desc', empty: ['Отменённых и отклонённых нет', 'И хорошо.'] },
};

export function MyBookingsPage() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as Tab | null) ?? 'up';
  const [draft, setDraft] = useState<Draft | null>(null);
  const [cancel, setCancel] = useState<Booking | null>(null);
  const now = hourNow();

  const t = tabs[tab];
  const list = useMyBookings({ status: t.status, from: t.from ? now : undefined, to: t.to ? now : undefined, sortDir: t.dir }, !!user);
  const active = useMyBookings({ status: ['Pending', 'Confirmed'], from: now, pageSize: 1 }, !!user).data?.pages[0]?.totalCount ?? 0;
  if (!user) return <Navigate to="/login" state={{ from: '/bookings' }} replace />;

  let items = list.data?.pages.flatMap((p) => p.items) ?? [];
  // Граница «предстоящие/прошедшие» — конец брони: идущая сейчас — ещё предстоящая.
  if (tab === 'up') items = items.filter((b) => new Date(b.endAt).getTime() > Date.now());
  if (tab === 'past') items = items.filter((b) => new Date(b.endAt).getTime() <= Date.now());

  return (
    <div className="page page-narrow">
      <Bar title="Мои брони">
        <span className="rv-badge rv-tnum" style={{ height: 28, padding: '0 10px', marginLeft: 'auto', marginRight: 16 }} title="Активные брони и лимит">{active} из {MAX_ACTIVE}</span>
      </Bar>
      <div className="pad" style={{ paddingBottom: 12 }}>
        <Seg full value={tab} onChange={(v) => setParams(v === 'up' ? {} : { tab: v }, { replace: true })} label="Какие брони"
          options={[{ value: 'up', label: 'Предстоящие' }, { value: 'past', label: 'Прошедшие' }, { value: 'gone', label: 'Отменённые' }]} />
      </div>

      {list.isLoading && <CardSkeletons />}
      {!list.isLoading && items.length === 0 && (
        <Empty icon={CalendarCheck} title={t.empty[0]} text={t.empty[1]}>
          <Link to="/" className="rv-btn rv-btn-primary rv-btn-lg">Найти ресурс</Link>
        </Empty>
      )}
      {items.length > 0 && (
        <div className="list pad" style={{ paddingTop: 4, paddingBottom: 12 }}>
          {items.map((b) => (
            <BookingCard key={b.id} b={b} onEdit={async () => setDraft(await editDraft(b))} onCancel={() => setCancel(b)} />
          ))}
        </div>
      )}
      <LoadMore hasNext={!!list.hasNextPage} loading={list.isFetchingNextPage} onLoad={() => list.fetchNextPage()} />

      {draft && <BookingFlow draft={draft} onClose={() => setDraft(null)} />}
      {cancel && <CancelSheet b={cancel} onClose={() => setCancel(null)} />}
    </div>
  );
}
