using Scalerize.InfiniteGpu.Desktop.Constants;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    /// <summary>
    /// Provides a persistent, deterministic device identifier for the desktop application instance.
    /// The identifier is generated once and stored on disk to ensure stability across restarts.
    /// </summary>
    public sealed class DeviceIdentifierService
    {
        private const string DeviceIdFileName = "device-id.txt";
        private static readonly Encoding FileEncoding = new UTF8Encoding(false);
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly string _storagePath;
        private string? _cachedIdentifier;

        public DeviceIdentifierService()
        {
            // Use standard .NET path for unpackaged Windows apps
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(localAppData, Constants.Constants.AppName);
            var root = Path.Combine(appFolder, "Device");

            Directory.CreateDirectory(root);
            _storagePath = Path.Combine(root, DeviceIdFileName);
        }

        /// <summary>
        /// Returns the persisted device identifier, generating and storing a new one if necessary.
        /// </summary>
        public async Task<string> GetOrCreateIdentifierAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(_cachedIdentifier))
            {
                return _cachedIdentifier!;
            }

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!string.IsNullOrWhiteSpace(_cachedIdentifier))
                {
                    return _cachedIdentifier!;
                }

                if (File.Exists(_storagePath))
                {
                    var existing = await File.ReadAllTextAsync(_storagePath, FileEncoding, cancellationToken).ConfigureAwait(false);
                    var normalized = existing.Trim();
                    if (Guid.TryParse(normalized, out _))
                    {
                        _cachedIdentifier = normalized;
                        return normalized;
                    }
                }

                var identifier = Guid.NewGuid().ToString("D");
                await File.WriteAllTextAsync(_storagePath, identifier, FileEncoding, cancellationToken).ConfigureAwait(false);
                _cachedIdentifier = identifier;
                return identifier;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}