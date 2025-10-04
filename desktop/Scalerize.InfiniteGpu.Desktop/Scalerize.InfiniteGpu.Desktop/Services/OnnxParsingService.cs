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

            var originalGraph = originalModel.Graph;

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
                Name = $"{originalGraph.Name}_partition_{partition.Id}"
            };

            // Extract nodes and collect used tensor names in one pass
            int nodeCount = Math.Min(partition.EndNodeIndex, originalGraph.Node.Count) - partition.StartNodeIndex;
            var usedTensorNames = new HashSet<string>(nodeCount * 4); // Estimate: ~4 tensors per node

            for (int i = partition.StartNodeIndex; i < partition.EndNodeIndex && i < originalGraph.Node.Count; i++)
            {
                var originalNode = originalGraph.Node[i];
                var clonedNode = originalNode.Clone();
                subGraph.Node.Add(clonedNode);

                // Collect tensor names while iterating
                foreach (var input in originalNode.Input)
                {
                    if (!string.IsNullOrEmpty(input))
                    {
                        usedTensorNames.Add(input);
                    }
                }

                foreach (var output in originalNode.Output)
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        usedTensorNames.Add(output);
                    }
                }
            }

            // Build initializer name lookup once
            var initializerLookup = new Dictionary<string, Onnx.TensorProto>(originalGraph.Initializer.Count);
            foreach (var initializer in originalGraph.Initializer)
            {
                if (usedTensorNames.Contains(initializer.Name))
                {
                    initializerLookup[initializer.Name] = initializer;
                    subGraph.Initializer.Add(initializer.Clone());
                }
            }

            // Add required sparse initializers
            foreach (var sparseInit in originalGraph.SparseInitializer)
            {
                var name = sparseInit.Values?.Name;
                if (name != null && usedTensorNames.Contains(name))
                {
                    subGraph.SparseInitializer.Add(sparseInit.Clone());
                }
            }

            // Build lookup dictionaries for inputs, outputs, and value info
            var originalInputLookup = BuildValueInfoLookup(originalGraph.Input);
            var originalOutputLookup = BuildValueInfoLookup(originalGraph.Output);
            var valueInfoLookup = BuildValueInfoLookup(originalGraph.ValueInfo);

            // Create inputs for the sub-model
            var inputNameSet = new HashSet<string>(partition.InputNames);
            foreach (var inputName in partition.InputNames)
            {
                // Skip if this is an initializer (weights are not inputs)
                if (initializerLookup.ContainsKey(inputName))
                {
                    continue;
                }

                // Try to find in original graph inputs
                if (originalInputLookup.TryGetValue(inputName, out var originalInput))
                {
                    subGraph.Input.Add(originalInput.Clone());
                    continue;
                }

                // Try to find in value info (intermediate tensors)
                if (valueInfoLookup.TryGetValue(inputName, out var valueInfo))
                {
                    subGraph.Input.Add(valueInfo.Clone());
                    continue;
                }

                // If not found, create a basic input entry
                subGraph.Input.Add(new Onnx.ValueInfoProto
                {
                    Name = inputName,
                    Type = new Onnx.TypeProto
                    {
                        TensorType = new Onnx.TypeProto.Types.Tensor
                        {
                            ElemType = 1 // Default to FLOAT
                        }
                    }
                });
            }

            // Create outputs for the sub-model
            var outputNameSet = new HashSet<string>(partition.OutputNames);
            foreach (var outputName in partition.OutputNames)
            {
                // Try to find in original graph outputs
                if (originalOutputLookup.TryGetValue(outputName, out var originalOutput))
                {
                    subGraph.Output.Add(originalOutput.Clone());
                    continue;
                }

                // Try to find in value info (intermediate tensors)
                if (valueInfoLookup.TryGetValue(outputName, out var valueInfo))
                {
                    subGraph.Output.Add(valueInfo.Clone());
                    continue;
                }

                // If not found, create a basic output entry
                subGraph.Output.Add(new Onnx.ValueInfoProto
                {
                    Name = outputName,
                    Type = new Onnx.TypeProto
                    {
                        TensorType = new Onnx.TypeProto.Types.Tensor
                        {
                            ElemType = 1 // Default to FLOAT
                        }
                    }
                });
            }

            // Copy relevant value info (intermediate tensors)
            foreach (var valueInfo in originalGraph.ValueInfo)
            {
                // Include if it's used by the sub-model and not already in inputs/outputs
                if (usedTensorNames.Contains(valueInfo.Name) &&
                    !inputNameSet.Contains(valueInfo.Name) &&
                    !outputNameSet.Contains(valueInfo.Name))
                {
                    subGraph.ValueInfo.Add(valueInfo.Clone());
                }
            }

            subModel.Graph = subGraph;
            return subModel;
        }

        /// <summary>
        /// Builds a lookup dictionary for ValueInfoProto by name
        /// </summary>
        private Dictionary<string, Onnx.ValueInfoProto> BuildValueInfoLookup(
            Google.Protobuf.Collections.RepeatedField<Onnx.ValueInfoProto> valueInfos)
        {
            var lookup = new Dictionary<string, Onnx.ValueInfoProto>(valueInfos.Count);
            foreach (var valueInfo in valueInfos)
            {
                lookup[valueInfo.Name] = valueInfo;
            }
            return lookup;
        }

        /// <summary>
        /// Extracts global model input and output tensor names from an ONNX model.
        /// Global inputs exclude initializers (weights/constants).
        /// </summary>
        /// <param name="model">The ONNX model to extract names from</param>
        /// <returns>Object containing arrays of global input and output names</returns>
        public object GetInputOutputNames(Onnx.ModelProto model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.Graph == null)
            {
                throw new InvalidOperationException("Model graph is null");
            }

            // Build set of initializer names to filter them out from inputs
            var initializerNames = new HashSet<string>(model.Graph.Initializer.Count);
            foreach (var initializer in model.Graph.Initializer)
            {
                if (!string.IsNullOrEmpty(initializer.Name))
                {
                    initializerNames.Add(initializer.Name);
                }
            }

            var inputNames = new List<string>();
            var outputNames = new List<string>();

            // Extract global input names (excluding initializers/weights)
            foreach (var input in model.Graph.Input)
            {
                if (!string.IsNullOrEmpty(input.Name) && !initializerNames.Contains(input.Name))
                {
                    inputNames.Add(input.Name);
                }
            }

            // Extract global output names
            foreach (var output in model.Graph.Output)
            {
                if (!string.IsNullOrEmpty(output.Name))
                {
                    outputNames.Add(output.Name);
                }
            }

            return new
            {
                inputs = inputNames.Select(name => new { name }).ToArray(),
                outputs = outputNames.Select(name => new { name }).ToArray()
            };
        }
    }
}