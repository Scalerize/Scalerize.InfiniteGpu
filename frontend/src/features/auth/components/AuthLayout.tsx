import type { ReactNode } from "react";
import {  ShieldCheck, Sparkles } from "lucide-react";
import scalerize from "../../../assets/logo-blue.png";

interface AuthLayoutProps {
  title: string;
  subtitle: string;
  footerHint?: ReactNode;
  children: ReactNode;
}

export const AuthLayout = ({
  title,
  subtitle,
  footerHint,
  children,
}: AuthLayoutProps) => {
  return (
    <div className="grid min-h-screen w-full bg-slate-100 md:grid-cols-[1fr_minmax(420px,520px)] dark:bg-slate-950">
      <aside className="relative hidden bg-gradient-to-br from-slate-900 via-indigo-900 to-indigo-700 md:flex md:flex-col md:justify-between dark:from-slate-950 dark:via-indigo-950 dark:to-indigo-900">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_top,rgba(255,255,255,0.08),transparent_60%)]" />
        <div className="relative flex flex-1 flex-col justify-between gap-10 p-12">
          <header className="flex items-center gap-3 text-indigo-50">
            <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white backdrop-blur-sm">
              <img
                src={scalerize}
                alt="logo"
                className="h-10 w-10"
              />
            </div>
            <div>
              <span className="text-lg font-semibold tracking-wide">
                InfiniteGPU
              </span>
              <p className="text-indigo-200">Distributed compute marketplace</p>
            </div>
          </header>

          <section className="space-y-6 text-indigo-50">
            <div className="space-y-3">
              <h1 className="text-4xl font-semibold leading-tight">
                Provision, orchestrate, and monetise accelerated workloads
                globally.
              </h1>
              <p className="text-indigo-200">
                Join a resilient mesh of compute providers and requestors with
                real-time telemetry, deterministic payouts, and integrated
                orchestration pipelines.
              </p>
            </div>

            <div className="grid gap-4 text-sm text-indigo-100">
              <div className="flex items-start gap-3 rounded-xl border border-white/10 bg-white/5 p-4 backdrop-blur">
                <ShieldCheck className="mt-0.5 h-5 w-5 text-emerald-300" />
                <div>
                  <p className="font-semibold text-indigo-50">
                    Enterprise-grade security
                  </p>
                  <p className="text-indigo-200/90">
                    End-to-end signing, isolation per partition, and encrypted
                    state checkpoints.
                  </p>
                </div>
              </div>
              <div className="flex items-start gap-3 rounded-xl border border-white/10 bg-white/5 p-4 backdrop-blur">
                <Sparkles className="mt-0.5 h-5 w-5 text-sky-300" />
                <div>
                  <p className="font-semibold text-indigo-50">
                    Adaptive workload routing
                  </p>
                  <p className="text-indigo-200/90">
                    Intelligent schedulers match heterogeneous accelerators with
                    partitioned graph tasks.
                  </p>
                </div>
              </div>
            </div>
          </section>

          <footer className="text-sm text-indigo-200">
          </footer>
        </div>
      </aside>

      <main className="flex w-full flex-col items-center justify-center px-6 py-16 md:px-12">
        <div className="w-full max-w-md space-y-8">
          <div className="space-y-3 text-center">
            <h2 className="text-3xl font-semibold text-slate-900 dark:text-white">{title}</h2>
            <p className="text-sm text-slate-500 dark:text-slate-400">{subtitle}</p>
          </div>
          <div className="rounded-3xl border border-slate-200 bg-white p-8 shadow-xl shadow-indigo-100/40 ring-1 ring-indigo-50 dark:border-slate-700 dark:bg-slate-900 dark:shadow-indigo-950/40 dark:ring-indigo-950/50">
            {children}
          </div>
          {footerHint ? (
            <div className="text-center text-sm text-slate-500 dark:text-slate-400">
              {footerHint}
            </div>
          ) : null}
        </div>
      </main>
    </div>
  );
};
