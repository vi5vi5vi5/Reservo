import { useEffect, useRef, type ReactNode } from 'react';
import { Check, ChevronLeft, Minus, Plus, Search, X, type LucideIcon } from 'lucide-react';
import { useNavigate } from 'react-router';

/** Кнопка «назад»: в историю браузера, а если пришли по прямой ссылке — на запасной адрес. */
export function BackButton({ fallback = '/', outline }: { fallback?: string; outline?: boolean }) {
  const navigate = useNavigate();
  return (
    <button
      type="button"
      className={`rv-btn rv-btn-icon ${outline ? 'rv-btn-outline' : 'rv-btn-ghost'} ib`}
      aria-label="Назад"
      onClick={() => (window.history.state?.idx > 0 ? navigate(-1) : navigate(fallback))}
    >
      <ChevronLeft className="rv-ic" aria-hidden />
    </button>
  );
}

/** Шапка экрана. */
export function Bar({ title, back, small, children, sticky = true }: { title?: ReactNode; back?: string; small?: boolean; children?: ReactNode; sticky?: boolean }) {
  return (
    <header className={`bar${sticky ? ' sticky' : ''}`}>
      {back !== undefined && <BackButton fallback={back} />}
      {title !== undefined && <h1 className={`bar-t${small ? ' bar-sm' : ''}`}>{title}</h1>}
      {children}
    </header>
  );
}

export function Seg<T extends string>({ options, value, onChange, full, small, label }: {
  options: { value: T; label: ReactNode }[];
  value: T;
  onChange: (v: T) => void;
  full?: boolean;
  small?: boolean;
  label?: string;
}) {
  return (
    <div className={`rv-seg${full ? ' seg-full' : ''}${small ? ' seg-sm' : ''}`} role="group" aria-label={label}>
      {options.map((o) => (
        <button key={o.value} type="button" aria-pressed={o.value === value} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}

export function Chip({ on, onClick, children, check, icon: Icon }: { on?: boolean; onClick: () => void; children: ReactNode; check?: boolean; icon?: LucideIcon }) {
  return (
    <button type="button" className="rv-chip" aria-pressed={on ?? false} onClick={onClick}>
      {check && <Check className="rv-ic rv-ic-sm rv-chip-check" aria-hidden />}
      {Icon && <Icon className="rv-ic rv-ic-sm" aria-hidden />}
      {children}
    </button>
  );
}

export function Stepper({ value, onDec, onInc, decDisabled, incDisabled, decLabel, incLabel, invalid, width }: {
  value: ReactNode;
  onDec: () => void;
  onInc: () => void;
  decDisabled?: boolean;
  incDisabled?: boolean;
  decLabel: string;
  incLabel: string;
  invalid?: boolean;
  width?: number;
}) {
  return (
    <div className="rv-control rv-stepper ctl-lg" data-invalid={invalid ? 'true' : undefined} style={width ? { width } : undefined}>
      <button type="button" className="rv-adorn" aria-label={decLabel} disabled={decDisabled} onClick={onDec}>
        <Minus className="rv-ic" aria-hidden />
      </button>
      <span className="stv" aria-live="polite">{value}</span>
      <button type="button" className="rv-adorn" aria-label={incLabel} disabled={incDisabled} onClick={onInc}>
        <Plus className="rv-ic" aria-hidden />
      </button>
    </div>
  );
}

export function SearchField({ value, onChange, placeholder, label, autoFocus }: {
  value: string;
  onChange: (v: string) => void;
  placeholder: string;
  label: string;
  autoFocus?: boolean;
}) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (autoFocus && window.matchMedia('(min-width: 768px)').matches) ref.current?.focus();
  }, [autoFocus]);
  return (
    <div className="rv-control rv-control-pill">
      <Search className="rv-ic" aria-hidden />
      <input ref={ref} type="search" aria-label={label} placeholder={placeholder} value={value} onChange={(e) => onChange(e.target.value)} />
      {value && (
        <button type="button" className="rv-adorn" aria-label="Очистить поиск" onClick={() => onChange('')}>
          <X className="rv-ic rv-ic-sm" aria-hidden />
        </button>
      )}
    </div>
  );
}

export function Empty({ icon: Icon, title, text, children, tone }: { icon: LucideIcon; title: string; text?: string; children?: ReactNode; tone?: 'ok' }) {
  return (
    <div className="rv-empty">
      <div className="rv-empty-art" style={tone === 'ok' ? { background: 'var(--status-confirmed-bg)', color: 'var(--status-confirmed)' } : undefined}>
        <Icon className="rv-ic" aria-hidden />
      </div>
      <h2 className="rv-h2" style={{ fontSize: 19 }}>{title}</h2>
      {text && <p>{text}</p>}
      {children && <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 12, alignItems: 'center' }}>{children}</div>}
    </div>
  );
}

