import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { Calendar, ChevronDown, ChevronRight } from 'lucide-react';
import { useAvailable, useCategories, useFeatures, useLocations, useSpaces } from '../../api/hooks';
import type { Resource, Space } from '../../api/types';
import { useRequireAuth } from '../../app/Shell';
import { featureWith } from '../../lib/icons';
import { np, people } from '../../lib/text';
import { interval, intervalMin, relDay, timeOf } from '../../lib/time';
import { Bar, CardSkeletons, Empty, LoadMore, Seg } from '../../ui/controls';
import { BookingFlow, type Draft } from '../booking/BookingFlow';
import { ResourceCard } from '../catalog/ResourceCard';
import { ParamSheet, type ParamMode } from './ParamSheet';
import { endMinutes, isRoom, isSeat, searchWindow, useSearch, whatLabel } from './state';
import { WhatSheet } from './WhatSheet';

/** Результаты поиска фразой: коворкинги со схемой мест или ресурсы, свободные на весь интервал. */
export function ResultsPage() {
  const { query: q, set } = useSearch();
  const categories = useCategories().data;
  const features = useFeatures().data ?? [];
  const locations = useLocations().data ?? [];
  const [sheet, setSheet] = useState<'what' | ParamMode | null>(null);
  const [sort, setSort] = useState<'Name' | 'Capacity'>('Name');
  const [draft, setDraft] = useState<Draft | null>(null);
  const requireAuth = useRequireAuth();

  const seat = isSeat(q.what);
  const room = isRoom(q.what);
  const { from, to } = searchWindow(q);

  const apiQuery = useMemo(() => {
    const w = q.what;
    return {
      from,
      to,
      kinds: w.type === 'kind' ? [w.kind] : [],
      categoryIds: w.type === 'category' ? [w.categoryId] : [],
      locationIds: q.locationId ? [q.locationId] : [],
      minCapacity: room && q.ppl ? q.ppl : undefined,
      featureIds: room ? q.featureIds : [],
      sortBy: sort,
      sortDir: sort === 'Capacity' ? 'Desc' : 'Asc',
    };
  }, [q, from, to, room, sort]);

  const list = useAvailable(apiQuery, !seat);
  const spaces = useSpaces({ from, to }, seat);
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const total = list.data?.pages[0]?.totalCount ?? 0;

  // Самые свободные — сверху: коворкингов может быть несколько десятков.
  const shownSpaces = (spaces.data ?? [])
    .filter((s) => !q.spaceId || s.id === q.spaceId)
    .sort((a, b) => (b.freeSeats ?? 0) - (a.freeSeats ?? 0));
  const pcOnly = q.what.type === 'seat' && q.what.pc;
  const seatsFree = shownSpaces.reduce((n, s) => n + s.seats.filter((x) => x.status === 'Free' && (!pcOnly || x.featureCodes.includes('computers'))).length, 0);

  const loading = seat ? spaces.isLoading : list.isLoading;
  const ft = q.featureIds.map((id) => features.find((f) => f.id === id)).filter(Boolean);

  const chips: { label: string; mode: 'what' | ParamMode }[] = [
    { label: q.now ? 'Сейчас' : relDay(q.day), mode: 'time' },
    { label: intervalMin(q.start, endMinutes(q)), mode: 'time' },
    { label: whatLabel(q.what, categories).replace(/^./, (c) => c.toUpperCase()), mode: 'what' },
    ...(room ? [{ label: q.ppl ? people(q.ppl) : 'Любое число людей', mode: 'ppl' as const }] : []),
    ...(room ? ft.map((f) => ({ label: `С ${featureWith(f!.code, f!.name)}`, mode: 'ft' as const })) : []),
    { label: seat ? (shownSpaces.length === 1 && q.spaceId ? shownSpaces[0].name : 'Любой коворкинг') : (locations.find((l) => l.id === q.locationId)?.name ?? 'Любой корпус'), mode: 'loc' },
  ];

  const book = (r: Resource) => {
    const d: Draft = { resource: r, startAt: from, endAt: to, attendees: room && q.ppl ? q.ppl : null };
    if (requireAuth({ draft: d })) setDraft(d);
  };

  return (
    <div className="page">
      <Bar title="Свободно" back="/" />
      <div className="hs" style={{ paddingBottom: 12, borderBottom: '1px solid var(--border)' }}>
        {chips.map((c, i) => (
          <button key={i} type="button" className="rv-chip" onClick={() => setSheet(c.mode)}>
            {c.label}<ChevronDown className="rv-ic rv-ic-sm" aria-hidden />
          </button>
        ))}
      </div>

      <div className="cnt-row">
        <p className="rv-sm rv-muted" style={{ margin: 0 }} aria-live="polite">
          {loading ? 'Ищем свободное…' : seat ? `Свободно ${np(seatsFree, 'место', 'места', 'мест')} на всё время` : `Свободно на всё время: ${np(total, 'ресурс', 'ресурса', 'ресурсов')}`}
        </p>
        {!seat && (
          <Seg small value={sort} onChange={setSort} label="Сортировка" options={[{ value: 'Name', label: 'По названию' }, { value: 'Capacity', label: 'По вместимости' }]} />
        )}
      </div>

      {loading && <CardSkeletons />}

      {seat && !loading && (
        <div className="pad" style={{ paddingBottom: 20 }}>
          {shownSpaces.length ? (
            <>
              <div className="grid-cards">{shownSpaces.map((s) => <SpaceCard key={s.id} s={s} pcOnly={pcOnly} />)}</div>
              <p className="rv-sm rv-muted" style={{ margin: '12px 0 0' }}>Сливовые квадраты на схеме — свободные места, серые — занятые. Конкретное место выбирается на следующем шаге.</p>
            </>
          ) : (
            <Empty icon={Calendar} title="Коворкингов пока нет" text="Администратор ещё не добавил ни одного коворкинга." />
          )}
        </div>
      )}

      {!seat && !loading && items.length === 0 && (
        <Empty icon={Calendar} title="На это время всё занято" text="Попробуйте сдвинуть время или убрать фильтр.">
          <button type="button" className="rv-btn rv-btn-primary rv-btn-lg" onClick={() => set({ start: q.start + 60, now: false })}>Сдвинуть на час позже</button>
          <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={() => set({ ppl: 0, featureIds: [], locationId: null })}>Убрать фильтры</button>
        </Empty>
      )}

      {!seat && items.length > 0 && (
        <>
          <div className="grid-cards pad" style={{ paddingBottom: 12 }}>
            {items.map(({ resource: r, freeUntil }) => (
              <ResourceCard
                key={r.id}
                r={r}
                to={`/resources/${r.id}?day=${q.day}&from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`}
                foot={
                  <>
                    <span className="rv-free rv-tnum" title={freeUntil ? `Свободно до ${timeOf(freeUntil)}` : undefined}>
                      Свободно {interval(from, to)}
                    </span>
                    <button type="button" className="rv-btn rv-btn-secondary" onClick={() => book(r)}>Забронировать</button>
                  </>
                }
              />
            ))}
          </div>
          <LoadMore hasNext={!!list.hasNextPage} loading={list.isFetchingNextPage} onLoad={() => list.fetchNextPage()} />
        </>
      )}

      {sheet === 'what' && <WhatSheet onClose={() => setSheet(null)} />}
      {sheet && sheet !== 'what' && <ParamSheet mode={sheet} onMode={setSheet} onClose={() => setSheet(null)} />}
      {draft && <BookingFlow draft={draft} onClose={() => setDraft(null)} />}
    </div>
  );
}

