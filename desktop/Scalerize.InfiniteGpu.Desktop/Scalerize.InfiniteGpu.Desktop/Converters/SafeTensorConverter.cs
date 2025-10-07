using Google.Protobuf;
using Scalerize.InfiniteGpu.Desktop.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;

#if NET8_0_OR_GREATER
using BFloat16 = System.Half; // BFloat16 type - using Half as fallback for compatibility
#endif

namespace Scalerize.InfiniteGpu.Desktop.Converters
{
    /// <summary>
    /// Converter for transforming SafeTensor files to ONNX format
    /// </summary>
    public class SafeTensorConverter
    {
        private readonly SafetensorsParser _safetensorService;
        private readonly OnnxParser _onnxService;

        public SafeTensorConverter(SafetensorsParser safetensorService, OnnxParser onnxService)
        {
            _safetensorService = safetensorService ?? throw new ArgumentNullException(nameof(safetensorService));
            _onnxService = onnxService ?? throw new ArgumentNullException(nameof(onnxService));
        }

        public SafeTensorConverter()
        {
            _safetensorService = new SafetensorsParser();
            _onnxService = new OnnxParser();
        }

        /// <summary>
        /// Converts a SafeTensor file to an ONNX model
        /// </summary>
        /// <param name="safetensorPath">Path to the SafeTensor file</param>
        /// <param name="modelName">Name for the output ONNX model</param>
        /// <param name="inputNames">Optional: Names of tensors to use as model inputs</param>
        /// <param name="outputNames">Optional: Names of tensors to use as model outputs</param>
        /// <returns>ONNX ModelProto</returns>
        public Onnx.ModelProto ConvertToOnnx(
            string safetensorPath,
            string modelName = "converted_model")
        {
            if (string.IsNullOrEmpty(safetensorPath))
            {
                throw new ArgumentNullException(nameof(safetensorPath));
            }

            // Open the SafeTensor file - service returns a SafeTensor object
            using var safeTensor = _safetensorService.OpenFile(new[] { safetensorPath });

            if (safeTensor.Tensors == null)
            {
                throw new InvalidOperationException("Failed to load SafeTensor file");
            }

            // Create ONNX model
            var model = new Onnx.ModelProto
            {
                IrVersion = 8, // ONNX IR version 8
                ProducerName = "SafeTensorConverter",
                ProducerVersion = "1.0",
                Domain = "ai.onnx",
                ModelVersion = 1,
                DocString = $"Converted from SafeTensor file: {System.IO.Path.GetFileName(safetensorPath)}"
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

            // Get all tensor names
            var allTensorNames = safeTensor.ListTensorNames().ToList();

            // Determine inputs and outputs
            var effectiveInputs = new List<string>();
            var effectiveOutputs = new List<string>();

            // If no inputs/outputs specified, use heuristics
            if (effectiveInputs.Count == 0 && effectiveOutputs.Count == 0)
            {
                // Use first tensor as input and last as output by default
                if (allTensorNames.Count > 0)
                {
                    effectiveInputs.Add(allTensorNames.First());
                }
                if (allTensorNames.Count > 1)
                {
                    effectiveOutputs.Add(allTensorNames.Last());
                }
                else if (allTensorNames.Count == 1)
                {
                    effectiveOutputs.Add(allTensorNames.First());
                }
            }

            var initializerNames = new HashSet<string>();

            // Convert all tensors to ONNX initializers
            foreach (var tensorName in allTensorNames)
            {
                var tensorInfo = safeTensor.GetInfo(tensorName);
                var onnxTensor = ConvertTensorToOnnx(safeTensor, tensorName, tensorInfo);

                // Tensors that are not inputs or outputs become initializers (weights)
                if (!effectiveInputs.Contains(tensorName) && !effectiveOutputs.Contains(tensorName))
                {
                    graph.Initializer.Add(onnxTensor);
                    initializerNames.Add(tensorName);
                }
                else if (effectiveInputs.Contains(tensorName) || effectiveOutputs.Contains(tensorName))
                {
                    // For tensors used as inputs/outputs, we might still want them as initializers
                    // depending on the use case
                    graph.Initializer.Add(onnxTensor);
                    initializerNames.Add(tensorName);
                }
            }

            // Add inputs to graph
            foreach (var inputName in effectiveInputs)
            {
                if (!safeTensor.Tensors.ContainsKey(inputName))
                {
                    throw new ArgumentException($"Input tensor '{inputName}' not found in SafeTensor file");
                }

                var tensorInfo = safeTensor.GetInfo(inputName);
                var valueInfo = CreateValueInfo(inputName, tensorInfo);
                graph.Input.Add(valueInfo);
            }

            // Add outputs to graph
            foreach (var outputName in effectiveOutputs)
            {
                if (!safeTensor.Tensors.ContainsKey(outputName))
                {
                    throw new ArgumentException($"Output tensor '{outputName}' not found in SafeTensor file");
                }

                var tensorInfo = safeTensor.GetInfo(outputName);
                var valueInfo = CreateValueInfo(outputName, tensorInfo);
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
        /// Converts a SafeTensor tensor to an ONNX TensorProto
        /// </summary>
        private Onnx.TensorProto ConvertTensorToOnnx(SafeTensor safeTensor, string name, TensorInfo tensorInfo)
        {
            var onnxTensor = new Onnx.TensorProto
            {
                Name = name,
                DataType = MapDTypeToOnnx(tensorInfo.DType)
            };

            // Add dimensions
            foreach (var dim in tensorInfo.Shape)
            {
                onnxTensor.Dims.Add(dim);
            }

            // Read and convert tensor data based on dtype
            byte[] rawData = tensorInfo.DType switch
            {
                "F32" => ConvertToRawData(safeTensor.ReadTensor<float>(name)),
                "F64" => ConvertToRawData(safeTensor.ReadTensor<double>(name)),
                "F16" => ConvertToRawData(safeTensor.ReadTensor<Half>(name)),
                "BF16" => ConvertToRawData(safeTensor.ReadTensor<Half>(name)), // BFloat16 - using Half as compatible type
                "I64" => ConvertToRawData(safeTensor.ReadTensor<long>(name)),
                "I32" => ConvertToRawData(safeTensor.ReadTensor<int>(name)),
                "I16" => ConvertToRawData(safeTensor.ReadTensor<short>(name)),
                "I8" => ConvertToRawData(safeTensor.ReadTensor<sbyte>(name)),
                "U64" => ConvertToRawData(safeTensor.ReadTensor<ulong>(name)),
                "U32" => ConvertToRawData(safeTensor.ReadTensor<uint>(name)),
                "U16" => ConvertToRawData(safeTensor.ReadTensor<ushort>(name)),
                "U8" => ConvertToRawData(safeTensor.ReadTensor<byte>(name)),
                "BOOL" => ConvertToRawData(safeTensor.ReadTensor<byte>(name)),
                _ => throw new NotSupportedException($"Unsupported dtype '{tensorInfo.DType}'")
            };

            onnxTensor.RawData = ByteString.CopyFrom(rawData);
            return onnxTensor;
        }

        /// <summary>
        /// Creates an ONNX ValueInfoProto from tensor information
        /// </summary>
        private Onnx.ValueInfoProto CreateValueInfo(string name, TensorInfo tensorInfo)
        {
            var valueInfo = new Onnx.ValueInfoProto
            {
                Name = name,
                Type = new Onnx.TypeProto
                {
                    TensorType = new Onnx.TypeProto.Types.Tensor
                    {
                        ElemType = MapDTypeToOnnx(tensorInfo.DType),
                        Shape = new Onnx.TensorShapeProto()
                    }
                }
            };

            // Add shape dimensions
            foreach (var dim in tensorInfo.Shape)
            {
                valueInfo.Type.TensorType.Shape.Dim.Add(new Onnx.TensorShapeProto.Types.Dimension
                {
                    DimValue = dim
                });
            }

            return valueInfo;
        }

        /// <summary>
        /// Maps SafeTensor dtype to ONNX data type
        /// </summary>
        private int MapDTypeToOnnx(string dtype) => dtype switch
        {
            "F64" => 11,   // DOUBLE
            "F32" => 1,    // FLOAT
            "F16" => 10,   // FLOAT16
            "BF16" => 16,  // BFLOAT16
            "I64" => 7,    // INT64
            "I32" => 6,    // INT32
            "I16" => 5,    // INT16
            "I8" => 3,     // INT8
            "U64" => 13,   // UINT64
            "U32" => 12,   // UINT32
            "U16" => 4,    // UINT16
            "U8" => 2,     // UINT8
            "BOOL" => 9,   // BOOL
            _ => throw new NotSupportedException($"Unsupported dtype '{dtype}'")
        };

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
        /// Converts a SafeTensor file to ONNX and saves it
        /// </summary>
        /// <param name="safetensorPath">Path to the SafeTensor file</param>
        /// <param name="onnxOutputPath">Path where the ONNX file should be saved</param>
        /// <param name="modelName">Name for the output ONNX model</param>
        /// <param name="inputNames">Optional: Names of tensors to use as model inputs</param>
        /// <param name="outputNames">Optional: Names of tensors to use as model outputs</param>
        public void ConvertAndSave(
            string safetensorPath,
            string onnxOutputPath,
            string modelName = "converted_model",
            string[]? inputNames = null,
            string[]? outputNames = null)
        {
            var model = ConvertToOnnx(safetensorPath, modelName);
            SaveOnnxModel(model, onnxOutputPath);
        }
    }
}