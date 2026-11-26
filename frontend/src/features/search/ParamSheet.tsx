import { useMemo, useRef, useState, type PointerEvent } from 'react';
import { Check, ChevronLeft, ChevronRight, CircleAlert, Timer } from 'lucide-react';
import { useFeatures, useLocations, useSpaces } from '../../api/hooks';
import { featureIcon } from '../../lib/icons';
import { duration, durationWords, np, people } from '../../lib/text';
import { addDays, DOW, dayLabel, dow, hhmm, intervalMin, monthTitle, nextSlot, relDay, SLOT, todayKey } from '../../lib/time';
import { Chip, SearchField, Stepper } from '../../ui/controls';
import { Sheet } from '../../ui/Sheet';
import { Building2, MapPin } from 'lucide-react';
import { DAY_FROM, DAY_TO, endMinutes, isEquipment, isSeat, MORNING, useSearch } from './state';

export type ParamMode = 'time' | 'date' | 'dur' | 'ppl' | 'ft' | 'loc';

const titles: Record<ParamMode, string> = {
  time: 'Когда',
  date: 'Какой день',
  dur: 'На сколько',
  ppl: 'Сколько человек',
  ft: 'Что должно быть',
  loc: 'Где',
};

export const MAX_ADVANCE_DAYS = 30;

export function ParamSheet({ mode, onMode, onClose }: { mode: ParamMode; onMode: (m: ParamMode) => void; onClose: () => void }) {
  return (
    <Sheet title={titles[mode]} onClose={onClose}>
      {mode === 'time' && <TimePane onMode={onMode} />}
      {mode === 'date' && <DatePane onPicked={() => onMode('time')} />}
      {mode === 'dur' && <DurPane />}
      {mode === 'ppl' && <PplPane />}
      {mode === 'ft' && <FtPane />}
      {mode === 'loc' && <LocPane />}
      <button type="button" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block" style={{ marginTop: 4 }} onClick={onClose}>
        Готово
      </button>
    </Sheet>
  );
}

// ---------- Когда: дни + шкала дня ----------

