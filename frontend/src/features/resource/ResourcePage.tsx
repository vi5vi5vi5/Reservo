import { useEffect, useMemo, useRef, useState, type PointerEvent } from 'react';
import { useLocation, useParams, useSearchParams } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import {
  ChevronLeft, ChevronRight, CircleAlert, Clock, Info, MapPin, Minus, MousePointerClick, Plus, RefreshCw, ShieldCheck, Timer,
  Users, Wrench,
} from 'lucide-react';
import { api } from '../../api/client';
import { invalidateBookings, keys, useAdminBookings, useResource, useSchedule } from '../../api/hooks';
import type { Resource } from '../../api/types';
import { useAuth } from '../../app/auth';
import { useLive, useLiveSubscription } from '../../app/live';
import { hourNow, useHideTabs, useRequireAuth } from '../../app/Shell';
import { useToast } from '../../app/toast';
import { categoryIcon, featureIcon } from '../../lib/icons';
import { duration, np, people, where } from '../../lib/text';
import { addDays, DOW, dayLabel, dayNum, dow, hhmm, intervalMin, iso, local, nextSlot, parseTime, relDay, SLOT, todayKey } from '../../lib/time';
import { useNow } from '../../lib/useNow';
import { BackButton, Banner, Empty, Seg } from '../../ui/controls';
import { Sheet } from '../../ui/Sheet';
import { BookingFlow, type Draft } from '../booking/BookingFlow';
import { blockedReason, busyMinutes, eventsOfDay, overlaps, type Ev } from './schedule';

const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);
const PX = 0.8; // 48px на час, как в сетке дизайн-системы
const MAX_DAYS = 30;

interface Sel { s: number; e: number }
type Note = { kind: 'err' | 'info'; text: string } | null;

export function ResourcePage() {
  useHideTabs();
  const id = Number(useParams().id);
  const { data: r, isLoading, error } = useResource(id);
  useLiveSubscription('Resource', id);

  if (isLoading) return <div className="page"><div className="rv-skel" style={{ height: 188, borderRadius: 0 }} /><div className="pad" style={{ paddingTop: 16 }}><div className="rv-skel" style={{ height: 28, width: '70%' }} /></div></div>;
  if (error || !r) return <div className="page"><div className="bar"><BackButton fallback="/catalog" /></div><Empty icon={CircleAlert} title="Такого ресурса нет" text="Возможно, ссылка устарела. Найдите нужное в каталоге." /></div>;
  return <ResourceView r={r} />;
}

