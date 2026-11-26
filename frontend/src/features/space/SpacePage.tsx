import { useMemo, useState } from 'react';
import { useLocation, useParams } from 'react-router';
import { CircleAlert, Monitor, MousePointerClick } from 'lucide-react';
import { api } from '../../api/client';
import { useSpace } from '../../api/hooks';
import type { Resource, Seat } from '../../api/types';
import { useLiveSubscription } from '../../app/live';
import { useHideTabs, useRequireAuth } from '../../app/Shell';
import { durationWords, np } from '../../lib/text';
import { hhmm, intervalMin, local, relDay, timeOf } from '../../lib/time';
import { Bar, BackButton, Chip, Empty } from '../../ui/controls';
import { BookingFlow, type Draft } from '../booking/BookingFlow';
import { ParamSheet, type ParamMode } from '../search/ParamSheet';
import { searchWindow, useSearch } from '../search/state';

/** Схема мест коворкинга на выбранный интервал: свободные, занятые, ваши. */
export function SpacePage() {
  useHideTabs();
  const id = Number(useParams().id);
  const location = useLocation();
  const { query: q, set } = useSearch();
  const { from, to } = searchWindow(q);
  const { data: space, isLoading, error } = useSpace(id, { from, to });
  useLiveSubscription('Space', id);
  const requireAuth = useRequireAuth();

  const [pcOnly, setPcOnly] = useState(q.what.type === 'seat' && q.what.pc);
  const [winOnly, setWinOnly] = useState(false);
  const [picked, setPicked] = useState<number | null>(null);
  const [sheet, setSheet] = useState<ParamMode | null>(null);
  const [draft, setDraft] = useState<Draft | null>((location.state as { resume?: { draft?: Draft } } | null)?.resume?.draft ?? null);
  const [opening, setOpening] = useState(false);

  const seats = space?.seats ?? [];
  const fits = (x: Seat) => (!pcOnly || x.featureCodes.includes('computers')) && (!winOnly || x.featureCodes.includes('window'));
  const shown = seats.filter((x) => x.isActive && fits(x));
  const free = shown.filter((x) => x.status === 'Free').length;
  const seat = seats.find((x) => x.resourceId === picked && x.status === 'Free');

  // Схема по рядам: на месте прохода — пустая колонка.
  const grid = useMemo(() => {
    if (!space) return { cols: '', cells: [] as (Seat | 'aisle' | null)[] };
    const cells: (Seat | 'aisle' | null)[] = [];
    for (let row = 1; row <= space.rows; row++) {
      for (let col = 1; col <= space.columns; col++) {
        cells.push(seats.find((x) => x.row === row && x.column === col) ?? null);
        if (space.aisleAfterColumn === col) cells.push('aisle');
      }
    }
    const left = space.aisleAfterColumn ?? space.columns;
    const right = space.columns - left;
    return {
      cols: right > 0 ? `repeat(${left}, 44px) 14px repeat(${right}, 44px)` : `repeat(${space.columns}, 44px)`,
      cells,
    };
  }, [space, seats]);

  if (isLoading) return <div className="page"><Bar back="/search" /><div className="pad"><div className="rv-skel" style={{ height: 320, borderRadius: 20 }} /></div></div>;
  if (error || !space) return <div className="page"><div className="bar"><BackButton fallback="/" /></div><Empty icon={CircleAlert} title="Коворкинг не найден" text="Возможно, ссылка устарела." /></div>;

  const endMin = q.start + q.dur;
  const close = space.closeTime ? Number(space.closeTime.slice(0, 2)) * 60 + Number(space.closeTime.slice(3, 5)) : endMin;
  const until = seat?.freeUntil ? timeOf(seat.freeUntil) : null;

  const book = async () => {
    if (!seat) return;
    setOpening(true);
    try {
      const r = await api<Resource>(`/resources/${seat.resourceId}`);
      const d: Draft = { resource: r, startAt: from, endAt: to };
      if (requireAuth({ draft: d })) setDraft(d);
    } finally {
      setOpening(false);
    }
  };

  return (
    <div className="page page-narrow">
      <Bar title={space.name} back="/search" small />
      <div className="pad" style={{ display: 'flex', flexDirection: 'column', gap: 14, paddingBottom: 4 }}>
        <p className="rv-sm rv-muted" style={{ margin: 0 }}>
          {space.location.name}{space.floor != null ? `, ${space.floor} этаж` : ''}.{space.openTime && ` Открыт ${space.openTime.slice(0, 5)}–${space.closeTime?.slice(0, 5)}.`}{space.description ? ` ${space.description}` : ''}
        </p>
        <div className="two">
          <button type="button" className="fld" onClick={() => setSheet('time')}>
            <span className="fld-l">Когда</span>
            <span className="fld-v ell rv-tnum">{q.now ? `Сейчас, с ${hhmm(q.start)}` : `${relDay(q.day)}, ${hhmm(q.start)}`}</span>
          </button>
          <button type="button" className="fld" onClick={() => setSheet('time')}>
            <span className="fld-l">На сколько</span>
            <span className="fld-v ell rv-tnum">{durationWords(q.dur)} · до {hhmm(endMin)}</span>
          </button>
        </div>
        <div className="rv-row">
          <Chip check on={pcOnly} onClick={() => { setPcOnly(!pcOnly); set({ what: { type: 'seat', pc: !pcOnly } }); setPicked(null); }}>С компьютером</Chip>
          <Chip check on={winOnly} onClick={() => { setWinOnly(!winOnly); setPicked(null); }}>У окна</Chip>
          <span className="rv-sm rv-muted" style={{ marginLeft: 'auto' }} aria-live="polite">{np(free, 'место свободно', 'места свободно', 'мест свободно')} из {shown.length}</span>
        </div>
        {endMin > close && <p className="rv-error-text" style={{ margin: 0 }}><CircleAlert className="rv-ic rv-ic-sm" aria-hidden />Коворкинг закрывается в {hhmm(close)} — сократите время, иначе места будут недоступны.</p>}
      </div>

      <div className="pad" style={{ marginTop: 16 }}>
        <div className="room" style={{ overflowX: 'auto' }}>
          <span className="room-win" aria-hidden>Окна</span>
          <div className="room-grid" style={{ gridTemplateColumns: grid.cols }} role="group" aria-label="Схема мест">
            {grid.cells.map((x, i) => {
              if (x === 'aisle') return <span key={i} className="room-aisle" aria-hidden />;
              if (!x) return <span key={i} aria-hidden />;
              const pc = x.featureCodes.includes('computers');
              const dim = !fits(x) || !x.isActive || x.status === 'Unavailable';
              const mine = x.status === 'Mine';
              const busy = x.status === 'Busy';
              const cls = mine ? 'seat-mine' : busy ? 'seat-busy' : dim ? 'seat-dim' : '';
              const on = picked === x.resourceId;
              return (
                <button key={i} type="button" className={`seat ${cls}`} aria-pressed={on} disabled={busy || dim || mine}
                  aria-label={`Место ${x.number}${pc ? ', компьютер' : ''}${x.featureCodes.includes('window') ? ', у окна' : ''}${mine ? ', ваша бронь' : busy ? ', занято' : dim ? ', недоступно' : ', свободно'}`}
                  onClick={() => setPicked(on ? null : x.resourceId)}>
                  {pc && <Monitor className="rv-ic" aria-hidden />}{x.number}
                </button>
              );
            })}
          </div>
          <span className="room-door" aria-hidden>Вход</span>
        </div>
        <div className="rv-legend" style={{ marginTop: 12 }}>
          <span><i style={{ background: 'var(--surface)', boxShadow: 'inset 0 0 0 1.5px var(--border-strong)' }} />Свободно</span>
          <span><i style={{ background: 'repeating-linear-gradient(135deg, var(--slot-busy) 0 3px, var(--surface-muted) 3px 5px)' }} />Занято</span>
          <span><i style={{ background: 'var(--accent-soft)', boxShadow: 'inset 0 0 0 1.5px var(--accent)' }} />Выбрано</span>
          <span><i style={{ background: 'var(--accent)' }} />Ваше</span>
        </div>
      </div>
      <div style={{ height: 20 }} />

      <div className="dock">
        {!seat ? (
          <>
            <p className="rv-sm rv-muted dockhint"><MousePointerClick className="rv-ic rv-ic-sm" aria-hidden />Коснитесь свободного места на схеме</p>
            <button type="button" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block" disabled>Выберите место</button>
          </>
        ) : (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <span className="seat-badge rv-tnum">Место {seat.number}</span>
              <div style={{ minWidth: 0, flex: 1 }}>
                <b className="ell">{seat.featureCodes.includes('computers') ? 'Компьютер' : 'Без компьютера, есть розетка'}{seat.featureCodes.includes('window') ? ' · у окна' : ''}</b>
                {until && <span className="rv-sm rv-muted ell">Свободно до {until}{seat.freeUntil && new Date(seat.freeUntil) > new Date(to) ? ' — можно продлить' : ''}</span>}
              </div>
            </div>
            <button type="button" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block" aria-busy={opening} onClick={book}>
              Забронировать {intervalMin(local(from).minutes, local(from).minutes + q.dur)}
            </button>
          </>
        )}
      </div>

      {sheet && <ParamSheet mode={sheet} onMode={setSheet} onClose={() => setSheet(null)} />}
      {draft && <BookingFlow draft={draft} onClose={() => { setDraft(null); setPicked(null); }} />}
    </div>
  );
}
