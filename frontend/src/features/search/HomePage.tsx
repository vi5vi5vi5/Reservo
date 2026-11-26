import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { CalendarCheck, ChevronRight, Clock, DoorOpen, Inbox, Monitor, Package } from 'lucide-react';
import { useAdminBookings, useAvailable, useCategories, useFeatures, useLocations, useMyBookings, useSpaces } from '../../api/hooks';
import { useAuth } from '../../app/auth';
import { hourNow } from '../../app/Shell';
import { featureWith } from '../../lib/icons';
import { durationWords, initials, np, plural, untilLabel } from '../../lib/text';
import { addDays, dayLabel, hhmm, iso, local, nextSlot, relDay, todayKey } from '../../lib/time';
import { Logo } from '../../ui/controls';
import { BookingCard } from '../bookings/BookingCard';
import { ParamSheet, type ParamMode } from './ParamSheet';
import { endMinutes, isEquipment, isRoom, isSeat, MORNING, useSearch, whatLabel, type SearchQuery } from './state';
import { WhatSheet } from './WhatSheet';

function greeting(name?: string) {
  const h = Math.floor(local(new Date()).minutes / 60);
  const part = h < 5 ? 'Доброй ночи' : h < 12 ? 'Доброе утро' : h < 18 ? 'Добрый день' : 'Добрый вечер';
  return name ? `${part}, ${name.split(' ')[0]}` : part;
}

/** Главная: поиск фразой, где каждое выделенное слово нажимается и открывает свой выбор. */
export function HomePage() {
  const { user, isAdmin } = useAuth();
  const navigate = useNavigate();
  const { query: q, set } = useSearch();
  const categories = useCategories().data;
  const features = useFeatures().data ?? [];
  const locations = useLocations().data ?? [];
  const spacesAll = useSpaces({}).data ?? [];
  const [sheet, setSheet] = useState<'what' | ParamMode | null>(null);

  const seat = isSeat(q.what);
  const room = isRoom(q.what);
  const equipment = isEquipment(q.what);
  const end = endMinutes(q);
  const today = todayKey();

  const when = q.now ? 'сейчас' : q.day === today ? `сегодня в ${hhmm(q.start)}` : q.day === addDays(today, 1) ? `завтра в ${hhmm(q.start)}` : `${dayLabel(q.day)} в ${hhmm(q.start)}`;
  const ft = q.featureIds.map((id) => features.find((f) => f.id === id)).filter(Boolean);
  const ftLabel = ft.length ? featureWith(ft[0]!.code, ft[0]!.name) + (ft.length > 1 ? ` и ещё ${ft.length - 1}` : '') : 'любым оснащением';
  const loc = seat
    ? (spacesAll.find((s) => s.id === q.spaceId)?.name.replace(/^Коворкинг\s*/, '') ?? 'любом коворкинге')
    : (locations.find((l) => l.id === q.locationId)?.name ?? 'любом корпусе');
  const endDay = Math.floor(end / 1440);
  const endNote = equipment && (q.night || end > 1440)
    ? `Закончится ${endDay === 0 ? '' : relDay(addDays(q.day, endDay)).toLowerCase() + ' в '}${hhmm(end)}. Ночью оборудование работает само — забрать и вернуть можно в часы выдачи.`
    : null;

  const go = () => {
    if (seat && q.spaceId) navigate(`/spaces/${q.spaceId}`);
    else navigate('/search');
  };

  const examples = useExamples(categories, features.find((f) => f.code === 'projector')?.id);

  return (
    <div className="page">
      <header className="bar only-phone" style={{ minHeight: 64, paddingRight: 16 }}>
        <Logo />
        <span style={{ flex: 1 }} />
        {user
          ? <Link to="/profile" className="rv-avatar avbtn" aria-label="Профиль" style={{ display: 'inline-grid', placeContent: 'center' }}>{initials(user.fullName)}</Link>
          : <Link to="/login" className="rv-btn rv-btn-secondary">Войти</Link>}
      </header>

      <div className="home">
        <section className="hero">
          <h1 className="rv-sr">Найти свободное</h1>
          <p className="hero-greet">{greeting(user?.fullName)}</p>
          <p className="phrase">
            Найти{' '}
            <button type="button" className="tok" onClick={() => setSheet('what')}>{whatLabel(q.what, categories)}</button>{' '}
            {room && (
              <>
                на <button type="button" className={`tok${q.ppl ? '' : ' tok-ghost'}`} onClick={() => setSheet('ppl')}>{q.ppl ? np(q.ppl, 'человека', 'человека', 'человек') : 'любое число людей'}</button>{' '}
                с <button type="button" className={`tok${ft.length ? '' : ' tok-ghost'}`} onClick={() => setSheet('ft')}>{ftLabel}</button>{' '}
              </>
            )}
            <button type="button" className="tok" onClick={() => setSheet('time')}>{when}</button>{' '}
            на <button type="button" className="tok" onClick={() => setSheet(equipment ? 'dur' : 'time')}>{durationWords(q.dur, q.night)}</button>{' '}
            в <button type="button" className={`tok${(seat ? q.spaceId : q.locationId) ? '' : ' tok-ghost'}`} onClick={() => setSheet('loc')}>{loc}</button>
          </p>
          {endNote && <p className="rv-hint hintrow" style={{ margin: '12px 0 0' }}><Clock className="rv-ic rv-ic-sm" aria-hidden />{endNote}</p>}
          <button type="button" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block hero-cta" style={{ marginTop: 20 }} onClick={go}>
            {seat ? 'Показать свободные места' : 'Найти свободное'}
          </button>
          {examples.length > 0 && (
            <div className="ex">
              <span className="rv-sm rv-muted">Например</span>
              {examples.map((e) => <button key={e.label} type="button" className="exb" onClick={() => set(e.patch)}>{e.label}</button>)}
            </div>
          )}
        </section>

        <div className="home-side">
          {isAdmin && <AdminCard />}
          <NowSection />
          {user ? <Upcoming /> : <GuestCard />}
        </div>
      </div>

      {sheet === 'what' && <WhatSheet onClose={() => setSheet(null)} />}
      {sheet && sheet !== 'what' && <ParamSheet mode={sheet} onMode={setSheet} onClose={() => setSheet(null)} />}
    </div>
  );
}