function ResourceView({ r }: { r: Resource }) {
  const { isAdmin } = useAuth();
  const requireAuth = useRequireAuth();
  const location = useLocation();
  const [params] = useSearchParams();
  const now = useNow();
  const today = todayKey();
  const open = parseTime(r.openTime);
  const close = parseTime(r.closeTime);
  const max = r.maxDurationMinutes ?? 240;
  const Icon = categoryIcon(r.category.code, r.category.kind);

  const initialDay = params.get('day') ?? today;
  const [day, setDay] = useState(initialDay);
  const [weekStart, setWeekStart] = useState(() => addDays(initialDay, -dow(initialDay)));
  const [view, setView] = useState<'day' | 'week'>('day');
  const [sel, setSel] = useState<Sel | null>(() => {
    const f = params.get('from');
    const t = params.get('to');
    if (!f || !t) return null;
    const lf = local(f);
    return { s: lf.minutes, e: lf.minutes + Math.round((new Date(t).getTime() - new Date(f).getTime()) / 60_000) };
  });
  const [note, setNote] = useState<Note>(null);
  const [draft, setDraft] = useState<Draft | null>((location.state as { resume?: { draft?: Draft } } | null)?.resume?.draft ?? null);
  const [manage, setManage] = useState<'deact' | null>(null);

  // Расписание грузится сразу на неделю: его же используют полоска дней, неделя и шкала на 3 дня.
  const rangeStart = r.allowsOvernight ? day : weekStart;
  const rangeDays = r.allowsOvernight ? 3 : 7;
  const { data: schedule } = useSchedule(r.id, iso(addDays(rangeStart, -1), 0), iso(addDays(rangeStart, rangeDays + 1), 0));
  const items = schedule?.items ?? [];
  const evs = useMemo(() => eventsOfDay(items, day), [items, day]);

  const slot = nextSlot(new Date(now));
  const minStart = day === slot.key ? slot.minutes : day < slot.key ? 1440 * 2 : 0;
  const opts = { open, close, minStart, overnight: r.allowsOvernight };

  // Живое обновление: если выбранное время только что заняли — снимаем выбор и объясняем почему.
  const { lastChange } = useLive();
  const prevChange = useRef(lastChange?.at);
  useEffect(() => {
    if (!lastChange || lastChange.at === prevChange.current || lastChange.resourceId !== r.id) return;
    prevChange.current = lastChange.at;
    if (sel && blockedReason(evs, sel.s, sel.e, opts) === 'busy') {
      setSel(null);
      setNote({ kind: 'err', text: 'Пока вы выбирали, это время заняли. Выберите другое — расписание уже обновилось.' });
    } else {
      setNote({ kind: 'info', text: 'Расписание только что обновилось само.' });
    }
  }, [lastChange, evs]);

  const pickDay = (d: string) => {
    setDay(d);
    setSel(null);
    setNote(null);
  };

  const book = () => {
    if (!sel) return;
    const d: Draft = { resource: r, startAt: iso(day, sel.s), endAt: iso(day, sel.e) };
    if (requireAuth({ draft: d })) setDraft(d);
  };

  const off = !r.isActive;
  const selReason = sel ? blockedReason(evs, sel.s, sel.e, opts) : null;

  return (
    <div className="page res-page">
      <div className="res-grid">
        <div className="res-info">
          <div className="rv-photo res-photo">
            {r.imageUrl ? <img src={r.imageUrl} alt="" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'cover' }} /> : <span className="rv-photo-grid" />}
            {!r.imageUrl && <Icon className="rv-ic" aria-hidden />}
            <div className="ovl-top"><BackButton fallback="/catalog" outline /></div>
          </div>
          <div className="pad" style={{ paddingTop: 16, display: 'flex', flexDirection: 'column', gap: 6 }}>
            <p className="rv-cap" style={{ margin: 0 }}>{r.category.name}</p>
            <h1 className="h1m">{r.name}</h1>
            {(r.requiresApproval || off) && (
              <div className="rv-row" style={{ gap: 6, marginTop: 2 }}>
                {r.requiresApproval && <span className="rv-badge rv-badge-approval"><ShieldCheck className="rv-ic" aria-hidden />Нужно согласование</span>}
                {off && <span className="rv-badge rv-badge-off"><Wrench className="rv-ic" aria-hidden />Временно недоступен</span>}
              </div>
            )}
          </div>
          {off && (
            <div className="pad" style={{ marginTop: 16 }}>
              <Banner icon={Wrench} tone="danger" title="Ресурс временно недоступен">
                {r.deactivationReason ? `Причина: ${r.deactivationReason}. ` : ''}Бронировать пока нельзя — выберите похожий ресурс в каталоге.
              </Banner>
            </div>
          )}
          <div className="pad mlist">
            <div className="mrow"><MapPin className="rv-ic" aria-hidden /><div><div>{r.seat ? `${r.seat.spaceName}, место ${r.seat.number}` : where(r.location.name, r.floor)}</div><div className="rv-sm rv-muted">{r.location.name}{r.location.address ? `, ${r.location.address}` : ''}</div></div></div>
            {r.capacity != null && <div className="mrow"><Users className="rv-ic" aria-hidden /><div>Вместимость: до {people(r.capacity)}</div></div>}
            <div className="mrow"><Clock className="rv-ic" aria-hidden /><div>{r.allowsOvernight ? 'Выдача и возврат' : 'Работает'} <span className="rv-tnum">{hhmm(open)}–{hhmm(close)}</span></div></div>
            <div className="mrow"><Timer className="rv-ic" aria-hidden /><div>Бронь до {duration(max)}, шаг 15 минут{r.allowsOvernight ? ', можно на ночь' : ''}</div></div>
          </div>
          {r.features.length > 0 && (
            <div className="pad rv-row" style={{ gap: 6, marginTop: 14 }}>
              {r.features.map((f) => { const F = featureIcon(f.code); return <span key={f.id} className="rv-badge"><F className="rv-ic" aria-hidden />{f.name}</span>; })}
            </div>
          )}
          {r.description && <p className="pad" style={{ margin: '14px 0 0', color: 'var(--ink-muted)' }}>{r.description}</p>}
          {r.requiresApproval && !off && <div className="pad" style={{ marginTop: 16 }}><Banner icon={ShieldCheck}>Бронь подтверждает администратор — мы пришлём уведомление о решении.</Banner></div>}
          {r.allowsOvernight && r.pickupInfo && <div className="pad" style={{ marginTop: 16 }}><Banner icon={Clock}>{r.pickupInfo}</Banner></div>}
          {isAdmin && <AdminManage r={r} onDeactivate={() => setManage('deact')} />}
        </div>

        <div className="res-sched">
          {!off && (r.allowsOvernight ? (
            <LongScale r={r} day={day} items={items} sel={sel} now={now} onDay={pickDay} onSelect={(d, s) => { setDay(d); setSel(s); }} setNote={setNote} />
          ) : (
            <>
              <section className="sec">
                <div className="sec-h">
                  <h2 className="h2m">Расписание</h2>
                  <Seg small value={view} onChange={(v) => { setView(v); if (v === 'week') setSel(null); }} label="Вид" options={[{ value: 'day', label: 'День' }, { value: 'week', label: 'Неделя' }]} />
                </div>
                <div className="wknav">
                  <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="Предыдущая неделя" disabled={weekStart <= addDays(today, -dow(today))} onClick={() => { const w = addDays(weekStart, -7); setWeekStart(w); pickDay(w < today ? today : w); }}><ChevronLeft className="rv-ic" aria-hidden /></button>
                  <span className="rv-label" style={{ flex: 1, textAlign: 'center' }}>{dayNum(weekStart)}–{dayLabel(addDays(weekStart, 6)).split(', ')[1]}</span>
                  <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="Следующая неделя" disabled={addDays(weekStart, 7) > addDays(today, MAX_DAYS)} onClick={() => { const w = addDays(weekStart, 7); setWeekStart(w); pickDay(w); }}><ChevronRight className="rv-ic" aria-hidden /></button>
                  <button type="button" className="rv-btn rv-btn-secondary rv-btn-sm" disabled={day === today} onClick={() => { setWeekStart(addDays(today, -dow(today))); pickDay(today); }}>Сегодня</button>
                </div>
              </section>
              {view === 'day' ? (
                <>
                  <DayStrip weekStart={weekStart} day={day} items={items} open={open} close={close} onPick={pickDay} />
                  <Legend />
                  <DayTimeline evs={evs} sel={sel} open={open} close={close} max={max} minStart={minStart} isToday={day === slot.key} nowMin={local(new Date(now)).minutes} setSel={setSel} setNote={setNote} opts={opts} />
                </>
              ) : (
                <WeekView weekStart={weekStart} items={items} open={open} close={close} day={day} onPick={(d) => { pickDay(d); setView('day'); }} />
              )}
            </>
          ))}

          {!off && (
            <div className="dock">
              {!sel && <p className="rv-sm rv-muted dockhint"><MousePointerClick className="rv-ic rv-ic-sm" aria-hidden />{r.allowsOvernight ? 'Коснитесь свободного времени на шкале' : 'Коснитесь свободного времени или протяните по сетке'}</p>}
              {sel && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div className="rv-tnum" style={{ fontSize: 18, lineHeight: '24px', fontWeight: 800 }}>{intervalMin(sel.s, sel.e)}</div>
                    <div className="rv-sm rv-muted">{relDay(day)} · {duration(sel.e - sel.s)}</div>
                  </div>
                  <button type="button" className="rv-btn rv-btn-icon rv-btn-secondary ib" aria-label={`Короче на ${r.allowsOvernight ? 'час' : '15 минут'}`}
                    disabled={sel.e - sel.s <= (r.allowsOvernight ? 60 : SLOT)}
                    onClick={() => setSel({ ...sel, e: sel.e - (r.allowsOvernight ? 60 : SLOT) })}><Minus className="rv-ic" aria-hidden /></button>
                  <button type="button" className="rv-btn rv-btn-icon rv-btn-secondary ib" aria-label={`Длиннее на ${r.allowsOvernight ? 'час' : '15 минут'}`}
                    disabled={sel.e - sel.s + SLOT > max || (!r.allowsOvernight && sel.e + SLOT > close)}
                    onClick={() => setSel({ ...sel, e: sel.e + (r.allowsOvernight ? 60 : SLOT) })}><Plus className="rv-ic" aria-hidden /></button>
                </div>
              )}
              {note && (
                <p className={note.kind === 'err' ? 'rv-error-text' : 'rv-hint hintrow'} style={{ margin: 0 }} role="status">
                  {note.kind === 'err' ? <CircleAlert className="rv-ic rv-ic-sm" aria-hidden /> : <Info className="rv-ic rv-ic-sm" aria-hidden />}{note.text}
                </p>
              )}
              {selReason === 'busy' && !note && <p className="rv-error-text" style={{ margin: 0 }}><CircleAlert className="rv-ic rv-ic-sm" aria-hidden />Это время уже занято — выберите свободное.</p>}
              <button type="button" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block" disabled={!sel || !!selReason} onClick={book}>
                {sel ? (r.requiresApproval ? `Отправить заявку на ${intervalMin(sel.s, sel.e)}` : `Забронировать ${intervalMin(sel.s, sel.e)}`) : 'Выберите время'}
              </button>
            </div>
          )}
        </div>
      </div>

      {draft && <BookingFlow draft={draft} onClose={() => { setDraft(null); setSel(null); }} />}
      {manage === 'deact' && <DeactivateSheet r={r} onClose={() => setManage(null)} />}
    </div>
  );
}

