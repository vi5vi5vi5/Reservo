import { useMemo, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { Check, ChevronRight, Hourglass, Search, ShieldAlert, Sparkles } from 'lucide-react';
import { api } from '../../api/client';
import { invalidateBookings, useAdminBookings, useAudit, useStats } from '../../api/hooks';
import type { Booking, BookingStatus } from '../../api/types';
import { useAuth } from '../../app/auth';
import { hourNow } from '../../app/Shell';
import { useToast } from '../../app/toast';
import { effectiveStatus, hasEnded, StatusChip } from '../../lib/status';
import { agoLabel, duration, initials, np, people } from '../../lib/text';
import { dayLabel, hhmm, interval, local, minutesBetween } from '../../lib/time';
import { useNow } from '../../lib/useNow';
import { Bar, Chip, Empty, LoadMore, SearchField, Seg } from '../../ui/controls';
import { bookingTitle } from '../bookings/BookingCard';
import { CancelSheet } from '../bookings/BookingSheets';
import { Timeline } from '../bookings/history';
import { useDebounced } from '../search/WhatSheet';
import { RejectSheet } from './RejectSheet';

type Tab = 'dash' | 'queue' | 'all';

export function AdminPage() {
  const { isAdmin, user } = useAuth();
  const tab = (useParams().tab as Tab | undefined) ?? 'dash';
  const navigate = useNavigate();
  const pending = useAdminBookings({ status: 'Pending', from: hourNow(), pageSize: 1 }, isAdmin).data?.pages[0]?.totalCount ?? 0;

  if (!user) return <Navigate to="/login" state={{ from: '/admin' }} replace />;
  if (!isAdmin) return <Empty icon={ShieldAlert} title="Недостаточно прав" text="Этот раздел доступен только администраторам."><Link to="/" className="rv-btn rv-btn-primary rv-btn-lg">На главную</Link></Empty>;

  return (
    <div className="page">
      <Bar title="Администрирование" back="/profile" />
      <div className="pad" style={{ paddingBottom: 12, maxWidth: 560 }}>
        <Seg full value={tab} onChange={(v) => navigate(v === 'dash' ? '/admin' : `/admin/${v}`, { replace: true })} label="Разделы"
          options={[
            { value: 'dash', label: 'Сводка' },
            { value: 'queue', label: <>Заявки{pending > 0 && <span className="rv-counter" style={{ marginLeft: 6 }}>{pending}</span>}</> },
            { value: 'all', label: 'Все брони' },
          ]} />
      </div>
      {tab === 'dash' && <Dashboard />}
      {tab === 'queue' && <Queue />}
      {tab === 'all' && <AllBookings />}
    </div>
  );
}

function Dashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { data: s } = useStats();
  const audit = useAudit({ pageSize: 6 }).data?.items ?? [];
  const next = useAdminBookings({ status: ['Confirmed', 'Pending'], from: new Date(Math.floor(Date.now() / 60_000) * 60_000).toISOString(), pageSize: 5 }).data?.pages[0]?.items ?? [];
  const nowHour = Math.floor(local(new Date()).minutes / 60);

  const metrics = s ? [
    { label: 'Броней сегодня', value: s.bookingsOnDate, sub: `в ${np(s.activeResources, 'ресурсе', 'ресурсах', 'ресурсах')}`, color: 'var(--ink)', go: '/admin/all' },
    { label: 'Идёт сейчас', value: s.inProgressNow, sub: `на ${hhmm(local(s.generatedAt).minutes)}`, color: 'var(--accent-text)', go: '/admin/all' },
    { label: 'Ждут согласования', value: s.pendingApprovals, sub: s.urgentApprovals ? `срочных: ${s.urgentApprovals}` : 'срочных нет', color: 'var(--status-pending)', go: '/admin/queue' },
    { label: 'Выведено', value: s.inactiveResources, sub: s.inactiveResources ? 'из эксплуатации' : 'все ресурсы работают', color: 'var(--ink)', go: '/catalog?inactive=1' },
  ] : [];

  return (
    <div className="admin-dash">
      <div className="pad metrics">
        {s ? metrics.map((m) => (
          <button key={m.label} type="button" className="rv-card rv-metric mbtn" onClick={() => navigate(m.go)}>
            <span className="rv-cap">{m.label}</span><b className="rv-tnum" style={{ color: m.color }}>{m.value}</b><span className="rv-sm rv-muted">{m.sub}</span>
          </button>
        )) : Array.from({ length: 4 }, (_, i) => <div key={i} className="rv-card rv-skel" style={{ height: 106 }} />)}
      </div>

      {s && <Heatmap stats={s} nowHour={nowHour} />}

      <div className="admin-cols">
        <section className="sec">
          <h2 className="h2m" style={{ marginBottom: 10 }}>Ближайшие брони</h2>
          <div className="rv-card" style={{ padding: '4px 0' }}>
            {next.length === 0 && <p className="rv-muted" style={{ margin: 0, padding: '12px 16px' }}>Сегодня больше ничего не запланировано.</p>}
            {next.map((b) => (
              <Link key={b.id} to={`/bookings/${b.id}`} className="arow">
                <span className="rv-tnum arow-t">{interval(b.startAt, b.endAt).replace(/ \(.*\)| след\. дня/, '')}</span>
                <span style={{ minWidth: 0, flex: 1 }}><b className="ell" style={{ fontSize: 14 }}>{bookingTitle(b)}{b.resource.spaceName ? ` · ${b.resource.spaceName}` : ''}</b><span className="rv-sm rv-muted ell">{b.user.fullName}{b.title ? ` · ${b.title}` : ''}</span></span>
                <ChevronRight className="rv-ic rv-ic-sm" aria-hidden style={{ color: 'var(--ink-muted)' }} />
              </Link>
            ))}
          </div>
        </section>
        <section className="sec" style={{ paddingBottom: 24 }}>
          <h2 className="h2m" style={{ marginBottom: 12 }}>Последние действия</h2>
          <Timeline logs={audit} meId={user?.id} withEntity />
        </section>
      </div>
    </div>
  );
}

