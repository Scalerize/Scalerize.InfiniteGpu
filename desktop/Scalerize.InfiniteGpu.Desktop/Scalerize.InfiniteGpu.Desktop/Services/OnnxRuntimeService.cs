using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Windows.AI.MachineLearning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Scalerize.InfiniteGpu.Desktop.Services
{
    public class OnnxRuntimeService
    {
        private const string ExecutionProviderId = "InfiniteGpu";
        private bool _initialized;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);

        public async Task<OnnxInferenceResult> ExecuteOnnxModelAsync(
            byte[] model,
            IReadOnlyList<NamedOnnxValue> inputs,
            CancellationToken cancellationToken = default)
        {
            if (model is null || model.Length == 0)
            {
                throw new ArgumentException("Model buffer cannot be null or empty.", nameof(model));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using var sessionOptions = new SessionOptions();
            sessionOptions.SetEpSelectionPolicy(ExecutionProviderDevicePolicy.MAX_PERFORMANCE);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var session = new InferenceSession(model, sessionOptions);
                using var results = session.Run(inputs);

                var outputs = new List<OnnxInferenceOutput>(results.Count);
                foreach (var output in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    outputs.Add(ConvertOutput(output));
                }

                return new OnnxInferenceResult(outputs);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OnnxRuntimeService] Model execution failed: {ex}");
                throw;
            }
        }

        public async Task<bool> InitializeOnnxRuntimeAsync()
        {
            if (_initialized)
            {
                return true;
            }

            await _initializationLock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_initialized)
                {
                    return true;
                }

                EnvironmentCreationOptions envOptions = new()
                {
                    logId = ExecutionProviderId,
                    logLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR
                };

                using var ortEnv = OrtEnv.CreateInstanceWithOptions(ref envOptions);

                var catalog = ExecutionProviderCatalog.GetDefault();
                await catalog.EnsureAndRegisterCertifiedAsync();

                _initialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OnnxRuntimeService] Failed to initialize ONNX Runtime: {ex}");
                return false;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        private static OnnxInferenceOutput ConvertOutput(DisposableNamedOnnxValue value)
        {
            var elementType = DescribeElementType(value);
            var dimensions = TryGetDimensions(value);
            var data = ExtractFullData(value);

            return new OnnxInferenceOutput(value.Name, elementType, dimensions, data);
        }

        private static string DescribeElementType(DisposableNamedOnnxValue value)
        {
            try
            {
                var tensorElementType = value.ElementType;
                if (tensorElementType != TensorElementType.DataTypeMax)
                {
                    return tensorElementType.ToString();
                }
            }
            catch
            {
                // ElementType may throw for non-tensor outputs, ignore and fall back.
            }

            var runtimeType = value.Value?.GetType();
            if (runtimeType is not null)
            {
                return runtimeType.Name;
            }

            return "unknown";
        }

        private static int[]? TryGetDimensions(DisposableNamedOnnxValue value)
        {
            return value.Value switch
            {
                Tensor<float> tensor => tensor.Dimensions.ToArray(),
                Tensor<double> tensor => tensor.Dimensions.ToArray(),
                Tensor<long> tensor => tensor.Dimensions.ToArray(),
                Tensor<int> tensor => tensor.Dimensions.ToArray(),
                Tensor<bool> tensor => tensor.Dimensions.ToArray(),
                Tensor<string> tensor => tensor.Dimensions.ToArray(),
                _ => null
            };
        }

        private static object? ExtractFullData(DisposableNamedOnnxValue value)
        {
            return value.Value switch
            {
                Tensor<float> tensor => tensor.ToArray(),
                Tensor<double> tensor => tensor.ToArray(),
                Tensor<long> tensor => tensor.ToArray(),
                Tensor<int> tensor => tensor.ToArray(),
                Tensor<bool> tensor => tensor.ToArray(),
                Tensor<string> tensor => tensor.ToArray(),
                float[] array => array,
                double[] array => array,
                long[] array => array,
                int[] array => array,
                bool[] array => array,
                string[] array => array,
                IEnumerable<float> enumerable => enumerable.ToArray(),
                IEnumerable<double> enumerable => enumerable.ToArray(),
                IEnumerable<long> enumerable => enumerable.ToArray(),
                IEnumerable<int> enumerable => enumerable.ToArray(),
                IEnumerable<bool> enumerable => enumerable.ToArray(),
                IEnumerable<string> enumerable => enumerable.ToArray(),
                _ => value.Value
            };
        }
    }

    public sealed record OnnxInferenceResult(IReadOnlyList<OnnxInferenceOutput> Outputs);

    public sealed record OnnxInferenceOutput(string Name, string ElementType, int[]? Dimensions, object? Data);
}