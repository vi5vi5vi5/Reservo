import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { Calendar, ChevronLeft, ChevronRight, CircleAlert, CircleCheck, CircleX, Clock, Hourglass, Info, Pencil, ShieldCheck } from 'lucide-react';
import { api, ApiError } from '../../api/client';
import { invalidateBookings, useMyBookings, useSuggestions } from '../../api/hooks';
import type { Booking, BookingStatus, Resource } from '../../api/types';
import { hourNow } from '../../app/Shell';
import { useToast } from '../../app/toast';
import { categoryIcon } from '../../lib/icons';
import { duration, people, where } from '../../lib/text';
import { addDays, at, dayLabel, hhmm, interval, intervalMin, iso, local, minutesBetween, nextSlot, parseTime, SLOT, todayKey } from '../../lib/time';
import { Banner, Stepper } from '../../ui/controls';
import { CloseButton, Sheet } from '../../ui/Sheet';

export const MAX_ACTIVE = 5;
const DEFAULT_MAX = 240;

/** Черновик брони: что и на какое время. bookingId — значит, это перенос существующей брони. */
export interface Draft {
  resource: Resource;
  startAt: string;
  endAt: string;
  title?: string | null;
  attendees?: number | null;
  bookingId?: number;
  status?: BookingStatus;
}

type View = { kind: 'form' } | { kind: 'conflict'; detail?: string } | { kind: 'success'; booking: Booking };

/**
 * Оформление брони в одном листе: форма → (если время успели занять) варианты рядом → готово.
 * Правила (сетка 15 минут, рабочие часы, лимиты) проверяет и сервер; форма лишь не даёт выбрать заведомо неверное.
 */
export function BookingFlow({ draft, onClose }: { draft: Draft; onClose: () => void }) {
  const [current, setCurrent] = useState(draft);
  const [view, setView] = useState<View>({ kind: 'form' });

  return (
    <Sheet title={view.kind === 'success' ? 'Готово' : current.bookingId ? 'Изменение брони' : 'Оформление брони'} onClose={onClose} bare>
      {view.kind === 'form' && <Form key={`${current.resource.id}:${current.startAt}`} draft={current} onConflict={(d, detail) => { setCurrent(d); setView({ kind: 'conflict', detail }); }} onDone={(b) => setView({ kind: 'success', booking: b })} onClose={onClose} />}
      {view.kind === 'conflict' && <Conflict draft={current} detail={view.detail} onPick={(d) => { setCurrent(d); setView({ kind: 'form' }); }} onClose={onClose} />}
      {view.kind === 'success' && <Success booking={view.booking} onClose={onClose} />}
    </Sheet>
  );
}