/** Примеры запросов — только для тех категорий, что есть в каталоге. */
function useExamples(categories: ReturnType<typeof useCategories>['data'], projectorId?: number) {
  const byCode = (code: string) => categories?.find((c) => c.code === code);
  const slot = nextSlot();
  const tomorrow = addDays(todayKey(), 1);
  const list: { label: string; patch: Partial<SearchQuery> }[] = [];
  const meeting = byCode('meeting-room');
  if (meeting) list.push({ label: 'Переговорная на 4 завтра', patch: { what: { type: 'category', categoryId: meeting.id, kind: 'Room' }, day: tomorrow, start: 15 * 60, dur: 120, ppl: 4 } });
  const classroom = byCode('classroom');
  if (classroom) list.push({ label: 'Аудитория с проектором', patch: { what: { type: 'category', categoryId: classroom.id, kind: 'Room' }, day: tomorrow, start: 10 * 60, dur: 90, ppl: 20, featureIds: projectorId ? [projectorId] : [] } });
  const projectors = byCode('projectors');
  if (projectors) list.push({ label: 'Проектор на пару', patch: { what: { type: 'category', categoryId: projectors.id, kind: 'Equipment' }, now: true, dur: 90 } });
  const printers = byCode('3d-printers');
  if (printers) list.push({ label: '3D-принтер на ночь', patch: { what: { type: 'category', categoryId: printers.id, kind: 'Equipment' }, day: slot.key, start: 18 * 60, night: true, dur: 1440 + MORNING - 18 * 60 } });
  const laptops = byCode('laptops');
  if (laptops) list.push({ label: 'Ноутбук на 3 дня', patch: { what: { type: 'category', categoryId: laptops.id, kind: 'Equipment' }, now: true, dur: 4320 } });
  return list;
}

function AdminCard() {
  const pending = useAdminBookings({ status: 'Pending', from: hourNow(), pageSize: 1, sortDir: 'Asc' }, true).data?.pages[0];
  const count = pending?.totalCount ?? 0;
  const first = pending?.items[0];
  if (!count) return null;
  return (
    <Link to="/admin/queue" className="rv-card rowcard" style={{ width: '100%' }}>
      <span className="icirc" style={{ background: 'var(--status-pending-bg)', color: 'var(--status-pending)' }}><Inbox className="rv-ic" aria-hidden /></span>
      <span style={{ flex: 1, minWidth: 0 }}>
        <b className="ell" style={{ fontSize: 15 }}>{np(count, 'заявка ждёт', 'заявки ждут', 'заявок ждут')} согласования</b>
        {first && <span className="rv-sm rv-muted ell">Ближайшая: {first.resource.name}, {untilLabel(first.startAt)}</span>}
      </span>
      <ChevronRight className="rv-ic" aria-hidden style={{ color: 'var(--ink-muted)' }} />
    </Link>
  );
}

