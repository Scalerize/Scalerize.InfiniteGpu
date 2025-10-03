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
        public Onnx.ModelProto Deserialize(byte[] data)
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

        public byte[] Serialize(Onnx.ModelProto model)
        {
            try
            {
                return model.ToByteArray();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to serialize ONNX model to protobuf format.", ex);
            }
        }

        public long GetTotalWeightsCount(Onnx.ModelProto model)
        {
            if (model.Graph is null)
            {
                return 0;
            }

            long totalCount = 0;

            foreach (var initializer in model.Graph.Initializer)
            {
                totalCount += CalculateElementCount(initializer.Dims);
            }

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

        /// <summary>
        /// Creates a sub-model from the original model based on a partition node
        /// </summary>
        /// <param name="originalModel">The original ONNX model</param>
        /// <param name="partition">The partition node defining the sub-model boundaries</param>
        /// <returns>A new ModelProto representing the sub-model</returns>
        public Onnx.ModelProto CreateSubModel(Onnx.ModelProto originalModel, PartitionNode partition)
        {
            if (originalModel == null)
            {
                throw new ArgumentNullException(nameof(originalModel));
            }

            if (partition == null)
            {
                throw new ArgumentNullException(nameof(partition));
            }

            if (originalModel.Graph == null)
            {
                throw new InvalidOperationException("Original model graph is null");
            }

            // Create a new model proto
            var subModel = new Onnx.ModelProto
            {
                IrVersion = originalModel.IrVersion,
                ProducerName = originalModel.ProducerName,
                ProducerVersion = originalModel.ProducerVersion,
                Domain = originalModel.Domain,
                ModelVersion = originalModel.ModelVersion,
                DocString = $"Sub-model partition {partition.Id}"
            };

            // Copy opset imports
            foreach (var opset in originalModel.OpsetImport)
            {
                subModel.OpsetImport.Add(opset);
            }

            // Copy metadata
            foreach (var metadata in originalModel.MetadataProps)
            {
                subModel.MetadataProps.Add(metadata);
            }

            // Create the sub-graph
            var subGraph = new Onnx.GraphProto
            {
                Name = $"{originalModel.Graph.Name}_partition_{partition.Id}"
            };

            // Extract nodes from the partition range
            var extractedNodes = ExtractNodes(originalModel.Graph, partition.StartNodeIndex, partition.EndNodeIndex);
            foreach (var node in extractedNodes)
            {
                subGraph.Node.Add(node);
            }

            // Collect all tensor names used by extracted nodes
            var usedTensorNames = CollectUsedTensorNames(extractedNodes);

            // Add required initializers (weights)
            var requiredInitializers = CollectRequiredInitializers(originalModel.Graph, usedTensorNames);
            foreach (var initializer in requiredInitializers)
            {
                subGraph.Initializer.Add(initializer);
            }

            // Add required sparse initializers
            var requiredSparseInitializers = CollectRequiredSparseInitializers(originalModel.Graph, usedTensorNames);
            foreach (var sparseInit in requiredSparseInitializers)
            {
                subGraph.SparseInitializer.Add(sparseInit);
            }

            // Create inputs for the sub-model
            var inputs = CreateSubModelInputs(originalModel.Graph, partition.InputNames, usedTensorNames);
            foreach (var input in inputs)
            {
                subGraph.Input.Add(input);
            }

            // Create outputs for the sub-model
            var outputs = CreateSubModelOutputs(originalModel.Graph, partition.OutputNames, usedTensorNames);
            foreach (var output in outputs)
            {
                subGraph.Output.Add(output);
            }

            // Copy relevant value info (intermediate tensors)
            var valueInfos = CollectRelevantValueInfo(originalModel.Graph, usedTensorNames, partition.InputNames, partition.OutputNames);
            foreach (var valueInfo in valueInfos)
            {
                subGraph.ValueInfo.Add(valueInfo);
            }

            subModel.Graph = subGraph;
            return subModel;
        }

        /// <summary>
        /// Extracts nodes from the graph within the specified range
        /// </summary>
        private List<Onnx.NodeProto> ExtractNodes(Onnx.GraphProto graph, int startIndex, int endIndex)
        {
            var nodes = new List<Onnx.NodeProto>();
            
            for (int i = startIndex; i < endIndex && i < graph.Node.Count; i++)
            {
                nodes.Add(graph.Node[i].Clone());
            }

            return nodes;
        }

        /// <summary>
        /// Collects all tensor names used by the given nodes
        /// </summary>
        private HashSet<string> CollectUsedTensorNames(List<Onnx.NodeProto> nodes)
        {
            var tensorNames = new HashSet<string>();

            foreach (var node in nodes)
            {
                foreach (var input in node.Input)
                {
                    if (!string.IsNullOrEmpty(input))
                    {
                        tensorNames.Add(input);
                    }
                }

                foreach (var output in node.Output)
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        tensorNames.Add(output);
                    }
                }
            }

            return tensorNames;
        }

        /// <summary>
        /// Collects initializers that are required by the sub-model
        /// </summary>
        private List<Onnx.TensorProto> CollectRequiredInitializers(Onnx.GraphProto graph, HashSet<string> usedTensorNames)
        {
            var initializers = new List<Onnx.TensorProto>();

            foreach (var initializer in graph.Initializer)
            {
                if (usedTensorNames.Contains(initializer.Name))
                {
                    initializers.Add(initializer.Clone());
                }
            }

            return initializers;
        }

        /// <summary>
        /// Collects sparse initializers that are required by the sub-model
        /// </summary>
        private List<Onnx.SparseTensorProto> CollectRequiredSparseInitializers(Onnx.GraphProto graph, HashSet<string> usedTensorNames)
        {
            var sparseInitializers = new List<Onnx.SparseTensorProto>();

            foreach (var sparseInit in graph.SparseInitializer)
            {
                if (usedTensorNames.Contains(sparseInit.Values?.Name))
                {
                    sparseInitializers.Add(sparseInit.Clone());
                }
            }

            return sparseInitializers;
        }

        /// <summary>
        /// Creates input ValueInfoProto entries for the sub-model
        /// </summary>
        private List<Onnx.ValueInfoProto> CreateSubModelInputs(
            Onnx.GraphProto originalGraph,
            List<string> inputNames,
            HashSet<string> usedTensorNames)
        {
            var inputs = new List<Onnx.ValueInfoProto>();
            var initializerNames = new HashSet<string>(originalGraph.Initializer.Select(i => i.Name));

            foreach (var inputName in inputNames)
            {
                // Skip if this is an initializer (weights are not inputs)
                if (initializerNames.Contains(inputName))
                {
                    continue;
                }

                // Try to find in original graph inputs
                var originalInput = originalGraph.Input.FirstOrDefault(i => i.Name == inputName);
                if (originalInput != null)
                {
                    inputs.Add(originalInput.Clone());
                    continue;
                }

                // Try to find in value info (intermediate tensors)
                var valueInfo = originalGraph.ValueInfo.FirstOrDefault(v => v.Name == inputName);
                if (valueInfo != null)
                {
                    inputs.Add(valueInfo.Clone());
                    continue;
                }

                // If not found, create a basic input entry
                var newInput = new Onnx.ValueInfoProto
                {
                    Name = inputName,
                    Type = new Onnx.TypeProto
                    {
                        TensorType = new Onnx.TypeProto.Types.Tensor
                        {
                            ElemType = 1 // Default to FLOAT
                        }
                    }
                };
                inputs.Add(newInput);
            }

            return inputs;
        }

        /// <summary>
        /// Creates output ValueInfoProto entries for the sub-model
        /// </summary>
        private List<Onnx.ValueInfoProto> CreateSubModelOutputs(
            Onnx.GraphProto originalGraph,
            List<string> outputNames,
            HashSet<string> usedTensorNames)
        {
            var outputs = new List<Onnx.ValueInfoProto>();

            foreach (var outputName in outputNames)
            {
                // Try to find in original graph outputs
                var originalOutput = originalGraph.Output.FirstOrDefault(o => o.Name == outputName);
                if (originalOutput != null)
                {
                    outputs.Add(originalOutput.Clone());
                    continue;
                }

                // Try to find in value info (intermediate tensors)
                var valueInfo = originalGraph.ValueInfo.FirstOrDefault(v => v.Name == outputName);
                if (valueInfo != null)
                {
                    outputs.Add(valueInfo.Clone());
                    continue;
                }

                // If not found, create a basic output entry
                var newOutput = new Onnx.ValueInfoProto
                {
                    Name = outputName,
                    Type = new Onnx.TypeProto
                    {
                        TensorType = new Onnx.TypeProto.Types.Tensor
                        {
                            ElemType = 1 // Default to FLOAT
                        }
                    }
                };
                outputs.Add(newOutput);
            }

            return outputs;
        }

        /// <summary>
        /// Collects relevant value info entries for intermediate tensors
        /// </summary>
        private List<Onnx.ValueInfoProto> CollectRelevantValueInfo(
            Onnx.GraphProto originalGraph,
            HashSet<string> usedTensorNames,
            List<string> inputNames,
            List<string> outputNames)
        {
            var valueInfos = new List<Onnx.ValueInfoProto>();
            var inputOutputNames = new HashSet<string>(inputNames.Concat(outputNames));

            foreach (var valueInfo in originalGraph.ValueInfo)
            {
                // Include if it's used by the sub-model and not already in inputs/outputs
                if (usedTensorNames.Contains(valueInfo.Name) && !inputOutputNames.Contains(valueInfo.Name))
                {
                    valueInfos.Add(valueInfo.Clone());
                }
            }

            return valueInfos;
        }
    }
}