export function Banner({ icon: Icon, tone, title, children, role }: { icon: LucideIcon; tone?: 'danger' | 'warn'; title?: ReactNode; children?: ReactNode; role?: string }) {
  return (
    <div className={`rv-banner${tone ? ` rv-banner-${tone}` : ''}`} role={role}>
      <Icon className="rv-ic" aria-hidden />
      <div style={{ minWidth: 0 }}>
        {title && <b style={{ display: 'block' }}>{title}</b>}
        {children && <span className={title ? 'rv-sm' : 'bt'} style={{ display: 'block', marginTop: title ? 2 : 0 }}>{children}</span>}
      </div>
    </div>
  );
}

/** Скелетоны карточек, пока список грузится. */
export function CardSkeletons({ n = 3 }: { n?: number }) {
  return (
    <div className="grid-cards pad" aria-busy="true" aria-label="Загрузка">
      {Array.from({ length: n }, (_, i) => (
        <div key={i} className="rv-card skc">
          <div className="rv-skel" style={{ width: 88, height: 88, borderRadius: 10 }} />
          <div className="skc-b">
            <div className="rv-skel" style={{ height: 12, width: '40%' }} />
            <div className="rv-skel" style={{ height: 16, width: '85%' }} />
            <div className="rv-skel" style={{ height: 12, width: '60%' }} />
            <div style={{ display: 'flex', gap: 4 }}>
              <div className="rv-skel" style={{ width: 26, height: 26, borderRadius: 8 }} />
              <div className="rv-skel" style={{ width: 26, height: 26, borderRadius: 8 }} />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

/** Подгрузка следующей страницы: сама, когда низ списка показался на экране, и кнопкой — для клавиатуры. */
export function LoadMore({ hasNext, loading, onLoad }: { hasNext: boolean; loading: boolean; onLoad: () => void }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!hasNext || !ref.current) return;
    const io = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting) && !loading) onLoad();
    }, { rootMargin: '400px' });
    io.observe(ref.current);
    return () => io.disconnect();
  }, [hasNext, loading, onLoad]);

  if (!hasNext) return null;
  return (
    <div ref={ref} className="more">
      <button type="button" className="rv-btn rv-btn-secondary" aria-busy={loading} onClick={onLoad}>
        Показать ещё
      </button>
    </div>
  );
}

export function Logo({ small }: { small?: boolean }) {
  return (
    <span className="rv-logo">
      <LogoMark size={small ? 28 : 32} />
      <span className="rv-logo-word">Reservo</span>
    </span>
  );
}

export function LogoMark({ size = 32 }: { size?: number }) {
  return (
    <svg className="rv-mark" viewBox="0 0 32 32" aria-hidden style={{ width: size, height: size }}>
      <path className="m-shackle" d="M10 13V9.5a6 6 0 0 1 12 0V13" />
      <rect className="m-body" x="4" y="12" width="24" height="18" rx="6" />
      <rect className="m-slot" x="9" y="17" width="14" height="3" rx="1.5" />
      <rect className="m-slot-on" x="9" y="22" width="14" height="3" rx="1.5" />
    </svg>
  );
}
