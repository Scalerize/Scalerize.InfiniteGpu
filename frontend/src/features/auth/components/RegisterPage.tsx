import { type FormEvent, useMemo, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { register, type RegisterRequest } from '../api';
import { AuthLayout } from './AuthLayout';
import { parseJwt } from '../../../shared/utils/jwt';
import { useAuthStore, type UserRole } from '../stores/authStore';

interface RegisterFormState {
  userName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

const DEFAULT_FORM: RegisterFormState = {
  userName: '',
  email: '',
  password: '',
  confirmPassword: ''
};

const resolveRole = (rawRole: unknown): UserRole => {
  if (rawRole === 'Provider' || rawRole === 'Admin') {
    return rawRole;
  }
  return 'Requestor';
};

const resolveEmail = (payload: Record<string, unknown>) => {
  const directEmail = payload.email;
  if (typeof directEmail === 'string' && directEmail.length > 0) {
    return directEmail;
  }

  const claimEmail = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'];
  if (typeof claimEmail === 'string' && claimEmail.length > 0) {
    return claimEmail;
  }

  const jwtEmail = payload['email'];
  return typeof jwtEmail === 'string' && jwtEmail.length > 0 ? jwtEmail : undefined;
};

const buildUserFromToken = (token: string) => {
  const payload = parseJwt(token);
  if (!payload) {
    return null;
  }

  const candidateIds = [
    payload.sub,
    (payload as Record<string, unknown>)?.nameid,
    (payload as Record<string, unknown>)['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
  ].filter((value) => typeof value === 'string' && value.length > 0) as string[];

  const userId = candidateIds[0];
  if (!userId) {
    return null;
  }

  const rawRole =
    Array.isArray(payload.role) && payload.role.length > 0
      ? payload.role[0]
      : (payload as Record<string, unknown>)['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
        payload.role;

  return {
    id: userId,
    email: resolveEmail(payload as Record<string, unknown>),
    role: resolveRole(rawRole)
  };
};

export const RegisterPage = () => {
  const [form, setForm] = useState<RegisterFormState>(DEFAULT_FORM);
  const [error, setError] = useState<string | null>(null);

  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const setAuth = useAuthStore((state) => state.setAuth);

  const mutation = useMutation({
    mutationFn: (payload: RegisterRequest) => register(payload),
    onSuccess: (token) => {
      const mappedUser = buildUserFromToken(token);
      if (!mappedUser) {
        throw new Error('Failed to resolve authenticated user from token payload.');
      }
      setAuth(mappedUser, token);
      navigate('/', { replace: true });
    },
    onError: (mutationError: unknown) => {
      const message =
        mutationError instanceof Error
          ? mutationError.message
          : 'Unable to create account. Please try again.';
      setError(message);
    }
  });

  const isDisabled = mutation.isPending;

  const submitHandler = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    if (!form.userName.trim()) {
      setError('A username is required.');
      return;
    }

    if (!form.email.trim()) {
      setError('An email address is required.');
      return;
    }

    if (!form.password || !form.confirmPassword) {
      setError('Both password fields are required.');
      return;
    }

    if (form.password !== form.confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    mutation.mutate({
      userName: form.userName.trim(),
      email: form.email.trim().toLowerCase(),
      password: form.password
    });
  };

  const footerHint = useMemo(
    () => (
      <>
        Already have access?{' '}
        <Link to="/auth/login" className="font-semibold text-indigo-600 hover:text-indigo-500 dark:text-indigo-400 dark:hover:text-indigo-300">
          Sign in here
        </Link>
      </>
    ),
    []
  );

  if (user) {
    return <Navigate to="/" replace />;
  }

  return (
    <AuthLayout
      title="Create your InfiniteGPU account"
      subtitle="Provision your role-balanced workspace and join the global compute mesh."
      footerHint={footerHint}
    >
      <form className="space-y-6" onSubmit={submitHandler} noValidate>
        <div className="space-y-5">
          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-700 dark:text-slate-300" htmlFor="userName">
              Username
            </label>
            <input
              id="userName"
              type="text"
              value={form.userName}
              onChange={(event) =>
                setForm((prev) => ({
                  ...prev,
                  userName: event.target.value
                }))
              }
              className="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-indigo-400 focus:ring-2 focus:ring-indigo-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:border-indigo-600 dark:focus:ring-indigo-900/60"
              placeholder="johndoe"
              autoComplete="name"
              disabled={isDisabled}
              required
            />
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium text-slate-700 dark:text-slate-300" htmlFor="email">
              Email address
            </label>
            <input
              id="email"
              type="email"
              value={form.email}
              onChange={(event) =>
                setForm((prev) => ({
                  ...prev,
                  email: event.target.value
                }))
              }
              className="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-indigo-400 focus:ring-2 focus:ring-indigo-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:border-indigo-600 dark:focus:ring-indigo-900/60"
              placeholder="you@example.com"
              autoComplete="email"
              disabled={isDisabled}
              required
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300" htmlFor="password">
                Password
              </label>
              <input
                id="password"
                type="password"
                value={form.password}
                onChange={(event) =>
                  setForm((prev) => ({
                    ...prev,
                    password: event.target.value
                  }))
                }
                className="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-indigo-400 focus:ring-2 focus:ring-indigo-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:border-indigo-600 dark:focus:ring-indigo-900/60"
                placeholder="••••••••"
                autoComplete="new-password"
                disabled={isDisabled}
                required
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-700 dark:text-slate-300" htmlFor="confirmPassword">
                Confirm password
              </label>
              <input
                id="confirmPassword"
                type="password"
                value={form.confirmPassword}
                onChange={(event) =>
                  setForm((prev) => ({
                    ...prev,
                    confirmPassword: event.target.value
                  }))
                }
                className="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-indigo-400 focus:ring-2 focus:ring-indigo-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:border-indigo-600 dark:focus:ring-indigo-900/60"
                placeholder="••••••••"
                autoComplete="new-password"
                disabled={isDisabled}
                required
              />
            </div>
          </div>
        </div>

        {error ? (
          <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700 dark:border-rose-900/50 dark:bg-rose-950/50 dark:text-rose-400">
            {error}
          </div>
        ) : null}

        <button
          type="submit"
          className="inline-flex w-full items-center justify-center rounded-xl bg-indigo-600 px-4 py-3 text-sm font-semibold text-white shadow-lg shadow-indigo-500/30 transition hover:bg-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-200 focus:ring-offset-2 dark:shadow-indigo-950/50 dark:focus:ring-indigo-900 dark:focus:ring-offset-slate-900"
          disabled={isDisabled}
        >
          {mutation.isPending ? 'Creating account…' : 'Create account'}
        </button>
      </form>
    </AuthLayout>
  );
};