function TimePane({ onMode }: { onMode: (m: ParamMode) => void }) {
  const { query: q, set } = useSearch();
  const equipment = isEquipment(q.what);
  const today = todayKey();
  const slot = nextSlot();
  const end = endMinutes(q);
  const maxDur = isSeat(q.what) ? 480 : 240;

  const days = [today, addDays(today, 1), addDays(today, 2)];
  const isOther = !days.includes(q.day);

  const track = useRef<HTMLDivElement>(null);
  const drag = useRef<{ a: number; moved: boolean } | null>(null);
  const span = DAY_TO - DAY_FROM;
  const toMin = (e: PointerEvent) => {
    const r = track.current!.getBoundingClientRect();
    const ratio = Math.min(1, Math.max(0, (e.clientX - r.left) / r.width));
    return DAY_FROM + Math.round((ratio * span) / SLOT) * SLOT;
  };
  const minStart = q.day === slot.key ? Math.max(DAY_FROM, slot.minutes) : DAY_FROM;

  const onDown = (e: PointerEvent<HTMLDivElement>) => {
    e.currentTarget.setPointerCapture(e.pointerId);
    drag.current = { a: toMin(e), moved: false };
  };
  const onMove = (e: PointerEvent<HTMLDivElement>) => {
    const d = drag.current;
    if (!d) return;
    const m = toMin(e);
    if (m !== d.a) d.moved = true;
    if (!d.moved) return;
    if (equipment) {
      set({ start: Math.max(minStart, Math.min(m, DAY_TO - SLOT)) });
      return;
    }
    let s = Math.max(minStart, Math.min(d.a, m));
    const en = Math.max(s + SLOT, Math.max(d.a, m));
    if (en - s > maxDur) s = en - maxDur;
    set({ start: s, dur: en - s });
  };
  const onUp = () => {
    const d = drag.current;
    drag.current = null;
    if (!d || d.moved) return;
    const s = Math.max(minStart, Math.min(d.a, DAY_TO - SLOT));
    set(equipment ? { start: s } : { start: s, dur: Math.min(q.dur, DAY_TO - s) });
  };

  const pct = (m: number) => ((Math.min(Math.max(m, DAY_FROM), DAY_TO) - DAY_FROM) / span) * 100;
  const selEnd = Math.min(end, DAY_TO);
  const durOptions = (isSeat(q.what) ? [60, 120, 180, 240, 360, 480] : [30, 60, 90, 120, 180, 240]);
  const lateHint = !equipment && end > DAY_TO;

  return (
    <>
      <div className="rv-row">
        {days.map((d) => (
          <Chip key={d} on={q.day === d && !(q.now && d === today)} onClick={() => set({ day: d, start: d === today ? Math.max(q.start, slot.minutes) : q.start })}>
            {relDay(d)}
          </Chip>
        ))}
        {q.day === today && <Chip on={q.now} onClick={() => set({ now: true })}>Сейчас</Chip>}
        <Chip on={isOther} onClick={() => onMode('date')}>{isOther ? dayLabel(q.day) : 'Другой день'}</Chip>
      </div>

      <div className="tsum">
        <b className="rv-tnum">{intervalMin(q.start, end)}</b>
        <span className="rv-sm rv-muted">{q.night ? 'до утра' : duration(q.dur)}</span>
      </div>

      <div>
        <div
          ref={track}
          className="trk"
          aria-label={`Шкала дня с ${hhmm(DAY_FROM)} до ${hhmm(DAY_TO)}: проведите, чтобы выбрать интервал`}
          onPointerDown={onDown}
          onPointerMove={onMove}
          onPointerUp={onUp}
          onPointerCancel={() => (drag.current = null)}
        >
          {Array.from({ length: (span / 60) - 1 }, (_, i) => <span key={i} className="trk-tick" style={{ left: `${((i + 1) * 60 / span) * 100}%` }} />)}
          {minStart > DAY_FROM && <span className="trk-past" style={{ width: `${pct(minStart)}%` }} />}
          <span className="trk-sel" style={{ left: `${pct(q.start)}%`, width: `${Math.max(2, pct(selEnd) - pct(q.start))}%` }}><i /><i /></span>
        </div>
        <div className="trk-lbls" aria-hidden>
          {[8, 10, 12, 14, 16, 18, 20, 22].map((h) => (
            <span key={h} style={{ left: `${pct(h * 60)}%`, transform: h === 22 ? 'translateX(-100%)' : undefined }}>{hhmm(h * 60)}</span>
          ))}
        </div>
      </div>

      {equipment ? (
        <>
          <p className="rv-hint" style={{ margin: 0 }}>Коснитесь шкалы, чтобы выбрать начало. Сколько держать — в «На сколько»: хоть до утра или на несколько дней.</p>
          <button type="button" className="rv-btn rv-btn-secondary rv-btn-block" onClick={() => onMode('dur')}>
            <Timer className="rv-ic rv-ic-sm" aria-hidden />Выбрать длительность
          </button>
        </>
      ) : (
        <>
          <p className="rv-hint" style={{ margin: 0 }}>Проведите по шкале — или коснитесь, чтобы сдвинуть начало. Точнее — кнопками ниже, шаг 15 минут.</p>
          <div className="tgrid">
            <div className="rv-field">
              <span className="rv-label">Начало</span>
              <Stepper value={hhmm(q.start)} decLabel="Начать раньше" incLabel="Начать позже"
                decDisabled={q.start - SLOT < minStart}
                incDisabled={q.dur <= SLOT}
                onDec={() => set({ start: q.start - SLOT, dur: Math.min(maxDur, q.dur + SLOT) })}
                onInc={() => set({ start: q.start + SLOT, dur: q.dur - SLOT })} />
            </div>
            <div className="rv-field">
              <span className="rv-label">Конец</span>
              <Stepper value={hhmm(end)} decLabel="Закончить раньше" incLabel="Закончить позже"
                decDisabled={q.dur <= SLOT}
                incDisabled={q.dur >= maxDur || end >= DAY_TO}
                onDec={() => set({ dur: q.dur - SLOT })}
                onInc={() => set({ dur: q.dur + SLOT })} />
            </div>
          </div>
          <div>
            <p className="rv-label" style={{ margin: '0 0 8px' }}>Длительность</p>
            <div className="rv-row">
              {durOptions.map((m) => <Chip key={m} on={q.dur === m} onClick={() => set({ dur: m })}>{duration(m)}</Chip>)}
            </div>
          </div>
        </>
      )}
      {lateHint && (
        <p className="rv-error-text" style={{ margin: 0 }}>
          <CircleAlert className="rv-ic rv-ic-sm" aria-hidden />После {hhmm(DAY_TO)} почти всё закрыто — начните раньше или сократите время.
        </p>
      )}
    </>
  );
}

// ---------- Какой день: календарь на 30 дней ----------