function Legend() {
  return (
    <div className="pad rv-legend" style={{ margin: '14px 0 10px' }}>
      <span><i style={{ background: 'var(--slot-free)', boxShadow: 'inset 0 0 0 1px var(--border-strong)' }} />Свободно</span>
      <span><i style={{ background: 'var(--slot-busy)' }} />Занято</span>
      <span><i style={{ background: 'var(--accent)' }} />Моя бронь</span>
      <span><i style={{ background: 'repeating-linear-gradient(135deg, var(--status-pending-bg) 0 3px, var(--surface) 3px 5px)', boxShadow: 'inset 0 0 0 1.5px var(--status-pending)' }} />Заявка</span>
    </div>
  );
}

/** Неделя полоской: день, число и сколько он уже занят. */
function DayStrip({ weekStart, day, items, open, close, onPick }: { weekStart: string; day: string; items: Parameters<typeof eventsOfDay>[0]; open: number; close: number; onPick: (d: string) => void }) {
  const today = todayKey();
  return (
    <div className="daystrip pad">
      {Array.from({ length: 7 }, (_, i) => {
        const d = addDays(weekStart, i);
        const dis = d < today || d > addDays(today, MAX_DAYS);
        const load = dis ? 0 : Math.round((busyMinutes(eventsOfDay(items, d), open, close) / Math.max(1, close - open)) * 100);
        return (
          <button key={d} type="button" className="dayb" aria-pressed={d === day} data-today={d === today ? '' : undefined} disabled={dis}
            aria-label={`${dayLabel(d)}${dis ? ', недоступно' : `, занято на ${load}%`}`} onClick={() => onPick(d)}>
            <small>{DOW[i]}</small><b>{dayNum(d)}</b><span className="load"><i style={{ width: `${load}%` }} /></span>
          </button>
        );
      })}
    </div>
  );
}

