import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import { Check, CircleAlert, CircleX, Eye, EyeOff, Info, ShieldCheck, User as UserIcon, X } from 'lucide-react';
import type { ApiError } from '../../api/client';
import { useAuth } from '../../app/auth';
import { useHideTabs } from '../../app/Shell';
import { useToast } from '../../app/toast';
import { Banner, BackButton, LogoMark } from '../../ui/controls';

interface AuthState { from?: string; resume?: unknown; email?: string }

/** После входа — туда, откуда пришли, и с тем же черновиком брони, чтобы не выбирать время заново. */
function useAfterAuth() {
  const navigate = useNavigate();
  const state = (useLocation().state ?? {}) as AuthState;
  return () => navigate(state.from ?? '/', { replace: true, state: state.resume ? { resume: state.resume } : undefined });
}

// Демо-аккаунты из сида (PROJECT.md 11.4). Пароль подставляется только при локальной разработке.
const DEMO = { student: 'student@reservo.example', admin: 'admin@reservo.example' };
const DEMO_PASSWORDS = import.meta.env.DEV ? { student: 'Demo12345', admin: 'Admin12345' } : null;

export function LoginPage() {
  useHideTabs();
  const { login } = useAuth();
  const toast = useToast();
  const after = useAfterAuth();
  const state = (useLocation().state ?? {}) as AuthState;
  const [email, setEmail] = useState(state.email ?? '');
  const [password, setPassword] = useState('');
  const [show, setShow] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fe, setFe] = useState<{ email?: string; password?: string }>({});

  const submit = async (e?: FormEvent, override?: { email: string; password: string }) => {
    e?.preventDefault();
    const em = (override?.email ?? email).trim();
    const pw = override?.password ?? password;
    const errs: typeof fe = {};
    if (!em) errs.email = 'Введите email.';
    else if (!em.includes('@')) errs.email = 'Email указан в неверном формате.';
    if (!pw) errs.password = 'Введите пароль.';
    setFe(errs);
    if (errs.email || errs.password) return;
    setBusy(true);
    setError(null);
    try {
      const u = await login(em, pw);
      toast('success', 'Вы вошли', `Добро пожаловать, ${u.fullName.split(' ')[0]}.`);
      after();
    } catch (err) {
      const ex = err as ApiError;
      setError(ex.code === 'auth.user_blocked' ? 'Учётная запись заблокирована. Обратитесь к администратору.' : ex.code === 'auth.invalid_credentials' ? 'Неверный email или пароль.' : ex.message);
    } finally {
      setBusy(false);
    }
  };

  const demo = (who: 'student' | 'admin') => {
    setEmail(DEMO[who]);
    if (DEMO_PASSWORDS) {
      setPassword(DEMO_PASSWORDS[who]);
      submit(undefined, { email: DEMO[who], password: DEMO_PASSWORDS[who] });
    }
  };

  return (
    <div className="page auth-page">
      <header className="bar"><BackButton fallback="/" /></header>
      <form className="pad auth-box" onSubmit={submit} noValidate>
        <LogoMark size={44} />
        <div><h1 className="h1m">Вход</h1><p className="rv-muted" style={{ margin: '6px 0 0' }}>Бронируйте аудитории, переговорные, места и оборудование за 30 секунд.</p></div>
        {state.resume !== undefined && <Banner icon={Info} title="Войдите, чтобы забронировать">Выбранное время сохранится — после входа сразу откроется оформление.</Banner>}
        {error && <Banner icon={CircleX} tone="danger" role="alert" title={error} />}
        <label className="rv-field">
          <span className="rv-label">Email</span>
          <span className="rv-control ctl-lg" data-invalid={fe.email ? 'true' : undefined}>
            <input type="email" inputMode="email" autoComplete="email" placeholder="name@univ.ru" value={email} onChange={(e) => setEmail(e.target.value)} aria-invalid={!!fe.email} />
          </span>
          {fe.email && <span className="rv-error-text"><CircleAlert className="rv-ic rv-ic-sm" aria-hidden />{fe.email}</span>}
        </label>
        <label className="rv-field">
          <span className="rv-label">Пароль</span>
          <span className="rv-control ctl-lg" data-invalid={fe.password ? 'true' : undefined}>
            <input type={show ? 'text' : 'password'} autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} aria-invalid={!!fe.password} />
            <button type="button" className="rv-adorn" aria-label={show ? 'Скрыть пароль' : 'Показать пароль'} onClick={() => setShow(!show)}>{show ? <EyeOff className="rv-ic" aria-hidden /> : <Eye className="rv-ic" aria-hidden />}</button>
          </span>
          {fe.password && <span className="rv-error-text"><CircleAlert className="rv-ic rv-ic-sm" aria-hidden />{fe.password}</span>}
        </label>
        <button type="submit" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block" aria-busy={busy}>Войти</button>
        <p className="rv-muted" style={{ margin: 0, textAlign: 'center' }}>Нет аккаунта? <Link to="/register" state={state} className="lnk">Зарегистрироваться</Link></p>
        <div className="fbox" style={{ marginTop: 8, border: '1px dashed var(--border-strong)' }}>
          <p className="rv-cap" style={{ margin: 0 }}>Демо-доступ</p>
          <div className="two">
            <button type="button" className="rv-btn rv-btn-secondary" onClick={() => demo('student')}><UserIcon className="rv-ic rv-ic-sm" aria-hidden />Студент</button>
            <button type="button" className="rv-btn rv-btn-secondary" onClick={() => demo('admin')}><ShieldCheck className="rv-ic rv-ic-sm" aria-hidden />Администратор</button>
          </div>
          <p className="rv-sm rv-muted" style={{ margin: 0 }}>
            Студент — {DEMO.student}, администратор — {DEMO.admin}.{DEMO_PASSWORDS ? ' Вход в один клик.' : ' Пароль выдаёт организатор демо.'}
          </p>
        </div>
      </form>
    </div>
  );
}