/** Тепловая карта: строки — ресурсы и коворкинги, столбцы — часы. Сотни строк прокручиваются, а поиск сужает список. */
function Heatmap({ stats, nowHour }: { stats: NonNullable<ReturnType<typeof useStats>['data']>; nowHour: number }) {
  const [term, setTerm] = useState('');
  const t = term.trim().toLowerCase();
  // Сервер уже прислал только то, что стоит показать: коворкинги и ресурсы с бронями, самые загруженные первыми.
  const rows = useMemo(() => stats.heatmap.filter((r) => !t || r.name.toLowerCase().includes(t)), [stats.heatmap, t]);
  const resources = stats.heatmap.filter((r) => r.type === 'resource').length;
  const level = (p: number | null) => (p == null ? null : p === 0 ? 0 : p <= 25 ? 1 : p <= 50 ? 2 : p <= 75 ? 3 : 4);
  const LIMIT = 80;

  return (
    <section className="sec" style={{ marginTop: 16 }}>
      <div className="rv-card" style={{ padding: '16px 0 14px' }}>
        <div style={{ padding: '0 16px 12px', display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'flex-end', justifyContent: 'space-between' }}>
          <div>
            <h2 className="h2m">Загрузка сегодня</h2>
            <p className="rv-sm rv-muted" style={{ margin: '2px 0 0' }}>
              {dayLabel(stats.date)} · коворкинги и {np(resources, 'ресурс', 'ресурса', 'ресурсов')} с бронями из {stats.activeResources} · чем темнее ячейка, тем плотнее час
            </p>
          </div>
          {stats.heatmap.length > 12 && (
            <div style={{ width: 260, maxWidth: '100%' }}><SearchField value={term} onChange={setTerm} placeholder="Ресурс или коворкинг" label="Найти ресурс на карте" /></div>
          )}
        </div>
        <div className="heat-wrap">
          <div className="hrow"><span className="lbl" />{stats.hours.map((h) => <span key={h} className={`hr${h === nowHour ? ' now' : ''}`}>{h}</span>)}</div>
          {rows.slice(0, LIMIT).map((r) => (
            <div key={`${r.type}${r.id}`} className="hrow">
              <Link className="lbl" title={r.name} to={r.type === 'space' ? `/spaces/${r.id}` : `/resources/${r.id}`} style={{ color: 'inherit', textDecoration: 'none' }}>{r.name}</Link>
              {r.busyPercent.map((p, i) => {
                const v = level(p);
                return <span key={i} className={`hc${v == null ? ' off' : ''}`} data-v={v ?? undefined} title={`${r.name}, ${stats.hours[i]}:00 — ${p == null ? 'не работает' : `занято ${p}%`}`} />;
              })}
            </div>
          ))}
          {rows.length === 0 && <p className="rv-sm rv-muted" style={{ margin: '8px 0' }}>{t ? 'Ничего не нашлось.' : 'В этот день пока ничего не забронировано.'}</p>}
        </div>
        {rows.length > LIMIT && <p className="rv-sm rv-muted" style={{ padding: '8px 16px 0', margin: 0 }}>Показаны первые {LIMIT} из {rows.length} — уточните поиск.</p>}
        <div className="rv-legend" style={{ padding: '10px 16px 0' }}>
          <span><i style={{ background: 'var(--surface-muted)' }} />Свободно</span>
          <span><i style={{ background: 'var(--accent)', opacity: 0.5 }} />Наполовину</span>
          <span><i style={{ background: 'var(--accent)' }} />Занято</span>
          <span><i style={{ background: 'repeating-linear-gradient(135deg, var(--slot-offhours) 0 3px, var(--border-strong) 3px 4px)' }} />Не работает</span>
        </div>
      </div>
    </section>
  );
}

