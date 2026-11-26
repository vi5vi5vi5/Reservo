import { Link, Navigate, useNavigate } from 'react-router';
import { ChevronRight, LogOut, ShieldCheck } from 'lucide-react';
import { useAdminBookings, useMyBookings } from '../../api/hooks';
import { useAuth } from '../../app/auth';
import { hourNow } from '../../app/Shell';
import { useTheme, type ThemeChoice } from '../../app/theme';
import { useToast } from '../../app/toast';
import { initials } from '../../lib/text';
import { dayLabel, local } from '../../lib/time';
import { Bar, Seg } from '../../ui/controls';
import { MAX_ACTIVE } from '../booking/BookingFlow';

export function ProfilePage() {
  const { user, isAdmin, logout } = useAuth();
  const { theme, setTheme } = useTheme();
  const toast = useToast();
  const navigate = useNavigate();
  const active = useMyBookings({ status: ['Pending', 'Confirmed'], from: hourNow(), pageSize: 1 }, !!user).data?.pages[0]?.totalCount ?? 0;
  const pending = useAdminBookings({ status: 'Pending', from: hourNow(), pageSize: 1 }, isAdmin).data?.pages[0]?.totalCount ?? 0;
  if (!user) return <Navigate to="/login" state={{ from: '/profile' }} replace />;

  return (
    <div className="page page-narrow">
      <Bar title="Профиль" />
      <div className="pad" style={{ display: 'flex', flexDirection: 'column', gap: 16, paddingBottom: 28 }}>
        <div className="rv-card" style={{ padding: '18px 16px', display: 'flex', gap: 14, alignItems: 'center' }}>
          <span className="rv-avatar" style={{ width: 60, height: 60, fontSize: 20 }}>{initials(user.fullName)}</span>
          <div style={{ minWidth: 0, display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'flex-start' }}>
            <b className="ell" style={{ fontSize: 18, lineHeight: '24px', maxWidth: '100%' }}>{user.fullName}</b>
            <span className="rv-sm rv-muted ell" style={{ maxWidth: '100%' }}>{user.email}</span>
            {isAdmin && <span className="rv-badge rv-badge-role">Администратор</span>}
          </div>
        </div>
        <div className="rv-card" style={{ padding: '4px 0' }}>
          <div className="kv"><span className="rv-muted">Аккаунт создан</span><b>{dayLabel(local(user.createdAt).key).split(', ')[1]}</b></div>
          <div className="kv"><span className="rv-muted">Активные брони</span><b className="rv-tnum">{active} из {MAX_ACTIVE}</b></div>
        </div>
        {isAdmin && (
          <Link to="/admin" className="rv-card rowcard" style={{ width: '100%' }}>
            <span className="icirc" style={{ background: 'var(--accent-soft)', color: 'var(--accent-text)' }}><ShieldCheck className="rv-ic" aria-hidden /></span>
            <span style={{ flex: 1, minWidth: 0 }}><b className="ell" style={{ fontSize: 15 }}>Администрирование</b><span className="rv-sm rv-muted ell">Заявки, все брони, загрузка</span></span>
            {pending > 0 && <span className="rv-counter">{pending}</span>}
            <ChevronRight className="rv-ic" aria-hidden style={{ color: 'var(--ink-muted)' }} />
          </Link>
        )}
        <div className="rv-field">
          <span className="rv-label">Тема</span>
          <Seg<ThemeChoice> full value={theme} onChange={setTheme} label="Тема" options={[{ value: 'light', label: 'Светлая' }, { value: 'dark', label: 'Тёмная' }, { value: 'system', label: 'Как в системе' }]} />
          <span className="rv-hint">«Как в системе» повторяет настройку телефона или компьютера.</span>
        </div>
        <button type="button" className="rv-btn rv-btn-secondary rv-btn-lg rv-btn-block" style={{ marginTop: 8 }}
          onClick={async () => { await logout(); toast('info', 'Вы вышли из аккаунта'); navigate('/'); }}>
          <LogOut className="rv-ic" aria-hidden />Выйти
        </button>
      </div>
    </div>
  );
}