function SpaceCard({ s, pcOnly }: { s: Space; pcOnly: boolean }) {
  const fit = (x: Space['seats'][number]) => !pcOnly || x.featureCodes.includes('computers');
  const total = s.seats.filter((x) => x.isActive && fit(x)).length;
  const free = s.seats.filter((x) => x.status === 'Free' && fit(x)).length;
  const cols = s.columns + (s.aisleAfterColumn ? 1 : 0);

  // Точки мини-схемы по рядам и колонкам, с пустой колонкой на месте прохода.
  const dots: string[] = [];
  for (let row = 1; row <= s.rows; row++) {
    for (let col = 1; col <= s.columns; col++) {
      const x = s.seats.find((z) => z.row === row && z.column === col);
      dots.push(!x || !x.isActive ? 'g' : !fit(x) ? 'n' : x.status === 'Free' ? '' : 'b');
      if (s.aisleAfterColumn === col) dots.push('g');
    }
  }

  return (
    <Link to={`/spaces/${s.id}`} className="rv-card spc" style={{ textDecoration: 'none' }}>
      <span className="spc-top">
        <span style={{ minWidth: 0 }}>
          <span className="resm-name">{s.name}</span>
          <span className="rv-sm rv-muted" style={{ display: 'block' }}>{s.location.name}{s.floor != null ? `, ${s.floor} этаж` : ''}{s.isQuiet ? ' · тихая зона' : ''}</span>
        </span>
        <span className="spc-n"><b className="rv-tnum">{free}</b><small>из {total}{pcOnly ? ' с ПК' : ''}</small></span>
      </span>
      <span className="minimap" style={{ gridTemplateColumns: `repeat(${cols}, 10px)` }} aria-hidden>
        {dots.map((c, i) => <i key={i} className={c} />)}
      </span>
      <span className="spc-f">
        <span className="relx">{free ? 'Выбрать место на схеме' : 'На это время всё занято'}</span>
        <ChevronRight className="rv-ic rv-ic-sm" aria-hidden style={{ color: 'var(--accent-text)' }} />
      </span>
    </Link>
  );
}
