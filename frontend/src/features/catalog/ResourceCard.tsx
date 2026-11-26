import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { ChevronRight, Clock, MapPin, ShieldCheck, Users, Wrench } from 'lucide-react';
import type { Resource } from '../../api/types';
import { categoryIcon, featureIcon } from '../../lib/icons';
import { people, where } from '../../lib/text';

/** Сколько значков оснащения помещается в карточке; остальное — «+N». */
const MAX_FEATS = 5;

export function ResourceThumb({ r, size = 88 }: { r: Pick<Resource, 'category' | 'imageUrl' | 'name'>; size?: number }) {
  const Icon = categoryIcon(r.category.code, r.category.kind);
  return (
    <span className="resm-ph" style={{ width: size, height: size }}>
      {r.imageUrl ? <img src={r.imageUrl} alt="" loading="lazy" style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', objectFit: 'cover' }} /> : <span className="rv-photo-grid" />}
      {!r.imageUrl && <Icon className="rv-ic" aria-hidden />}
    </span>
  );
}

export function Feats({ r }: { r: Resource }) {
  const shown = r.features.slice(0, MAX_FEATS);
  return (
    <span className="feats">
      {shown.map((f) => {
        const Icon = featureIcon(f.code);
        return <span key={f.id} className="feat" role="img" aria-label={f.name} title={f.name}><Icon className="rv-ic" aria-hidden /></span>;
      })}
      {r.features.length > MAX_FEATS && <span className="feat" title={r.features.slice(MAX_FEATS).map((f) => f.name).join(', ')} style={{ width: 'auto', padding: '0 6px', fontSize: 12, fontWeight: 700 }}>+{r.features.length - MAX_FEATS}</span>}
      {r.requiresApproval && <span className="rv-badge rv-badge-approval"><ShieldCheck className="rv-ic" aria-hidden />Согласование</span>}
      {!r.isActive && <span className="rv-badge rv-badge-off"><Wrench className="rv-ic" aria-hidden />Временно недоступен</span>}
    </span>
  );
}

/** Карточка ресурса: в каталоге и в результатах поиска. foot — нижняя строка (свободно до…, кнопка брони). */
export function ResourceCard({ r, to, foot, hours }: { r: Resource; to: string; foot?: ReactNode; hours?: boolean }) {
  return (
    <article className="rv-card resm">
      <Link to={to} className="resm-hit">
        <ResourceThumb r={r} />
        <span className="resm-b">
          <span className="rv-cap">{r.category.name}</span>
          <span className="resm-name">{r.name}</span>
          <span className="rv-res-meta">
            <span><MapPin className="rv-ic" aria-hidden />{where(r.location.name, r.floor)}</span>
            {r.capacity != null && <span><Users className="rv-ic" aria-hidden />до {people(r.capacity)}</span>}
            {hours && <span><Clock className="rv-ic" aria-hidden />{r.openTime.slice(0, 5)}–{r.closeTime.slice(0, 5)}</span>}
          </span>
          <Feats r={r} />
        </span>
      </Link>
      <div className="resm-f">
        {foot ?? (
          <>
            <span className="rv-sm rv-muted">{r.allowsOvernight ? 'Можно на ночь и на несколько дней' : r.description?.slice(0, 60) ?? 'Открыть расписание'}</span>
            <ChevronRight className="rv-ic rv-ic-sm" aria-hidden style={{ color: 'var(--ink-muted)' }} />
          </>
        )}
      </div>
    </article>
  );
}