function Form({ draft, onConflict, onDone, onClose }: {
  draft: Draft;
  onConflict: (d: Draft, detail?: string) => void;
  onDone: (b: Booking) => void;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const toast = useToast();
  const r = draft.resource;
  const edit = !!draft.bookingId;
  const seat = r.category.kind === 'Seat';
  const overnight = r.allowsOvernight;
  const open = parseTime(r.openTime);
  const close = parseTime(r.closeTime);
  const max = r.maxDurationMinutes ?? DEFAULT_MAX;

  const s0 = local(draft.startAt);
  const [day, setDay] = useState(s0.key);
  const [start, setStart] = useState(s0.minutes);
  const [end, setEnd] = useState(minutesBetween(at(s0.key, 0), draft.endAt));
  const [title, setTitle] = useState(draft.title ?? '');
  const [ppl, setPpl] = useState<number | null>(draft.attendees ?? null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [titleError, setTitleError] = useState<string | null>(null);

  const active = useMyBookings({ status: ['Pending', 'Confirmed'], from: hourNow(), pageSize: 1 }, !edit).data?.pages[0]?.totalCount ?? 0;

  const slot = nextSlot();
  const minStart = Math.max(open, day === slot.key ? slot.minutes : 0);
  const endStep = overnight ? 60 : SLOT;
  const maxEnd = overnight ? start + max : Math.min(close, start + max);
  const len = end - start;
  const timeChanged = edit && (iso(day, start) !== new Date(draft.startAt).toISOString() || iso(day, end) !== new Date(draft.endAt).toISOString());
  const pplOver = r.capacity != null && ppl != null && ppl > r.capacity;
  const Icon = categoryIcon(r.category.code, r.category.kind);

  const moveDay = (n: number) => {
    const d = addDays(day, n);
    setDay(d);
    if (d === slot.key && start < slot.minutes) {
      setStart(slot.minutes);
      setEnd(slot.minutes + len);
    }
  };

  const submit = async () => {
    if (!seat && !title.trim()) {
      setTitleError('Укажите тему — например, «Подготовка к защите проекта».');
      return;
    }
    if (pplOver) return;
    setBusy(true);
    setError(null);
    const body = {
      resourceId: r.id,
      startAt: iso(day, start),
      endAt: iso(day, end),
      title: title.trim() || null,
      attendees: r.capacity != null ? ppl : null,
    };
    try {
      const booking = edit
        ? await api<Booking>(`/bookings/${draft.bookingId}`, { method: 'PUT', body })
        : await api<Booking>('/bookings', { method: 'POST', body });
      await invalidateBookings(qc);
      if (edit) {
        toast('success', 'Изменения сохранены', booking.status === 'Pending' && draft.status === 'Confirmed' ? 'Бронь снова ждёт решения администратора.' : undefined);
        onClose();
      } else {
        onDone(booking);
      }
    } catch (e) {
      const err = e as ApiError;
      if (err.code === 'booking.time_conflict') {
        onConflict({ ...draft, startAt: body.startAt, endAt: body.endAt, title, attendees: ppl }, err.detail);
        return;
      }
      if (err.code === 'validation_failed' && err.fieldError('title')) setTitleError(err.fieldError('title')!);
      setError(err);
    } finally {
      setBusy(false);
    }
  };

  const endDay = Math.floor(end / 1440);

  return (
    <>
      <div className="sheet-h">
        <div style={{ minWidth: 0, display: 'flex', gap: 12, alignItems: 'center' }}>
          <span className="icirc only-desktop" style={{ background: 'var(--accent-soft)', color: 'var(--accent-text)', width: 48, height: 48 }}><Icon className="rv-ic" aria-hidden /></span>
          <div style={{ minWidth: 0 }}>
            <p className="rv-cap" style={{ margin: '0 0 2px' }}>{edit ? 'Изменение брони' : 'Оформление брони'}</p>
            <h2 className="rv-h2">{r.name}</h2>
            <p className="rv-sm rv-muted" style={{ margin: '2px 0 0' }}>{r.seat ? r.seat.spaceName : where(r.location.name, r.floor)}</p>
          </div>
        </div>
        <CloseButton onClick={onClose} />
      </div>

      {edit && <p className="rv-hint hintrow" style={{ margin: 0 }}><Info className="rv-ic rv-ic-sm" aria-hidden />Ресурс изменить нельзя. Чтобы выбрать другой, отмените бронь и создайте новую.</p>}

      <div className="fbox">
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Calendar className="rv-ic" aria-hidden style={{ color: 'var(--ink-muted)' }} />
          <b style={{ flex: 1 }}>{dayLabel(day)}</b>
          <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="На день раньше" disabled={day <= todayKey()} onClick={() => moveDay(-1)}><ChevronLeft className="rv-ic" aria-hidden /></button>
          <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="На день позже" disabled={day >= addDays(todayKey(), 30)} onClick={() => moveDay(1)}><ChevronRight className="rv-ic" aria-hidden /></button>
        </div>
        <div className="tgrid">
          <div className="rv-field">
            <span className="rv-label">Начало</span>
            <Stepper value={hhmm(start)} decLabel="Начать раньше" incLabel="Начать позже"
              decDisabled={start - SLOT < minStart || end - (start - SLOT) > max}
              incDisabled={start + SLOT >= end || (overnight && start + SLOT >= close)}
              onDec={() => setStart(start - SLOT)} onInc={() => setStart(start + SLOT)} />
          </div>
          <div className="rv-field">
            <span className="rv-label">Конец {endDay > 0 && <span className="opt">{endDay === 1 ? 'след. дня' : `+${endDay} дн.`}</span>}</span>
            <Stepper value={hhmm(end)} decLabel="Закончить раньше" incLabel="Закончить позже"
              decDisabled={end - endStep <= start}
              incDisabled={end + endStep > maxEnd}
              onDec={() => setEnd(end - endStep)} onInc={() => setEnd(end + endStep)} />
          </div>
        </div>
        <p className="rv-sm rv-muted" style={{ margin: 0 }}>
          Длительность <b style={{ color: 'var(--ink)' }}>{duration(len)}</b> · не больше {duration(max)} · {overnight ? 'выдача' : 'работает'} {hhmm(open)}–{hhmm(close)}
        </p>
        {overnight && <p className="rv-hint hintrow" style={{ margin: 0 }}><Clock className="rv-ic rv-ic-sm" aria-hidden />{r.pickupInfo ?? 'Забрать и вернуть — в часы выдачи.'}</p>}
      </div>

      <label className="rv-field">
        <span className="rv-label">Тема {seat && <span className="opt">· необязательно</span>}</span>
        <span className="rv-control ctl-lg" data-invalid={titleError ? 'true' : undefined}>
          <input type="text" maxLength={200} placeholder={seat ? 'Например, подготовка к экзамену' : 'Подготовка к защите проекта'} value={title}
            onChange={(e) => { setTitle(e.target.value); setTitleError(null); }} aria-invalid={!!titleError} />
        </span>
        {titleError
          ? <span className="rv-error-text"><CircleAlert className="rv-ic rv-ic-sm" aria-hidden />{titleError}</span>
          : <span className="rv-hint">Тему видите только вы и администратор. До 200 символов.</span>}
      </label>

      {r.capacity != null && (
        <div className="rv-field">
          <span className="rv-label">Сколько человек <span className="opt">· необязательно</span></span>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <Stepper value={ppl ?? '—'} width={156} invalid={pplOver} decLabel="Меньше" incLabel="Больше" decDisabled={!ppl}
              onDec={() => setPpl(ppl && ppl > 1 ? ppl - 1 : null)} onInc={() => setPpl((ppl ?? 0) + 1)} />
            <span className="rv-sm rv-muted">до {people(r.capacity)}</span>
          </div>
          {pplOver && <span className="rv-error-text"><CircleAlert className="rv-ic rv-ic-sm" aria-hidden />Здесь помещается до {people(r.capacity)}.</span>}
        </div>
      )}

      {r.requiresApproval && !edit && <Banner icon={ShieldCheck}>Бронь подтверждает администратор — мы пришлём уведомление о решении.</Banner>}
      {edit && r.requiresApproval && draft.status === 'Confirmed' && timeChanged && (
        <Banner icon={Hourglass} tone="warn">После переноса бронь снова уйдёт на согласование. До решения администратора время остаётся за вами.</Banner>
      )}
      {!edit && active === MAX_ACTIVE - 1 && (
        <Banner icon={Info} tone="warn">У вас {active} из {MAX_ACTIVE} активных броней. После этой новых создать не получится, пока одна не пройдёт.</Banner>
      )}
      {error?.code === 'booking.user_limit_reached' ? (
        <Banner icon={CircleX} tone="danger" role="alert" title={`У вас уже ${MAX_ACTIVE} активных броней`}>
          Отмените одну, чтобы создать новую.{' '}
          <button type="button" className="lnk" onClick={() => { onClose(); navigate('/bookings'); }}>Открыть «Мои брони»</button>
        </Banner>
      ) : error && error.code !== 'validation_failed' ? (
        <Banner icon={CircleX} tone="danger" role="alert" title={error.message}>{error.detail}</Banner>
      ) : error && !titleError ? (
        <Banner icon={CircleX} tone="danger" role="alert" title={error.message}>{error.detail}</Banner>
      ) : null}

      <button type="button" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block" aria-busy={busy} disabled={pplOver} onClick={submit}>
        {edit ? 'Сохранить' : r.requiresApproval ? 'Отправить заявку' : `Забронировать ${intervalMin(start, end)}`}
      </button>
      <button type="button" className="rv-btn rv-btn-ghost rv-btn-block" onClick={onClose}>Отмена</button>
    </>
  );
}

/** Время заняли: объясняем и сразу предлагаем ближайшие свободные варианты. */
function Conflict({ draft, detail, onPick, onClose }: { draft: Draft; detail?: string; onPick: (d: Draft) => void; onClose: () => void }) {
  const q = { startAt: draft.startAt, endAt: draft.endAt, attendees: draft.attendees ?? undefined };
  const { data, isLoading } = useSuggestions(draft.resource.id, q, true);
  const [loading, setLoading] = useState<number | null>(null);

  const pick = async (s: NonNullable<typeof data>[number]) => {
    let resource = draft.resource;
    if (s.resourceId !== resource.id) {
      setLoading(s.resourceId);
      resource = await api<Resource>(`/resources/${s.resourceId}`).finally(() => setLoading(null));
    }
    onPick({ ...draft, resource, startAt: s.startAt, endAt: s.endAt });
  };

  const reasonLabel = { Earlier: 'Тот же ресурс, раньше', Later: 'Тот же ресурс, позже', SimilarResource: '' };

  return (
    <>
      <div className="sheet-h"><h2 className="rv-h2">Время уже заняли</h2><CloseButton onClick={onClose} /></div>
      <Banner icon={CircleX} tone="danger" role="alert" title="Пока вы оформляли, это время заняли">
        {detail ?? `${draft.resource.name}, ${interval(draft.startAt, draft.endAt)}.`}
      </Banner>
      {isLoading && <div className="rv-skel" style={{ height: 60, borderRadius: 14 }} />}
      {data && data.length > 0 && (
        <>
          <p className="rv-label" style={{ margin: '4px 0 0' }}>Ближайшие свободные варианты</p>
          <div className="altlist">
            {data.map((s, i) => (
              <button key={i} type="button" className="rv-btn rv-btn-outline alt" aria-busy={loading === s.resourceId} onClick={() => pick(s)}>
                <span style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', minWidth: 0 }}>
                  <span className="rv-tnum" style={{ fontSize: 16, fontWeight: 800 }}>{interval(s.startAt, s.endAt)}</span>
                  <span className="rv-sm rv-muted ell" style={{ fontWeight: 600, maxWidth: 360 }}>{reasonLabel[s.reason] || `${s.resourceName} · ${s.locationName}`}</span>
                </span>
                <ChevronRight className="rv-ic" aria-hidden style={{ marginLeft: 'auto' }} />
              </button>
            ))}
          </div>
        </>
      )}
      {data && data.length === 0 && <p className="rv-muted" style={{ margin: 0 }}>Рядом свободного времени нет — посмотрите другой день.</p>}
      {draft.title && <p className="rv-hint" style={{ margin: 0 }}>Тема «{draft.title}» сохранится.</p>}
      <button type="button" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block" onClick={onClose}>Выбрать другое время</button>
    </>
  );
}

function Success({ booking: b, onClose }: { booking: Booking; onClose: () => void }) {
  const navigate = useNavigate();
  const pending = b.status === 'Pending';
  const Icon = categoryIcon(b.resource.categoryCode, b.resource.kind);
  return (
    <>
      <div className="succ-box">
        <div className={`succ${pending ? ' pend' : ''}`}>{pending ? <Hourglass className="rv-ic" aria-hidden /> : <CircleCheck className="rv-ic" aria-hidden />}</div>
        <h2 className="rv-h2" style={{ textAlign: 'center' }}>{pending ? 'Заявка отправлена на согласование' : 'Готово, время за вами'}</h2>
        <p className="rv-muted" style={{ margin: 0, textAlign: 'center', maxWidth: 320 }}>
          {pending ? 'Время уже закреплено за вами. Решение администратора придёт уведомлением.' : 'Бронь подтверждена. Напомним за 30 минут до начала.'}
        </p>
      </div>
      <div className="fbox">
        <div className="mrow"><Icon className="rv-ic" aria-hidden /><div><b>{b.resource.seatNumber != null ? `Место ${b.resource.seatNumber}` : b.resource.name}</b><div className="rv-sm rv-muted">{b.resource.spaceName ?? where(b.resource.locationName, b.resource.floor)}</div></div></div>
        <div className="mrow"><Calendar className="rv-ic" aria-hidden /><div>{dayLabel(local(b.startAt).key)}</div></div>
        <div className="mrow"><Clock className="rv-ic" aria-hidden /><div className="rv-tnum">{interval(b.startAt, b.endAt)}</div></div>
        {b.title && <div className="mrow"><Pencil className="rv-ic" aria-hidden /><div>{b.title}</div></div>}
      </div>
      <div className="two">
        <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={() => { onClose(); navigate('/bookings'); }}>Мои брони</button>
        <button type="button" className="rv-btn rv-btn-primary rv-btn-lg" onClick={onClose}>Готово</button>
      </div>
    </>
  );
}
