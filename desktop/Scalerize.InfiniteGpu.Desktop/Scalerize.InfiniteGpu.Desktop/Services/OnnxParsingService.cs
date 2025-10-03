using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    /// <summary>
    /// Service responsible for parsing ONNX model files and extracting structure information
    /// </summary>
    public class OnnxParsingService
    {
        /// <summary>
        /// Parses an ONNX model file and returns its structure
        /// </summary>
        public async Task<OnnxModelStructure> ParseOnnxModelAsync(
            byte[] modelData,
            CancellationToken cancellationToken = default)
        {
            if (modelData is null || modelData.Length == 0)
            {
                throw new ArgumentException("Model data cannot be null or empty.", nameof(modelData));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                return await Task.Run(() =>
                 {
                     var model = ParseModelProto(modelData);
                     return new OnnxModelStructure(
                         ModelInfo: ExtractModelMetadata(model),
                         Graph: ExtractGraphInformation(model),
                         TotalWeightCount: CountTotalWeights(model),
                         RawModel: model
                     );
                 }, cancellationToken);

            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OnnxParsingService] Failed to parse ONNX model: {ex}");
                throw new InvalidOperationException("Failed to parse ONNX model file.", ex);
            }
        }

        private static Onnx.ModelProto ParseModelProto(byte[] data)
        {
            try
            {
                return Onnx.ModelProto.Parser.ParseFrom(data);
            }
            catch (InvalidProtocolBufferException ex)
            {
                throw new InvalidOperationException("Invalid ONNX protobuf format.", ex);
            }
        }

        private static OnnxModelInfo ExtractModelMetadata(Onnx.ModelProto model)
        {
            var opsetImports = model.OpsetImport
                .Select(opset => new OnnxOpsetInfo(opset.Domain ?? string.Empty, opset.Version))
                .ToList();

            var metadataProps = model.MetadataProps
                .ToDictionary(prop => prop.Key, prop => prop.Value);

            return new OnnxModelInfo(
                DocString: model.DocString ?? string.Empty,
                OpsetImports: opsetImports,
                MetadataProps: metadataProps,
                ModelVersion: model.ModelVersion,
                FunctionCount: model.Functions.Count,
                TrainingInfoCount: model.TrainingInfo.Count
            );
        }

        private static OnnxGraphStructure ExtractGraphInformation(Onnx.ModelProto model)
        {
            if (model.Graph is null)
            {
                throw new InvalidOperationException("ONNX model does not contain a graph.");
            }

            var graph = model.Graph;
            var operators = new List<OnnxOperatorInfo>();

            // Extract operator information from nodes
            foreach (var node in graph.Node)
            {
                var inputs = node.Input.ToList();
                var outputs = node.Output.ToList();
                var attributes = ExtractAttributes(node.Attribute);

                operators.Add(new OnnxOperatorInfo(
                    Name: node.Name ?? $"node_{operators.Count}",
                    OpType: node.OpType,
                    Domain: node.Domain ?? string.Empty,
                    Inputs: inputs,
                    Outputs: outputs,
                    Attributes: attributes,
                    DocString: node.DocString ?? string.Empty
                ));
            }

            // Extract input information
            var graphInputs = graph.Input
                .Select(input => new OnnxValueInfo(
                    Name: input.Name,
                    DocString: input.DocString ?? string.Empty,
                    TypeInfo: ExtractTypeInfo(input.Type)
                ))
                .ToList();

            // Extract output information
            var graphOutputs = graph.Output
                .Select(output => new OnnxValueInfo(
                    Name: output.Name,
                    DocString: output.DocString ?? string.Empty,
                    TypeInfo: ExtractTypeInfo(output.Type)
                ))
                .ToList();

            // Extract initializer information
            var initializers = graph.Initializer
                .Select(init => new OnnxTensorInfo(
                    Name: init.Name,
                    DataType: init.DataType.ToString(),
                    Dims: init.Dims.ToList(),
                    ElementCount: CalculateElementCount(init.Dims)
                ))
                .ToList();

            return new OnnxGraphStructure(
                Name: graph.Name,
                DocString: graph.DocString ?? string.Empty,
                Operators: operators,
                Inputs: graphInputs,
                Outputs: graphOutputs,
                Initializers: initializers,
                ValueInfos: graph.ValueInfo.Count
            );
        }

        private static Dictionary<string, string> ExtractAttributes(Google.Protobuf.Collections.RepeatedField<Onnx.AttributeProto> attributes)
        {
            var result = new Dictionary<string, string>();

            foreach (var attr in attributes)
            {
                var value = attr.Type switch
                {
                    Onnx.AttributeProto.Types.AttributeType.Float => attr.F.ToString(),
                    Onnx.AttributeProto.Types.AttributeType.Int => attr.I.ToString(),
                    Onnx.AttributeProto.Types.AttributeType.String => System.Text.Encoding.UTF8.GetString(attr.S.ToByteArray()),
                    Onnx.AttributeProto.Types.AttributeType.Floats => $"[{string.Join(", ", attr.Floats)}]",
                    Onnx.AttributeProto.Types.AttributeType.Ints => $"[{string.Join(", ", attr.Ints)}]",
                    Onnx.AttributeProto.Types.AttributeType.Strings => $"[{string.Join(", ", attr.Strings.Select(s => System.Text.Encoding.UTF8.GetString(s.ToByteArray())))}]",
                    Onnx.AttributeProto.Types.AttributeType.Tensor => "TensorAttribute",
                    Onnx.AttributeProto.Types.AttributeType.Graph => "GraphAttribute",
                    _ => attr.Type.ToString()
                };

                result[attr.Name] = value;
            }

            return result;
        }

        private static string ExtractTypeInfo(Onnx.TypeProto? typeProto)
        {
            if (typeProto is null)
            {
                return "unknown";
            }

            return typeProto.ValueCase switch
            {
                Onnx.TypeProto.ValueOneofCase.TensorType => ExtractTensorTypeInfo(typeProto.TensorType),
                Onnx.TypeProto.ValueOneofCase.SequenceType => "sequence",
                Onnx.TypeProto.ValueOneofCase.MapType => "map",
                Onnx.TypeProto.ValueOneofCase.OptionalType => "optional",
                Onnx.TypeProto.ValueOneofCase.SparseTensorType => "sparse_tensor",
                _ => "unknown"
            };
        }

        private static string ExtractTensorTypeInfo(Onnx.TypeProto.Types.Tensor tensorType)
        {
            var elemType = tensorType.ElemType.ToString();

            if (tensorType.Shape?.Dim != null && tensorType.Shape.Dim.Count > 0)
            {
                var dims = tensorType.Shape.Dim
                    .Select(dim => dim.ValueCase switch
                    {
                        Onnx.TensorShapeProto.Types.Dimension.ValueOneofCase.DimValue => dim.DimValue.ToString(),
                        Onnx.TensorShapeProto.Types.Dimension.ValueOneofCase.DimParam => dim.DimParam,
                        _ => "?"
                    })
                    .ToList();

                return $"tensor<{elemType}>[{string.Join(", ", dims)}]";
            }

            return $"tensor<{elemType}>";
        }

        private static long CountTotalWeights(Onnx.ModelProto model)
        {
            if (model.Graph is null)
            {
                return 0;
            }

            long totalCount = 0;

            // Count weights from initializers
            foreach (var initializer in model.Graph.Initializer)
            {
                totalCount += CalculateElementCount(initializer.Dims);
            }

            // Count weights from sparse tensors if any
            foreach (var sparseInit in model.Graph.SparseInitializer)
            {
                if (sparseInit.Values != null)
                {
                    totalCount += CalculateElementCount(sparseInit.Values.Dims);
                }
            }

            return totalCount;
        }

        private static long CalculateElementCount(Google.Protobuf.Collections.RepeatedField<long> dims)
        {
            if (dims is null || dims.Count == 0)
            {
                return 0;
            }

            long count = 1;
            foreach (var dim in dims)
            {
                if (dim <= 0)
                {
                    return 0;
                }
                count *= dim;
            }

            return count;
        }
    }

    #region Model Structure Classes

    /// <summary>
    /// Represents the complete structure of an ONNX model
    /// </summary>
    public sealed record OnnxModelStructure(
        OnnxModelInfo ModelInfo,
        OnnxGraphStructure Graph,
        Onnx.ModelProto RawModel,
        long TotalWeightCount
    );

    /// <summary>
    /// Model-level metadata information
    /// </summary>
    public sealed record OnnxModelInfo(
        string DocString,
        IReadOnlyList<OnnxOpsetInfo> OpsetImports,
        IReadOnlyDictionary<string, string> MetadataProps,
        long ModelVersion,
        int FunctionCount,
        int TrainingInfoCount
    );

    /// <summary>
    /// Operator set information
    /// </summary>
    public sealed record OnnxOpsetInfo(
        string Domain,
        long Version
    );

    /// <summary>
    /// Graph structure information
    /// </summary>
    public sealed record OnnxGraphStructure(
        string Name,
        string DocString,
        IReadOnlyList<OnnxOperatorInfo> Operators,
        IReadOnlyList<OnnxValueInfo> Inputs,
        IReadOnlyList<OnnxValueInfo> Outputs,
        IReadOnlyList<OnnxTensorInfo> Initializers,
        int ValueInfos
    );

    /// <summary>
    /// Information about a graph operator/node
    /// </summary>
    public sealed record OnnxOperatorInfo(
        string Name,
        string OpType,
        string Domain,
        IReadOnlyList<string> Inputs,
        IReadOnlyList<string> Outputs,
        IReadOnlyDictionary<string, string> Attributes,
        string DocString
    );

    /// <summary>
    /// Value information (inputs/outputs)
    /// </summary>
    public sealed record OnnxValueInfo(
        string Name,
        string DocString,
        string TypeInfo
    );

    /// <summary>
    /// Tensor/initializer information
    /// </summary>
    public sealed record OnnxTensorInfo(
        string Name,
        string DataType,
        IReadOnlyList<long> Dims,
        long ElementCount
    );

    #endregion
}