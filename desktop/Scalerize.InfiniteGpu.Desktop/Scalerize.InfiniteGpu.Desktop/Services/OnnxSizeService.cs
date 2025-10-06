using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    /// <summary>
    /// Service responsible for calculating memory sizes and parameter counts for ONNX models
    /// Handles dynamic dimension resolution and comprehensive tensor memory estimation
    /// </summary>
    public class OnnxSizeService
    {
        /// <summary>
        /// Calculates the total memory size required by the model
        /// Includes inputs, weights, and all intermediate activations
        /// </summary>
        public long CalculateModelMemorySize(Onnx.ModelProto model, Dictionary<string, int[]> inputDimensions)
        {
            long totalBytes = 0;

            // Build a comprehensive tensor memory map including all tensors
            var tensorMemoryMap = BuildCompleteTensorMemoryMap(model.Graph, inputDimensions);

            // Sum all tensor memory requirements
            foreach (var memorySize in tensorMemoryMap.Values)
            {
                totalBytes += memorySize;
            }

            return totalBytes;
        }

        /// <summary>
        /// Builds a complete tensor memory map including inputs, weights, and all intermediate tensors
        /// This properly accounts for the cascading memory requirements through the computation graph
        /// </summary>
        public Dictionary<string, long> BuildCompleteTensorMemoryMap(Onnx.GraphProto graph, Dictionary<string, int[]> inputDimensions)
        {
            var memoryMap = new Dictionary<string, long>();

            // Step 1: Add input tensors with resolved dynamic dimensions
            foreach (var input in graph.Input)
            {
                long inputSize = CalculateInputTensorSize(input, inputDimensions);
                memoryMap[input.Name] = inputSize;
            }

            // Step 2: Add initializers (weights) - these are constant
            foreach (var initializer in graph.Initializer)
            {
                memoryMap[initializer.Name] = CalculateTensorMemorySize(initializer);
            }

            // Step 3: Add sparse initializers
            foreach (var sparseInit in graph.SparseInitializer)
            {
                if (sparseInit.Values != null)
                {
                    memoryMap[sparseInit.Values.Name] = CalculateTensorMemorySize(sparseInit.Values);
                }
                if (sparseInit.Indices != null)
                {
                    memoryMap[sparseInit.Indices.Name] = CalculateTensorMemorySize(sparseInit.Indices);
                }
            }

            // Step 4: Calculate intermediate tensors by propagating shapes through the graph
            // This accounts for activations created by each operation
            var shapeMap = new Dictionary<string, long[]>();
            
            // Initialize with known input shapes
            foreach (var input in graph.Input)
            {
                if (inputDimensions.TryGetValue(input.Name, out int[] dims))
                {
                    shapeMap[input.Name] = dims.Select(d => (long)d).ToArray();
                }
                else if (input.Type?.TensorType?.Shape != null)
                {
                    var resolvedDims = ResolveShapeDimensions(input.Type.TensorType.Shape, inputDimensions);
                    if (resolvedDims != null)
                    {
                        shapeMap[input.Name] = resolvedDims;
                    }
                }
            }

            // Propagate shapes through nodes to calculate intermediate tensor sizes
            foreach (var node in graph.Node)
            {
                // For each output of this node, estimate its memory requirement
                foreach (var output in node.Output)
                {
                    if (string.IsNullOrEmpty(output))
                        continue;

                    // Skip if already in memory map (from initializers)
                    if (memoryMap.ContainsKey(output))
                        continue;

                    // Try to get shape from value_info
                    var valueInfo = graph.ValueInfo.FirstOrDefault(v => v.Name == output);
                    if (valueInfo != null)
                    {
                        long size = EstimateTensorSize(valueInfo, inputDimensions);
                        memoryMap[output] = size;
                    }
                    else
                    {
                        // Estimate based on input shapes and operation type
                        long estimatedSize = EstimateNodeOutputSize(node, shapeMap, memoryMap, inputDimensions);
                        if (estimatedSize > 0)
                        {
                            memoryMap[output] = estimatedSize;
                        }
                    }
                }
            }

            // Step 5: Add output tensors if not already included
            foreach (var output in graph.Output)
            {
                if (!memoryMap.ContainsKey(output.Name))
                {
                    long size = EstimateTensorSize(output, inputDimensions);
                    memoryMap[output.Name] = size;
                }
            }

            return memoryMap;
        }

        /// <summary>
        /// Calculates memory size for an input tensor, resolving dynamic dimensions
        /// Properly handles dynamic dimensions by using actual input values or intelligent defaults
        /// </summary>
        public long CalculateInputTensorSize(Onnx.ValueInfoProto input, Dictionary<string, int[]> inputDimensions)
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
                // Use shape from model, resolving dynamic dimensions intelligently
                for (int i = 0; i < input.Type.TensorType.Shape.Dim.Count; i++)
                {
                    var dim = input.Type.TensorType.Shape.Dim[i];
                    
                    if (dim.DimValue > 0)
                    {
                        // Fixed dimension - use the value
                        elementCount *= dim.DimValue;
                    }
                    else if (!string.IsNullOrEmpty(dim.DimParam))
                    {
                        // Dynamic dimension - try to resolve from other inputs
                        long resolvedDim = TryResolveDynamicDimensionFromInputs(dim.DimParam, i, inputDimensions);
                        elementCount *= resolvedDim;
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
        public long CalculateTensorMemorySize(Onnx.TensorProto tensor)
        {
            long elementCount = CalculateElementCount(tensor.Dims);
            int bytesPerElement = GetBytesPerElement(tensor.DataType);

            return elementCount * bytesPerElement;
        }

        /// <summary>
        /// Calculates the number of elements in a tensor based on dimensions
        /// </summary>
        public long CalculateElementCount(Google.Protobuf.Collections.RepeatedField<long> dims)
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
        public int GetBytesPerElement(int dataType)
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
        /// Resolves shape dimensions from tensor shape proto, handling dynamic dimensions
        /// </summary>
        public long[]? ResolveShapeDimensions(Onnx.TensorShapeProto shape, Dictionary<string, int[]> inputDimensions)
        {
            if (shape == null || shape.Dim == null || shape.Dim.Count == 0)
                return null;

            var dims = new long[shape.Dim.Count];
            for (int i = 0; i < shape.Dim.Count; i++)
            {
                var dim = shape.Dim[i];
                if (dim.DimValue > 0)
                {
                    dims[i] = dim.DimValue;
                }
                else if (!string.IsNullOrEmpty(dim.DimParam))
                {
                    // Try to resolve dynamic dimension from inputs
                    long resolved = TryResolveDynamicDimensionFromInputs(dim.DimParam, i, inputDimensions);
                    dims[i] = resolved;
                }
                else
                {
                    dims[i] = 1; // Unknown, default to 1
                }
            }
            return dims;
        }

        /// <summary>
        /// Estimates tensor size from ValueInfo, resolving dynamic dimensions where possible
        /// </summary>
        public long EstimateTensorSize(Onnx.ValueInfoProto valueInfo, Dictionary<string, int[]> inputDimensions)
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
        /// Estimates output size for a node based on its inputs and operation type
        /// </summary>
        public long EstimateNodeOutputSize(
            Onnx.NodeProto node, 
            Dictionary<string, long[]> shapeMap, 
            Dictionary<string, long> memoryMap,
            Dictionary<string, int[]> inputDimensions)
        {
            // Get input shapes for this node
            var inputShapes = new List<long[]>();
            foreach (var input in node.Input)
            {
                if (!string.IsNullOrEmpty(input) && shapeMap.TryGetValue(input, out long[] shape))
                {
                    inputShapes.Add(shape);
                }
            }

            if (inputShapes.Count == 0)
                return 1024 * 1024; // Default 1MB

            // For most operations, output shape is similar to input shape
            // This is a simplified heuristic - proper shape inference would be more complex
            long[] outputShape = inputShapes[0];
            
            // Special cases for certain operations
            switch (node.OpType?.ToLowerInvariant())
            {
                case "matmul":
                case "gemm":
                    // Matrix multiplication: [M, K] x [K, N] = [M, N]
                    if (inputShapes.Count >= 2)
                    {
                        var shape1 = inputShapes[0];
                        var shape2 = inputShapes[1];
                        if (shape1.Length >= 2 && shape2.Length >= 2)
                        {
                            // Output is batch dimensions from input1 + last dim from input2
                            outputShape = shape1.Take(shape1.Length - 1).Concat(new[] { shape2[shape2.Length - 1] }).ToArray();
                        }
                    }
                    break;
                    
                case "conv":
                    // Convolution typically maintains spatial dimensions (approximately)
                    outputShape = inputShapes[0];
                    break;
                    
                case "concat":
                    // Concatenation increases size along one dimension
                    if (inputShapes.Count > 1)
                    {
                        long totalSize = 0;
                        foreach (var shape in inputShapes)
                        {
                            totalSize += shape.Aggregate(1L, (a, b) => a * b);
                        }
                        return totalSize * 4; // Assume float32
                    }
                    break;
            }

            // Calculate memory from output shape
            long elementCount = outputShape.Aggregate(1L, (a, b) => a * b);
            return elementCount * 4; // Assume float32 (4 bytes per element)
        }

        /// <summary>
        /// Attempts to resolve a dynamic dimension parameter using input dimensions
        /// Enhanced version that looks at dimension position and parameter name
        /// </summary>
        public long TryResolveDynamicDimensionFromInputs(string dimParam, int dimIndex, Dictionary<string, int[]> inputDimensions)
        {
            // Try to find matching dimension in provided inputs
            foreach (var inputDims in inputDimensions.Values)
            {
                if (inputDims != null && dimIndex < inputDims.Length)
                {
                    // If this is a batch dimension (usually index 0), use it
                    if (dimIndex == 0 && dimParam.ToLowerInvariant().Contains("batch"))
                    {
                        return inputDims[0];
                    }
                    // If the parameter name matches dimension type, use it
                    else if (dimParam.ToLowerInvariant().Contains("seq") && dimIndex == 1)
                    {
                        return inputDims[dimIndex];
                    }
                }
            }

            // Fallback: use heuristic based on parameter name
            string lowerParam = dimParam.ToLowerInvariant();
            if (lowerParam.Contains("batch"))
                return 1; // Conservative batch size
            else if (lowerParam.Contains("seq") || lowerParam.Contains("length"))
                return 512; // Common sequence length
            else if (lowerParam.Contains("hidden") || lowerParam.Contains("dim"))
                return 768; // Common hidden dimension
            
            // Default to 1 if cannot resolve
            return 1;
        }

        /// <summary>
        /// Legacy method - redirects to the enhanced version
        /// Attempts to resolve a dynamic dimension parameter using input dimensions
        /// </summary>
        public long TryResolveDynamicDimension(string dimParam, Dictionary<string, int[]> inputDimensions)
        {
            // Use the enhanced version with dimension index 0 (batch dimension) as default
            return TryResolveDynamicDimensionFromInputs(dimParam, 0, inputDimensions);
        }

        /// <summary>
        /// Calculates memory required by a node's outputs
        /// </summary>
        public long CalculateNodeMemory(Onnx.NodeProto node, Dictionary<string, long> tensorMemorySizes)
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