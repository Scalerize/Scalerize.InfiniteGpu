import { useEffect, useMemo, useState } from "react";
import { Activity, Cpu, HardDrive, MemoryStick, Wifi } from "lucide-react";
import {
  DesktopBridge,
  type HardwareMetrics,
} from "../../../shared/services/DesktopBridge";

const formatOneDecimal = new Intl.NumberFormat(undefined, {
  maximumFractionDigits: 1,
});
const formatTwoDecimals = new Intl.NumberFormat(undefined, {
  maximumFractionDigits: 2,
});
const formatZeroDecimals = new Intl.NumberFormat(undefined, {
  maximumFractionDigits: 0,
});

const detectBridgeAvailability = () => {
  if (typeof window === "undefined") {
    return false;
  }
  return DesktopBridge.isAvailable();
};

const resolveFallbackValue = (loading: boolean, error: string | null) => {
  if (loading) {
    return "Collecting…";
  }
  if (error) {
    return "Unavailable";
  }
  return "Not provided";
};

const CardStat = ({
  icon: Icon,
  label,
  value,
  helper,
}: {
  icon: typeof Cpu;
  label: string;
  value: string;
  helper?: string;
}) => (
  <div className="flex h-full flex-col gap-3 rounded-lg border border-slate-100 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-800">
    <div className="flex items-center gap-3">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-indigo-500/10 text-indigo-600 dark:bg-indigo-950/50 dark:text-indigo-400">
        <Icon className="h-5 w-5" />
      </div>
      <div className="space-y-1 overflow-hidden">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-slate-500">
          {label}
        </p>
        <p className="truncate text-lg font-semibold text-slate-900 dark:text-slate-100">{value}</p>
        {helper ? (
          <p className="truncate text-sm text-slate-500 dark:text-slate-400">{helper}</p>
        ) : null}
      </div>
    </div>
  </div>
);

export const ProviderThroughputCard = () => {
  const [metrics, setMetrics] = useState<HardwareMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const available = detectBridgeAvailability();
    if (!available) {
      setMetrics(null);
      setLoading(false);
      return () => {
        cancelled = true;
      };
    }

    const fetchMetrics = async () => {
      try {
        const snapshot = await DesktopBridge.getHardwareMetrics();
        if (cancelled) {
          return;
        }
        setMetrics(snapshot);
        setError(null);
      } catch (err) {
        if (cancelled) {
          return;
        }
        // eslint-disable-next-line no-console
        console.error(
          "Failed to fetch hardware metrics from desktop runtime.",
          err
        );
        setMetrics(null);
        setError("Failed to retrieve hardware metrics.");
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void fetchMetrics();

    return () => {
      cancelled = true;
    };
  }, []);

  const stats = useMemo(() => {
    const fallback = resolveFallbackValue(loading, error);

    const cpuValue =
      metrics?.cpuCores != null ? `${metrics.cpuCores} cores` : fallback;
    const cpuHelper =
      metrics?.cpuFrequencyGhz != null
        ? `${formatTwoDecimals.format(metrics.cpuFrequencyGhz)} GHz clock`
        : undefined;

    const gpuValue = metrics?.gpuName ?? fallback;
    const gpuHelperParts: string[] = [];
    if (metrics?.videoMemoryAvailable != null) {
      gpuHelperParts.push(
        `${formatZeroDecimals.format(metrics.videoMemoryAvailable)} GB Video RAM`
      );
    }
    const gpuHelper =
      gpuHelperParts.length > 0 ? gpuHelperParts.join(" • ") : undefined;

    const memoryValue =
      metrics?.memoryAvailableGb != null
        ? `${formatOneDecimal.format(metrics.memoryAvailableGb)} GB free`
        : metrics?.memoryTotalGb != null
        ? `${formatOneDecimal.format(metrics.memoryTotalGb)} GB total`
        : fallback;
    const memoryHelper =
      metrics?.memoryAvailableGb != null && metrics?.memoryTotalGb != null
        ? `Total ${formatOneDecimal.format(metrics.memoryTotalGb)} GB`
        : undefined;

    const networkValue =
      metrics?.networkDownlinkMbps != null
        ? `${formatOneDecimal.format(
            metrics.networkDownlinkMbps
          )} Mbps`
        : fallback;
    const networkHelper =
      metrics?.networkLatencyMs != null
        ? `${formatZeroDecimals.format(metrics.networkLatencyMs)} ms ping`
        : undefined;

    const storageValue =
      metrics?.storageFreeGb != null
        ? `${formatOneDecimal.format(metrics.storageFreeGb)} GB free`
        : fallback;
    const storageHelper =
      metrics?.storageTotalGb != null
        ? `Total ${formatOneDecimal.format(metrics.storageTotalGb)} GB`
        : undefined;

    return [
      { icon: Cpu, label: "CPU", value: cpuValue, helper: cpuHelper },
      { icon: Activity, label: "GPU", value: gpuValue, helper: gpuHelper },
      {
        icon: MemoryStick,
        label: "Memory",
        value: memoryValue,
        helper: memoryHelper,
      },
      {
        icon: Wifi,
        label: "Network",
        value: networkValue,
        helper: networkHelper,
      },
      {
        icon: HardDrive,
        label: "Storage",
        value: storageValue,
        helper: storageHelper,
      },
    ];
  }, [metrics, loading, error]);

  return (
    <div className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-100 dark:bg-slate-900 dark:ring-slate-800">
      <div className="bg-gradient-to-br from-indigo-500 via-indigo-600 to-indigo-700 px-6 py-5 text-indigo-50 dark:from-indigo-600 dark:via-indigo-700 dark:to-indigo-800">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <span className="text-xs font-semibold uppercase tracking-wide text-indigo-200">
              Provider throughput
            </span>
            <p className="mt-2 text-xl font-semibold text-white">
              0 tasks / hour
            </p>
            <p className="text-sm text-indigo-100">
              Computed from recent task history.
            </p>
          </div>
          <div className="flex gap-3">
          </div>
        </div>
      </div>

      <div className="space-y-6 p-6">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <span className="text-xs font-semibold uppercase tracking-wide text-indigo-500 dark:text-indigo-400">
              Machine configuration
            </span>
            <h4 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
              Compute node profile
            </h4>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              Overview of hardaware indicators
            </p>
          </div>
        </div>

        {error ? (
          <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/50 dark:text-red-400">
            {error}
          </div>
        ) : null}

        <div className="grid gap-4 sm:grid-cols-2">
          {stats.map((stat) => (
            <CardStat
              key={stat.label}
              icon={stat.icon}
              label={stat.label}
              value={stat.value}
              helper={stat.helper}
            />
          ))}
        </div>
      </div>
    </div>
  );
};