/** Сетка дня: касание выбирает час с этого места, протягивание — любой интервал. */
function DayTimeline({ evs, sel, open, close, max, minStart, isToday, nowMin, setSel, setNote, opts }: {
  evs: Ev[]; sel: Sel | null; open: number; close: number; max: number; minStart: number; isToday: boolean; nowMin: number;
  setSel: (s: Sel | null) => void; setNote: (n: Note) => void; opts: Parameters<typeof blockedReason>[3];
}) {
  const T0 = Math.max(0, Math.floor(Math.min(open, 8 * 60) / 60) * 60);
  const T1 = Math.min(1440, Math.ceil(Math.max(close, 21 * 60) / 60) * 60);
  const height = (T1 - T0) * PX;
  const col = useRef<HTMLDivElement>(null);
  const drag = useRef<{ a: number; moved: boolean } | null>(null);

  const pos = (e: PointerEvent) => {
    const rc = col.current!.getBoundingClientRect();
    const m = T0 + Math.floor(((e.clientY - rc.top) / PX) / SLOT) * SLOT;
    return Math.max(T0, Math.min(T1 - SLOT, m));
  };

  const tap = (m: number) => {
    const why = blockedReason(evs, m, m + SLOT, opts);
    if (why) {
      setSel(null);
      setNote({ kind: 'err', text: why === 'past' ? `Это время уже прошло — выберите позже ${hhmm(minStart)}.` : why === 'off' ? `Нерабочее время. Ресурс работает ${hhmm(open)}–${hhmm(close)}.` : 'Это время занято — выберите свободное.' });
      return;
    }
    let lim = Math.min(close, m + max, m + 60);
    evs.filter((x) => x.s >= m + SLOT && x.s < lim).forEach((x) => { lim = x.s; });
    setSel({ s: m, e: lim });
    setNote(lim - m < 60 ? { kind: 'info', text: `До следующей брони — только ${duration(lim - m)}.` } : null);
  };

  return (
    <div className="tl">
      <div className="tl-times" style={{ height }} aria-hidden>
        {Array.from({ length: (T1 - T0) / 60 + 1 }, (_, i) => <span key={i} style={{ top: i * 60 * PX }}>{hhmm(T0 + i * 60)}</span>)}
      </div>
      <div
        ref={col}
        className="tl-col"
        style={{ height }}
        aria-label="Сетка времени. Коснитесь свободного места или протяните по нему"
        onPointerDown={(e) => { e.currentTarget.setPointerCapture(e.pointerId); drag.current = { a: pos(e), moved: false }; }}
        onPointerMove={(e) => {
          const d = drag.current;
          if (!d) return;
          const m = pos(e);
          if (m !== d.a) d.moved = true;
          if (d.moved) setSel({ s: Math.min(d.a, m), e: Math.max(d.a, m) + SLOT });
        }}
        onPointerUp={() => {
          const d = drag.current;
          drag.current = null;
          if (!d) return;
          if (!d.moved) { tap(d.a); return; }
          if (sel && sel.e - sel.s > max) {
            setSel({ s: sel.s, e: sel.s + max });
            setNote({ kind: 'info', text: `Максимум для этого ресурса — ${duration(max)}. Выбор укоротили.` });
          } else setNote(null);
        }}
        onPointerCancel={() => { drag.current = null; }}
      >
        {open > T0 && <div className="rv-off" style={{ top: 0, height: (open - T0) * PX }} />}
        {close < T1 && <div className="rv-off" style={{ top: (close - T0) * PX, height: (T1 - close) * PX }} />}
        {minStart > T0 && <div className="tl-past" style={{ height: (Math.min(minStart, T1) - T0) * PX }} />}
        {evs.map((x, i) => {
          const s = Math.max(x.s, T0);
          const e = Math.min(x.e, T1);
          if (e <= s) return null;
          const h = (e - s) * PX;
          const mine = x.item.isMine;
          const pending = x.item.status === 'Pending';
          const cls = pending ? 'rv-ev rv-ev-pending' : mine ? 'rv-ev rv-ev-mine' : 'rv-ev rv-ev-busy';
          const title = mine || x.item.title ? (x.item.title || 'Ваша бронь') : 'Занято';
          const meta = `${intervalMin(x.s, x.e)}${x.item.ownerName && !mine ? ` · ${x.item.ownerName}` : ''}${pending ? ' · на согласовании' : ''}`;
          return (
            <div key={i} className={cls} style={{ top: (s - T0) * PX, height: h - 2 }}>
              <b>{title}</b>{h >= 34 && <span className="t">{meta}</span>}
            </div>
          );
        })}
        {isToday && nowMin > T0 && nowMin < T1 && <div className="rv-now" style={{ top: (nowMin - T0) * PX }} />}
        {sel && (
          <div className="rv-sel" data-conflict={evs.some((x) => (x.item.status === 'Pending' || x.item.status === 'Confirmed') && overlaps(sel.s, sel.e, x.s, x.e)) ? '' : undefined}
            style={{ top: (sel.s - T0) * PX, height: (sel.e - sel.s) * PX }}>
            {intervalMin(sel.s, sel.e)}
          </div>
        )}
      </div>
    </div>
  );
}