function DatePane({ onPicked }: { onPicked: () => void }) {
  const { query: q, set } = useSearch();
  const today = todayKey();
  const last = addDays(today, MAX_ADVANCE_DAYS);
  const [month, setMonth] = useState(q.day.slice(0, 7));
  const first = `${month}-01`;
  const lead = dow(first);
  const daysInMonth = new Date(Date.UTC(Number(month.slice(0, 4)), Number(month.slice(5, 7)), 0)).getUTCDate();
  const shift = (n: number) => {
    const [y, m] = month.split('-').map(Number);
    const d = new Date(Date.UTC(y, m - 1 + n, 1));
    setMonth(d.toISOString().slice(0, 7));
  };

  return (
    <>
      <div className="rv-row">
        <Chip on={q.day === today} onClick={() => { set({ day: today, now: true }); onPicked(); }}>Сегодня</Chip>
        <Chip on={q.day === addDays(today, 1)} onClick={() => { set({ day: addDays(today, 1) }); onPicked(); }}>Завтра</Chip>
        {[5, 6].map((wd) => {
          const d = addDays(today, (wd - dow(today) + 7) % 7 || 7);
          return <Chip key={wd} on={q.day === d} onClick={() => { set({ day: d }); onPicked(); }}>{wd === 5 ? 'В субботу' : 'В воскресенье'}</Chip>;
        })}
      </div>
      <div className="rv-card" style={{ padding: 12, boxShadow: 'none' }}>
        <div className="rv-cal-head">
          <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="Предыдущий месяц" disabled={month <= today.slice(0, 7)} onClick={() => shift(-1)}><ChevronLeft className="rv-ic" aria-hidden /></button>
          <b>{monthTitle(first)}</b>
          <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="Следующий месяц" disabled={month >= last.slice(0, 7)} onClick={() => shift(1)}><ChevronRight className="rv-ic" aria-hidden /></button>
        </div>
        <div className="rv-cal-grid">
          {DOW.map((w) => <span key={w} className="dow">{w}</span>)}
          {Array.from({ length: lead }, (_, i) => <span key={`b${i}`} />)}
          {Array.from({ length: daysInMonth }, (_, i) => {
            const d = `${month}-${String(i + 1).padStart(2, '0')}`;
            const dis = d < today || d > last;
            return (
              <button key={d} type="button" className="rv-day" disabled={dis} aria-pressed={q.day === d}
                data-today={d === today ? '' : undefined} aria-label={dayLabel(d)}
                onClick={() => { set({ day: d }); onPicked(); }}>
                {i + 1}
              </button>
            );
          })}
        </div>
      </div>
      <p className="rv-hint" style={{ margin: 0 }}>Бронировать можно на {MAX_ADVANCE_DAYS} дней вперёд — до {dayLabel(last).split(', ')[1]}.</p>
    </>
  );
}

// ---------- На сколько ----------

function DurPane() {
  const { query: q, set } = useSearch();
  const equipment = isEquipment(q.what);
  const opts: { label: string; dur: number; night?: boolean }[] = equipment
    ? [
        { label: '2 часа', dur: 120 }, { label: '4 часа', dur: 240 }, { label: '8 часов', dur: 480 },
        { label: 'На ночь', dur: 0, night: true }, { label: 'Сутки', dur: 1440 }, { label: '2 дня', dur: 2880 }, { label: '3 дня', dur: 4320 },
      ]
    : (isSeat(q.what) ? [60, 120, 180, 240, 360, 480] : [30, 60, 90, 120, 180, 240]).map((d) => ({ label: durationWords(d), dur: d }));

  return (
    <>
      <p className="rv-hint" style={{ margin: 0 }}>
        {equipment ? 'Оборудование можно оставить на ночь или взять на несколько дней. Забрать и вернуть — в часы выдачи.' : 'Бронь длится от 15 минут. У каждого ресурса свой предел — его видно на странице ресурса.'}
      </p>
      <div className="optlist">
        {opts.map((o) => {
          const end = o.night ? 1440 + MORNING : q.start + o.dur;
          const on = o.night ? q.night : !q.night && q.dur === o.dur;
          const days = Math.floor(end / 1440);
          const sub = `до ${days === 0 ? '' : days === 1 ? 'завтра ' : `${relDay(addDays(q.day, days)).toLowerCase()} `}${hhmm(end)}`;
          return (
            <button key={o.label} type="button" className="optrow" aria-pressed={on} onClick={() => set(o.night ? { night: true, dur: 1 } : { dur: o.dur })}>
              <span className="opt-b"><span>{o.label}</span><span className="rv-sm rv-muted rv-tnum">{sub}</span></span>
              {on && <Check className="rv-ic" aria-hidden />}
            </button>
          );
        })}
      </div>
    </>
  );
}

// ---------- Сколько человек ----------

function PplPane() {
  const { query: q, set } = useSearch();
  return (
    <>
      <div className="pplbox">
        <Stepper value={q.ppl || '—'} decLabel="Меньше" incLabel="Больше" decDisabled={q.ppl <= 0} onDec={() => set({ ppl: q.ppl - 1 })} onInc={() => set({ ppl: q.ppl + 1 })} width={180} />
      </div>
      <p className="rv-sm rv-muted" style={{ margin: 0, textAlign: 'center' }}>{q.ppl ? `Покажем помещения, где поместятся ${people(q.ppl)}` : 'Любое число людей'}</p>
      <div className="rv-row" style={{ justifyContent: 'center' }}>
        <Chip on={q.ppl === 0} onClick={() => set({ ppl: 0 })}>Не важно</Chip>
        {[2, 4, 6, 8, 12, 20, 30].map((n) => <Chip key={n} on={q.ppl === n} onClick={() => set({ ppl: n })}>{n}</Chip>)}
      </div>
    </>
  );
}

