using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scalerize.InfiniteGpu.Desktop.Parsers
{
    /// <summary>
    /// Represents the index metadata for multi-file safetensors models
    /// </summary>
    public sealed class SafetensorsIndex
    {
        [JsonPropertyName("metadata")]
        public SafetensorsIndexMetadata? Metadata { get; set; }

        [JsonPropertyName("weight_map")]
        public Dictionary<string, string> WeightMap { get; set; } = new();
    }

    /// <summary>
    /// Metadata information in the safetensors index
    /// </summary>
    public sealed class SafetensorsIndexMetadata
    {
        [JsonPropertyName("total_size")]
        public long? TotalSize { get; set; }
    }

    /// <summary>
    /// Represents information about a tensor in a SafeTensor file
    /// </summary>
    public sealed record TensorInfo(string Name, string DType, long[] Shape, long Start, long End, string? SourceFile = null)
    {
        public long ElementCount => checked(Shape.Aggregate(1L, (a, b) => a * b));
        public long ByteLength => End - Start;
    }

    /// <summary>
    /// Represents an opened SafeTensor file (or multiple files) with methods to access its contents
    /// </summary>
    public sealed class SafeTensor : IDisposable
    {
        // For single-file mode
        private readonly FileStream? _fs;
        private readonly MemoryMappedFile? _mmf;
        
        // For multi-file mode
        private readonly Dictionary<string, (FileStream fs, MemoryMappedFile mmf)>? _multiFiles;
        
        private readonly Dictionary<string, TensorInfo> _tensors;
        private readonly bool _isMultiFile;

        public IReadOnlyDictionary<string, TensorInfo> Tensors => _tensors;
        public bool IsMultiFile => _isMultiFile;

        // Constructor for single-file mode
        internal SafeTensor(FileStream fs, MemoryMappedFile mmf, Dictionary<string, TensorInfo> tensors)
        {
            _fs = fs;
            _mmf = mmf;
            _tensors = tensors;
            _isMultiFile = false;
        }

        // Constructor for multi-file mode
        internal SafeTensor(Dictionary<string, (FileStream fs, MemoryMappedFile mmf)> multiFiles, Dictionary<string, TensorInfo> tensors)
        {
            _multiFiles = multiFiles;
            _tensors = tensors;
            _isMultiFile = true;
        }

        /// <summary>
        /// Gets a list of all tensor names in the file
        /// </summary>
        /// <returns>Ordered list of tensor names</returns>
        public IEnumerable<string> ListTensorNames()
        {
            return _tensors.Keys.OrderBy(n => n);
        }

        /// <summary>
        /// Gets information about a specific tensor
        /// </summary>
        /// <param name="name">Name of the tensor</param>
        /// <returns>Tensor information</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the tensor is not found</exception>
        public TensorInfo GetInfo(string name)
        {
            return _tensors.TryGetValue(name, out var info) ? info :
                throw new KeyNotFoundException($"Tensor '{name}' not found.");
        }

        /// <summary>
        /// Reads a tensor into a managed array of type T
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read the tensor as</typeparam>
        /// <param name="name">Name of the tensor</param>
        /// <returns>Array containing the tensor data</returns>
        public T[] ReadTensor<T>(string name) where T : unmanaged
        {
            var info = GetInfo(name);
            int sizeofT = Unsafe.SizeOf<T>();
            if (info.ByteLength % sizeofT != 0)
            {
                throw new InvalidDataException($"Byte size of '{name}' is not a multiple of sizeof({typeof(T).Name}).");
            }

            long elementCount = info.ByteLength / sizeofT;
            
            // Optional: check dtype vs T
            EnsureDTypeMatches<T>(info);

            if (elementCount > int.MaxValue)
            {
                throw new NotSupportedException("Tensor too large for single managed array.");
            }

            T[] result = new T[(int)elementCount];

            // Get the appropriate memory-mapped file
            MemoryMappedFile mmf = GetMemoryMappedFileForTensor(info);

            using var view = mmf.CreateViewAccessor(info.Start, info.ByteLength, MemoryMappedFileAccess.Read);
            unsafe
            {
                byte* ptr = null;
                try
                {
                    view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                    var src = new ReadOnlySpan<byte>(ptr + view.PointerOffset, (int)info.ByteLength);
                    var dst = MemoryMarshal.Cast<byte, T>(src);
                    dst.CopyTo(result);
                }
                finally
                {
                    if (ptr != null)
                    {
                        view.SafeMemoryMappedViewHandle.ReleasePointer();
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the memory-mapped file for a specific tensor (handles both single and multi-file modes)
        /// </summary>
        private MemoryMappedFile GetMemoryMappedFileForTensor(TensorInfo info)
        {
            if (_isMultiFile)
            {
                if (info.SourceFile == null)
                {
                    throw new InvalidOperationException($"Tensor '{info.Name}' has no source file specified.");
                }

                if (_multiFiles == null || !_multiFiles.TryGetValue(info.SourceFile, out var filePair))
                {
                    throw new InvalidOperationException($"Source file '{info.SourceFile}' not found for tensor '{info.Name}'.");
                }

                return filePair.mmf;
            }
            else
            {
                if (_mmf == null)
                {
                    throw new InvalidOperationException("Memory-mapped file is not available.");
                }
                return _mmf;
            }
        }

        /// <summary>
        /// Convenience method: returns shape and typed data for a tensor
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read the tensor as</typeparam>
        /// <param name="name">Name of the tensor</param>
        /// <returns>Tuple containing the shape and data</returns>
        public (long[] shape, T[] data) ReadTensorData<T>(string name) where T : unmanaged
        {
            var info = GetInfo(name);
            var data = ReadTensor<T>(name);

            // Optional sanity check: element count matches shape
            long countFromShape = info.ElementCount;
            if (countFromShape != data.LongLength)
            {
                throw new InvalidDataException($"Shape mismatch for '{name}': header={countFromShape}, bytes={data.LongLength}.");
            }
            return (info.Shape, data);
        }

        /// <summary>
        /// Maps dtype string to .NET type and size
        /// </summary>
        private static (Type t, int size) MapDType(string dtype) => dtype switch
        {
            "F64" => (typeof(double), 8),
            "F32" => (typeof(float), 4),
            "F16" => (typeof(Half), 2),
            "BF16" => (typeof(Half), 2), // BFloat16 - using Half as compatible type
            "I64" => (typeof(long), 8),
            "I32" => (typeof(int), 4),
            "I16" => (typeof(short), 2),
            "I8" => (typeof(sbyte), 1),
            "U64" => (typeof(ulong), 8),
            "U32" => (typeof(uint), 4),
            "U16" => (typeof(ushort), 2),
            "U8" => (typeof(byte), 1),
            "BOOL" => (typeof(byte), 1), // stored as 0/1 bytes
            _ => throw new NotSupportedException($"Unsupported dtype '{dtype}'.")
        };

        /// <summary>
        /// Ensures the data type matches the requested type
        /// </summary>
        private static void EnsureDTypeMatches<T>(TensorInfo info) where T : unmanaged
        {
            var (t, size) = MapDType(info.DType);
            if (t != typeof(T))
            {
                // Allow reading BOOL as byte
                if (!(info.DType == "BOOL" && typeof(T) == typeof(byte)))
                {
                    throw new InvalidOperationException($"Tensor '{info.Name}' has dtype {info.DType}, not {typeof(T).Name}.");
                }
            }
            // Optional: also verify size matches ByteLength
            if (info.ByteLength % size != 0)
            {
                throw new InvalidDataException($"Tensor '{info.Name}' byte length is not a multiple of its dtype size.");
            }
        }

        /// <summary>
        /// Disposes resources used by the SafeTensor
        /// </summary>
        public void Dispose()
        {
            if (_isMultiFile && _multiFiles != null)
            {
                foreach (var (fs, mmf) in _multiFiles.Values)
                {
                    mmf?.Dispose();
                    fs?.Dispose();
                }
            }
            else
            {
                _mmf?.Dispose();
                _fs?.Dispose();
            }
        }
    }

    /// <summary>
    /// Stateless service for opening and parsing SafeTensor files
    /// </summary>
    public class SafetensorsParser
    {
        /// <summary>
        /// Opens and parses a SafeTensor file or multi-file model
        /// </summary>
        /// <param name="pathList">Array of file paths. If single .safetensors file, opens as single file.
        /// If multiple files including model.safetensors.index.json, opens as indexed model.</param>
        /// <returns>SafeTensor object for accessing the file contents</returns>
        /// <exception cref="InvalidDataException">Thrown when the file format is invalid</exception>
        public SafeTensor OpenFile(string[] pathList)
        {
            if (pathList == null || pathList.Length == 0)
            {
                throw new ArgumentException("Path list cannot be null or empty", nameof(pathList));
            }

            // Validate all files exist
            foreach (var path in pathList)
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"File not found: {path}");
                }
            }

            // Single file mode: one file with .safetensors extension
            if (pathList.Length == 1 && pathList[0].EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
            {
                return OpenSingleFile(pathList[0]);
            }

            // Multi-file mode: multiple files with model.safetensors.index.json
            var indexFile = pathList.FirstOrDefault(p => p.EndsWith("model.safetensors.index.json", StringComparison.OrdinalIgnoreCase));
            if (indexFile != null)
            {
                return OpenIndexedModel(indexFile);
            }

            // If neither condition is met, throw an error
            throw new ArgumentException("Invalid file set. Expected either a single .safetensors file or multiple files including model.safetensors.index.json", nameof(pathList));
        }

        /// <summary>
        /// Opens a single SafeTensor file
        /// </summary>
        private SafeTensor OpenSingleFile(string path, string? sourceFileName = null)
        {
            var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                if (fs.Length < 8)
                {
                    throw new InvalidDataException("File too small for SafeTensors header.");
                }

                // Read u64 header length (little-endian)
                Span<byte> lenBuf = stackalloc byte[8];
                fs.ReadExactly(lenBuf);
                ulong headerLen = BinaryPrimitives.ReadUInt64LittleEndian(lenBuf);
                if ((long)(8 + (long)headerLen) > fs.Length)
                {
                    throw new InvalidDataException("Invalid header length.");
                }

                // Read header JSON
                var headerBytes = new byte[checked((int)headerLen)];
                fs.ReadExactly(headerBytes);
                var doc = JsonDocument.Parse(headerBytes);

                // Parse tensors
                var tensors = new Dictionary<string, TensorInfo>(StringComparer.Ordinal);
                foreach (var kv in doc.RootElement.EnumerateObject())
                {
                    var name = kv.Name;
                    var obj = kv.Value;
                    var dtype = obj.GetProperty("dtype").GetString()!;
                    var shapeJson = obj.GetProperty("shape");
                    var shape = new long[shapeJson.GetArrayLength()];
                    for (int i = 0; i < shape.Length; i++)
                    {
                        shape[i] = shapeJson[i].GetInt64();
                    }

                    var offs = obj.GetProperty("data_offsets");
                    long start = offs[0].GetInt64();
                    long end = offs[1].GetInt64();

                    // Offsets are relative to the start of the data section (just after header)
                    long dataBase = 8 + (long)headerLen;
                    start += dataBase;
                    end += dataBase;

                    tensors[name] = new TensorInfo(name, dtype, shape, start, end, sourceFileName);
                }

                // Basic bounds validation
                foreach (var t in tensors.Values)
                {
                    if (t.Start < 0 || t.End < 0 || t.Start > t.End || t.End > fs.Length)
                    {
                        throw new InvalidDataException($"Tensor '{t.Name}' has invalid data offsets.");
                    }
                }

                // Map file for fast reads
                var mmf = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);

                return new SafeTensor(fs, mmf, tensors);
            }
            catch
            {
                fs?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Opens a multi-file SafeTensor model using an index.json file
        /// </summary>
        private SafeTensor OpenIndexedModel(string indexPath)
        {
            if (!File.Exists(indexPath))
            {
                throw new FileNotFoundException($"Index file not found: {indexPath}");
            }

            string baseDir = Path.GetDirectoryName(indexPath) ?? "";

            // Parse the index file
            string indexJson = File.ReadAllText(indexPath);
            var index = JsonSerializer.Deserialize<SafetensorsIndex>(indexJson);
            if (index == null || index.WeightMap == null)
            {
                throw new InvalidDataException("Invalid index file format.");
            }

            // Get unique safetensors files
            var uniqueFiles = index.WeightMap.Values.Distinct().ToList();
            
            var multiFiles = new Dictionary<string, (FileStream fs, MemoryMappedFile mmf)>();
            var allTensors = new Dictionary<string, TensorInfo>(StringComparer.Ordinal);

            try
            {
                // Load each safetensors file
                foreach (var fileName in uniqueFiles)
                {
                    string filePath = Path.Combine(baseDir, fileName);
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException($"Safetensors file not found: {filePath}");
                    }

                    var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    try
                    {
                        if (fs.Length < 8)
                        {
                            throw new InvalidDataException($"File too small for SafeTensors header: {fileName}");
                        }

                        // Read u64 header length (little-endian)
                        Span<byte> lenBuf = stackalloc byte[8];
                        fs.ReadExactly(lenBuf);
                        ulong headerLen = BinaryPrimitives.ReadUInt64LittleEndian(lenBuf);
                        if ((long)(8 + (long)headerLen) > fs.Length)
                        {
                            throw new InvalidDataException($"Invalid header length in {fileName}");
                        }

                        // Read header JSON
                        var headerBytes = new byte[checked((int)headerLen)];
                        fs.ReadExactly(headerBytes);
                        var doc = JsonDocument.Parse(headerBytes);

                        // Parse tensors from this file
                        foreach (var kv in doc.RootElement.EnumerateObject())
                        {
                            var name = kv.Name;
                            var obj = kv.Value;
                            var dtype = obj.GetProperty("dtype").GetString()!;
                            var shapeJson = obj.GetProperty("shape");
                            var shape = new long[shapeJson.GetArrayLength()];
                            for (int i = 0; i < shape.Length; i++)
                            {
                                shape[i] = shapeJson[i].GetInt64();
                            }

                            var offs = obj.GetProperty("data_offsets");
                            long start = offs[0].GetInt64();
                            long end = offs[1].GetInt64();

                            // Offsets are relative to the start of the data section
                            long dataBase = 8 + (long)headerLen;
                            start += dataBase;
                            end += dataBase;

                            // Only add tensors that are mapped in the index
                            if (index.WeightMap.TryGetValue(name, out var mappedFile) && mappedFile == fileName)
                            {
                                allTensors[name] = new TensorInfo(name, dtype, shape, start, end, fileName);
                            }
                        }

                        // Map file for fast reads
                        var mmf = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);
                        multiFiles[fileName] = (fs, mmf);
                    }
                    catch
                    {
                        fs?.Dispose();
                        throw;
                    }
                }

                // Validate all tensors
                foreach (var t in allTensors.Values)
                {
                    if (t.Start < 0 || t.End < 0 || t.Start > t.End)
                    {
                        throw new InvalidDataException($"Tensor '{t.Name}' has invalid data offsets.");
                    }
                }

                return new SafeTensor(multiFiles, allTensors);
            }
            catch
            {
                // Cleanup on error
                foreach (var (fs, mmf) in multiFiles.Values)
                {
                    mmf?.Dispose();
                    fs?.Dispose();
                }
                throw;
            }
        }
    }
}