/** Неделя миниатюрами: видно, где плотно, и сколько часов ещё свободно. */
function WeekView({ weekStart, items, open, close, day, onPick }: { weekStart: string; items: Parameters<typeof eventsOfDay>[0]; open: number; close: number; day: string; onPick: (d: string) => void }) {
  const today = todayKey();
  const H = 130;
  const k = H / Math.max(60, close - open);
  return (
    <>
      <div className="wk pad">
        {Array.from({ length: 7 }, (_, i) => {
          const d = addDays(weekStart, i);
          const dis = d < today || d > addDays(today, MAX_DAYS);
          const evs = eventsOfDay(items, d);
          const freeH = Math.max(0, close - open - busyMinutes(evs, open, close)) / 60;
          return (
            <button key={d} type="button" className="wk-c" aria-pressed={d === day} data-today={d === today ? '' : undefined} disabled={dis}
              aria-label={`${dayLabel(d)}, свободно ${String(Math.round(freeH * 2) / 2).replace('.', ',')} ч`} onClick={() => onPick(d)}>
              <span className="wk-h"><small>{DOW[i]}</small><b>{dayNum(d)}</b></span>
              <span className="wk-m">
                {evs.map((x, j) => {
                  const s = Math.max(x.s, open);
                  const e = Math.min(x.e, close);
                  if (e <= s) return null;
                  return <i key={j} className={x.item.status === 'Pending' ? 'wk-pend' : x.item.isMine ? 'wk-mine' : 'wk-busy'} style={{ top: (s - open) * k, height: Math.max(2, (e - s) * k) }} />;
                })}
              </span>
              <span className="wk-f">{dis ? '—' : `${String(Math.round(freeH * 2) / 2).replace('.', ',')} ч`}</span>
            </button>
          );
        })}
      </div>
      <p className="pad rv-sm rv-muted" style={{ margin: '12px 0 0' }}>Под каждым днём — сколько часов ещё свободно. Коснитесь дня, чтобы выбрать время.</p>
    </>
  );
}

