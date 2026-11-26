import type { ScheduleItem } from '../../api/types';
import { addDays, at, local } from '../../lib/time';

/** Занятый интервал в минутах от полуночи выбранного дня (может начинаться до 0 и кончаться после 1440). */
export interface Ev {
  s: number;
  e: number;
  item: ScheduleItem;
}

/** Брони расписания, попадающие в день, — в минутах от его полуночи. */
export function eventsOfDay(items: ScheduleItem[], day: string): Ev[] {
  const dayStart = at(day, 0).getTime();
  const dayEnd = at(addDays(day, 1), 0).getTime();
  return items
    .filter((i) => new Date(i.startAt).getTime() < dayEnd && new Date(i.endAt).getTime() > dayStart)
    .map((i) => ({
      s: Math.round((new Date(i.startAt).getTime() - dayStart) / 60_000),
      e: Math.round((new Date(i.endAt).getTime() - dayStart) / 60_000),
      item: i,
    }));
}

export const overlaps = (a: number, b: number, c: number, d: number) => a < d && c < b;

/** Бронь мешает, только если она активна: завершённые лежат в прошлом и ничего не занимают. */
export const blocking = (e: Ev) => e.item.status === 'Pending' || e.item.status === 'Confirmed';

/** Почему нельзя выбрать интервал: прошло, вне часов работы, занято — или null. */
export function blockedReason(
  evs: Ev[], s: number, e: number, opts: { open: number; close: number; minStart: number; overnight: boolean },
): 'past' | 'off' | 'busy' | null {
  if (s < opts.minStart) return 'past';
  if (opts.overnight ? s < opts.open || s >= opts.close : s < opts.open || e > opts.close) return 'off';
  if (evs.some((x) => blocking(x) && overlaps(s, e, x.s, x.e))) return 'busy';
  return null;
}

/** Сколько минут дня занято в часы работы — для полоски загрузки под днём. */
export function busyMinutes(evs: Ev[], open: number, close: number) {
  return evs.reduce((n, x) => n + Math.max(0, Math.min(x.e, close) - Math.max(x.s, open)), 0);
}

export function dayOf(iso: string) {
  return local(iso).key;
}
