import { type ReactNode } from "react";

interface DataTableColumn<T> {
  key: string;
  header: string;
  render: (item: T) => ReactNode;
  headerClassName?: string;
  cellClassName?: string;
}

interface DataTableProps<T> {
  data: T[];
  columns: DataTableColumn<T>[];
  keyExtractor: (item: T) => string;
  isLoading?: boolean;
  isError?: boolean;
  emptyMessage?: string;
  errorMessage?: string;
  rowClassName?: string | ((item: T) => string);
  skeletonRows?: number;
}

export function DataTable<T>({
  data,
  columns,
  keyExtractor,
  isLoading = false,
  isError = false,
  emptyMessage = "No data available",
  errorMessage = "Unable to load data. Please retry shortly.",
  rowClassName,
  skeletonRows = 3,
}: DataTableProps<T>) {
  const renderTableBody = () => {
    if (isLoading) {
      return (
        <tbody className="divide-y divide-slate-100 bg-white text-sm text-slate-600 dark:divide-slate-700 dark:bg-slate-900 dark:text-slate-300">
          {Array.from({ length: skeletonRows }).map((_, row) => (
            <tr key={`skeleton-${row}`} className="animate-pulse">
              <td className="px-6 py-4" colSpan={columns.length}>
                <div className="space-y-3">
                  <div className="h-4 w-3/4 rounded bg-slate-200 dark:bg-slate-700" />
                  <div className="h-4 w-1/2 rounded bg-slate-200 dark:bg-slate-700" />
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      );
    }

    if (isError) {
      return (
        <tbody className="bg-white text-sm text-slate-600 dark:bg-slate-900 dark:text-slate-300">
          <tr>
            <td colSpan={columns.length} className="px-6 py-6 text-center text-rose-600 dark:text-rose-400">
              {errorMessage}
            </td>
          </tr>
        </tbody>
      );
    }

    if (data.length === 0) {
      return (
        <tbody className="bg-white text-sm text-slate-600 dark:bg-slate-900 dark:text-slate-300">
          <tr>
            <td colSpan={columns.length} className="px-6 py-6 text-center text-slate-500 dark:text-slate-400">
              {emptyMessage}
            </td>
          </tr>
        </tbody>
      );
    }

    return (
      <tbody className="divide-y divide-slate-100 bg-white text-sm text-slate-600 dark:divide-slate-700 dark:bg-slate-900 dark:text-slate-300">
        {data.map((item) => {
          const rowClass = typeof rowClassName === "function"
            ? rowClassName(item)
            : rowClassName || "transition hover:bg-indigo-50/50 dark:hover:bg-indigo-950/30";
          
          return (
            <tr key={keyExtractor(item)} className={rowClass}>
              {columns.map((column) => (
                <td
                  key={column.key}
                  className={column.cellClassName || "px-6 py-4 align-top"}
                >
                  {column.render(item)}
                </td>
              ))}
            </tr>
          );
        })}
      </tbody>
    );
  };

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-slate-100 text-left dark:divide-slate-700">
        <thead className="bg-slate-50 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:bg-slate-800 dark:text-slate-400">
          <tr>
            {columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                className={column.headerClassName || "whitespace-nowrap px-6 py-3"}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        {renderTableBody()}
      </table>
    </div>
  );
}