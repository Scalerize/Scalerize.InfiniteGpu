using Google.Protobuf;
using Scalerize.InfiniteGpu.Desktop.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scalerize.InfiniteGpu.Desktop.Converters
{
    /// <summary>
    /// Converter for transforming HDF5 files to ONNX format
    /// </summary>
    public class Hdf5Converter
    {
        private readonly Hdf5Parser _hdf5Service;
        private readonly OnnxParser _onnxService;

        public Hdf5Converter(Hdf5Parser hdf5Service, OnnxParser onnxService)
        {
            _hdf5Service = hdf5Service ?? throw new ArgumentNullException(nameof(hdf5Service));
            _onnxService = onnxService ?? throw new ArgumentNullException(nameof(onnxService));
        }

        public Hdf5Converter()
        {
            _hdf5Service = new Hdf5Parser();
            _onnxService = new OnnxParser();
        }

        /// <summary>
        /// Converts an HDF5 file to an ONNX model
        /// </summary>
        /// <param name="hdf5Path">Path to the HDF5 file</param>
        /// <param name="modelName">Name for the output ONNX model</param>
        /// <param name="inputNames">Optional: Names of datasets to use as model inputs</param>
        /// <param name="outputNames">Optional: Names of datasets to use as model outputs</param>
        /// <returns>ONNX ModelProto</returns>
        public Onnx.ModelProto ConvertToOnnx(
            string hdf5Path,
            string modelName = "converted_model",
            string[]? inputNames = null,
            string[]? outputNames = null)
        {
            if (string.IsNullOrEmpty(hdf5Path))
            {
                throw new ArgumentNullException(nameof(hdf5Path));
            }

            // Open the HDF5 file
            using var hdf5File = _hdf5Service.OpenFile(hdf5Path);

            if (hdf5File.Datasets == null)
            {
                throw new InvalidOperationException("Failed to load HDF5 file");
            }

            // Create ONNX model
            var model = new Onnx.ModelProto
            {
                IrVersion = 8, // ONNX IR version 8
                ProducerName = "Hdf5Converter",
                ProducerVersion = "1.0",
                Domain = "ai.onnx",
                ModelVersion = 1,
                DocString = $"Converted from HDF5 file: {System.IO.Path.GetFileName(hdf5Path)}"
            };

            // Add default opset import
            model.OpsetImport.Add(new Onnx.OperatorSetIdProto
            {
                Domain = "",
                Version = 21
            });

            // Create graph
            var graph = new Onnx.GraphProto
            {
                Name = modelName
            };

            // Get all dataset names
            var allDatasetNames = hdf5File.ListDatasetNames().ToList();

            // Determine inputs and outputs
            var effectiveInputs = inputNames?.ToList() ?? new List<string>();
            var effectiveOutputs = outputNames?.ToList() ?? new List<string>();

            // If no inputs/outputs specified, use heuristics
            if (effectiveInputs.Count == 0 && effectiveOutputs.Count == 0)
            {
                // Use first dataset as input and last as output by default
                if (allDatasetNames.Count > 0)
                {
                    effectiveInputs.Add(allDatasetNames.First());
                }
                if (allDatasetNames.Count > 1)
                {
                    effectiveOutputs.Add(allDatasetNames.Last());
                }
                else if (allDatasetNames.Count == 1)
                {
                    effectiveOutputs.Add(allDatasetNames.First());
                }
            }

            var initializerNames = new HashSet<string>();

            // Convert all datasets to ONNX initializers
            foreach (var datasetName in allDatasetNames)
            {
                var datasetInfo = hdf5File.GetInfo(datasetName);
                var onnxTensor = ConvertDatasetToOnnx(hdf5File, datasetName, datasetInfo);

                // Datasets that are not inputs or outputs become initializers (weights)
                if (!effectiveInputs.Contains(datasetName) && !effectiveOutputs.Contains(datasetName))
                {
                    graph.Initializer.Add(onnxTensor);
                    initializerNames.Add(datasetName);
                }
                else if (effectiveInputs.Contains(datasetName) || effectiveOutputs.Contains(datasetName))
                {
                    // For datasets used as inputs/outputs, we might still want them as initializers
                    // depending on the use case
                    graph.Initializer.Add(onnxTensor);
                    initializerNames.Add(datasetName);
                }
            }

            // Add inputs to graph
            foreach (var inputName in effectiveInputs)
            {
                if (!hdf5File.Datasets.ContainsKey(inputName))
                {
                    throw new ArgumentException($"Input dataset '{inputName}' not found in HDF5 file");
                }

                var datasetInfo = hdf5File.GetInfo(inputName);
                var valueInfo = CreateValueInfo(inputName, datasetInfo);
                graph.Input.Add(valueInfo);
            }

            // Add outputs to graph
            foreach (var outputName in effectiveOutputs)
            {
                if (!hdf5File.Datasets.ContainsKey(outputName))
                {
                    throw new ArgumentException($"Output dataset '{outputName}' not found in HDF5 file");
                }

                var datasetInfo = hdf5File.GetInfo(outputName);
                var valueInfo = CreateValueInfo(outputName, datasetInfo);
                graph.Output.Add(valueInfo);
            }

            // Add a simple Identity node if we have both inputs and outputs
            if (effectiveInputs.Count > 0 && effectiveOutputs.Count > 0)
            {
                var identityNode = new Onnx.NodeProto
                {
                    OpType = "Identity",
                    Name = "identity_node"
                };
                identityNode.Input.Add(effectiveInputs[0]);
                identityNode.Output.Add(effectiveOutputs[0]);
                graph.Node.Add(identityNode);
            }

            model.Graph = graph;
            return model;
        }

        /// <summary>
        /// Converts an HDF5 dataset to an ONNX TensorProto
        /// </summary>
        private Onnx.TensorProto ConvertDatasetToOnnx(Hdf5File hdf5File, string name, Hdf5TensorInfo datasetInfo)
        {
            var onnxTensor = new Onnx.TensorProto
            {
                Name = name,
                DataType = MapTypeToOnnx(datasetInfo.DType)
            };

            // Add dimensions
            foreach (var dim in datasetInfo.Shape)
            {
                onnxTensor.Dims.Add(dim);
            }

            // Read and convert dataset data based on type
            byte[] rawData = GetRawDataForType(hdf5File, name, datasetInfo.DType);

            onnxTensor.RawData = ByteString.CopyFrom(rawData);
            return onnxTensor;
        }

        /// <summary>
        /// Gets raw data bytes for a dataset based on its type
        /// </summary>
        private byte[] GetRawDataForType(Hdf5File hdf5File, string name, Type dtype)
        {
            if (dtype == typeof(float))
                return ConvertToRawData(hdf5File.ReadDataset<float>(name));
            else if (dtype == typeof(double))
                return ConvertToRawData(hdf5File.ReadDataset<double>(name));
            else if (dtype == typeof(Half))
                return ConvertToRawData(hdf5File.ReadDataset<Half>(name));
            else if (dtype == typeof(long))
                return ConvertToRawData(hdf5File.ReadDataset<long>(name));
            else if (dtype == typeof(int))
                return ConvertToRawData(hdf5File.ReadDataset<int>(name));
            else if (dtype == typeof(short))
                return ConvertToRawData(hdf5File.ReadDataset<short>(name));
            else if (dtype == typeof(sbyte))
                return ConvertToRawData(hdf5File.ReadDataset<sbyte>(name));
            else if (dtype == typeof(ulong))
                return ConvertToRawData(hdf5File.ReadDataset<ulong>(name));
            else if (dtype == typeof(uint))
                return ConvertToRawData(hdf5File.ReadDataset<uint>(name));
            else if (dtype == typeof(ushort))
                return ConvertToRawData(hdf5File.ReadDataset<ushort>(name));
            else if (dtype == typeof(byte))
                return ConvertToRawData(hdf5File.ReadDataset<byte>(name));
            else if (dtype == typeof(bool))
                return ConvertToRawData(hdf5File.ReadDataset<byte>(name)); // Read bool as byte
            else
                throw new NotSupportedException($"Unsupported data type '{dtype.Name}'");
        }

        /// <summary>
        /// Creates an ONNX ValueInfoProto from dataset information
        /// </summary>
        private Onnx.ValueInfoProto CreateValueInfo(string name, Hdf5TensorInfo datasetInfo)
        {
            var valueInfo = new Onnx.ValueInfoProto
            {
                Name = name,
                Type = new Onnx.TypeProto
                {
                    TensorType = new Onnx.TypeProto.Types.Tensor
                    {
                        ElemType = MapTypeToOnnx(datasetInfo.DType),
                        Shape = new Onnx.TensorShapeProto()
                    }
                }
            };

            // Add shape dimensions
            foreach (var dim in datasetInfo.Shape)
            {
                valueInfo.Type.TensorType.Shape.Dim.Add(new Onnx.TensorShapeProto.Types.Dimension
                {
                    DimValue = dim
                });
            }

            return valueInfo;
        }

        /// <summary>
        /// Maps .NET type to ONNX data type
        /// </summary>
        private int MapTypeToOnnx(Type dtype)
        {
            if (dtype == typeof(double)) return 11;   // DOUBLE
            if (dtype == typeof(float)) return 1;     // FLOAT
            if (dtype == typeof(Half)) return 10;     // FLOAT16
            if (dtype == typeof(long)) return 7;      // INT64
            if (dtype == typeof(int)) return 6;       // INT32
            if (dtype == typeof(short)) return 5;     // INT16
            if (dtype == typeof(sbyte)) return 3;     // INT8
            if (dtype == typeof(ulong)) return 13;    // UINT64
            if (dtype == typeof(uint)) return 12;     // UINT32
            if (dtype == typeof(ushort)) return 4;    // UINT16
            if (dtype == typeof(byte)) return 2;      // UINT8
            if (dtype == typeof(bool)) return 9;      // BOOL
            
            throw new NotSupportedException($"Unsupported data type '{dtype.Name}'");
        }

        /// <summary>
        /// Converts typed array to raw bytes
        /// </summary>
        private byte[] ConvertToRawData<T>(T[] data) where T : unmanaged
        {
            var bytes = new byte[data.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>()];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// Saves the converted ONNX model to a file
        /// </summary>
        /// <param name="model">The ONNX model to save</param>
        /// <param name="outputPath">Path where the ONNX file should be saved</param>
        public void SaveOnnxModel(Onnx.ModelProto model, string outputPath)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentNullException(nameof(outputPath));
            }

            var serialized = _onnxService.Serialize(model);
            System.IO.File.WriteAllBytes(outputPath, serialized);
        }

        /// <summary>
        /// Converts an HDF5 file to ONNX and saves it
        /// </summary>
        /// <param name="hdf5Path">Path to the HDF5 file</param>
        /// <param name="onnxOutputPath">Path where the ONNX file should be saved</param>
        /// <param name="modelName">Name for the output ONNX model</param>
        /// <param name="inputNames">Optional: Names of datasets to use as model inputs</param>
        /// <param name="outputNames">Optional: Names of datasets to use as model outputs</param>
        public void ConvertAndSave(
            string hdf5Path,
            string onnxOutputPath,
            string modelName = "converted_model",
            string[]? inputNames = null,
            string[]? outputNames = null)
        {
            var model = ConvertToOnnx(hdf5Path, modelName, inputNames, outputNames);
            SaveOnnxModel(model, onnxOutputPath);
        }
    }
}