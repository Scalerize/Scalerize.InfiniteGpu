using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    public sealed record HardwareMetricsSnapshot(
        int? CpuCores,
        double? CpuFrequencyGhz,
        double? GpuTotalRam,
        string? GpuName,
        string? GpuVendor,
        double? MemoryTotalGb,
        double? MemoryAvailableGb,
        double? NetworkDownlinkMbps,
        double? NetworkLatencyMs,
        double? StorageFreeGb,
        double? StorageTotalGb);

    public sealed class HardwareMetricsService
    {
        private static readonly string[] LatencyTargets = { "1.1.1.1", "8.8.8.8" };

        public Task<HardwareMetricsSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => CollectInternal(cancellationToken), cancellationToken);
        }

        private HardwareMetricsSnapshot CollectInternal(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (cpuCores, cpuFrequencyGhz) = GetCpuInfo();

            cancellationToken.ThrowIfCancellationRequested();
            var (gpuName, gpuVendor, gpuVideoRam) = GetGpuInfo();

            cancellationToken.ThrowIfCancellationRequested();
            var (memoryTotalGb, memoryAvailableGb) = GetMemoryInfo();

            cancellationToken.ThrowIfCancellationRequested();
            var (storageFreeGb, storageTotalGb) = GetStorageInfo();

            cancellationToken.ThrowIfCancellationRequested();
            var (networkDownlinkMbps, networkLatencyMs) = GetNetworkInfo(cancellationToken);

            return new HardwareMetricsSnapshot(
                cpuCores,
                cpuFrequencyGhz,
                gpuVideoRam,
                gpuName,
                gpuVendor,
                memoryTotalGb,
                memoryAvailableGb,
                networkDownlinkMbps,
                networkLatencyMs,
                storageFreeGb,
                storageTotalGb);
        }

        public (int? cores, double? frequencyGhz) GetCpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores, MaxClockSpeed FROM Win32_Processor");
                using var results = searcher.Get();

                var totalCores = 0;
                double? maxClockMhz = null;

                foreach (var obj in results.Cast<ManagementObject>())
                {
                    totalCores += ConvertToInt(obj["NumberOfCores"]) ?? 0;

                    var maxClockCandidate = ConvertToDouble(obj["MaxClockSpeed"]);
                    if (maxClockCandidate.HasValue)
                    {
                        maxClockMhz = maxClockMhz.HasValue
                            ? Math.Max(maxClockMhz.Value, maxClockCandidate.Value)
                            : maxClockCandidate;
                    }
                }

                if (totalCores <= 0)
                {
                    totalCores = Environment.ProcessorCount;
                }

                double? frequencyGhz = maxClockMhz.HasValue && maxClockMhz.Value > 0
                    ? maxClockMhz.Value / 1000d
                    : null;

                return (totalCores > 0 ? totalCores : null, frequencyGhz);
            }
            catch
            {
                return (Environment.ProcessorCount, null);
            }
        }

        public (string? name, string? vendor, double? frequencyMhz) GetGpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterCompatibility, DriverVersion, AdapterRAM, VideoProcessor FROM Win32_VideoController");
                using var results = searcher.Get();

                var gpu = results.Cast<ManagementObject>().FirstOrDefault();
                if (gpu is null)
                {
                    return (null, null, null);
                }

                var name = gpu["Name"] as string;
                var vendor = gpu["AdapterCompatibility"] as string;

                var videoRam = ConvertToDouble(gpu["AdapterRAM"]);
                if (!videoRam.HasValue || videoRam.Value <= 0)
                {
                    videoRam = null;
                }
                double? ToGb(double? b) => b.HasValue ? b.Value / 1024d / 1024d / 1024d : null;

                return (name, vendor, ToGb(videoRam));
            }
            catch
            {
                return (null, null, null);
            }
        }

        public (double? totalGb, double? availableGb) GetMemoryInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                using var results = searcher.Get();

                var os = results.Cast<ManagementObject>().FirstOrDefault();
                if (os is null)
                {
                    return (null, null);
                }

                var totalKb = ConvertToDouble(os["TotalVisibleMemorySize"]);
                var freeKb = ConvertToDouble(os["FreePhysicalMemory"]);

                double? ToGb(double? kb) => kb.HasValue ? kb.Value / 1024d / 1024d : null;

                return (ToGb(totalKb), ToGb(freeKb));
            }
            catch
            {
                return (null, null);
            }
        }

        public (double? freeGb, double? totalGb) GetStorageInfo()
        {
            try
            {
                var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? string.Empty;
                var drives = DriveInfo.GetDrives().Where(drive => drive.IsReady).ToArray();

                var systemDrive = drives.FirstOrDefault(drive =>
                    string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase));

                var target = systemDrive ?? drives.FirstOrDefault();
                if (target is null)
                {
                    return (null, null);
                }

                double ToGb(long bytes) => bytes / 1024d / 1024d / 1024d;

                return (ToGb(target.AvailableFreeSpace), ToGb(target.TotalSize));
            }
            catch
            {
                return (null, null);
            }
        }

        public (double? downlinkMbps, double? latencyMs) GetNetworkInfo(CancellationToken cancellationToken)
        {
            double? downlink = null;
            try
            {
                var candidate = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni =>
                        ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        ni.Speed > 0)
                    .OrderByDescending(ni => ni.Speed)
                    .FirstOrDefault();

                if (candidate is not null && candidate.Speed > 0)
                {
                    downlink = candidate.Speed / 1_000_000d;
                }
            }
            catch
            {
                downlink = null;
            }

            double? latency = null;
            foreach (var host in LatencyTargets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var ping = new Ping();
                    var reply = ping.Send(host, 1200);
                    if (reply is { Status: IPStatus.Success })
                    {
                        latency = reply.RoundtripTime;
                        break;
                    }
                }
                catch
                {
                    // Continue to next host.
                }
            }

            return (downlink, latency);
        }

        private static int? ConvertToInt(object? value)
        {
            try
            {
                return value is null ? null : Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
        }

        private static double? ConvertToDouble(object? value)
        {
            try
            {
                return value is null ? null : Convert.ToDouble(value);
            }
            catch
            {
                return null;
            }
        }
    }
}