using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using static Onnx.TensorShapeProto.Types;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    /// <summary>
    /// Represents a node in the partition graph
    /// Each node represents a parallelizable subpart of the ONNX model
    /// </summary>
    public class PartitionNode
    {
        /// <summary>
        /// Unique identifier for this partition node
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Input tensor names that this partition consumes
        /// </summary>
        public List<string> InputNames { get; set; } = new List<string>();

        /// <summary>
        /// Output tensor names that this partition produces
        /// </summary>
        public List<string> OutputNames { get; set; } = new List<string>();

        /// <summary>
        /// Estimated memory required by this partition in bytes
        /// </summary>
        public long EstimatedMemoryBytes { get; set; }

        /// <summary>
        /// References to previous partition nodes in the graph
        /// </summary>
        public List<PartitionNode> PreviousNodes { get; set; } = new List<PartitionNode>();

        /// <summary>
        /// References to next partition nodes in the graph
        /// </summary>
        public List<PartitionNode> NextNodes { get; set; } = new List<PartitionNode>();

        /// <summary>
        /// ONNX node indices included in this partition
        /// </summary>
        public List<int> NodeIndices { get; set; } = new List<int>();

        /// <summary>
        /// Execution level/stage - partitions at the same level can execute in parallel
        /// </summary>
        public int ExecutionLevel { get; set; }

        /// <summary>
        /// Range of ONNX node indices included in this partition (start index, inclusive)
        /// </summary>
        public int StartNodeIndex { get; set; }

        /// <summary>
        /// Range of ONNX node indices included in this partition (end index, exclusive)
        /// </summary>
        public int EndNodeIndex { get; set; }
    }

    /// <summary>
    /// Represents the partition graph result
    /// A directed acyclic graph (DAG) where each node represents a parallelizable subpart
    /// </summary>
    public class PartitionGraph
    {
        /// <summary>
        /// All nodes in the partition graph
        /// </summary>
        public List<PartitionNode> Nodes { get; set; } = new List<PartitionNode>();

        /// <summary>
        /// Root nodes (entry points) of the partition graph
        /// </summary>
        public List<PartitionNode> RootNodes { get; set; } = new List<PartitionNode>();

        /// <summary>
        /// Leaf nodes (exit points) of the partition graph
        /// </summary>
        public List<PartitionNode> LeafNodes { get; set; } = new List<PartitionNode>();

        /// <summary>
        /// Nodes grouped by execution level - all nodes at the same level can execute in parallel
        /// Key: execution level, Value: list of partition nodes at that level
        /// </summary>
        public Dictionary<int, List<PartitionNode>> ExecutionLevels { get; set; } = new Dictionary<int, List<PartitionNode>>();

        /// <summary>
        /// Maximum parallelism possible (max number of partitions at any execution level)
        /// </summary>
        public int MaxParallelism { get; set; }

        /// <summary>
        /// Indicates whether the model was partitioned into multiple subgraphs
        /// </summary>
        public bool IsPartitioned { get; set; }

        /// <summary>
        /// Total memory required by the entire model in bytes
        /// </summary>
        public long TotalModelMemoryBytes { get; set; }
    }

    /// <summary>
    /// Service responsible for partitioning ONNX models into parallelizable subgraphs
    /// based on memory constraints
    /// </summary>
    public class OnnxPartitionerService
    {
        private readonly OnnxParsingService _parsingService;
        private const long DEFAULT_MEMORY_THRESHOLD_BYTES = 3L * 1024 * 1024 * 1024;

        public OnnxPartitionerService(OnnxParsingService parsingService)
        {
            _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
        }

        /// <summary>
        /// Dependency information for ONNX nodes
        /// </summary>
        private class DependencyInfo
        {
            /// <summary>
            /// Map of tensor name to the node index that produces it
            /// </summary>
            public Dictionary<string, int> TensorProducers { get; set; } = new Dictionary<string, int>();

            /// <summary>
            /// Map of node index to list of node indices that depend on it
            /// </summary>
            public Dictionary<int, List<int>> NodeDependents { get; set; } = new Dictionary<int, List<int>>();

            /// <summary>
            /// Map of node index to list of node indices it depends on
            /// </summary>
            public Dictionary<int, List<int>> NodeDependencies { get; set; } = new Dictionary<int, List<int>>();
        }

        /// <summary>
        /// Partitions an ONNX model into parallelizable subgraphs if memory constraints require it
        /// </summary>
        /// <param name="model">The ONNX model to partition</param>
        /// <param name="inputs">List of inputs (for resolving dynamic shapes like batch_size)</param>
        /// <param name="availableMemoryBytes">Available memory in bytes (default: 8GB)</param>
        /// <returns>Partition graph with nodes representing parallelizable subparts</returns>
        public PartitionGraph PartitionModel(
            Onnx.ModelProto model,
            List<NamedOnnxValue> inputs,
            long availableMemoryBytes = DEFAULT_MEMORY_THRESHOLD_BYTES)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.Graph == null)
            {
                throw new InvalidOperationException("Model graph is null");
            }

            if (availableMemoryBytes <= 0)
            {
                throw new ArgumentException("Available memory must be positive", nameof(availableMemoryBytes));
            }

            // Use empty dictionary if no input dimensions provided
            var inputDimensions = inputs
                 .ToDictionary(x => x.Name, x => x.AsTensor<float>().Dimensions.ToArray());

            var graph = new PartitionGraph();

            // Calculate total model memory requirements including inputs
            long totalMemoryBytes = CalculateModelMemorySize(model, inputDimensions);
            graph.TotalModelMemoryBytes = totalMemoryBytes;

            // Check if partitioning is needed
            if (totalMemoryBytes <= availableMemoryBytes)
            {
                // Model fits in memory - create single partition with original input/output
                graph.IsPartitioned = false;
                var singleNode = CreateSinglePartitionNode(model, inputDimensions);
                graph.Nodes.Add(singleNode);
                graph.RootNodes.Add(singleNode);
                graph.LeafNodes.Add(singleNode);
                graph.ExecutionLevels[0] = new List<PartitionNode> { singleNode };
                graph.MaxParallelism = 1;
                return graph;
            }

            // Model doesn't fit - perform partitioning
            graph.IsPartitioned = true;
            BuildPartitionGraph(model, inputDimensions, availableMemoryBytes, graph);

            return graph;
        }

        /// <summary>
        /// Calculates the total memory size required by the model
        /// </summary>
        private long CalculateModelMemorySize(Onnx.ModelProto model, Dictionary<string, int[]> inputDimensions)
        {
            long totalBytes = 0;

            // Calculate memory for input tensors
            foreach (var input in model.Graph.Input)
            {
                long inputSize = CalculateInputTensorSize(input, inputDimensions);
                totalBytes += inputSize;
            }

            // Calculate memory for initializers (weights)
            foreach (var initializer in model.Graph.Initializer)
            {
                totalBytes += CalculateTensorMemorySize(initializer);
            }

            // Calculate memory for sparse initializers
            foreach (var sparseInit in model.Graph.SparseInitializer)
            {
                if (sparseInit.Values != null)
                {
                    totalBytes += CalculateTensorMemorySize(sparseInit.Values);
                }
                if (sparseInit.Indices != null)
                {
                    totalBytes += CalculateTensorMemorySize(sparseInit.Indices);
                }
            }

            return totalBytes;
        }

        /// <summary>
        /// Calculates memory size for an input tensor, resolving dynamic dimensions
        /// </summary>
        private long CalculateInputTensorSize(Onnx.ValueInfoProto input, Dictionary<string, int[]> inputDimensions)
        {
            if (input.Type?.TensorType?.Shape == null)
            {
                return 1024 * 1024; // Default to 1MB if unknown
            }

            long elementCount = 1;

            // If specific dimensions provided for this input, use them
            if (inputDimensions.TryGetValue(input.Name, out int[] providedDims))
            {
                foreach (var dim in providedDims)
                {
                    elementCount *= dim;
                }
            }
            else
            {
                // Use shape from model, resolving dynamic dimensions
                foreach (var dim in input.Type.TensorType.Shape.Dim)
                {
                    if (dim.DimValue > 0)
                    {
                        elementCount *= dim.DimValue;
                    }
                    else if (!string.IsNullOrEmpty(dim.DimParam))
                    {
                        // Dynamic dimension (e.g., batch_size) - use default of 1
                        elementCount *= 1;
                    }
                    else
                    {
                        // Unknown dimension - assume 1
                        elementCount *= 1;
                    }
                }
            }

            int bytesPerElement = GetBytesPerElement(input.Type.TensorType.ElemType);
            return elementCount * bytesPerElement;
        }

        /// <summary>
        /// Calculates memory size for a single tensor
        /// </summary>
        private long CalculateTensorMemorySize(Onnx.TensorProto tensor)
        {
            long elementCount = CalculateElementCount(tensor.Dims);
            int bytesPerElement = GetBytesPerElement(tensor.DataType);

            return elementCount * bytesPerElement;
        }

        /// <summary>
        /// Calculates the number of elements in a tensor based on dimensions
        /// </summary>
        private long CalculateElementCount(Google.Protobuf.Collections.RepeatedField<long> dims)
        {
            if (dims == null || dims.Count == 0)
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
        /// Returns the size in bytes for each data type
        /// </summary>
        private int GetBytesPerElement(int dataType)
        {
            return dataType switch
            {
                1 => 4,   // FLOAT (32-bit)
                2 => 1,   // UINT8
                3 => 1,   // INT8
                4 => 2,   // UINT16
                5 => 2,   // INT16
                6 => 4,   // INT32
                7 => 8,   // INT64
                8 => 4,   // STRING (approximate)
                9 => 1,   // BOOL
                10 => 2,  // FLOAT16
                11 => 8,  // DOUBLE
                12 => 4,  // UINT32
                13 => 8,  // UINT64
                14 => 8,  // COMPLEX64
                15 => 16, // COMPLEX128
                16 => 2,  // BFLOAT16
                17 => 1,  // FLOAT8E4M3FN
                18 => 1,  // FLOAT8E4M3FNUZ
                19 => 1,  // FLOAT8E5M2
                20 => 1,  // FLOAT8E5M2FNUZ
                21 => 1,  // UINT4 (0.5 bytes, rounded up)
                22 => 1,  // INT4 (0.5 bytes, rounded up)
                23 => 1,  // FLOAT4E2M1
                24 => 1,  // FLOAT8E8M0
                _ => 4    // Default to 4 bytes
            };
        }

        /// <summary>
        /// Creates a single partition node containing the entire model
        /// </summary>
        private PartitionNode CreateSinglePartitionNode(Onnx.ModelProto model, Dictionary<string, int[]> inputDimensions)
        {
            var node = new PartitionNode
            {
                Id = 0,
                InputNames = model.Graph.Input.Select(i => i.Name).ToList(),
                OutputNames = model.Graph.Output.Select(o => o.Name).ToList(),
                StartNodeIndex = 0,
                EndNodeIndex = model.Graph.Node.Count,
                EstimatedMemoryBytes = CalculateModelMemorySize(model, inputDimensions),
                ExecutionLevel = 0
            };

            return node;
        }

        /// <summary>
        /// Builds the partition graph by creating nodes and establishing connections
        /// </summary>
        private void BuildPartitionGraph(
            Onnx.ModelProto model,
            Dictionary<string, int[]> inputDimensions,
            long availableMemoryBytes,
            PartitionGraph graph)
        {
            var nodes = model.Graph.Node.ToList();
            var tensorMemorySizes = BuildTensorMemoryMap(model.Graph, inputDimensions);

            // Build dependency graph to identify parallelizable branches
            var dependencyInfo = BuildDependencyInfo(model.Graph);

            // Create partition nodes based on memory constraints AND parallelization opportunities
            var partitionNodes = CreateParallelPartitions(model, nodes, tensorMemorySizes, dependencyInfo, availableMemoryBytes);

            // Build graph connections between partition nodes
            ConnectPartitionNodes(partitionNodes, model.Graph);

            // Calculate execution levels for parallel execution
            AssignExecutionLevels(partitionNodes);

            // Populate the partition graph
            graph.Nodes = partitionNodes;
            graph.RootNodes = partitionNodes.Where(n => n.PreviousNodes.Count == 0).ToList();
            graph.LeafNodes = partitionNodes.Where(n => n.NextNodes.Count == 0).ToList();
            
            // Group by execution levels
            graph.ExecutionLevels = partitionNodes
                .GroupBy(n => n.ExecutionLevel)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            graph.MaxParallelism = graph.ExecutionLevels.Values.Any() ? graph.ExecutionLevels.Values.Max(list => list.Count) : 0;
        }

        /// <summary>
        /// Builds dependency information for the ONNX graph
        /// </summary>
        private DependencyInfo BuildDependencyInfo(Onnx.GraphProto graph)
        {
            var info = new DependencyInfo();
            var nodes = graph.Node.ToList();

            // Mark initial inputs as produced
            foreach (var input in graph.Input)
            {
                info.TensorProducers[input.Name] = -1; // -1 indicates model input
            }

            // Build tensor producer map and node dependencies
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                info.NodeDependencies[i] = new List<int>();
                info.NodeDependents[i] = new List<int>();

                // Record outputs produced by this node
                foreach (var output in node.Output)
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        info.TensorProducers[output] = i;
                    }
                }

                // Record dependencies for this node
                foreach (var input in node.Input)
                {
                    if (!string.IsNullOrEmpty(input) && info.TensorProducers.TryGetValue(input, out int producerIdx))
                    {
                        if (producerIdx >= 0) // Not a model input
                        {
                            info.NodeDependencies[i].Add(producerIdx);
                            if (!info.NodeDependents.ContainsKey(producerIdx))
                            {
                                info.NodeDependents[producerIdx] = new List<int>();
                            }
                            info.NodeDependents[producerIdx].Add(i);
                        }
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// Creates parallel-aware partitions by identifying independent subgraphs
        /// </summary>
        private List<PartitionNode> CreateParallelPartitions(
            Onnx.ModelProto model,
            List<Onnx.NodeProto> nodes,
            Dictionary<string, long> tensorMemorySizes,
            DependencyInfo dependencyInfo,
            long availableMemoryBytes)
        {
            var partitions = new List<PartitionNode>();
            var processedNodes = new HashSet<int>();
            var availableNodes = new HashSet<int>();
            int partitionId = 0;

            // Initialize with nodes that have no dependencies (can start immediately)
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!dependencyInfo.NodeDependencies.ContainsKey(i) || 
                    dependencyInfo.NodeDependencies[i].Count == 0)
                {
                    availableNodes.Add(i);
                }
            }

            // Process nodes in waves, creating parallel partitions
            while (availableNodes.Count > 0 || processedNodes.Count < nodes.Count)
            {
                // Find independent branches that can run in parallel
                var parallelBranches = IdentifyParallelBranches(
                    nodes, 
                    availableNodes, 
                    dependencyInfo, 
                    processedNodes,
                    tensorMemorySizes,
                    availableMemoryBytes);

                // Create a partition for each branch
                foreach (var branch in parallelBranches)
                {
                    if (branch.Count > 0)
                    {
                        var partition = CreatePartitionFromNodes(
                            model,
                            nodes,
                            branch,
                            partitionId++,
                            tensorMemorySizes,
                            dependencyInfo);
                        
                        partitions.Add(partition);

                        // Mark nodes as processed
                        foreach (var nodeIdx in branch)
                        {
                            processedNodes.Add(nodeIdx);
                            availableNodes.Remove(nodeIdx);
                        }
                    }
                }

                // Add newly available nodes (whose dependencies are now satisfied)
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!processedNodes.Contains(i) && !availableNodes.Contains(i))
                    {
                        // Check if all dependencies are processed
                        bool allDependenciesMet = dependencyInfo.NodeDependencies.ContainsKey(i) &&
                            dependencyInfo.NodeDependencies[i].All(dep => processedNodes.Contains(dep));
                        
                        if (allDependenciesMet || !dependencyInfo.NodeDependencies.ContainsKey(i))
                        {
                            availableNodes.Add(i);
                        }
                    }
                }

                // Safety check to prevent infinite loops
                if (availableNodes.Count == 0 && processedNodes.Count < nodes.Count)
                {
                    // Add remaining nodes as fallback
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (!processedNodes.Contains(i))
                        {
                            availableNodes.Add(i);
                        }
                    }
                }
            }

            return partitions;
        }

        /// <summary>
        /// Identifies independent branches that can execute in parallel
        /// </summary>
        private List<List<int>> IdentifyParallelBranches(
            List<Onnx.NodeProto> nodes,
            HashSet<int> availableNodes,
            DependencyInfo dependencyInfo,
            HashSet<int> processedNodes,
            Dictionary<string, long> tensorMemorySizes,
            long availableMemoryBytes)
        {
            var branches = new List<List<int>>();
            var assignedNodes = new HashSet<int>();

            foreach (var startNode in availableNodes.OrderBy(n => n))
            {
                if (assignedNodes.Contains(startNode))
                    continue;

                var branch = new List<int>();
                var branchMemory = 0L;
                var currentNode = startNode;
                
                // Grow branch while memory permits and no conflicts
                while (currentNode >= 0 && !assignedNodes.Contains(currentNode))
                {
                    long nodeMemory = CalculateNodeMemory(nodes[currentNode], tensorMemorySizes);
                    
                    if (branchMemory + nodeMemory > availableMemoryBytes && branch.Count > 0)
                    {
                        break; // Would exceed memory limit
                    }

                    branch.Add(currentNode);
                    assignedNodes.Add(currentNode);
                    branchMemory += nodeMemory;

                    // Try to extend branch with next dependent node
                    currentNode = -1;
                    if (dependencyInfo.NodeDependents.TryGetValue(branch[branch.Count - 1], out var dependents))
                    {
                        // Find a dependent that's available and not yet assigned
                        foreach (var dependent in dependents.OrderBy(d => d))
                        {
                            if (availableNodes.Contains(dependent) || 
                                (dependencyInfo.NodeDependencies[dependent].All(dep => 
                                    processedNodes.Contains(dep) || branch.Contains(dep))))
                            {
                                if (!assignedNodes.Contains(dependent))
                                {
                                    currentNode = dependent;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (branch.Count > 0)
                {
                    branches.Add(branch);
                }
            }

            // If no branches created but we have available nodes, create single-node branches
            if (branches.Count == 0 && availableNodes.Count > 0)
            {
                foreach (var node in availableNodes.Take(1))
                {
                    branches.Add(new List<int> { node });
                }
            }

            return branches;
        }

        /// <summary>
        /// Creates a partition from a list of node indices
        /// </summary>
        private PartitionNode CreatePartitionFromNodes(
            Onnx.ModelProto model,
            List<Onnx.NodeProto> nodes,
            List<int> nodeIndices,
            int partitionId,
            Dictionary<string, long> tensorMemorySizes,
            DependencyInfo dependencyInfo)
        {
            var partition = new PartitionNode
            {
                Id = partitionId,
                NodeIndices = nodeIndices,
                StartNodeIndex = nodeIndices.Min(),
                EndNodeIndex = nodeIndices.Max() + 1
            };

            // Calculate inputs and outputs
            var inputs = new HashSet<string>();
            var outputs = new HashSet<string>();
            var internalTensors = new HashSet<string>();
            long totalMemory = 0;

            foreach (var idx in nodeIndices)
            {
                var node = nodes[idx];
                
                // Track outputs
                foreach (var output in node.Output)
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        outputs.Add(output);
                        internalTensors.Add(output);
                    }
                }

                // Track inputs
                foreach (var input in node.Input)
                {
                    if (!string.IsNullOrEmpty(input) && !internalTensors.Contains(input))
                    {
                        inputs.Add(input);
                    }
                }

                // Calculate memory
                totalMemory += CalculateNodeMemory(node, tensorMemorySizes);
            }

            partition.InputNames = inputs.ToList();
            partition.OutputNames = outputs.ToList();
            partition.EstimatedMemoryBytes = totalMemory;

            return partition;
        }

        /// <summary>
        /// Assigns execution levels to partition nodes for parallel execution
        /// Uses topological sort to determine which partitions can run concurrently
        /// </summary>
        private void AssignExecutionLevels(List<PartitionNode> partitions)
        {
            // Calculate the longest path from root to each node (execution level)
            var levels = new Dictionary<PartitionNode, int>();
            
            // Initialize all nodes to level 0
            foreach (var partition in partitions)
            {
                levels[partition] = 0;
            }

            // Use BFS to assign levels based on dependencies
            var queue = new Queue<PartitionNode>(partitions.Where(p => p.PreviousNodes.Count == 0));
            var visited = new HashSet<PartitionNode>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current))
                    continue;
                
                visited.Add(current);

                // Calculate this node's level as max of all predecessor levels + 1
                int maxPredecessorLevel = -1;
                foreach (var predecessor in current.PreviousNodes)
                {
                    maxPredecessorLevel = Math.Max(maxPredecessorLevel, levels[predecessor]);
                }
                levels[current] = maxPredecessorLevel + 1;

                // Add successors to queue
                foreach (var successor in current.NextNodes)
                {
                    if (!visited.Contains(successor))
                    {
                        queue.Enqueue(successor);
                    }
                }
            }

            // Assign calculated levels to partitions
            foreach (var partition in partitions)
            {
                partition.ExecutionLevel = levels[partition];
            }
        }

        /// <summary>
        /// Connects partition nodes by establishing Previous/Next relationships
        /// </summary>
        private void ConnectPartitionNodes(List<PartitionNode> partitionNodes, Onnx.GraphProto graph)
        {
            // Build a map of which outputs are produced by which partitions
            var outputToPartitionMap = new Dictionary<string, PartitionNode>();

            foreach (var partition in partitionNodes)
            {
                foreach (var output in partition.OutputNames)
                {
                    outputToPartitionMap[output] = partition;
                }
            }

            // Connect partitions based on data dependencies
            foreach (var partition in partitionNodes)
            {
                foreach (var inputName in partition.InputNames)
                {
                    // Find which partition produces this input
                    if (outputToPartitionMap.TryGetValue(inputName, out var producerPartition))
                    {
                        // Connect the partitions
                        if (!partition.PreviousNodes.Contains(producerPartition))
                        {
                            partition.PreviousNodes.Add(producerPartition);
                        }

                        if (!producerPartition.NextNodes.Contains(partition))
                        {
                            producerPartition.NextNodes.Add(partition);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds a map of tensor names to their memory sizes
        /// </summary>
        private Dictionary<string, long> BuildTensorMemoryMap(Onnx.GraphProto graph, Dictionary<string, int[]> inputDimensions)
        {
            var memoryMap = new Dictionary<string, long>();

            // Add input tensors with resolved dimensions
            foreach (var input in graph.Input)
            {
                memoryMap[input.Name] = CalculateInputTensorSize(input, inputDimensions);
            }

            // Add initializers (weights)
            foreach (var initializer in graph.Initializer)
            {
                memoryMap[initializer.Name] = CalculateTensorMemorySize(initializer);
            }

            // Add intermediate tensors
            foreach (var valueInfo in graph.ValueInfo)
            {
                // Estimate memory for intermediate tensors with dimension resolution
                long estimatedSize = EstimateTensorSize(valueInfo, inputDimensions);
                memoryMap[valueInfo.Name] = estimatedSize;
            }

            return memoryMap;
        }

        /// <summary>
        /// Estimates tensor size from ValueInfo, resolving dynamic dimensions where possible
        /// </summary>
        private long EstimateTensorSize(Onnx.ValueInfoProto valueInfo, Dictionary<string, int[]> inputDimensions)
        {
            if (valueInfo.Type?.TensorType?.Shape == null)
            {
                return 1024 * 1024; // Default to 1MB if unknown
            }

            long elementCount = 1;
            foreach (var dim in valueInfo.Type.TensorType.Shape.Dim)
            {
                if (dim.DimValue > 0)
                {
                    elementCount *= dim.DimValue;
                }
                else if (!string.IsNullOrEmpty(dim.DimParam))
                {
                    // Dynamic dimension - try to resolve from input dimensions
                    long resolvedDim = TryResolveDynamicDimension(dim.DimParam, inputDimensions);
                    elementCount *= resolvedDim;
                }
                else
                {
                    // Unknown dimension - assume conservative estimate
                    elementCount *= 100;
                }
            }

            int bytesPerElement = GetBytesPerElement(valueInfo.Type.TensorType.ElemType);
            return elementCount * bytesPerElement;
        }

        /// <summary>
        /// Attempts to resolve a dynamic dimension parameter using input dimensions
        /// </summary>
        private long TryResolveDynamicDimension(string dimParam, Dictionary<string, int[]> inputDimensions)
        {
            // Try to find the dimension in any of the provided inputs
            foreach (var inputDims in inputDimensions.Values)
            {
                if (inputDims != null && inputDims.Length > 0)
                {
                    // Common patterns: batch_size is usually the first dimension
                    if (dimParam.ToLowerInvariant().Contains("batch"))
                    {
                        return inputDims[0];
                    }
                }
            }

            // Default to 1 if can't resolve
            return 1;
        }

        /// <summary>
        /// Calculates memory required by a node's outputs
        /// </summary>
        private long CalculateNodeMemory(Onnx.NodeProto node, Dictionary<string, long> tensorMemorySizes)
        {
            long totalMemory = 0;

            foreach (var output in node.Output)
            {
                if (tensorMemorySizes.TryGetValue(output, out long size))
                {
                    totalMemory += size;
                }
                else
                {
                    totalMemory += 1024 * 1024; // Default 1MB for unknown tensors
                }
            }

            return totalMemory;
        }
    }
}