import { useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { Monitor, Search, SlidersHorizontal, X } from 'lucide-react';
import { useCategories, useFeatures, useLocations, useResources, useSpaces } from '../../api/hooks';
import type { ResourceKind } from '../../api/types';
import { useAuth } from '../../app/auth';
import { featureIcon, categoryIcon } from '../../lib/icons';
import { np, people } from '../../lib/text';
import { iso, nextSlot } from '../../lib/time';
import { Bar, CardSkeletons, Chip, Empty, LoadMore, SearchField, Seg, Stepper } from '../../ui/controls';
import { Sheet } from '../../ui/Sheet';
import { useDebounced } from '../search/WhatSheet';
import { ResourceCard } from './ResourceCard';

/**
 * Каталог. Фильтры живут в адресной строке (?kind=Equipment&cat=5&cat=7): ссылку можно отправить,
 * а «назад» возвращает к тем же фильтрам. Категорий может быть сотни, поэтому в ряду чипов — только виды,
 * а категории выбираются в листе фильтров с поиском.
 */
export function CatalogPage() {
  const { isAdmin } = useAuth();
  const [params, setParams] = useSearchParams();
  const categories = useCategories().data ?? [];
  const features = useFeatures().data ?? [];
  const locations = useLocations().data ?? [];
  const [filters, setFilters] = useState(false);

  const term = params.get('q') ?? '';
  const debounced = useDebounced(term.trim());
  const kind = (params.get('kind') as ResourceKind | null) ?? null;
  const cats = params.getAll('cat').map(Number);
  const locs = params.getAll('loc').map(Number);
  const fts = params.getAll('ft').map(Number);
  const cap = Number(params.get('cap') ?? 0);
  const sort = (params.get('sort') as 'Name' | 'Capacity' | null) ?? 'Name';
  const inactive = params.get('inactive') === '1';

  const update = (patch: Record<string, string | number | number[] | null>) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(patch)) {
      next.delete(k);
      if (Array.isArray(v)) v.forEach((x) => next.append(k, String(x)));
      else if (v !== null && v !== '' && v !== 0) next.set(k, String(v));
    }
    setParams(next, { replace: true });
  };

  const query = useMemo(() => ({
    search: debounced,
    kinds: kind ? [kind] : ['Room', 'Equipment'],
    categoryIds: cats,
    locationIds: locs,
    featureIds: fts,
    minCapacity: cap || undefined,
    sortBy: sort,
    sortDir: sort === 'Capacity' ? 'Desc' : 'Asc',
    includeInactive: isAdmin && inactive,
  }), [debounced, kind, cats.join(), locs.join(), fts.join(), cap, sort, isAdmin, inactive]); // eslint-disable-line react-hooks/exhaustive-deps

  const list = useResources(query);
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const total = list.data?.pages[0]?.totalCount ?? 0;
  const filterCount = cats.length + locs.length + fts.length + (cap ? 1 : 0);

  // Коворкинги — отдельным блоком сверху: их места в общий список не попадают.
  const slot = nextSlot();
  const showSpaces = !kind && !filterCount && !debounced;
  const spaces = useSpaces({ from: iso(slot.key, slot.minutes), to: iso(slot.key, slot.minutes + 60) }, showSpaces).data ?? [];

  const reset = () => setParams(new URLSearchParams(), { replace: true });

  return (
    <div className="page">
      <Bar title="Каталог">
        {isAdmin && (
          <label className="rv-switch only-desktop" style={{ marginLeft: 'auto', fontSize: 14, fontWeight: 600 }}>
            <input type="checkbox" checked={inactive} onChange={(e) => update({ inactive: e.target.checked ? 1 : null })} />
            Показать выведенные
          </label>
        )}
      </Bar>
      <div style={{ padding: '0 16px 12px' }}>
        <SearchField value={term} onChange={(v) => update({ q: v })} placeholder="Название или описание" label="Поиск по названию и описанию" />
      </div>
      <div className="hs" style={{ paddingBottom: 12, borderBottom: '1px solid var(--border)' }}>
        <button type="button" className="rv-chip" aria-pressed={filterCount > 0} onClick={() => setFilters(true)}>
          <SlidersHorizontal className="rv-ic rv-ic-sm" aria-hidden />Фильтры{filterCount > 0 && <span className="rv-counter">{filterCount}</span>}
        </button>
        <Chip on={!kind} onClick={() => update({ kind: null, cat: [] })}>Всё</Chip>
        <Chip on={kind === 'Room'} onClick={() => update({ kind: 'Room', cat: [] })}>Помещения</Chip>
        <Chip on={kind === 'Equipment'} onClick={() => update({ kind: 'Equipment', cat: [], cap: null, ft: [] })}>Оборудование</Chip>
        {cats.map((id) => {
          const c = categories.find((x) => x.id === id);
          return c && (
            <button key={id} type="button" className="rv-chip" aria-pressed onClick={() => update({ cat: cats.filter((x) => x !== id) })} aria-label={`Убрать фильтр «${c.name}»`}>
              {c.name}<X className="rv-ic rv-ic-sm" aria-hidden />
            </button>
          );
        })}
      </div>

      <div className="cnt-row">
        <p className="rv-sm rv-muted" style={{ margin: 0 }} aria-live="polite">{list.isLoading ? 'Загружаем каталог…' : np(total, 'ресурс', 'ресурса', 'ресурсов')}</p>
        <Seg small value={sort} onChange={(v) => update({ sort: v === 'Name' ? null : v })} label="Сортировка" options={[{ value: 'Name', label: 'По названию' }, { value: 'Capacity', label: 'По вместимости' }]} />
      </div>

      {showSpaces && spaces.length > 0 && (
        <div className="pad" style={{ paddingBottom: 12 }}>
          <p className="rv-label" style={{ margin: '0 0 8px' }}>Коворкинги</p>
          <div className="nowlist rv-card spaces-row">
            {spaces.map((s) => (
              <Link key={s.id} to={`/spaces/${s.id}`} className="nowr">
                <span className="icirc" style={{ background: 'var(--accent-soft)', color: 'var(--accent-text)' }}><Monitor className="rv-ic" aria-hidden /></span>
                <span className="nowr-b"><b className="ell">{s.name}</b><span className="rv-sm rv-muted ell">{s.location.name}{s.isQuiet ? ' · тихая зона' : ''} · {np(s.totalSeats, 'место', 'места', 'мест')}</span></span>
                <span className="nowr-v"><b className="rv-tnum">{s.freeSeats ?? '—'}</b><small>свободно</small></span>
              </Link>
            ))}
          </div>
          <p className="rv-label" style={{ margin: '16px 0 0' }}>Помещения и оборудование</p>
        </div>
      )}

      {list.isLoading && <CardSkeletons n={4} />}
      {!list.isLoading && items.length === 0 && (
        <Empty icon={Search} title="Ничего не нашлось" text="Проверьте название или ослабьте фильтры.">
          <button type="button" className="rv-btn rv-btn-primary rv-btn-lg" onClick={reset}>Сбросить фильтры</button>
        </Empty>
      )}
      {items.length > 0 && (
        <div className="grid-cards pad" style={{ paddingBottom: 12, opacity: list.isPlaceholderData ? 0.6 : 1, transition: 'opacity var(--duration-fast)' }}>
          {items.map((r) => <ResourceCard key={r.id} r={r} to={`/resources/${r.id}`} hours />)}
        </div>
      )}
      <LoadMore hasNext={!!list.hasNextPage} loading={list.isFetchingNextPage} onLoad={() => list.fetchNextPage()} />

      {filters && (
        <FiltersSheet
          kind={kind}
          cats={cats}
          locs={locs}
          fts={fts}
          cap={cap}
          categories={categories}
          locations={locations}
          features={features}
          baseQuery={query}
          onApply={(p) => { update(p); setFilters(false); }}
          onClose={() => setFilters(false)}
        />
      )}
    </div>
  );
}