/** Оборудование «на ночь» и «на дни»: три дня полосками, ночи затенены, длительность выбирается чипами. */
function LongScale({ r, day, items, sel, now, onDay, onSelect, setNote }: {
  r: Resource; day: string; items: Parameters<typeof eventsOfDay>[0]; sel: Sel | null; now: number;
  onDay: (d: string) => void; onSelect: (d: string, s: Sel) => void; setNote: (n: Note) => void;
}) {
  const open = parseTime(r.openTime);
  const close = parseTime(r.closeTime);
  const max = r.maxDurationMinutes ?? 240;
  const [len, setLen] = useState<number | 'night'>(240);
  const today = todayKey();
  const slot = nextSlot(new Date(now));
  const days = [0, 1, 2].map((i) => addDays(day, i));
  const evs = eventsOfDay(items, day); // в минутах от полуночи первого дня, в том числе на следующие дни
  const lenOf = (v: number | 'night', s: number) => (v === 'night' ? Math.max(60, 1440 + open - s) : v);
  const free = (s: number, e: number) => !evs.some((x) => (x.item.status === 'Pending' || x.item.status === 'Confirmed') && overlaps(s, e, x.s, x.e));

  const options: { v: number | 'night'; label: string }[] = ([[120, '2 ч'], [240, '4 ч'], [480, '8 ч'], ['night', 'На ночь'], [1440, 'Сутки'], [2880, '2 дня'], [4320, '3 дня']] as [number | 'night', string][])
    .filter(([v]) => v === 'night' || v <= max)
    .map(([v, label]) => ({ v, label }));

  const tapAt = (i: number, x: number) => {
    const m = Math.max(0, Math.min(1425, Math.round(x * 96) * SLOT));
    const abs = i * 1440 + m;
    if (days[i] === slot.key ? m < slot.minutes : days[i] < today) { setNote({ kind: 'err', text: `Это время уже прошло — начните не раньше ${hhmm(slot.minutes)}.` }); return; }
    if (m < open || m >= close) { setNote({ kind: 'err', text: `Начать можно только в часы выдачи: ${hhmm(open)}–${hhmm(close)}.` }); return; }
    const l = Math.min(max, lenOf(len, m));
    if (!free(abs, abs + l)) {
      const next = evs.filter((x) => x.s >= abs).sort((a, b) => a.s - b.s)[0];
      const room = next ? Math.floor((next.s - abs) / SLOT) * SLOT : 0;
      if (room >= 60 && free(abs, abs + room)) {
        onSelect(day, { s: abs, e: abs + room });
        setNote({ kind: 'info', text: `Дальше уже занято — выбор укоротили до ${duration(room)}.` });
        return;
      }
      setNote({ kind: 'err', text: 'Это время занято — выберите свободный участок шкалы.' });
      return;
    }
    onSelect(day, { s: abs, e: abs + l });
    setNote(null);
  };

  const setLength = (v: number | 'night') => {
    setLen(v);
    const s = sel?.s ?? Math.max(day === slot.key ? slot.minutes : open, open);
    const l = Math.min(max, lenOf(v, s % 1440));
    if (!free(s, s + l)) { setNote({ kind: 'err', text: 'Столько не получится: дальше время уже занято. Выберите короче или сдвиньте начало.' }); return; }
    onSelect(day, { s, e: s + l });
    setNote(null);
  };

  const pct = (m: number) => `${(m / 1440) * 100}%`;
  const nowAbs = local(new Date(now));

  return (
    <section className="sec">
      <div className="sec-h">
        <h2 className="h2m">Занятость на 3 дня</h2>
        <div className="rv-row" style={{ gap: 4 }}>
          <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="Раньше" disabled={day <= today} onClick={() => onDay(addDays(day, -1))}><ChevronLeft className="rv-ic" aria-hidden /></button>
          <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="Позже" disabled={day >= addDays(today, MAX_DAYS)} onClick={() => onDay(addDays(day, 1))}><ChevronRight className="rv-ic" aria-hidden /></button>
        </div>
      </div>
      <p className="rv-sm rv-muted" style={{ margin: '-4px 0 14px' }}>Коснитесь полоски, чтобы выбрать начало; сколько держать — чипами ниже. Затенённое — здание закрыто, выдачи нет.</p>
      <div className="ldays">
        {days.map((d, i) => {
          const base = i * 1440;
          const dayEvs = evs.filter((x) => x.e > base && x.s < base + 1440);
          const pastW = d < today ? 1440 : d === today ? nowAbs.minutes : 0;
          return (
            <div key={d} className="lday">
              <span className="lday-l"><b>{i < 2 && d <= addDays(today, 1) ? relDay(d) : cap(DOW[dow(d)])}</b><small>{dayLabel(d).split(', ')[1]}</small></span>
              <button type="button" className="lbar" aria-label={`${dayLabel(d)}: коснитесь, чтобы выбрать начало`}
                onClick={(e) => { const rc = e.currentTarget.getBoundingClientRect(); tapAt(i, (e.clientX - rc.left) / rc.width); }}>
                <i className="lb-night" style={{ left: 0, width: pct(open) }} />
                <i className="lb-night" style={{ left: pct(close), width: pct(1440 - close) }} />
                {pastW > 0 && <i className="lb-past" style={{ width: pct(pastW) }} />}
                {dayEvs.map((x, j) => {
                  const s = Math.max(x.s, base) - base;
                  const e = Math.min(x.e, base + 1440) - base;
                  return <i key={j} className={x.item.status === 'Pending' ? 'lb-pend' : x.item.isMine ? 'lb-mine' : 'lb-busy'} style={{ left: pct(s), width: pct(e - s) }} />;
                })}
                {sel && sel.e > base && sel.s < base + 1440 && (
                  <i className={`lb-sel${free(sel.s, sel.e) ? '' : ' lb-bad'}`} style={{ left: pct(Math.max(sel.s, base) - base), width: pct(Math.min(sel.e, base + 1440) - Math.max(sel.s, base)) }} />
                )}
                {d === today && <i className="lb-now" style={{ left: pct(nowAbs.minutes) }} />}
              </button>
            </div>
          );
        })}
        <div className="lday"><span /><div className="laxis" aria-hidden>{[0, 6, 12, 18, 24].map((h) => <span key={h} style={{ left: `${(h / 24) * 100}%` }}>{hhmm(h * 60)}</span>)}</div></div>
      </div>
      <div className="rv-legend" style={{ marginTop: 14 }}>
        <span><i style={{ background: 'var(--slot-busy)' }} />Занято</span>
        <span><i style={{ background: 'var(--accent-soft)', boxShadow: 'inset 0 0 0 1.5px var(--accent)' }} />Ваш выбор</span>
        <span><i style={{ background: 'var(--status-pending)' }} />Заявка</span>
        <span><i style={{ background: 'var(--surface-sunken)', boxShadow: 'inset 0 0 0 1px var(--border-strong)' }} />Здание закрыто</span>
      </div>
      <div className="hs" style={{ margin: '16px -16px 0' }}>
        {options.map((o) => <button key={String(o.v)} type="button" className="rv-chip" aria-pressed={len === o.v} onClick={() => setLength(o.v)}>{o.label}</button>)}
      </div>
    </section>
  );
}

