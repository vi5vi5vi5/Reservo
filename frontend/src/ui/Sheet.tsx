import { useEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';

interface SheetProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** Скрыть стандартный заголовок — когда экран рисует свой. */
  bare?: boolean;
  wide?: boolean;
}

/**
 * Нижний лист на телефоне и окно по центру на компьютере — один компонент, разницу делает CSS (layout.css).
 * Закрывается по Esc и по тапу на затемнение, фокус переходит внутрь и возвращается обратно.
 */
export function Sheet({ title, onClose, children, bare, wide }: SheetProps) {
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const prevFocus = document.activeElement as HTMLElement | null;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    box.current?.focus();

    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('keydown', onKey);
      document.body.style.overflow = prevOverflow;
      prevFocus?.focus?.();
    };
  }, [onClose]);

  return createPortal(
    <div className="scrim" onClick={onClose}>
      <div
        ref={box}
        tabIndex={-1}
        className={`rv-sheet${wide ? ' wide' : ''}`}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(e) => e.stopPropagation()}
        style={{ outline: 'none' }}
      >
        <div className="rv-grabber" />
        {!bare && (
          <div className="sheet-h">
            <h2 className="rv-h2">{title}</h2>
            <CloseButton onClick={onClose} />
          </div>
        )}
        {children}
      </div>
    </div>,
    document.body,
  );
}

export function CloseButton({ onClick }: { onClick: () => void }) {
  return (
    <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost ib" aria-label="Закрыть" onClick={onClick}>
      <X className="rv-ic" aria-hidden />
    </button>
  );
}