function Queue() {
  const list = useAdminBookings({ status: 'Pending', from: hourNow(), sortDir: 'Asc' });
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const total = list.data?.pages[0]?.totalCount ?? 0;
  const [reject, setReject] = useState<Booking | null>(null);

  if (!list.isLoading && items.length === 0) {
    return (
      <Empty icon={Sparkles} tone="ok" title="Все заявки рассмотрены" text="Новые заявки появятся здесь сами — обновлять страницу не нужно.">
        <Link to="/admin/all" className="rv-btn rv-btn-secondary rv-btn-lg">Посмотреть все брони</Link>
      </Empty>
    );
  }

  return (
    <div className="pad" style={{ display: 'flex', flexDirection: 'column', gap: 12, paddingBottom: 24 }}>
      <p className="rv-sm rv-muted" style={{ margin: 0 }} aria-live="polite">{np(total, 'заявка', 'заявки', 'заявок')} · новые появятся здесь сами</p>
      <div className="grid-cards">{items.map((b) => <Request key={b.id} b={b} onReject={() => setReject(b)} />)}</div>
      <LoadMore hasNext={!!list.hasNextPage} loading={list.isFetchingNextPage} onLoad={() => list.fetchNextPage()} />
      {reject && <RejectSheet b={reject} onClose={() => setReject(null)} />}
    </div>
  );
}

function Request({ b, onReject }: { b: Booking; onReject: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const now = useNow();
  const [busy, setBusy] = useState(false);
  const left = minutesBetween(new Date(now), b.startAt);
  const urgent = left < 180;

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

  return (
    <article className="rv-card" style={{ padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 12 }}>
      {urgent && <div className="rv-banner rv-banner-warn" style={{ padding: '10px 12px' }}><Hourglass className="rv-ic" aria-hidden /><span className="rv-sm" style={{ fontWeight: 700 }}>Начнётся через {duration(Math.max(0, left))}. Если не решить до начала, заявка отклонится автоматически.</span></div>}
      <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
        <span className="rv-avatar">{initials(b.user.fullName)}</span>
        <div style={{ minWidth: 0, flex: 1 }}><b className="ell">{b.user.fullName}</b><span className="rv-sm rv-muted ell">{b.user.email}</span></div>
        <span className="rv-sm rv-muted" style={{ whiteSpace: 'nowrap' }}>{agoLabel(b.createdAt)}</span>
      </div>
      <Link to={`/bookings/${b.id}`} className="bare" style={{ display: 'flex', flexDirection: 'column', gap: 2, textAlign: 'left', textDecoration: 'none' }}>
        <span className="upc-n">{b.resource.name}</span>
        <span className="rv-tnum" style={{ fontWeight: 700 }}>{dayLabel(local(b.startAt).key)}, {interval(b.startAt, b.endAt)}</span>
        <span className="rv-sm rv-muted">{[b.title, b.attendees ? people(b.attendees) : null].filter(Boolean).join(' · ')}</span>
        <span className="relx">До начала {duration(Math.max(0, left))}</span>
      </Link>
      <div className="two">
        <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={onReject}>Отклонить</button>
        <button type="button" className="rv-btn rv-btn-primary rv-btn-lg" aria-busy={busy} onClick={approve}><Check className="rv-ic rv-ic-sm" aria-hidden />Одобрить</button>
      </div>
    </article>
  );
}

const statusFilters: { value: BookingStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'Все' },
  { value: 'Pending', label: 'На согласовании' },
  { value: 'Confirmed', label: 'Подтверждена' },
  { value: 'Completed', label: 'Завершена' },
  { value: 'Cancelled', label: 'Отменена' },
  { value: 'Rejected', label: 'Отклонена' },
];

