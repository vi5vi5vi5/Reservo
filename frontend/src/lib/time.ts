// Время площадки. Бэкенд хранит всё в UTC, а люди думают в местном времени университета,
// поэтому все расчёты «какой это день и час» идут в часовом поясе площадки, а не браузера.

export const SITE_TZ = 'Europe/Moscow';
export const SLOT = 15;

const MONTHS_GEN = ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня', 'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря'];
const MONTHS = ['Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь', 'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'];
export const DOW = ['пн', 'вт', 'ср', 'чт', 'пт', 'сб', 'вс'];

const partsFmt = new Intl.DateTimeFormat('en-GB', {
  timeZone: SITE_TZ,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

const offsetFmt = new Intl.DateTimeFormat('en-US', { timeZone: SITE_TZ, timeZoneName: 'longOffset' });

/** Местные дата и время площадки для момента времени. */
export function local(moment: string | number | Date) {
  const d = moment instanceof Date ? moment : new Date(moment);
  const p = Object.fromEntries(partsFmt.formatToParts(d).map((x) => [x.type, x.value]));
  const y = Number(p.year);
  const m = Number(p.month);
  const day = Number(p.day);
  const minutes = Number(p.hour) * 60 + Number(p.minute);
  return { key: `${p.year}-${p.month}-${p.day}`, y, m, d: day, minutes };
}

/** Смещение пояса площадки в минутах для момента (у Москвы всегда +180, но код не полагается на это). */
function offsetMinutes(at: Date): number {
  const name = offsetFmt.formatToParts(at).find((p) => p.type === 'timeZoneName')?.value ?? 'GMT';
  const m = /GMT([+-])(\d{2}):?(\d{2})?/.exec(name);
  if (!m) return 0;
  return (m[1] === '-' ? -1 : 1) * (Number(m[2]) * 60 + Number(m[3] ?? 0));
}

/** Местный день площадки + минуты от полуночи → момент времени. Минуты могут выходить за сутки. */
export function at(dayKey: string, minutes: number): Date {
  const [y, m, d] = dayKey.split('-').map(Number);
  const naive = Date.UTC(y, m - 1, d, 0, minutes);
  const offset = offsetMinutes(new Date(naive));
  return new Date(naive - offset * 60_000);
}

/** ISO со смещением площадки — так API понимает время однозначно. */
export function iso(dayKey: string, minutes: number): string {
  return at(dayKey, minutes).toISOString();
}

export function todayKey(now = new Date()) {
  return local(now).key;
}

export function addDays(dayKey: string, n: number): string {
  const [y, m, d] = dayKey.split('-').map(Number);
  const t = new Date(Date.UTC(y, m - 1, d + n));
  return t.toISOString().slice(0, 10);
}

export function diffDays(a: string, b: string): number {
  const [y1, m1, d1] = a.split('-').map(Number);
  const [y2, m2, d2] = b.split('-').map(Number);
  return Math.round((Date.UTC(y2, m2 - 1, d2) - Date.UTC(y1, m1 - 1, d1)) / 86_400_000);
}

/** День недели: 0 — понедельник. */
export function dow(dayKey: string): number {
  const [y, m, d] = dayKey.split('-').map(Number);
  return (new Date(Date.UTC(y, m - 1, d)).getUTCDay() + 6) % 7;
}

export function dayNum(dayKey: string) {
  return Number(dayKey.slice(8, 10));
}

export function monthTitle(dayKey: string) {
  return `${MONTHS[Number(dayKey.slice(5, 7)) - 1]} ${dayKey.slice(0, 4)}`;
}

/** «пн, 26 октября». */
export function dayLabel(dayKey: string): string {
  return `${DOW[dow(dayKey)]}, ${dayNum(dayKey)} ${MONTHS_GEN[Number(dayKey.slice(5, 7)) - 1]}`;
}

/** «Сегодня», «Завтра» или «пн, 26 октября». */
export function relDay(dayKey: string, now = new Date()): string {
  const diff = diffDays(todayKey(now), dayKey);
  if (diff === 0) return 'Сегодня';
  if (diff === 1) return 'Завтра';
  if (diff === -1) return 'Вчера';
  return dayLabel(dayKey);
}

/** «14:05» из минут от полуночи. */
export function hhmm(minutes: number): string {
  const m = ((minutes % 1440) + 1440) % 1440;
  return `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;
}

/** «08:00:00» из API → минуты. */
export function parseTime(t: string | null | undefined): number {
  if (!t) return 0;
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
}

/** Время момента по-местному: «14:05». */
export function timeOf(moment: string | Date): string {
  return hhmm(local(moment).minutes);
}

/** Интервал: «14:00–15:30», через полночь — «18:00–09:00 след. дня», дольше — «(+2 дн.)». */
export function interval(start: string | Date, end: string | Date): string {
  const s = local(start);
  const e = local(end);
  const days = diffDays(s.key, e.key);
  const tail = days === 1 ? ' след. дня' : days > 1 ? ` (+${days} дн.)` : '';
  return `${hhmm(s.minutes)}–${hhmm(e.minutes)}${tail}`;
}

/** Интервал внутри дня по минутам (для выбора до бронирования). */
export function intervalMin(startMin: number, endMin: number): string {
  const days = Math.floor((endMin - 1) / 1440);
  const tail = days === 1 ? ' след. дня' : days > 1 ? ` (+${days} дн.)` : '';
  return `${hhmm(startMin)}–${hhmm(endMin)}${tail}`;
}

/** Ближайший слот сетки не раньше «сейчас». */
export function nextSlot(now = new Date()): { key: string; minutes: number } {
  const l = local(now);
  const m = Math.ceil(l.minutes / SLOT) * SLOT;
  return m >= 1440 ? { key: addDays(l.key, 1), minutes: m - 1440 } : { key: l.key, minutes: m };
}

export function minutesBetween(a: string | Date, b: string | Date) {
  return Math.round((new Date(b).getTime() - new Date(a).getTime()) / 60_000);
}

export const MONTHS_GENITIVE = MONTHS_GEN;
