import { UploadCloud } from 'lucide-react';
import {
  type ChangeEvent,
  type ReactNode,
  useEffect,
  useMemo,
  useState
} from 'react';

export interface FileDropzoneProps {
  /**
   * The id assigned to the underlying file input. Useful for associating external labels.
   */
  inputId: string;
  /**
   * Name attribute used during form submission.
   */
  name: string;
  /**
   * Native accept attribute for file filtering.
   */
  accept?: string;
  /**
   * Allows selecting multiple files if enabled.
   */
  multiple?: boolean;
  /**
   * Disables the dropzone and prevents selection when true.
   */
  disabled?: boolean;
  /**
   * File name provided by the parent component. If omitted, the component manages a local copy.
   */
  selectedFileName?: string | null;
  /**
   * Content rendered when no file has been selected.
   */
  emptyState: ReactNode;
  /**
   * Optional helper text displayed beneath the primary content.
   */
  helperText?: ReactNode;
  /**
   * Optional icon rendered above the text content. Defaults to UploadCloud.
   */
  icon?: ReactNode;
  /**
   * Additional classes merged onto the root dropzone element.
   */
  className?: string;
  /**
   * Optional aria-label forwarded to the input for accessibility.
   */
  inputAriaLabel?: string;
  /**
   * Callback fired when a file selection changes.
   */
  onFileSelect?: (file: File | null, event: ChangeEvent<HTMLInputElement>) => void;
}

/**
 * Shared dropzone-style file picker with consistent styling across the app.
 */
export const FileDropzone = ({
  inputId,
  name,
  accept,
  multiple,
  disabled,
  selectedFileName,
  emptyState,
  helperText,
  icon,
  className,
  inputAriaLabel,
  onFileSelect
}: FileDropzoneProps) => {
  const [internalFileName, setInternalFileName] = useState<string | null>(selectedFileName ?? null);

  useEffect(() => {
    setInternalFileName(selectedFileName ?? null);
  }, [selectedFileName]);

  const displayFileName = useMemo(
    () => selectedFileName ?? internalFileName,
    [selectedFileName, internalFileName]
  );

  const accessibilityProps = useMemo(
    () =>
      disabled
        ? ({
            tabIndex: -1,
            role: 'button',
            'aria-disabled': 'true'
          } as const)
        : ({
            tabIndex: 0,
            role: 'button'
          } as const),
    [disabled]
  );

  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    if (disabled) {
      return;
    }

    const file = event.target.files?.[0] ?? null;
    setInternalFileName(file?.name ?? null);
    onFileSelect?.(file, event);
  };

  const baseClasses =
    'group relative flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-slate-200 bg-slate-50 px-6 py-10 text-center transition hover:border-indigo-200 hover:bg-indigo-50/50 focus:outline-none focus:ring-2 focus:ring-indigo-200/60 focus:ring-offset-2 focus:ring-offset-white dark:border-slate-700 dark:bg-slate-800 dark:hover:border-indigo-700 dark:hover:bg-indigo-950/30 dark:focus:ring-indigo-900/60 dark:focus:ring-offset-slate-900';
  const disabledClasses = disabled
    ? 'cursor-not-allowed opacity-60 hover:border-slate-200 hover:bg-slate-50 dark:hover:border-slate-700 dark:hover:bg-slate-800'
    : 'cursor-pointer';

  return (
    <div
      {...accessibilityProps}
      className={`${baseClasses} ${disabledClasses}${className ? ` ${className}` : ''}`}
    >
      <div className="pointer-events-none flex flex-col items-center">
        <span className="mb-3 text-indigo-500 transition group-hover:text-indigo-600 dark:text-indigo-400 dark:group-hover:text-indigo-300">
          {icon ?? <UploadCloud className="h-8 w-8" />}
        </span>

        <div className="text-sm font-medium text-slate-700 dark:text-slate-200">
          {displayFileName ? (
            <span className="break-words">{displayFileName}</span>
          ) : (
            emptyState
          )}
        </div>

        {helperText ? (
          <p className="mt-1 text-xs text-slate-400 dark:text-slate-500">{helperText}</p>
        ) : null}
      </div>

      <input
        id={inputId}
        name={name}
        type="file"
        accept={accept}
        multiple={multiple}
        disabled={disabled}
        aria-label={inputAriaLabel}
        className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
        onChange={handleChange}
      />
    </div>
  );
};