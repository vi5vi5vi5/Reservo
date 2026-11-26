import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router';
import { Bell, CalendarCheck, House, LayoutGrid, LogIn, ShieldCheck, User as UserIcon, WifiOff, type LucideIcon } from 'lucide-react';
import { useAdminBookings, useUnread } from '../api/hooks';
import { initials } from '../lib/text';
import { Logo } from '../ui/controls';
import { useAuth } from './auth';
import { useLive } from './live';

/**
 * Каркас: на телефоне — нижние вкладки, на планшете и компьютере — левое меню (layout.css).
 * Экраны «внутри» (ресурс, бронь, вход) прячут вкладки на телефоне, чтобы не мешали панели действия.
 */

const ShellContext = createContext<(visible: boolean) => void>(() => undefined);

/** Экран прячет нижние вкладки, пока он открыт. */
export function useHideTabs() {
  const set = useContext(ShellContext);
  useEffect(() => {
    set(false);
    return () => set(true);
  }, [set]);
}

interface Tab { to: string; label: string; icon: LucideIcon; count?: number; ring?: boolean; end?: boolean }

export function Shell({ children }: { children: ReactNode }) {
  const { user, isAdmin } = useAuth();
  const { ring } = useLive();
  const [tabs, setTabs] = useState(true);
  const unread = useUnread(!!user).data ?? 0;
  const pending = useAdminBookings({ status: 'Pending', from: hourNow(), pageSize: 1 }, isAdmin).data?.pages[0]?.totalCount ?? 0;
  const [ringing, setRinging] = useState(false);
  const online = useOnline();
  const location = useLocation();

  useEffect(() => {
    if (!ring) return;
    setRinging(true);
    const t = window.setTimeout(() => setRinging(false), 900);
    return () => window.clearTimeout(t);
  }, [ring]);

  // Новый экран — с начала страницы.
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [location.pathname]);

  const main: Tab[] = user
    ? [
        { to: '/', label: 'Главная', icon: House, end: true },
        { to: '/catalog', label: 'Каталог', icon: LayoutGrid },
        { to: '/bookings', label: 'Брони', icon: CalendarCheck },
        { to: '/notifications', label: 'Уведомления', icon: Bell, count: unread, ring: ringing },
        { to: '/profile', label: 'Профиль', icon: UserIcon, count: isAdmin ? pending : 0 },
      ]
    : [
        { to: '/', label: 'Главная', icon: House, end: true },
        { to: '/catalog', label: 'Каталог', icon: LayoutGrid },
        { to: '/login', label: 'Войти', icon: LogIn },
      ];

  const side: Tab[] = user
    ? [
        { to: '/', label: 'Найти свободное', icon: House, end: true },
        { to: '/catalog', label: 'Каталог', icon: LayoutGrid },
        { to: '/bookings', label: 'Мои брони', icon: CalendarCheck },
        { to: '/notifications', label: 'Уведомления', icon: Bell, count: unread, ring: ringing },
      ]
    : main.slice(0, 2);

  return (
    <ShellContext.Provider value={setTabs}>
      <div className="app" data-tabs={tabs ? 'true' : 'false'}>
        <aside className="rv-side" aria-label="Меню">
          <NavLink to="/" className="rv-logo" aria-label="Reservo — на главную"><Logo /></NavLink>
          {side.map((t) => <SideLink key={t.to} tab={t} />)}
          {isAdmin && (
            <>
              <div className="rv-nav-title">Администратор</div>
              <SideLink tab={{ to: '/admin', label: 'Управление', icon: ShieldCheck, count: pending }} />
            </>
          )}
          {user ? (
            <NavLink to="/profile" className="side-user" aria-label="Профиль">
              <span className="rv-avatar">{initials(user.fullName)}</span>
              <span className="who"><b>{user.fullName}</b><small>{user.email}</small></span>
            </NavLink>
          ) : (
            <NavLink to="/login" className="rv-btn rv-btn-primary" style={{ marginTop: 'auto' }}>
              <LogIn className="rv-ic rv-ic-sm" aria-hidden /><span className="nav-label">Войти</span>
            </NavLink>
          )}
        </aside>

        <main className="app-main">{children}</main>

        {tabs && (
          <nav className="rv-tabbar" style={{ gridTemplateColumns: `repeat(${main.length}, minmax(0, 1fr))` }} aria-label="Разделы">
            {main.map((t) => (
              <NavLink key={t.to} to={t.to} end={t.end} className={`rv-tab${t.ring ? ' ring' : ''}`}>
                <span className="pill"><t.icon className="rv-ic" aria-hidden /></span>
                {t.label}
                {!!t.count && <span className="rv-counter">{t.count > 99 ? '99+' : t.count}</span>}
              </NavLink>
            ))}
          </nav>
        )}

        {!online && (
          <div className="offz"><div className="rv-offline" role="status"><WifiOff className="rv-ic rv-ic-sm" aria-hidden />Нет соединения — пробуем переподключиться</div></div>
        )}
      </div>
    </ShellContext.Provider>
  );
}

function SideLink({ tab }: { tab: Tab }) {
  return (
    <NavLink to={tab.to} end={tab.end} className={`rv-nav${tab.ring ? ' rv-bell ring' : ''}`} title={tab.label}>
      <tab.icon className="rv-ic" aria-hidden />
      <span className="nav-label">{tab.label}</span>
      {!!tab.count && <span className="rv-counter">{tab.count > 99 ? '99+' : tab.count}</span>}
    </NavLink>
  );
}

/** «Сейчас» с точностью до часа: ключ запроса не меняется на каждой перерисовке. */
export function hourNow() {
  const d = new Date();
  d.setMinutes(0, 0, 0);
  return d.toISOString();
}

function useOnline() {
  const [online, setOnline] = useState(navigator.onLine);
  useEffect(() => {
    const on = () => setOnline(true);
    const off = () => setOnline(false);
    window.addEventListener('online', on);
    window.addEventListener('offline', off);
    return () => {
      window.removeEventListener('online', on);
      window.removeEventListener('offline', off);
    };
  }, []);
  return online;
}

/** Требует входа: гостя отправляет на вход и потом возвращает обратно. */
export function useRequireAuth() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  return (resume?: unknown) => {
    if (user) return true;
    navigate('/login', { state: { from: location.pathname + location.search, resume } });
    return false;
  };
}