export function RegisterPage() {
  useHideTabs();
  const { register } = useAuth();
  const toast = useToast();
  const after = useAfterAuth();
  const navigate = useNavigate();
  const state = (useLocation().state ?? {}) as AuthState;
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [show, setShow] = useState(false);
  const [busy, setBusy] = useState(false);
  const [tried, setTried] = useState(false);
  const [fe, setFe] = useState<{ name?: string; email?: string; dup?: boolean; other?: string }>({});

  // BR-18: 8–128 символов, хотя бы одна буква и одна цифра.
  const checks = [
    { ok: password.length >= 8 && password.length <= 128, label: 'От 8 до 128 символов' },
    { ok: /[A-Za-zА-Яа-яЁё]/.test(password), label: 'Хотя бы одна буква' },
    { ok: /\d/.test(password), label: 'Хотя бы одна цифра' },
  ];

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setTried(true);
    const errs: typeof fe = {};
    if (!name.trim()) errs.name = 'Укажите имя и фамилию.';
    if (!email.trim()) errs.email = 'Введите email.';
    else if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email.trim())) errs.email = 'Email указан в неверном формате.';
    setFe(errs);
    if (errs.name || errs.email || checks.some((c) => !c.ok)) return;
    setBusy(true);
    try {
      await register(name.trim(), email.trim(), password);
      toast('success', 'Аккаунт создан', 'Вы уже вошли — можно бронировать.');
      after();
    } catch (err) {
      const ex = err as ApiError;
      if (ex.code === 'auth.email_taken') setFe({ email: 'Пользователь с таким email уже зарегистрирован.', dup: true });
      else setFe({ other: ex.detail ?? ex.message });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="page auth-page">
      <header className="bar"><BackButton fallback="/login" /></header>
      <form className="pad auth-box" onSubmit={submit} noValidate>
        <div><h1 className="h1m">Регистрация</h1><p className="rv-muted" style={{ margin: '6px 0 0' }}>После регистрации вы сразу войдёте в аккаунт.</p></div>
        {fe.other && <Banner icon={CircleX} tone="danger" role="alert" title={fe.other} />}
        <label className="rv-field">
          <span className="rv-label">Имя и фамилия</span>
          <span className="rv-control ctl-lg" data-invalid={fe.name ? 'true' : undefined}><input type="text" autoComplete="name" placeholder="Анна Смирнова" value={name} onChange={(e) => setName(e.target.value)} /></span>
          {fe.name && <span className="rv-error-text"><CircleAlert className="rv-ic rv-ic-sm" aria-hidden />{fe.name}</span>}
        </label>
        <label className="rv-field">
          <span className="rv-label">Email</span>
          <span className="rv-control ctl-lg" data-invalid={fe.email ? 'true' : undefined}><input type="email" inputMode="email" autoComplete="email" placeholder="name@univ.ru" value={email} onChange={(e) => setEmail(e.target.value)} /></span>
          {fe.email && <span className="rv-error-text"><CircleAlert className="rv-ic rv-ic-sm" aria-hidden />{fe.email}</span>}
          {fe.dup && <button type="button" className="lnk" style={{ alignSelf: 'flex-start' }} onClick={() => navigate('/login', { state: { ...state, email } })}>Войти с этим email</button>}
        </label>
        <label className="rv-field">
          <span className="rv-label">Пароль</span>
          <span className="rv-control ctl-lg" data-invalid={tried && checks.some((c) => !c.ok) ? 'true' : undefined}>
            <input type={show ? 'text' : 'password'} autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
            <button type="button" className="rv-adorn" aria-label={show ? 'Скрыть пароль' : 'Показать пароль'} onClick={() => setShow(!show)}>{show ? <EyeOff className="rv-ic" aria-hidden /> : <Eye className="rv-ic" aria-hidden />}</button>
          </span>
          <ul className="pwlist">
            {checks.map((c) => (
              <li key={c.label} data-ok={c.ok ? '' : undefined} style={!c.ok && tried ? { color: 'var(--danger)' } : undefined}>
                {c.ok ? <Check className="rv-ic rv-ic-sm" aria-hidden /> : <X className="rv-ic rv-ic-sm" aria-hidden />}{c.label}
              </li>
            ))}
          </ul>
        </label>
        <button type="submit" className="rv-btn rv-btn-primary rv-btn-lg rv-btn-block" aria-busy={busy}>Создать аккаунт</button>
        <p className="rv-muted" style={{ margin: 0, textAlign: 'center' }}>Уже есть аккаунт? <Link to="/login" state={state} className="lnk">Войти</Link></p>
      </form>
    </div>
  );
}
