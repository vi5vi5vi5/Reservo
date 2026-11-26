import type { AuthResponse, Problem } from './types';

/**
 * HTTP-клиент API. Хранит пару токенов, подставляет access-токен в каждый запрос,
 * а получив 401 — один раз обменивает refresh-токен на новую пару и повторяет запрос.
 * Несколько запросов, упавших одновременно, ждут одного и того же обмена.
 */

const STORAGE_KEY = 'reservo.session';

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly problem: Problem;

  constructor(status: number, problem: Problem) {
    super(problem.title ?? 'Что-то пошло не так');
    this.status = status;
    this.code = problem.code ?? (status === 0 ? 'network' : 'unknown');
    this.problem = problem;
  }

  /** Подробности для человека: detail или первая ошибка поля. */
  get detail(): string | undefined {
    if (this.problem.detail) return this.problem.detail;
    const first = this.problem.errors && Object.values(this.problem.errors)[0];
    return first?.[0];
  }

  fieldError(field: string): string | undefined {
    const errors = this.problem.errors;
    if (!errors) return undefined;
    const key = Object.keys(errors).find((k) => k.toLowerCase() === field.toLowerCase());
    return key ? errors[key][0] : undefined;
  }
}

type Listener = (session: AuthResponse | null) => void;

let session: AuthResponse | null = load();
const listeners = new Set<Listener>();
let refreshing: Promise<AuthResponse | null> | null = null;

function load(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthResponse) : null;
  } catch {
    return null;
  }
}

export function getSession() {
  return session;
}

export function setSession(next: AuthResponse | null) {
  session = next;
  try {
    if (next) localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Хранилище может быть недоступно (приватный режим) — тогда сессия живёт до перезагрузки.
  }
  listeners.forEach((l) => l(next));
}

export function onSessionChange(listener: Listener) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** Обмен refresh-токена. Неудача означает, что сессия закончилась: пользователь выходит. */
async function refresh(): Promise<AuthResponse | null> {
  const current = session;
  if (!current) return null;

  refreshing ??= (async () => {
    try {
      const res = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: current.refreshToken }),
      });
      if (!res.ok) {
        setSession(null);
        return null;
      }
      const next = (await res.json()) as AuthResponse;
      setSession(next);
      return next;
    } catch {
      return session;
    } finally {
      refreshing = null;
    }
  })();

  return refreshing;
}

/** Действующий access-токен; если он вот-вот истечёт — сначала обновляем. Нужен и SignalR. */
export async function accessToken(): Promise<string | null> {
  if (!session) return null;
  const expiresIn = new Date(session.expiresAt).getTime() - Date.now();
  if (expiresIn < 30_000) {
    const next = await refresh();
    return next?.accessToken ?? null;
  }
  return session.accessToken;
}

export type Query = Record<string, string | number | boolean | null | undefined | (string | number)[]>;

/** Строка запроса: массивы повторяют параметр (categoryIds=1&categoryIds=2), пустые значения пропускаются. */
export function qs(query?: Query): string {
  if (!query) return '';
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) value.forEach((v) => params.append(key, String(v)));
    else params.append(key, String(value));
  }
  const s = params.toString();
  return s ? `?${s}` : '';
}

interface RequestOptions {
  method?: string;
  query?: Query;
  body?: unknown;
  signal?: AbortSignal;
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const send = async (token: string | null) => {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (options.body !== undefined) headers['Content-Type'] = 'application/json';
    if (token) headers.Authorization = `Bearer ${token}`;
    return fetch(`/api${path}${qs(options.query)}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      signal: options.signal,
    });
  };

  let res: Response;
  try {
    res = await send(await accessToken());
    if (res.status === 401 && session) {
      const next = await refresh();
      if (next) res = await send(next.accessToken);
    }
  } catch (e) {
    if ((e as Error).name === 'AbortError') throw e;
    throw new ApiError(0, { title: 'Нет соединения с сервером', code: 'network' });
  }

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const data = text ? JSON.parse(text) : undefined;

  if (!res.ok) {
    throw new ApiError(res.status, (data as Problem) ?? { title: res.statusText });
  }

  return data as T;
}