const SHOW_CATEGORIES = 12;

function FiltersSheet(props: {
  kind: ResourceKind | null;
  cats: number[];
  locs: number[];
  fts: number[];
  cap: number;
  categories: ReturnType<typeof useCategories>['data'] & object;
  locations: ReturnType<typeof useLocations>['data'] & object;
  features: ReturnType<typeof useFeatures>['data'] & object;
  baseQuery: Record<string, unknown>;
  onApply: (p: Record<string, number[] | number | null>) => void;
  onClose: () => void;
}) {
  const [cats, setCats] = useState(props.cats);
  const [locs, setLocs] = useState(props.locs);
  const [fts, setFts] = useState(props.fts);
  const [cap, setCap] = useState(props.cap);
  const [catTerm, setCatTerm] = useState('');
  const [locTerm, setLocTerm] = useState('');
  const toggle = (list: number[], set: (v: number[]) => void, id: number) => set(list.includes(id) ? list.filter((x) => x !== id) : [...list, id]);

  const pool = props.categories.filter((c) => c.kind !== 'Seat' && (!props.kind || c.kind === props.kind));
  const t = catTerm.trim().toLowerCase();
  const matching = pool.filter((c) => !t || c.name.toLowerCase().includes(t));
  // Выбранные — всегда наверху, остальные до лимита: полный список доступен поиском.
  const shownCats = [...matching.filter((c) => cats.includes(c.id)), ...matching.filter((c) => !cats.includes(c.id))].slice(0, t ? 50 : Math.max(SHOW_CATEGORIES, cats.length));
  const lt = locTerm.trim().toLowerCase();
  const shownLocs = props.locations.filter((l) => !lt || `${l.name} ${l.address ?? ''}`.toLowerCase().includes(lt));
  const roomsPossible = props.kind !== 'Equipment';

  // Сколько ресурсов покажем — тем же запросом с размером страницы 1.
  const preview = useResources({ ...(props.baseQuery as object), categoryIds: cats, locationIds: locs, featureIds: roomsPossible ? fts : [], minCapacity: roomsPossible && cap ? cap : undefined, pageSize: 1 });
  const count = preview.data?.pages[0]?.totalCount;

  return (
    <Sheet title="Фильтры" onClose={props.onClose} wide>
      <div className="fgroup">
        <p className="rv-label">Категория{cats.length > 0 && <span className="opt"> · выбрано {cats.length}</span>}</p>
        {pool.length > SHOW_CATEGORIES && <SearchField value={catTerm} onChange={setCatTerm} placeholder={`Найти среди ${np(pool.length, 'категории', 'категорий', 'категорий')}`} label="Найти категорию" />}
        <div className="rv-row">
          {shownCats.map((c) => {
            const Icon = categoryIcon(c.code, c.kind);
            return <Chip key={c.id} check on={cats.includes(c.id)} onClick={() => toggle(cats, setCats, c.id)}>{!cats.includes(c.id) && <Icon className="rv-ic rv-ic-sm" aria-hidden />}{c.name}</Chip>;
          })}
          {!t && matching.length > shownCats.length && <span className="rv-sm rv-muted">и ещё {matching.length - shownCats.length} — найдите поиском</span>}
          {t && matching.length === 0 && <span className="rv-sm rv-muted">Такой категории нет</span>}
        </div>
      </div>

      <div className="fgroup">
        <p className="rv-label">Корпус</p>
        {props.locations.length > 8 && <SearchField value={locTerm} onChange={setLocTerm} placeholder="Корпус или адрес" label="Найти корпус" />}
        <div className="rv-row">
          {shownLocs.slice(0, 30).map((l) => <Chip key={l.id} check on={locs.includes(l.id)} onClick={() => toggle(locs, setLocs, l.id)}>{l.name}</Chip>)}
        </div>
      </div>

      {roomsPossible && (
        <>
          <div className="fgroup">
            <p className="rv-label">Вместимость</p>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <Stepper value={cap || '—'} width={156} decLabel="Меньше" incLabel="Больше" decDisabled={!cap} onDec={() => setCap(Math.max(0, cap - 1))} onInc={() => setCap(cap + 1)} />
              <span className="rv-sm rv-muted">{cap ? `от ${people(cap)}` : 'Любая · оборудование без вместимости скрыто'}</span>
            </div>
          </div>
          <div className="fgroup">
            <p className="rv-label">Оснащение</p>
            <p className="rv-hint" style={{ margin: '-4px 0 2px' }}>Покажем ресурсы, где есть всё выбранное.</p>
            <div className="rv-row">
              {props.features.filter((f) => f.code !== 'window').map((f) => {
                const Icon = featureIcon(f.code);
                return <Chip key={f.id} check on={fts.includes(f.id)} onClick={() => toggle(fts, setFts, f.id)}>{!fts.includes(f.id) && <Icon className="rv-ic rv-ic-sm" aria-hidden />}{f.name}</Chip>;
              })}
            </div>
          </div>
        </>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'auto minmax(0, 1fr)', gap: 8, marginTop: 4 }}>
        <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg" onClick={() => { setCats([]); setLocs([]); setFts([]); setCap(0); }}>Сбросить</button>
        <button type="button" className="rv-btn rv-btn-primary rv-btn-lg" disabled={count === 0}
          onClick={() => props.onApply({ cat: cats, loc: locs, ft: roomsPossible ? fts : [], cap: roomsPossible ? cap : null })}>
          {count === undefined ? 'Показать' : count === 0 ? 'Ничего не подходит' : `Показать ${np(count, 'ресурс', 'ресурса', 'ресурсов')}`}
        </button>
      </div>
    </Sheet>
  );
}