function AdminManage({ r, onDeactivate }: { r: Resource; onDeactivate: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [busy, setBusy] = useState(false);
  const activate = async () => {
    setBusy(true);
    try {
      await api(`/resources/${r.id}/activate`, { method: 'POST' });
      await qc.invalidateQueries({ queryKey: keys.resource(r.id) });
      qc.invalidateQueries({ queryKey: ['resources'] });
      toast('success', 'Ресурс снова доступен', 'Его видно в каталоге, бронировать можно.');
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="rv-card" style={{ margin: '16px 16px 0', padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 10 }}>
      <p className="rv-cap" style={{ margin: 0 }}>Управление ресурсом</p>
      {r.isActive
        ? <button type="button" className="rv-btn rv-btn-secondary rv-btn-block" onClick={onDeactivate}><Wrench className="rv-ic" aria-hidden />Вывести из эксплуатации</button>
        : <button type="button" className="rv-btn rv-btn-secondary rv-btn-block" aria-busy={busy} onClick={activate}><RefreshCw className="rv-ic" aria-hidden />Вернуть в эксплуатацию</button>}
    </div>
  );
}

function DeactivateSheet({ r, onClose }: { r: Resource; onClose: () => void }) {
  const qc = useQueryClient();
  const toast = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const affected = useAdminBookings({ resourceId: r.id, status: ['Pending', 'Confirmed'], from: hourNow(), pageSize: 1 }).data?.pages[0]?.totalCount;

  const submit = async () => {
    setBusy(true);
    try {
      await api(`/resources/${r.id}/deactivate`, { method: 'POST', body: { reason: reason.trim() } });
      await qc.invalidateQueries({ queryKey: keys.resource(r.id) });
      invalidateBookings(qc);
      qc.invalidateQueries({ queryKey: ['resources'] });
      toast('success', 'Ресурс выведен из эксплуатации', affected ? `Отменено ${np(affected, 'бронь', 'брони', 'броней')}, владельцы получат уведомления.` : undefined);
      onClose();
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet title="Вывести из эксплуатации" onClose={onClose}>
      <Banner icon={CircleAlert} tone="warn" title={affected ? `Будущие брони (${affected}) будут отменены` : 'Будущих броней нет — никого не затронет'}>
        Владельцы получат уведомления с вашей причиной. Ресурс пропадёт из каталога, но не удалится — его можно вернуть.
      </Banner>
      <label className="rv-field">
        <span className="rv-label">Причина</span>
        <span className="rv-control rv-control-area"><textarea maxLength={400} placeholder="Например, ремонт до конца недели" value={reason} onChange={(e) => setReason(e.target.value)} /></span>
        <span className="rv-hint">Её увидят владельцы броней и все — на карточке ресурса.</span>
      </label>
      <div className="two">
        <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={onClose}>Не выводить</button>
        <button type="button" className="rv-btn rv-btn-danger rv-btn-lg" disabled={!reason.trim()} aria-busy={busy} onClick={submit}>Вывести</button>
      </div>
    </Sheet>
  );
}
