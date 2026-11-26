import { dayLabel, diffDays, hhmm, local, todayKey } from './time';

/** Русское множественное число: plural(5, 'бронь', 'брони', 'броней') → «броней». */
export function plural(n: number, one: string, few: string, many: string): string {
  const t = n % 10;
  const h = n % 100;
  if (t === 1 && h !== 11) return one;
  if (t >= 2 && t <= 4 && (h < 12 || h > 14)) return few;
  return many;
}

export function np(n: number, one: string, few: string, many: string) {
  return `${n} ${plural(n, one, few, many)}`;
}

/** «1 ч 30 мин», «3 дня 2 ч». */
export function duration(minutes: number): string {
  const d = Math.floor(minutes / 1440);
  const h = Math.floor((minutes % 1440) / 60);
  const m = minutes % 60;
  const parts: string[] = [];
  if (d) parts.push(np(d, 'день', 'дня', 'дней'));
  if (h) parts.push(`${h} ч`);
  if (m) parts.push(`${m} мин`);
  return parts.join(' ') || '0 мин';
}

/** Длительность словами для фразы поиска: «2 часа», «1,5 часа», «3 дня», «ночь». */
export function durationWords(minutes: number, night = false): string {
  if (night) return 'ночь';
  if (minutes >= 1440 && minutes % 1440 === 0) return np(minutes / 1440, 'день', 'дня', 'дней');
  if (minutes < 60) return np(minutes, 'минуту', 'минуты', 'минут');
  if (minutes % 60 === 0) return np(minutes / 60, 'час', 'часа', 'часов');
  if (minutes % 30 === 0) return `${String(minutes / 60).replace('.', ',')} часа`;
  return duration(minutes);
}

/** «через 25 минут», «сегодня в 16:00», «завтра в 10:00», «чт, 29 октября в 10:00». */
export function untilLabel(moment: string, now = new Date()): string {
  const diff = Math.round((new Date(moment).getTime() - now.getTime()) / 60_000);
  if (diff <= 0) return '';
  if (diff < 60) return `через ${np(diff, 'минуту', 'минуты', 'минут')}`;
  const l = local(moment);
  const days = diffDays(todayKey(now), l.key);
  if (diff < 180 && days === 0) return `через ${duration(diff)}`;
  if (days === 0) return `сегодня в ${hhmm(l.minutes)}`;
  if (days === 1) return `завтра в ${hhmm(l.minutes)}`;
  return `${dayLabel(l.key)} в ${hhmm(l.minutes)}`;
}

/** «25 минут назад», «сегодня в 12:00», «вчера в 18:40», «пн, 19 октября в 08:12». */
export function agoLabel(moment: string, now = new Date()): string {
  const diff = Math.round((now.getTime() - new Date(moment).getTime()) / 60_000);
  if (diff < 1) return 'только что';
  if (diff < 60) return `${np(diff, 'минуту', 'минуты', 'минут')} назад`;
  const l = local(moment);
  const days = diffDays(l.key, todayKey(now));
  if (days === 0) return `сегодня в ${hhmm(l.minutes)}`;
  if (days === 1) return `вчера в ${hhmm(l.minutes)}`;
  return `${dayLabel(l.key)} в ${hhmm(l.minutes)}`;
}

/** Инициалы для аватара. */
export function initials(name: string | null | undefined): string {
  const p = (name ?? '').trim().split(/\s+/);
  return ((p[0]?.[0] ?? '') + (p[1]?.[0] ?? '')).toUpperCase() || '?';
}

export function people(n: number) {
  return np(n, 'человек', 'человека', 'человек');
}

/** «Главный корпус, 2 этаж». */
export function where(locationName: string, floor: number | null | undefined): string {
  return floor != null ? `${locationName}, ${floor} этаж` : locationName;
}
