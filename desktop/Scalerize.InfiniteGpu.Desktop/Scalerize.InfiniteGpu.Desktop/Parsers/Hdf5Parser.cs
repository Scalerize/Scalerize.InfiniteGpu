using HDF5CSharp;
using HDF5CSharp.DataTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Scalerize.InfiniteGpu.Desktop.Parsers
{
    /// <summary>
    /// Represents information about a dataset in an HDF5 file
    /// </summary>
    public sealed record Hdf5TensorInfo(string Name, Type DType, long[] Shape, long ElementCount)
    {
        public long ByteLength => ElementCount * GetTypeSize(DType);

        private static long GetTypeSize(Type type)
        {
            if (type == typeof(double)) return 8;
            if (type == typeof(float)) return 4;
            if (type == typeof(Half)) return 2;
            if (type == typeof(long)) return 8;
            if (type == typeof(int)) return 4;
            if (type == typeof(short)) return 2;
            if (type == typeof(sbyte)) return 1;
            if (type == typeof(ulong)) return 8;
            if (type == typeof(uint)) return 4;
            if (type == typeof(ushort)) return 2;
            if (type == typeof(byte)) return 1;
            if (type == typeof(bool)) return 1;
            return Marshal.SizeOf(type);
        }
    }

    /// <summary>
    /// Represents an opened HDF5 file with methods to access its contents
    /// </summary>
    public sealed class Hdf5File : IDisposable
    {
        private readonly long _fileId;
        private readonly Dictionary<string, Hdf5TensorInfo> _datasets;
        private bool _disposed = false;

        public IReadOnlyDictionary<string, Hdf5TensorInfo> Datasets => _datasets;

        internal Hdf5File(long fileId, Dictionary<string, Hdf5TensorInfo> datasets)
        {
            _fileId = fileId;
            _datasets = datasets;
        }

        /// <summary>
        /// Gets a list of all dataset names in the file
        /// </summary>
        /// <returns>Ordered list of dataset names</returns>
        public IEnumerable<string> ListDatasetNames()
        {
            return _datasets.Keys.OrderBy(n => n);
        }

        /// <summary>
        /// Gets information about a specific dataset
        /// </summary>
        /// <param name="name">Name of the dataset</param>
        /// <returns>Dataset information</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the dataset is not found</exception>
        public Hdf5TensorInfo GetInfo(string name)
        {
            return _datasets.TryGetValue(name, out var info) ? info :
                throw new KeyNotFoundException($"Dataset '{name}' not found.");
        }

        /// <summary>
        /// Reads a dataset into a managed array of type T
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read the dataset as</typeparam>
        /// <param name="name">Name of the dataset</param>
        /// <returns>Array containing the dataset data</returns>
        public T[] ReadDataset<T>(string name) where T : unmanaged
        {
            var info = GetInfo(name);
            
            // Verify type compatibility
            EnsureTypeMatches<T>(info);

            if (info.ElementCount > int.MaxValue)
            {
                throw new NotSupportedException("Dataset too large for single managed array.");
            }

            // Read the dataset using HDF5-CSharp
            try
            {
                var data = Hdf5.ReadDataset<T>(_fileId, name);
                
                if (data == null)
                {
                    throw new InvalidDataException($"Failed to read dataset '{name}'");
                }

                // Flatten multi-dimensional array if needed
                if (data.Rank > 1)
                {
                    var flatArray = new T[info.ElementCount];
                    Buffer.BlockCopy(data, 0, flatArray, 0, (int)(info.ElementCount * Unsafe.SizeOf<T>()));
                    return flatArray;
                }

                // Convert to 1D array if it's already 1D
                if (data is T[] oneDArray)
                {
                    return oneDArray;
                }

                // Handle multi-dimensional to 1D conversion
                var result = new T[info.ElementCount];
                int index = 0;
                foreach (T item in data)
                {
                    result[index++] = item;
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Error reading dataset '{name}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Convenience method: returns shape and typed data for a dataset
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read the dataset as</typeparam>
        /// <param name="name">Name of the dataset</param>
        /// <returns>Tuple containing the shape and data</returns>
        public (long[] shape, T[] data) ReadDatasetData<T>(string name) where T : unmanaged
        {
            var info = GetInfo(name);
            var data = ReadDataset<T>(name);

            // Sanity check: element count matches shape
            if (info.ElementCount != data.LongLength)
            {
                throw new InvalidDataException($"Shape mismatch for '{name}': header={info.ElementCount}, actual={data.LongLength}.");
            }
            return (info.Shape, data);
        }

        /// <summary>
        /// Ensures the data type matches the requested type
        /// </summary>
        private static void EnsureTypeMatches<T>(Hdf5TensorInfo info) where T : unmanaged
        {
            var expectedType = typeof(T);
            if (info.DType != expectedType)
            {
                // Allow reading bool as byte
                if (!(info.DType == typeof(bool) && expectedType == typeof(byte)))
                {
                    throw new InvalidOperationException($"Dataset '{info.Name}' has type {info.DType.Name}, not {expectedType.Name}.");
                }
            }
        }

        /// <summary>
        /// Disposes resources used by the Hdf5File
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    if (_fileId > 0)
                    {
                        Hdf5.CloseFile(_fileId);
                    }
                }
                catch
                {
                    // Suppress errors during disposal
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~Hdf5File()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Stateless service for opening and parsing HDF5 files
    /// </summary>
    public class Hdf5Parser
    {
        /// <summary>
        /// Opens and parses an HDF5 file
        /// </summary>
        /// <param name="path">Path to the HDF5 file</param>
        /// <returns>Hdf5File object for accessing the file contents</returns>
        /// <exception cref="InvalidDataException">Thrown when the file format is invalid</exception>
        public Hdf5File OpenFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"HDF5 file not found: {path}");
            }

            long fileId = -1;
            try
            {
                // Open the HDF5 file
                fileId = Hdf5.OpenFile(path, readOnly: true);
                
                if (fileId <= 0)
                {
                    throw new InvalidDataException("Failed to open HDF5 file.");
                }

                // Get all datasets in the file
                var datasets = new Dictionary<string, Hdf5TensorInfo>(StringComparer.Ordinal);
                
                // Recursively traverse the file to find all datasets
                TraverseGroup(fileId, "/", datasets);

                if (datasets.Count == 0)
                {
                    throw new InvalidDataException("No datasets found in HDF5 file.");
                }

                return new Hdf5File(fileId, datasets);
            }
            catch
            {
                if (fileId > 0)
                {
                    try { Hdf5.CloseFile(fileId); } catch { }
                }
                throw;
            }
        }

        /// <summary>
        /// Recursively traverses HDF5 groups to find all datasets
        /// </summary>
        private void TraverseGroup(long groupId, string groupPath, Dictionary<string, Hdf5TensorInfo> datasets)
        {
            try
            {
                // Get all objects in this group
                var objectNames = Hdf5.GroupObjects(groupId, groupPath);
                
                foreach (var objName in objectNames)
                {
                    var fullPath = groupPath == "/" ? $"/{objName}" : $"{groupPath}/{objName}";
                    
                    // Check if it's a dataset
                    if (Hdf5.GroupOrDataset(groupId, fullPath) == Hdf5ElementType.Dataset)
                    {
                        try
                        {
                            // Get dataset information
                            var datatype = Hdf5.GetDatatype(groupId, fullPath);
                            var shape = Hdf5.GetDimensions(groupId, fullPath);
                            
                            // Calculate element count
                            long elementCount = 1;
                            foreach (var dim in shape)
                            {
                                elementCount *= dim;
                            }

                            // Map HDF5 datatype to .NET type
                            var netType = MapHdf5TypeToNetType(datatype);
                            
                            var info = new Hdf5TensorInfo(fullPath, netType, shape, elementCount);
                            datasets[fullPath] = info;
                        }
                        catch (Exception ex)
                        {
                            // Log but continue - some datasets might not be readable
                            Console.WriteLine($"Warning: Could not read dataset '{fullPath}': {ex.Message}");
                        }
                    }
                    else if (Hdf5.GroupOrDataset(groupId, fullPath) == Hdf5ElementType.Group)
                    {
                        // Recursively traverse subgroups
                        TraverseGroup(groupId, fullPath, datasets);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error traversing group '{groupPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Maps HDF5 datatype to .NET type
        /// </summary>
        private Type MapHdf5TypeToNetType(Hdf5DataType hdf5Type)
        {
            return hdf5Type switch
            {
                Hdf5DataType.Float64 => typeof(double),
                Hdf5DataType.Float32 => typeof(float),
                Hdf5DataType.Float16 => typeof(Half),
                Hdf5DataType.Int64 => typeof(long),
                Hdf5DataType.Int32 => typeof(int),
                Hdf5DataType.Int16 => typeof(short),
                Hdf5DataType.Int8 => typeof(sbyte),
                Hdf5DataType.UInt64 => typeof(ulong),
                Hdf5DataType.UInt32 => typeof(uint),
                Hdf5DataType.UInt16 => typeof(ushort),
                Hdf5DataType.UInt8 => typeof(byte),
                _ => typeof(byte) // Default to byte for unknown types
            };
        }
    }
}