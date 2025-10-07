using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    public sealed record HardwareMetricsSnapshot(CpuInfosDto Cpu, GpuInfosDto Gpu, MemoryInfosDto Memory, NpuInfosDto Npu, StorageInfosDto Storage, NetworkInfosDto Network);

    public record GpuInfosDto(
        string? Name,
        string? Vendor,
        double? VideoRamGb,
        int? Cores,
        double? FrequencyMhz,
        double? MemoryClockMhz);

    public record CpuInfosDto(int Cores, double FrequencyGhz);

    public record NetworkInfosDto(double? DownlinkMbps, double? LatencyMs);

    public record StorageInfosDto(double? FreeGb, double? TotalGb);

    public record MemoryInfosDto(double? TotalGb, double? AvailableGb);

    public record NpuInfosDto(string? Name, double? Tops, double? FrequencyMhz);

    public sealed class HardwareMetricsService
    {
        private static readonly string[] LatencyTargets = { "1.1.1.1", "8.8.8.8" };

        public Task<HardwareMetricsSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => CollectInternal(cancellationToken), cancellationToken);
        }

        private HardwareMetricsSnapshot CollectInternal(CancellationToken cancellationToken)
        {
            var cpu = GetCpuInfo();
            var gpu = GetGpuInfo();
            var npu = GetNpuInfo();
            var memory = GetMemoryInfo();
            var storage = GetStorageInfo();
            var network = GetNetworkInfo(cancellationToken);

            return new HardwareMetricsSnapshot(cpu, gpu, memory, npu, storage, network);
        }

        public CpuInfosDto GetCpuInfo()
        {
            using var searcher =
                new ManagementObjectSearcher("SELECT NumberOfCores, MaxClockSpeed FROM Win32_Processor");
            using var results = searcher.Get();

            var totalCores = 0;
            double maxClockMhz = 0;

            foreach (var obj in results.Cast<ManagementObject>())
            {
                totalCores += Convert.ToInt32(obj["NumberOfCores"]);
                maxClockMhz = Math.Max(maxClockMhz, Convert.ToInt32(obj["MaxClockSpeed"]));
            }

            if (totalCores <= 0)
            {
                totalCores = Environment.ProcessorCount;
            }

            double? frequencyGhz = maxClockMhz / 1000;

            return new(totalCores > 0 ? totalCores : Environment.ProcessorCount, frequencyGhz.GetValueOrDefault());
        }

        public GpuInfosDto GetGpuInfo()
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            using var results = searcher.Get();

            var gpu = results.Cast<ManagementObject>().FirstOrDefault();
            if (gpu is null)
            {
                return null;
            }

            var name = gpu["Name"] as string;
            var vendor = gpu["AdapterCompatibility"] as string;
            var videoRam = Convert.ToDouble(gpu["AdapterRAM"]);
            double? ToGb(double? b) => b.HasValue ? b.Value / 1024d / 1024d / 1024d : null;

            int? cores = null;
            var videoProcessor = gpu["VideoProcessor"] as string;

            // Try to get current clock speeds
            double? frequencyMhz = null;
            double? memoryClockMhz = null;

            // Some drivers expose clock speeds through WMI
            frequencyMhz = Convert.ToDouble(gpu["CurrentClockSpeed"]);

            return new(name, vendor, ToGb(videoRam), cores, frequencyMhz, memoryClockMhz);
        }

        public NpuInfosDto GetNpuInfo()
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'ComputeAccelerator'");
            using var results = searcher.Get();

            foreach (var device in results.Cast<ManagementObject>())
            {
                var name = device["Name"] as string;
                var pnpId = device["PNPDeviceID"] as string;

                if (!string.IsNullOrEmpty(name))
                {
                    // Try to extract TOPS from device name
                    var topsMatch = System.Text.RegularExpressions.Regex.Match(
                        name, @"(\d+(?:\.\d+)?)\s*TOPS",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    double? tops = null;
                    if (topsMatch.Success && double.TryParse(topsMatch.Groups[1].Value, out var topsValue))
                    {
                        tops = topsValue;
                    }

                    // Frequency info is typically not available via WMI for NPUs
                    return new(name, tops, null);
                }
            }

            return null;
        }

        public MemoryInfosDto GetMemoryInfo()
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            using var results = searcher.Get();

            var os = results.Cast<ManagementObject>().FirstOrDefault();
            if (os is null)
            {
                return null;
            }

            var totalKb = Convert.ToDouble(os["TotalVisibleMemorySize"]);
            var freeKb = Convert.ToDouble(os["FreePhysicalMemory"]);

            double? ToGb(double? kb) => kb.HasValue ? kb.Value / 1024d / 1024d : null;

            return new(ToGb(totalKb), ToGb(freeKb));
        }

        public StorageInfosDto GetStorageInfo()
        {
            var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ??
                             string.Empty;
            var drives = DriveInfo.GetDrives().Where(drive => drive.IsReady).ToArray();

            var systemDrive = drives.FirstOrDefault(drive =>
                string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase));

            var target = systemDrive ?? drives.FirstOrDefault();
            if (target is null)
            {
                return null;
            }

            double ToGb(long bytes) => bytes / 1024d / 1024d / 1024d;

            return new(ToGb(target.AvailableFreeSpace), ToGb(target.TotalSize));
        }

        public NetworkInfosDto GetNetworkInfo(CancellationToken cancellationToken)
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

            return new(downlink, latency);
        }
    }
}
