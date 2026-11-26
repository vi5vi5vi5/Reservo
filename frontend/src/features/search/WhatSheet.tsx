import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { Armchair, Check, ChevronRight, DoorOpen, Laptop, Monitor, Package } from 'lucide-react';
import { useCategories, useResources } from '../../api/hooks';
import type { Category } from '../../api/types';
import { categoryIcon } from '../../lib/icons';
import { where } from '../../lib/text';
import { SearchField } from '../../ui/controls';
import { Sheet } from '../../ui/Sheet';
import { useSearch, type What } from './state';

/** Сколько категорий показывать в группе без поиска. Остальные находятся поиском — их может быть сотни. */
const GROUP_LIMIT = 8;

function sameWhat(a: What, b: What) {
  if (a.type !== b.type) return false;
  if (a.type === 'seat' && b.type === 'seat') return a.pc === b.pc;
  if (a.type === 'kind' && b.type === 'kind') return a.kind === b.kind;
  if (a.type === 'category' && b.type === 'category') return a.categoryId === b.categoryId;
  return false;
}

export function useDebounced<T>(value: T, ms = 250) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = window.setTimeout(() => setV(value), ms);
    return () => window.clearTimeout(t);
  }, [value, ms]);
  return v;
}

export function WhatSheet({ onClose }: { onClose: () => void }) {
  const { query, set } = useSearch();
  const navigate = useNavigate();
  const categories = useCategories().data ?? [];
  const [term, setTerm] = useState('');
  const t = term.trim().toLowerCase();
  const debounced = useDebounced(t);
  const found = useResources({ search: debounced, pageSize: 6 }, debounced.length >= 2);
  const resources = debounced.length >= 2 ? (found.data?.pages[0]?.items ?? []) : [];
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});

  const choose = (what: What) => {
    set({ what });
    onClose();
  };

  const groups = useMemo(() => {
    const match = (c: Category) => !t || `${c.name} ${c.description ?? ''} ${c.code}`.toLowerCase().includes(t);
    return {
      rooms: categories.filter((c) => c.kind === 'Room' && match(c)),
      equipment: categories.filter((c) => c.kind === 'Equipment' && match(c)),
    };
  }, [categories, t]);

  const seatOptions = [
    { what: { type: 'seat', pc: true } as What, label: 'Место с компьютером', sub: 'Коворкинг, рабочий компьютер на месте', icon: Monitor },
    { what: { type: 'seat', pc: false } as What, label: 'Место для своего ноутбука', sub: 'Коворкинг, розетка рядом', icon: Laptop },
  ].filter((o) => !t || `${o.label} коворкинг место ${o.sub}`.toLowerCase().includes(t));

  const anyRoom = !t || 'любое помещение комната аудитория'.includes(t);
  const anyEquipment = !t || 'любое оборудование техника'.includes(t);
  const nothing = !seatOptions.length && !groups.rooms.length && !groups.equipment.length && !resources.length && !anyRoom && !anyEquipment;

  const Opt = ({ what, label, sub, icon: Icon }: { what: What; label: string; sub?: string; icon: typeof Monitor }) => {
    const on = sameWhat(query.what, what);
    return (
      <button type="button" className="optrow" aria-pressed={on} onClick={() => choose(what)}>
        <span className="icirc" style={{ width: 36, height: 36, background: 'var(--surface-sunken)', color: 'var(--accent-text)' }}><Icon className="rv-ic" aria-hidden /></span>
        <span className="opt-b"><span>{label}</span>{sub && <span className="rv-sm rv-muted">{sub}</span>}</span>
        {on && <Check className="rv-ic" aria-hidden />}
      </button>
    );
  };

  const Group = ({ id, title, items, any }: { id: string; title: string; items: Category[]; any?: { what: What; label: string; icon: typeof Monitor } }) => {
    if (!items.length && !any) return null;
    const open = expanded[id] || !!t;
    const shown = open ? items : items.slice(0, GROUP_LIMIT);
    return (
      <>
        <p className="rv-label" style={{ margin: '4px 0 0' }}>{title}</p>
        <div className="optlist">
          {any && <Opt what={any.what} label={any.label} icon={any.icon} />}
          {shown.map((c) => (
            <Opt key={c.id} what={{ type: 'category', categoryId: c.id, kind: c.kind }} label={c.name} sub={c.description ?? undefined} icon={categoryIcon(c.code, c.kind)} />
          ))}
          {items.length > shown.length && (
            <button type="button" className="rv-btn rv-btn-ghost" style={{ alignSelf: 'flex-start' }} onClick={() => setExpanded((e) => ({ ...e, [id]: true }))}>
              Ещё {items.length - shown.length} — или найдите поиском
            </button>
          )}
        </div>
      </>
    );
  };

  return (
    <Sheet title="Что нужно" onClose={onClose} wide>
      <SearchField value={term} onChange={setTerm} placeholder="Тип, название или корпус" label="Что или где искать" autoFocus />

      {seatOptions.length > 0 && (
        <>
          <p className="rv-label" style={{ margin: '4px 0 0' }}>Место в коворкинге</p>
          <div className="optlist">{seatOptions.map((o) => <Opt key={o.label} {...o} />)}</div>
        </>
      )}
      <Group id="rooms" title="Помещения" items={groups.rooms} any={anyRoom ? { what: { type: 'kind', kind: 'Room' }, label: 'Любое помещение', icon: DoorOpen } : undefined} />
      <Group id="equipment" title="Оборудование" items={groups.equipment} any={anyEquipment ? { what: { type: 'kind', kind: 'Equipment' }, label: 'Любое оборудование', icon: Package } : undefined} />

      {resources.length > 0 && (
        <>
          <p className="rv-label" style={{ margin: '4px 0 0' }}>Конкретные места и вещи</p>
          <div className="optlist">
            {resources.map((r) => {
              const Icon = categoryIcon(r.category.code, r.category.kind);
              return (
                <button key={r.id} type="button" className="optrow" onClick={() => { onClose(); navigate(`/resources/${r.id}`); }}>
                  <span className="icirc" style={{ width: 36, height: 36, background: 'var(--surface-sunken)', color: 'var(--ink-muted)' }}><Icon className="rv-ic" aria-hidden /></span>
                  <span className="opt-b"><span>{r.name}</span><span className="rv-sm rv-muted">{r.category.name} · {where(r.location.name, r.floor)}</span></span>
                  <ChevronRight className="rv-ic rv-ic-sm" aria-hidden style={{ color: 'var(--ink-muted)' }} />
                </button>
              );
            })}
          </div>
        </>
      )}

      {nothing && <p className="rv-muted" style={{ margin: 0, padding: '8px 4px' }}>Ничего не нашлось. Попробуйте короче: «проектор», «305», «Точка».</p>}
      <p className="rv-hint" style={{ margin: 0 }}>
        <Armchair className="rv-ic rv-ic-sm" aria-hidden style={{ verticalAlign: '-3px', marginRight: 6 }} />
        Или найдите конкретное по названию: «Байкал», «Prusa», «305».
      </p>
    </Sheet>
  );
}