function AllBookings() {
  const [term, setTerm] = useState('');
  const [status, setStatus] = useState<BookingStatus | 'all'>('all');
  const search = useDebounced(term.trim());
  const list = useAdminBookings({ search, status: status === 'all' ? [] : [status], sortDir: 'Desc' });
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const total = list.data?.pages[0]?.totalCount ?? 0;
  const [reject, setReject] = useState<Booking | null>(null);
  const [cancel, setCancel] = useState<Booking | null>(null);
  const qc = useQueryClient();
  const toast = useToast();

  const approve = async (b: Booking) => {
    await api(`/admin/bookings/${b.id}/approve`, { method: 'POST' });
    await invalidateBookings(qc);
    toast('success', 'Заявка одобрена', `${b.user.fullName} получит уведомление.`);
  };

  return (
    <>
      <div style={{ padding: '0 16px 12px', maxWidth: 560 }}><SearchField value={term} onChange={setTerm} placeholder="Ресурс или владелец" label="Поиск по ресурсу или владельцу" /></div>
      <div className="hs" style={{ paddingBottom: 12 }}>{statusFilters.map((f) => <Chip key={f.value} on={status === f.value} onClick={() => setStatus(f.value)}>{f.label}</Chip>)}</div>
      <p className="rv-sm rv-muted pad" style={{ margin: '0 0 10px' }}>{list.isLoading ? 'Загружаем…' : np(total, 'бронь', 'брони', 'броней')}</p>
      {!list.isLoading && items.length === 0 && (
        <Empty icon={Search} title="Ничего не найдено" text="Попробуйте другой запрос или статус.">
          <button type="button" className="rv-btn rv-btn-secondary" onClick={() => { setTerm(''); setStatus('all'); }}>Сбросить фильтры</button>
        </Empty>
      )}
      <div className="grid-cards pad" style={{ paddingBottom: 12 }}>
        {items.map((b) => {
          const st = effectiveStatus(b);
          const pend = b.status === 'Pending' && new Date(b.startAt) > new Date();
          const canCancel = b.status === 'Confirmed' && !hasEnded(b);
          return (
            <article key={b.id} className="rv-card" style={{ padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 10 }}>
              <Link to={`/bookings/${b.id}`} className="bare" style={{ display: 'flex', flexDirection: 'column', gap: 2, textAlign: 'left', textDecoration: 'none' }}>
                <span style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center', width: '100%' }}>
                  <span className="upc-n ell" style={{ minWidth: 0 }}>{bookingTitle(b)}{b.resource.spaceName ? ` · ${b.resource.spaceName}` : ''}</span>
                  <StatusChip status={st} />
                </span>
                <span className="rv-tnum" style={{ fontWeight: 700 }}>{dayLabel(local(b.startAt).key)}, {interval(b.startAt, b.endAt)}</span>
                <span className="rv-sm rv-muted">{b.user.fullName}{b.title ? ` · ${b.title}` : ''}</span>
              </Link>
              {pend && (
                <div className="two">
                  <button type="button" className="rv-btn rv-btn-secondary" onClick={() => setReject(b)}>Отклонить</button>
                  <button type="button" className="rv-btn rv-btn-secondary" onClick={() => approve(b)}><Check className="rv-ic rv-ic-sm" aria-hidden style={{ color: 'var(--status-confirmed)' }} />Одобрить</button>
                </div>
              )}
              {canCancel && <button type="button" className="rv-btn rv-btn-secondary rv-btn-block" onClick={() => setCancel(b)}>Отменить с причиной</button>}
            </article>
          );
        })}
      </div>
      <LoadMore hasNext={!!list.hasNextPage} loading={list.isFetchingNextPage} onLoad={() => list.fetchNextPage()} />
      {reject && <RejectSheet b={reject} onClose={() => setReject(null)} />}
      {cancel && <CancelSheet b={cancel} onClose={() => setCancel(null)} />}
    </>
  );
}
