import { type ReactNode } from "react";
import { X } from "lucide-react";

interface DialogShellProps {
  open: boolean;
  onDismiss: () => void;
  closeLabel: string;
  children: ReactNode;
  title?: ReactNode;
  badgeIcon?: ReactNode;
  badgeLabel?: ReactNode;
  helperText?: ReactNode;
  helperId?: string;
  overlayClassName?: string;
  containerClassName?: string;
  closeButtonClassName?: string;
  contentWrapperClassName?: string;
  closeIcon?: ReactNode;
}

const baseOverlayClassName =
  "fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur dark:bg-slate-950/80";
const baseContainerClassName =
  "relative w-full max-w-3xl overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xl dark:border-slate-700 dark:bg-slate-900";
const baseCloseButtonClassName =
  "absolute right-4 top-4 rounded-full border border-slate-200 p-2 text-slate-500 transition hover:bg-slate-50 hover:text-slate-700 dark:border-slate-700 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-200";
const baseContentWrapperClassName = "space-y-8 p-8";

const mergeClassNames = (base: string, extra?: string) =>
  extra ? `${base} ${extra}` : base;

export const DialogShell = ({
  open,
  onDismiss,
  closeLabel,
  children,
  title,
  badgeIcon,
  badgeLabel,
  helperText,
  helperId,
  overlayClassName,
  containerClassName,
  closeButtonClassName,
  contentWrapperClassName,
  closeIcon,
}: DialogShellProps) => {
  if (!open) {
    return null;
  }

  const shouldRenderHeader = Boolean(badgeIcon || badgeLabel || title || helperText);

  return (
    <div className={mergeClassNames(baseOverlayClassName, overlayClassName)}>
      <div className={mergeClassNames(baseContainerClassName, containerClassName)}>
        <button
          type="button"
          className={mergeClassNames(baseCloseButtonClassName, closeButtonClassName)}
          onClick={onDismiss}
          aria-label={closeLabel}
        >
          {closeIcon ?? <X className="h-4 w-4" />}
        </button>
        <div className="max-h-[calc(100vh-2rem)] overflow-y-auto">
          <div
            className={mergeClassNames(
              baseContentWrapperClassName,
              contentWrapperClassName
            )}
          >
            {shouldRenderHeader && (
              <header className="flex flex-col gap-2">
                {(badgeIcon || badgeLabel) && (
                  <span className="inline-flex items-center gap-2 self-start rounded-full border border-indigo-100 bg-indigo-50 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-indigo-600 dark:border-indigo-900/50 dark:bg-indigo-950/50 dark:text-indigo-400">
                    {badgeIcon}
                    {badgeLabel}
                  </span>
                )}
                {title && (
                  <h2 className="text-2xl font-semibold text-slate-900 dark:text-white">{title}</h2>
                )}
                {helperText && (
                  <p id={helperId} className="text-sm text-slate-500 dark:text-slate-400">
                    {helperText}
                  </p>
                )}
              </header>
            )}
            {children}
          </div>
        </div>
      </div>
    </div>
  );
};