// ---------- Оснащение ----------

function FtPane() {
  const { query: q, set } = useSearch();
  const features = useFeatures().data ?? [];
  const [term, setTerm] = useState('');
  const shown = features.filter((f) => f.code !== 'window' && f.name.toLowerCase().includes(term.trim().toLowerCase()));
  const toggle = (id: number) => set({ featureIds: q.featureIds.includes(id) ? q.featureIds.filter((x) => x !== id) : [...q.featureIds, id] });

  return (
    <>
      {features.length > 10 && <SearchField value={term} onChange={setTerm} placeholder="Найти оснащение" label="Найти оснащение" />}
      <p className="rv-hint" style={{ margin: 0 }}>{q.featureIds.length ? 'Покажем только помещения, где есть всё выбранное.' : 'Ничего не выбрано — подойдёт любое помещение.'}</p>
      <div className="optlist locbox" style={{ maxHeight: 420 }}>
        {shown.map((f) => {
          const Icon = featureIcon(f.code);
          const on = q.featureIds.includes(f.id);
          return (
            <button key={f.id} type="button" className="optrow" aria-pressed={on} onClick={() => toggle(f.id)}>
              <span className="icirc" style={{ width: 36, height: 36, background: 'var(--surface-sunken)', color: 'var(--accent-text)' }}><Icon className="rv-ic" aria-hidden /></span>
              <span style={{ flex: 1 }}>{f.name}</span>
              {on && <Check className="rv-ic" aria-hidden />}
            </button>
          );
        })}
      </div>
    </>
  );
}

// ---------- Где: корпус или коворкинг ----------

function LocPane() {
  const { query: q, set } = useSearch();
  const seat = isSeat(q.what);
  const locations = useLocations().data ?? [];
  const spaces = useSpaces({}, seat).data ?? [];
  const [term, setTerm] = useState('');
  const t = term.trim().toLowerCase();

  const options = useMemo(() => {
    if (seat) {
      return spaces
        .filter((s) => !t || `${s.name} ${s.location.name}`.toLowerCase().includes(t))
        .map((s) => ({ id: s.id, label: s.name, sub: `${s.location.name}${s.floor != null ? `, ${s.floor} этаж` : ''} · ${np(s.totalSeats, 'место', 'места', 'мест')}` }));
    }
    return locations
      .filter((l) => !t || `${l.name} ${l.address ?? ''}`.toLowerCase().includes(t))
      .map((l) => ({ id: l.id, label: l.name, sub: l.address ?? '' }));
  }, [seat, spaces, locations, t]);

  const current = seat ? q.spaceId : q.locationId;
  const pick = (id: number | null) => set(seat ? { spaceId: id } : { locationId: id });
  const total = seat ? spaces.length : locations.length;

  return (
    <>
      {total > 6 && <SearchField value={term} onChange={setTerm} placeholder={seat ? 'Коворкинг или корпус' : 'Корпус или адрес'} label="Найти" autoFocus />}
      <div className="optlist locbox">
        {!t && (
          <button type="button" className="optrow" aria-pressed={current === null} onClick={() => pick(null)}>
            <span className="icirc" style={{ width: 36, height: 36, background: 'var(--surface-sunken)', color: 'var(--accent-text)' }}><Building2 className="rv-ic" aria-hidden /></span>
            <span style={{ flex: 1 }}>{seat ? 'Любой коворкинг' : 'Любой корпус'}</span>
            {current === null && <Check className="rv-ic" aria-hidden />}
          </button>
        )}
        {options.map((o) => (
          <button key={o.id} type="button" className="optrow" aria-pressed={current === o.id} onClick={() => pick(o.id)}>
            <span className="icirc" style={{ width: 36, height: 36, background: 'var(--surface-sunken)', color: 'var(--accent-text)' }}><MapPin className="rv-ic" aria-hidden /></span>
            <span className="opt-b"><span>{o.label}</span>{o.sub && <span className="rv-sm rv-muted">{o.sub}</span>}</span>
            {current === o.id && <Check className="rv-ic" aria-hidden />}
          </button>
        ))}
        {options.length === 0 && <p className="rv-muted" style={{ margin: 0, padding: '8px 4px' }}>Ничего не нашлось — проверьте название.</p>}
      </div>
    </>
  );
}
