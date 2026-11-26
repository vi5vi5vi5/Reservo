import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { Category, ResourceKind } from '../../api/types';
import { iso, nextSlot, todayKey } from '../../lib/time';

/**
 * Запрос «фразой» с главной: что нужно, когда, на сколько, где. Один и тот же запрос видят главная,
 * результаты и схема коворкинга, поэтому он живёт в контексте и переживает перезагрузку (sessionStorage).
 */

export type What =
  | { type: 'seat'; pc: boolean }
  | { type: 'kind'; kind: 'Room' | 'Equipment' }
  | { type: 'category'; categoryId: number; kind: ResourceKind };

export interface SearchQuery {
  what: What;
  /** Местный день площадки, «2026-10-26». */
  day: string;
  /** «Сейчас»: начало — ближайший слот, и оно само сдвигается со временем. */
  now: boolean;
  /** Начало в минутах от полуночи. */
  start: number;
  dur: number;
  /** «На ночь» — до открытия следующего утра. */
  night: boolean;
  ppl: number;
  featureIds: number[];
  locationId: number | null;
  spaceId: number | null;
}

/** Рабочий день на шкале выбора времени. */
export const DAY_FROM = 8 * 60;
export const DAY_TO = 22 * 60;
/** Утро, до которого длится бронь «на ночь». */
export const MORNING = 9 * 60;

export const defaultQuery = (): SearchQuery => ({
  what: { type: 'seat', pc: true },
  day: todayKey(),
  now: true,
  start: nextSlot().minutes,
  dur: 120,
  night: false,
  ppl: 0,
  featureIds: [],
  locationId: null,
  spaceId: null,
});

const KEY = 'reservo.search';

function load(): SearchQuery {
  try {
    const raw = sessionStorage.getItem(KEY);
    if (raw) {
      const q = JSON.parse(raw) as SearchQuery;
      // Вчерашний запрос бессмыслен — начинаем с «сейчас».
      if (q.day >= todayKey()) return normalize(q);
    }
  } catch {
    // Повреждённое или недоступное хранилище — просто начинаем с начала.
  }
  return defaultQuery();
}

/** «Сейчас» всегда означает ближайший слот, а время в прошлом сдвигается вперёд. */
export function normalize(q: SearchQuery): SearchQuery {
  const slot = nextSlot();
  if (q.now) return { ...q, day: slot.key, start: slot.minutes, dur: q.night ? 1440 + MORNING - slot.minutes : q.dur };
  if (q.day === slot.key && q.start < slot.minutes) return { ...q, start: slot.minutes };
  return q;
}

export const isEquipment = (w: What) => (w.type === 'kind' && w.kind === 'Equipment') || (w.type === 'category' && w.kind === 'Equipment');
export const isRoom = (w: What) => (w.type === 'kind' && w.kind === 'Room') || (w.type === 'category' && w.kind === 'Room');
export const isSeat = (w: What) => w.type === 'seat';

export function endMinutes(q: SearchQuery) {
  return q.night ? 1440 + MORNING : q.start + q.dur;
}

/** Интервал запроса для API. */
export function searchWindow(q: SearchQuery) {
  return { from: iso(q.day, q.start), to: iso(q.day, endMinutes(q)) };
}

interface SearchValue {
  query: SearchQuery;
  set: (patch: Partial<SearchQuery>) => void;
  reset: () => void;
}

const SearchContext = createContext<SearchValue | null>(null);

export function SearchProvider({ children }: { children: ReactNode }) {
  const [query, setQuery] = useState<SearchQuery>(load);

  // «Сейчас» обновляется раз в минуту, чтобы не застрять в прошлом слоте.
  useEffect(() => {
    const t = window.setInterval(() => setQuery((q) => normalize(q)), 60_000);
    return () => clearInterval(t);
  }, []);

  useEffect(() => {
    try {
      sessionStorage.setItem(KEY, JSON.stringify(query));
    } catch {
      // Без хранилища запрос просто не переживёт перезагрузку.
    }
  }, [query]);

  const value = useMemo<SearchValue>(() => ({
    query,
    set: (patch) => setQuery((q) => {
      let next = { ...q, ...patch };
      // Выбор времени вручную отключает «сейчас».
      if (patch.start !== undefined || patch.day !== undefined) next.now = patch.now ?? false;
      // Длительность «на ночь» и дни — только у оборудования.
      if (patch.what && !isEquipment(patch.what) && (next.night || next.dur > 480)) next = { ...next, night: false, dur: 120 };
      if (patch.what && !isRoom(patch.what)) next = { ...next, ppl: 0, featureIds: [] };
      if (patch.what && patch.what.type !== 'seat') next.spaceId = null;
      if (patch.dur !== undefined) next.night = patch.night ?? false;
      if (next.night) next.dur = 1440 + MORNING - next.start;
      return normalize(next);
    }),
    reset: () => setQuery(defaultQuery()),
  }), [query]);

  return <SearchContext.Provider value={value}>{children}</SearchContext.Provider>;
}

export function useSearch() {
  const ctx = useContext(SearchContext);
  if (!ctx) throw new Error('useSearch вне SearchProvider');
  return ctx;
}

/** Название того, что ищем, для фразы: «место с компьютером», «переговорные», «любое оборудование». */
export function whatLabel(w: What, categories: Category[] | undefined): string {
  if (w.type === 'seat') return w.pc ? 'место с компьютером' : 'место в коворкинге';
  if (w.type === 'kind') return w.kind === 'Room' ? 'любое помещение' : 'любое оборудование';
  const c = categories?.find((x) => x.id === w.categoryId);
  return c ? lowerFirst(c.name) : 'ресурс';
}

/** Строчная первая буква, но аббревиатуры («3D-принтеры», «VR-шлемы») остаются как есть. */
export function lowerFirst(s: string): string {
  if (s.length > 1 && s[1] === s[1].toUpperCase() && s[1] !== s[1].toLowerCase()) return s;
  return s.charAt(0).toLowerCase() + s.slice(1);
}
