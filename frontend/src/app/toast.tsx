import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { Bell, CircleCheck, CircleX, Info, X } from 'lucide-react';

type Kind = 'success' | 'error' | 'info' | 'notice';
interface Toast { id: number; kind: Kind; title: string; text?: string }

const ToastContext = createContext<(kind: Kind, title: string, text?: string) => void>(() => undefined);

const icons = { success: CircleCheck, error: CircleX, info: Info, notice: Bell };

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<Toast | null>(null);
  const timer = useRef<number | undefined>(undefined);

  const show = useCallback((kind: Kind, title: string, text?: string) => {
    window.clearTimeout(timer.current);
    setToast({ id: Date.now(), kind, title, text });
    timer.current = window.setTimeout(() => setToast(null), 4500);
  }, []);

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const Icon = toast ? icons[toast.kind] : null;

  return (
    <ToastContext.Provider value={show}>
      {children}
      {toast && Icon && (
        <div className="toastz">
          <div key={toast.id} className={`rv-toast rv-toast-${toast.kind}`} role={toast.kind === 'error' ? 'alert' : 'status'}>
            <span className="rv-toast-ic"><Icon className="rv-ic" aria-hidden /></span>
            <div className="rv-toast-body">
              <b>{toast.title}</b>
              {toast.text && <p>{toast.text}</p>}
            </div>
            <button type="button" className="rv-btn rv-btn-icon rv-btn-ghost rv-toast-x" style={{ ['--h' as string]: '36px' }} aria-label="Закрыть" onClick={() => setToast(null)}>
              <X className="rv-ic rv-ic-sm" aria-hidden />
            </button>
          </div>
        </div>
      )}
    </ToastContext.Provider>
  );
}

export const useToast = () => useContext(ToastContext);