/** «Свободно прямо сейчас»: коворкинги со счётчиком мест и сколько помещений и оборудования свободно на ближайший час. */
function NowSection() {
  const { set } = useSearch();
  const navigate = useNavigate();
  const slot = nextSlot();
  const from = iso(slot.key, slot.minutes);
  const to = iso(slot.key, slot.minutes + 60);
  const spaces = useSpaces({ from, to }).data ?? [];
  const rooms = useAvailable({ from, to, kinds: ['Room'], pageSize: 1 }).data?.pages[0];
  const equipment = useAvailable({ from, to, kinds: ['Equipment'], pageSize: 1 }).data?.pages[0];

  const rows = [
    ...spaces
      .filter((s) => s.freeSeats != null && s.totalSeats > 0)
      .sort((a, b) => (b.freeSeats ?? 0) - (a.freeSeats ?? 0))
      .slice(0, 3)
      .map((s) => ({
        key: `s${s.id}`,
        icon: Monitor,
        title: s.name,
        sub: `${s.location.name}${s.isQuiet ? ' · тихая зона' : s.closeTime ? ` · до ${s.closeTime.slice(0, 5)}` : ''}`,
        big: String(s.freeSeats),
        unit: plural(s.freeSeats ?? 0, 'место', 'места', 'мест'),
        go: () => {
          set({ what: { type: 'seat', pc: false }, now: true, dur: 120, spaceId: s.id });
          navigate(`/spaces/${s.id}`);
        },
      })),
    ...(rooms ? [{
      key: 'rooms', icon: DoorOpen, title: 'Помещения', sub: 'Переговорные, аудитории, лаборатории',
      big: String(rooms.totalCount), unit: 'свободно на час',
      go: () => { set({ what: { type: 'kind', kind: 'Room' }, now: true, dur: 60 }); navigate('/search'); },
    }] : []),
    ...(equipment ? [{
      key: 'eq', icon: Package, title: 'Оборудование', sub: 'Можно взять прямо сейчас',
      big: String(equipment.totalCount), unit: 'свободно',
      go: () => { set({ what: { type: 'kind', kind: 'Equipment' }, now: true, dur: 120 }); navigate('/search'); },
    }] : []),
  ];

  return (
    <section className="sec">
      <div className="sec-h"><h2 className="h2m">Свободно прямо сейчас</h2><span className="rv-sm rv-muted rv-tnum">на {hhmm(slot.minutes)}</span></div>
      <div className="nowlist rv-card">
        {rows.length === 0 && Array.from({ length: 3 }, (_, i) => (
          <div key={i} className="nowr"><span className="rv-skel" style={{ width: 40, height: 40, borderRadius: '50%' }} /><span className="nowr-b"><span className="rv-skel" style={{ height: 14, width: '60%' }} /></span></div>
        ))}
        {rows.map((r) => (
          <button key={r.key} type="button" className="nowr" onClick={r.go}>
            <span className="icirc" style={{ background: 'var(--accent-soft)', color: 'var(--accent-text)' }}><r.icon className="rv-ic" aria-hidden /></span>
            <span className="nowr-b"><b className="ell">{r.title}</b><span className="rv-sm rv-muted ell">{r.sub}</span></span>
            <span className="nowr-v"><b className="rv-tnum">{r.big}</b><small>{r.unit}</small></span>
          </button>
        ))}
      </div>
    </section>
  );
}

function Upcoming() {
  const list = useMyBookings({ status: ['Pending', 'Confirmed'], from: hourNow(), pageSize: 3 }).data?.pages[0]?.items ?? [];
  const items = list.filter((b) => new Date(b.endAt).getTime() > Date.now());
  if (!items.length) return null;
  return (
    <section className="sec">
      <div className="sec-h"><h2 className="h2m">Ваши ближайшие брони</h2><Link to="/bookings" className="rv-btn rv-btn-ghost">Все</Link></div>
      <div className="list">{items.map((b) => <BookingCard key={b.id} b={b} compact />)}</div>
    </section>
  );
}

function GuestCard() {
  return (
    <section className="rv-card sec guest" style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
        <span className="icirc" style={{ background: 'var(--accent-soft)', color: 'var(--accent-text)' }}><CalendarCheck className="rv-ic" aria-hidden /></span>
        <div>
          <b style={{ display: 'block', fontSize: 16 }}>Войдите, чтобы бронировать</b>
          <span className="rv-sm rv-muted">Смотреть каталог и расписание можно и так. Ваши брони, напоминания и уведомления появятся после входа.</span>
        </div>
      </div>
      <div className="two">
        <Link to="/login" className="rv-btn rv-btn-secondary">Войти</Link>
        <Link to="/register" className="rv-btn rv-btn-ghost">Регистрация</Link>
      </div>
    </section>
  );
}
