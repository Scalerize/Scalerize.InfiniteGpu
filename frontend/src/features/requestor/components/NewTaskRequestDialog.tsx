import {
  type AriaAttributes,
  type FormEvent,
  type ReactNode,
  useEffect,
  useId,
  useMemo,
  useState,
} from "react";
import { CirclePlus, Copy, NotebookPen, Trash2 } from "lucide-react";
import {
  createTask,
  generateTaskUploadUrl,
  TaskType,
  TaskUploadFileType,
  type CreateTaskRequestBody,
} from "../api";
import { SelectDropdown } from "../../../shared/components/SelectDropdown";
import { FileDropzone } from "../../../shared/components/FileDropzone";
import { DialogShell } from "../../../shared/components/DialogShell";
import { appQueryClient } from "../../../shared/providers/queryClient";
import { invalidateMyTasksQueryKey } from "../queries/useMyTasksQuery";

interface NewTaskRequestDialogProps {
  open: boolean;
  onDismiss: () => void;
}

const modeOptions: Array<{
  value: "inference" | "training";
  label: ReactNode;
  helper?: string;
  disabled?: boolean;
}> = [
  {
    value: "inference",
    label: "Inference",
  },
  {
    value: "training",
    label: (
      <>
        Training{" "}
        <span className="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-amber-600">
          Coming soon
        </span>
      </>
    ),
    disabled: true,
  },
];

type InferenceBinding = {
  id: string;
  tensorName: string;
  payloadType: "json" | "text" | "binary";
  textPayload: string;
  fileName: string | null;
  file: File | null;
};

type OutputBinding = {
  id: string;
  tensorName: string;
  payloadType: "json" | "text" | "binary";
  fileFormat?: string;
};

type DatasetConfig = {
  sourceType: "upload" | "url";
  url: string;
  fileName: string | null;
  format: "vision" | "audio" | "text" | "tabular";
};

const datasetFormatOptions: Array<{
  value: DatasetConfig["format"];
  label: string;
}> = [
  { value: "vision", label: "Vision tensors" },
  { value: "audio", label: "Audio waveforms" },
  { value: "text", label: "Text / token sequences" },
  { value: "tabular", label: "Tabular / CSV" },
];

const getFileExtension = (fileName: string): string => {
  const trimmed = fileName?.trim() ?? "";
  if (!trimmed) {
    return "";
  }

  const lastDotIndex = trimmed.lastIndexOf(".");
  if (lastDotIndex < 0 || lastDotIndex === trimmed.length - 1) {
    return "";
  }

  return trimmed.slice(lastDotIndex + 1).toLowerCase();
};

const createInferenceBinding = (): InferenceBinding => ({
  id: `binding-${Math.random().toString(36).slice(2, 10)}`,
  tensorName: "",
  payloadType: "json",
  textPayload: "",
  fileName: null,
  file: null,
});

const createOutputBinding = (): OutputBinding => ({
  id: `output-${Math.random().toString(36).slice(2, 10)}`,
  tensorName: "",
  payloadType: "json",
  fileFormat: "npz",
});

const createDatasetConfig = (): DatasetConfig => ({
  sourceType: "upload",
  url: "",
  fileName: null,
  format: "text",
});

export const NewTaskRequestDialog = ({
  open,
  onDismiss,
}: NewTaskRequestDialogProps) => {
  const nameFieldId = useId();
  const modeFieldId = useId();
  const fileFieldId = useId();
  const inferenceSectionId = useId();
  const hyperparameterEpochsId = useId();
  const hyperparameterBatchSizeId = useId();
  const hyperparameterLearningRateId = useId();
  const trainingDatasetFileFieldId = useId();
  const validationDatasetFileFieldId = useId();
  const trainingDatasetFormatFieldId = useId();
  const validationDatasetFormatFieldId = useId();
  const trainDatasetUrlFieldId = useId();
  const validationDatasetUrlFieldId = useId();

  const [selectedMode, setSelectedMode] = useState<"inference" | "training">(
    "inference"
  );
  const [inferenceBindings, setInferenceBindings] = useState<
    Array<InferenceBinding>
  >([createInferenceBinding()]);
  const [outputBindings, setOutputBindings] = useState<
    Array<OutputBinding>
  >([createOutputBinding()]);
  const [clientTaskId, setClientTaskId] = useState<string>(() =>
    crypto.randomUUID()
  );
  const [clientSubtaskId, setClientSubtaskId] = useState<string>(() =>
    crypto.randomUUID()
  );
  const [submissionStage, setSubmissionStage] = useState<string>("");
  const [submissionError, setSubmissionError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [inferenceBindingMode, setInferenceBindingMode] = useState<
    "manual" | "api"
  >("manual");
  const publicApiDocUrl = "https://docs.scalerize.ai/public-inference-api";
  const maskedPublicApiKey = "pk-live-******-tenant";
  const [trainingDataset, setTrainingDataset] =
    useState<DatasetConfig>(createDatasetConfig);
  const [validationDataset, setValidationDataset] =
    useState<DatasetConfig>(createDatasetConfig);
  const [trainHyperparameters, setTrainHyperparameters] = useState<{
    epochs: number;
    batchSize: number;
    learningRate: string;
  }>({
    epochs: 3,
    batchSize: 32,
    learningRate: "",
  });

  useEffect(() => {
    if (!open) {
      setClientTaskId(crypto.randomUUID());
      setClientSubtaskId(crypto.randomUUID());
      setSubmissionStage("");
      setSubmissionError(null);
      setIsSubmitting(false);
      setInferenceBindings([createInferenceBinding()]);
      setOutputBindings([createOutputBinding()]);
    }
  }, [open]);

  const handleInferenceBindingChange = <Field extends keyof InferenceBinding>(
    id: string,
    field: Field,
    value: InferenceBinding[Field]
  ) => {
    setInferenceBindings((current) =>
      current.map((binding) => {
        if (binding.id !== id) {
          return binding;
        }

        if (field === "payloadType") {
          const nextValue = value as InferenceBinding["payloadType"];
          if (nextValue === "binary") {
            return {
              ...binding,
              payloadType: nextValue,
              textPayload: "",
            };
          }

          return {
            ...binding,
            payloadType: nextValue,
            textPayload: binding.textPayload,
            file: null,
            fileName: null,
          };
        }

        if (field === "textPayload") {
          return { ...binding, textPayload: value as string };
        }

        return { ...binding, [field]: value };
      })
    );
  };

  const handleInferenceBindingFile = (id: string, file: File | null) => {
    setInferenceBindings((current) =>
      current.map((binding) =>
        binding.id === id
          ? {
              ...binding,
              fileName: file?.name ?? null,
              file,
            }
          : binding
      )
    );
  };

  const handleDatasetChange = <Field extends keyof DatasetConfig>(
    setter: (updater: (prev: DatasetConfig) => DatasetConfig) => void,
    field: Field,
    value: DatasetConfig[Field]
  ) => {
    setter((prev) => ({ ...prev, [field]: value }));
  };

  const resolvedTaskType = useMemo(
    () => (selectedMode === "training" ? TaskType.Train : TaskType.Inference),
    [selectedMode]
  );

  const uploadFileToSas = async (uploadUri: string, file: File) => {
    const response = await fetch(uploadUri, {
      method: "PUT",
      headers: {
        "x-ms-blob-type": "BlockBlob",
        "Content-Type": file.type || "application/octet-stream",
      },
      body: file,
    });

    if (!response.ok) {
      const message = await response.text().catch(() => "Upload failed");
      throw new Error(
        `Azure upload failed (${response.status} ${response.statusText}): ${message}`
      );
    }
  };

  type ApiBindingPayloadType = NonNullable<
    CreateTaskRequestBody["inference"]
  >["bindings"][number]["payloadType"];

  const mapPayloadType: Record<
    InferenceBinding["payloadType"],
    ApiBindingPayloadType
  > = {
    json: "Json",
    text: "Text",
    binary: "Binary",
  };

  const busyAriaProps = useMemo<AriaAttributes>(
    () => (isSubmitting ? { "aria-busy": true } : {}),
    [isSubmitting]
  );

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (isSubmitting) {
      return;
    }

    const formElement = event.currentTarget;
    const formData = new FormData(formElement);
    const onnxFile = formData.get("onnxFile") as File | null;

    if (!onnxFile) {
      setSubmissionError("Please attach an ONNX artifact before dispatching.");
      return;
    }

    const taskId = clientTaskId || crypto.randomUUID();
    const fillBindingsViaApi =
      selectedMode === "inference" && inferenceBindingMode === "api";
    const resolvedSubtaskId = clientSubtaskId ?? crypto.randomUUID();

    try {
      setIsSubmitting(true);
      setSubmissionError(null);

      setSubmissionStage("Negotiating secure upload channel…");
      const modelFileExtension = getFileExtension(onnxFile.name) || "onnx";
      const modelUploadUrl = await generateTaskUploadUrl({
        taskId,
        subtaskId: resolvedSubtaskId,
        inputName: "model",
        fileExtension: modelFileExtension,
        fileType: TaskUploadFileType.Model,
      });

      setSubmissionStage("Uploading ONNX artifact to Storage…");
      await uploadFileToSas(modelUploadUrl.uploadUri, onnxFile);

      const bindings: NonNullable<
        CreateTaskRequestBody["inference"]
      >["bindings"] = [];

      if (selectedMode === "inference") {
        for (const binding of inferenceBindings) {
          if (!binding.tensorName.trim()) {
            throw new Error("Every inference binding requires a tensor name.");
          }

          const payloadType = mapPayloadType[binding.payloadType];

          if (binding.payloadType === "binary") {
            if (!binding.file) {
              throw new Error(
                `Binary binding "${binding.tensorName}" is missing an uploaded tensor.`
              );
            }

            setSubmissionStage(
              `Preparing upload slot for tensor "${binding.tensorName}"…`
            );
            const tensorFileExtension =
              getFileExtension(binding.file.name ?? "") || "bin";

            const tensorUploadUrl = await generateTaskUploadUrl({
              taskId,
              subtaskId: resolvedSubtaskId,
              inputName: binding.tensorName,
              fileExtension: tensorFileExtension,
              fileType: TaskUploadFileType.Input,
            });

            setSubmissionStage(
              `Streaming tensor "${binding.tensorName}" to Azure…`
            );
            await uploadFileToSas(tensorUploadUrl.uploadUri, binding.file);

            bindings.push({
              tensorName: binding.tensorName,
              payloadType,
              payload: null,
              fileUrl: tensorUploadUrl.blobUri,
            });
          } else {
            const payload = binding.textPayload.trim();
            if (!payload) {
              throw new Error(
                `Binding "${binding.tensorName}" must include a payload.`
              );
            }

            bindings.push({
              tensorName: binding.tensorName,
              payloadType,
              payload,
              fileUrl: null,
            });
          }
        }
      }

      const requestBody: CreateTaskRequestBody = {
        taskId,
        type: resolvedTaskType,
        modelUrl: modelUploadUrl.blobUri,
        fillBindingsViaApi,
      };

      if (!fillBindingsViaApi) {
        requestBody.initialSubtaskId = resolvedSubtaskId;
      }

      if (selectedMode === "inference") {
        requestBody.inference = {
          bindings,
          outputs: outputBindings.map((output) => ({
            tensorName: output.tensorName,
            payloadType: mapPayloadType[output.payloadType],
            fileFormat: output.payloadType === "binary" ? output.fileFormat : undefined,
          })),
        };
      }

      setSubmissionStage("Registering workload with orchestration service…");
      await createTask(requestBody);

      setSubmissionStage("Workload registered. Dispatching to providers…");

      // Refetch tasks immediately after creating a new one
      await appQueryClient.invalidateQueries({ queryKey: invalidateMyTasksQueryKey });

      onDismiss();
      setClientTaskId(crypto.randomUUID());
      setClientSubtaskId(crypto.randomUUID());
    } catch (error) {
      setSubmissionError(
        error instanceof Error
          ? error.message
          : "Failed to dispatch workload. Please try again."
      );
    } finally {
      setIsSubmitting(false);
      setSubmissionStage("");
    }
  };

  return (
    <DialogShell
      open={open}
      onDismiss={onDismiss}
      closeLabel="Close new task request dialog"
      badgeIcon={<NotebookPen className="h-3.5 w-3.5" />}
      badgeLabel="New task request"
      title="Dispatch ONNX workload"
      helperText="Upload an ONNX artifact, choose the execution mode, and label the run to track downstream compilation and provider fan-out."
    >
      <form
        onSubmit={handleSubmit}
        className="relative space-y-8"
        {...busyAriaProps}
      >
        {isSubmitting ? (
          <div className="absolute inset-0 bottom-10 z-10 flex flex-col items-center justify-center gap-4 rounded-2xl bg-slate-100/70 backdrop-blur dark:bg-slate-900/70">
            <div className="h-12 w-12 animate-spin rounded-full border-2 border-indigo-900 border-t-transparent dark:border-indigo-400" />
            <div className="space-y-2 text-center">
              <p className="text-xs font-semibold uppercase tracking-[0.3em] text-indigo-700 dark:text-indigo-400">
                Dispatching workload
              </p>
              <p className="mx-auto max-w-xs text-sm text-indigo-700/90 dark:text-indigo-300/90">
                {submissionStage ||
                  "Preparing your workload for distributed execution…"}
              </p>
            </div>
          </div>
        ) : null}
        <section className="space-y-6">
          <div className="grid gap-6 md:grid-cols-2">
            <div className="space-y-2">
              <label
                htmlFor={nameFieldId}
                className="text-sm font-semibold text-slate-700 dark:text-slate-300"
              >
                Name
              </label>
              <input
                id={nameFieldId}
                name="name"
                type="text"
                placeholder="e.g. diffusion-sampler preview run"
                className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition placeholder:text-slate-400 focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:placeholder:text-slate-500 dark:focus:border-indigo-600 dark:focus:ring-indigo-900/60"
              />
            </div>

            <div className="space-y-2">
              <label
                htmlFor={modeFieldId}
                className="text-sm font-semibold text-slate-700 dark:text-slate-300"
              >
                Execution mode
              </label>
              <input type="hidden" name="mode" value={selectedMode} />
              <SelectDropdown
                id={modeFieldId}
                ariaLabel="Execution mode"
                value={selectedMode}
                onValueChange={(value) => {
                  const selectedOption = modeOptions.find(
                    (option) => option.value === value
                  );
                  if (selectedOption?.disabled) {
                    return;
                  }
                  setSelectedMode(value);
                }}
                placeholder="Select execution mode"
                options={modeOptions.map((option) => ({
                  value: option.value,
                  label: option.label,
                  description: option.helper,
                  disabled: option.disabled,
                }))}
              />
            </div>
          </div>

          <div className="space-y-2">
            <label
              htmlFor={fileFieldId}
              className="text-sm font-semibold text-slate-700 dark:text-slate-300"
            >
              ONNX artifact
            </label>
            <FileDropzone
              inputId={fileFieldId}
              name="onnxFile"
              accept=".onnx"
              emptyState={
                <span>
                  Drag & drop your .onnx file here, or
                  <span className="ml-1 text-indigo-600 transition group-hover:text-indigo-500">
                    browse
                  </span>
                </span>
              }
              helperText="Supports up to 2 GB, validated against latest opset."
            />
          </div>
        </section>

        {selectedMode === "inference" && (
          <section
            className="space-y-4 rounded-xl border border-indigo-100 bg-indigo-50/30 p-4 dark:border-indigo-900/50 dark:bg-indigo-950/30"
            aria-labelledby={`${inferenceSectionId}-label`}
          >
            <div className="flex-col gap-1">
              <h3
                id={`${inferenceSectionId}-label`}
                className="text-sm font-semibold text-indigo-700 dark:text-indigo-400"
              >
                Inference payload bindings
              </h3>
              <p className="text-xs text-indigo-600/80 dark:text-indigo-400/80">
                Define tensor names and payloads to feed into the ONNX graph.
                Use JSON arrays for batched inputs or upload binary blobs for
                large tensors.
              </p>
            </div>

            <div className="flex flex-wrap gap-2">
              {(["manual", "api"] as const).map((modeOption) => (
                <button
                  key={modeOption}
                  type="button"
                  onClick={() => setInferenceBindingMode(modeOption)}
                  className={`inline-flex items-center rounded-md border px-3 py-1.5 text-xs font-semibold uppercase tracking-wide transition ${
                    inferenceBindingMode === modeOption
                      ? "border-indigo-400 bg-white text-indigo-600 shadow-sm dark:border-indigo-600 dark:bg-slate-800 dark:text-indigo-400"
                      : "border-indigo-100/70 bg-indigo-100/50 text-indigo-500 hover:border-indigo-200 hover:text-indigo-600 dark:border-indigo-900/50 dark:bg-indigo-950/50 dark:text-indigo-400 dark:hover:border-indigo-800 dark:hover:text-indigo-300"
                  }`}
                >
                  {modeOption === "manual"
                    ? "Manually fill payload bindings"
                    : "Fill by API"}
                </button>
              ))}
            </div>

            {inferenceBindingMode === "manual" ? (
              <>
                <div className="space-y-4">
                  {inferenceBindings.map((binding, index) => (
                    <div
                      key={binding.id}
                      className="space-y-3 rounded-lg border border-indigo-100 bg-white/70 p-4 shadow-sm dark:border-indigo-900/50 dark:bg-slate-800/70"
                    >
                      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                        <div className="flex flex-1 flex-col gap-1">
                          <label className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                            Tensor name
                          </label>
                          <input
                            name={`inferenceBindings[${index}].tensorName`}
                            value={binding.tensorName}
                            onChange={(event) =>
                              handleInferenceBindingChange(
                                binding.id,
                                "tensorName",
                                event.target.value
                              )
                            }
                            placeholder="e.g. input_ids"
                            className="rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition placeholder:text-slate-400 focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:placeholder:text-slate-500 dark:focus:border-indigo-600 dark:focus:ring-indigo-900/60"
                            required
                          />
                        </div>

                        <div className="flex flex-col gap-1 sm:w-48">
                          <span className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                            Payload type
                          </span>
                          <SelectDropdown
                            ariaLabel="Payload type"
                            value={binding.payloadType}
                            onValueChange={(value) =>
                              handleInferenceBindingChange(
                                binding.id,
                                "payloadType",
                                value
                              )
                            }
                            options={[
                              { value: "json", label: "JSON tensor" },
                              { value: "text", label: "Plain text" },
                              { value: "binary", label: "Binary upload" },
                            ]}
                            triggerClassName="w-full"
                          />
                        </div>
                      </div>

                      {binding.payloadType !== "binary" ? (
                        <div className="space-y-1">
                          <label className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                            {binding.payloadType === "json"
                              ? "JSON payload"
                              : "Text payload"}
                          </label>
                          <textarea
                            name={`inferenceBindings[${index}].payload`}
                            value={binding.textPayload}
                            onChange={(event) =>
                              handleInferenceBindingChange(
                                binding.id,
                                "textPayload",
                                event.target.value
                              )
                            }
                            placeholder={
                              binding.payloadType === "json"
                                ? "[[1, 2, 3], [4, 5, 6]]"
                                : "Provide plain text inputs or tokenized sequences"
                            }
                            className="h-32 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 font-mono text-xs leading-5 text-slate-700 shadow-sm transition placeholder:text-slate-400 focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:placeholder:text-slate-500 dark:focus:border-indigo-600 dark:focus:ring-indigo-900/60"
                            required
                          />
                          <p className="text-xs text-slate-400 dark:text-slate-500">
                            {binding.payloadType === "json"
                              ? "Ensure shape matches the model input signature. Arrays are validated server-side."
                              : "Useful for prompt-only workloads or token sequences."}
                          </p>
                        </div>
                      ) : (
                        <div className="space-y-1">
                          <label className="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                            Upload tensor
                          </label>
                          <FileDropzone
                            inputId={`${binding.id}-file`}
                            name={`inferenceBindings[${index}].file`}
                            emptyState="Attach .npy, .npz tensor file or image, video files"
                            helperText="Stored securely and streamed to inference workers."
                            className="min-h-[140px] py-6"
                            selectedFileName={binding.fileName}
                            onFileSelect={(file) =>
                              handleInferenceBindingFile(binding.id, file)
                            }
                          />
                        </div>
                      )}

                      <div className="flex justify-end">
                        <button
                          type="button"
                          onClick={() =>
                            setInferenceBindings((current) =>
                              current.length === 1
                                ? current
                                : current.filter(
                                    (bindingEntry) =>
                                      bindingEntry.id !== binding.id
                                  )
                            )
                          }
                          className="inline-flex items-center gap-2 rounded-md border border-slate-200 px-3 py-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500 transition hover:border-red-200 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-400 dark:hover:border-red-900/50 dark:hover:bg-red-950/30 dark:hover:text-red-400"
                          disabled={inferenceBindings.length === 1}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                          Remove binding
                        </button>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="flex justify-end">
                  <button
                    type="button"
                    onClick={() =>
                      setInferenceBindings((current) => [
                        ...current,
                        createInferenceBinding(),
                      ])
                    }
                    className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-white shadow-sm transition hover:bg-indigo-500 dark:bg-indigo-700 dark:hover:bg-indigo-600"
                  >
                    <CirclePlus className="h-3.5 w-3.5" />
                    Add tensor binding
                  </button>
                </div>
              </>
            ) : (
              <div className="space-y-6 rounded-lg border border-indigo-100 bg-white p-4 shadow-sm">
                <header className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div className="space-y-1">
                    <h4 className="text-sm font-semibold text-indigo-700">
                      Public inference API
                    </h4>
                    <p className="text-xs leading-5 text-slate-500">
                      Each api call will trigger a new task request to connected
                      providers.
                    </p>
                  </div>
                  <a
                    href={publicApiDocUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="text-nowrap inline-flex items-center gap-1 rounded-md border border-indigo-200 px-2.5 py-1 text-xs font-semibold uppercase tracking-wide text-indigo-600 transition hover:bg-indigo-50"
                  >
                    View API docs
                  </a>
                </header>

                <div className="space-y-4">
                  {inferenceBindings.map((binding, index) => (
                    <div
                      key={binding.id}
                      className="space-y-3 rounded-lg border border-indigo-100 bg-indigo-50/40 p-4 shadow-sm"
                    >
                      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                        <div className="flex flex-1 flex-col gap-1">
                          <label className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                            Tensor name
                          </label>
                          <input
                            name={`inferenceBindings[${index}].tensorName`}
                            value={binding.tensorName}
                            onChange={(event) =>
                              handleInferenceBindingChange(
                                binding.id,
                                "tensorName",
                                event.target.value
                              )
                            }
                            placeholder="e.g. input_ids"
                            className="rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition placeholder:text-slate-400 focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60"
                            required
                          />
                        </div>

                        <div className="flex flex-col gap-1 sm:w-48">
                          <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                            Payload type
                          </span>
                          <SelectDropdown
                            ariaLabel="Payload type"
                            value={binding.payloadType}
                            onValueChange={(value) =>
                              handleInferenceBindingChange(
                                binding.id,
                                "payloadType",
                                value
                              )
                            }
                            options={[
                              { value: "json", label: "JSON tensor" },
                              { value: "text", label: "Plain text" },
                              { value: "binary", label: "Binary upload" },
                            ]}
                            triggerClassName="w-full"
                          />
                        </div>
                      </div>

                      <div className="flex justify-end">
                        <button
                          type="button"
                          onClick={() =>
                            setInferenceBindings((current) =>
                              current.length === 1
                                ? current
                                : current.filter(
                                    (bindingEntry) =>
                                      bindingEntry.id !== binding.id
                                  )
                            )
                          }
                          className="inline-flex items-center gap-2 rounded-md border border-slate-200 px-3 py-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500 transition hover:border-red-200 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-50"
                          disabled={inferenceBindings.length === 1}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                          Remove binding
                        </button>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="flex justify-end">
                  <button
                    type="button"
                    onClick={() =>
                      setInferenceBindings((current) => [
                        ...current,
                        createInferenceBinding(),
                      ])
                    }
                    className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-white shadow-sm transition hover:bg-indigo-500"
                  >
                    <CirclePlus className="h-3.5 w-3.5" />
                    Add tensor binding
                  </button>
                </div>

                <aside className="space-y-3 rounded-lg border border-slate-200 bg-slate-50/70 p-4">
                  <div className="space-y-2">
                    <h5 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                      API key
                    </h5>
                    <div className="flex items-center gap-2 rounded-md border border-slate-200 bg-white px-3 py-2 shadow-sm">
                      <span className="flex-1 truncate font-mono text-xs text-slate-600">
                        {maskedPublicApiKey}
                      </span>
                      <button
                        type="button"
                        className="inline-flex items-center gap-1 rounded-md border border-slate-200 px-2 py-1 text-[11px] font-semibold uppercase tracking-wide text-slate-600 transition hover:bg-slate-100"
                      >
                        <Copy className="h-3.5 w-3.5" />
                        Copy
                      </button>
                    </div>
                  </div>
                  <div className="space-y-1 text-xs text-slate-500">
                    <p>
                      Authenticate each call with the{" "}
                      <span className="font-mono text-slate-700">
                        X-Api-Key
                      </span>{" "}
                      header. Rotate credentials from the console when required.
                    </p>
                    <p>
                      Payload metadata defined above is enforced alongside
                      request descriptors when the public API is invoked.
                    </p>
                  </div>
                </aside>
              </div>
            )}
          </section>
        )}

        {selectedMode === "inference" && (
          <section
            className="space-y-4 rounded-xl border border-emerald-100 bg-emerald-50/30 p-4"
            aria-labelledby="output-settings-label"
          >
            <div className="flex flex-col gap-1">
              <h3
                id="output-settings-label"
                className="text-sm font-semibold text-emerald-700"
              >
                Output settings
              </h3>
              <p className="text-xs text-emerald-600/80">
                Define output tensor names and their format. Binary outputs will be uploaded to storage and returned as URLs.
              </p>
            </div>

            <div className="space-y-4">
              {outputBindings.map((output, index) => (
                <div
                  key={output.id}
                  className="space-y-3 rounded-lg border border-emerald-100 bg-white/70 p-4 shadow-sm"
                >
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex flex-1 flex-col gap-1">
                      <label className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                        Tensor name
                      </label>
                      <input
                        name={`outputBindings[${index}].tensorName`}
                        value={output.tensorName}
                        onChange={(event) =>
                          setOutputBindings((current) =>
                            current.map((binding) =>
                              binding.id === output.id
                                ? { ...binding, tensorName: event.target.value }
                                : binding
                            )
                          )
                        }
                        placeholder="e.g. output_0"
                        className="rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition placeholder:text-slate-400 focus:border-emerald-300 focus:outline-none focus:ring focus:ring-emerald-200/60"
                        required
                      />
                    </div>

                    <div className="flex flex-col gap-1 sm:w-48">
                      <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                        Payload type
                      </span>
                      <SelectDropdown
                        ariaLabel="Output payload type"
                        value={output.payloadType}
                        onValueChange={(value) =>
                          setOutputBindings((current) =>
                            current.map((binding) =>
                              binding.id === output.id
                                ? { ...binding, payloadType: value as OutputBinding["payloadType"] }
                                : binding
                            )
                          )
                        }
                        options={[
                          { value: "json", label: "JSON tensor" },
                          { value: "text", label: "Plain text" },
                          { value: "binary", label: "Binary upload" },
                        ]}
                        triggerClassName="w-full"
                      />
                    </div>
                  </div>

                  {output.payloadType === "binary" && (
                    <div className="flex flex-col gap-1">
                      <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                        File format
                      </span>
                      <SelectDropdown
                        ariaLabel="Output file format"
                        value={output.fileFormat || "npy"}
                        onValueChange={(value) =>
                          setOutputBindings((current) =>
                            current.map((binding) =>
                              binding.id === output.id
                                ? { ...binding, fileFormat: value }
                                : binding
                            )
                          )
                        }
                        options={[
                          { value: "npy", label: "NumPy Array (.npy)" },
                          { value: "npz", label: "Compressed NumPy (.npz)" },
                          { value: "png", label: "PNG Image (.png)" },
                          { value: "jpg", label: "JPEG Image (.jpg)" },
                          { value: "webp", label: "WebP Image (.webp)" },
                          { value: "bmp", label: "Bitmap Image (.bmp)" },
                          { value: "tiff", label: "TIFF Image (.tiff)" },
                        ]}
                        triggerClassName="w-full"
                      />
                      <p className="text-xs text-slate-400">
                        Choose the binary format for saving the output tensor
                      </p>
                    </div>
                  )}

                  <div className="flex justify-end">
                    <button
                      type="button"
                      onClick={() =>
                        setOutputBindings((current) =>
                          current.length === 1
                            ? current
                            : current.filter(
                                (bindingEntry) =>
                                  bindingEntry.id !== output.id
                              )
                        )
                      }
                      className="inline-flex items-center gap-2 rounded-md border border-slate-200 px-3 py-1.5 text-xs font-semibold uppercase tracking-wide text-slate-500 transition hover:border-red-200 hover:bg-red-50 hover:text-red-600 disabled:cursor-not-allowed disabled:opacity-50"
                      disabled={outputBindings.length === 1}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                      Remove output
                    </button>
                  </div>
                </div>
              ))}
            </div>

            <div className="flex justify-end">
              <button
                type="button"
                onClick={() =>
                  setOutputBindings((current) => [
                    ...current,
                    createOutputBinding(),
                  ])
                }
                className="inline-flex items-center gap-2 rounded-md bg-emerald-600 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-white shadow-sm transition hover:bg-emerald-500"
              >
                <CirclePlus className="h-3.5 w-3.5" />
                Add output binding
              </button>
            </div>
          </section>
        )}

        {selectedMode === "training" && (
          <section className="space-y-6">
            <div className="space-y-4 rounded-xl border border-slate-200 bg-slate-50/60 p-4">
              <header className="space-y-1">
                <h3 className="text-sm font-semibold text-slate-700">
                  Training dataset
                </h3>
                <p className="text-xs text-slate-500">
                  Provide the primary dataset used for gradient updates. Upload
                  large archives or reference a signed URI.
                </p>
              </header>

              <div className="flex flex-wrap gap-2">
                {(["upload", "url"] as const).map((source) => (
                  <button
                    key={source}
                    type="button"
                    onClick={() =>
                      setTrainingDataset((prev) => ({
                        ...prev,
                        sourceType: source,
                      }))
                    }
                    className={`inline-flex items-center rounded-md border px-3 py-1.5 text-xs font-semibold uppercase tracking-wide transition ${
                      trainingDataset.sourceType === source
                        ? "border-indigo-300 bg-white text-indigo-600 shadow-sm"
                        : "border-slate-200 bg-slate-100 text-slate-500 hover:border-indigo-200 hover:text-indigo-500"
                    }`}
                  >
                    {source === "upload"
                      ? "Upload dataset"
                      : "Reference via URL"}
                  </button>
                ))}
              </div>

              {trainingDataset.sourceType === "upload" ? (
                <FileDropzone
                  inputId={trainingDatasetFileFieldId}
                  name="trainDatasetFile"
                  emptyState="Drop archive or dataset manifest"
                  helperText="Supports TAR, ZIP, parquet, or sharded dataset manifests."
                  className="min-h-[160px] bg-white"
                  selectedFileName={trainingDataset.fileName}
                  onFileSelect={(file) =>
                    setTrainingDataset((prev) => ({
                      ...prev,
                      fileName: file?.name ?? null,
                    }))
                  }
                />
              ) : (
                <div className="space-y-2">
                  <label
                    htmlFor={trainDatasetUrlFieldId}
                    className="text-xs font-semibold uppercase tracking-wide text-slate-500"
                  >
                    Dataset URL
                  </label>
                  <input
                    id={trainDatasetUrlFieldId}
                    name="trainDatasetUrl"
                    type="url"
                    value={trainingDataset.url}
                    onChange={(event) =>
                      handleDatasetChange(
                        setTrainingDataset,
                        "url",
                        event.target.value
                      )
                    }
                    placeholder="https://storage.example.com/train_manifest.parquet"
                    className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition placeholder:text-slate-400 focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60"
                    required={trainingDataset.sourceType === "url"}
                  />
                </div>
              )}

              <div className="grid gap-4">
                <div className="space-y-2">
                  <label
                    htmlFor={trainingDatasetFormatFieldId}
                    className="text-xs font-semibold uppercase tracking-wide text-slate-500"
                  >
                    Dataset format
                  </label>
                  <SelectDropdown
                    id={trainingDatasetFormatFieldId}
                    ariaLabel="Training dataset format"
                    value={trainingDataset.format}
                    onValueChange={(value) =>
                      handleDatasetChange(
                        setTrainingDataset,
                        "format",
                        value as DatasetConfig["format"]
                      )
                    }
                    options={datasetFormatOptions.map((option) => ({
                      value: option.value,
                      label: option.label,
                    }))}
                    triggerClassName="w-full"
                  />
                </div>
              </div>
            </div>

            <div className="space-y-4 rounded-xl border border-slate-200 bg-slate-50/60 p-4">
              <header className="space-y-1">
                <h3 className="text-sm font-semibold text-slate-700">
                  Validation dataset
                </h3>
                <p className="text-xs text-slate-500">
                  Optional evaluation split streamed between checkpoints.
                  Provide a held-out subset to track metrics.
                </p>
              </header>

              <div className="flex flex-wrap gap-2">
                {(["upload", "url"] as const).map((source) => (
                  <button
                    key={source}
                    type="button"
                    onClick={() =>
                      setValidationDataset((prev) => ({
                        ...prev,
                        sourceType: source,
                      }))
                    }
                    className={`inline-flex items-center rounded-md border px-3 py-1.5 text-xs font-semibold uppercase tracking-wide transition ${
                      validationDataset.sourceType === source
                        ? "border-indigo-300 bg-white text-indigo-600 shadow-sm"
                        : "border-slate-200 bg-slate-100 text-slate-500 hover:border-indigo-200 hover:text-indigo-500"
                    }`}
                  >
                    {source === "upload"
                      ? "Upload dataset"
                      : "Reference via URL"}
                  </button>
                ))}
              </div>

              {validationDataset.sourceType === "upload" ? (
                <FileDropzone
                  inputId={validationDatasetFileFieldId}
                  name="validationDatasetFile"
                  emptyState="Drop validation set archive"
                  helperText="Supports the same formats as the training dataset."
                  className="min-h-[160px] bg-white"
                  selectedFileName={validationDataset.fileName}
                  onFileSelect={(file) =>
                    setValidationDataset((prev) => ({
                      ...prev,
                      fileName: file?.name ?? null,
                    }))
                  }
                />
              ) : (
                <div className="space-y-2">
                  <label
                    htmlFor={validationDatasetUrlFieldId}
                    className="text-xs font-semibold uppercase tracking-wide text-slate-500"
                  >
                    Dataset URL
                  </label>
                  <input
                    id={validationDatasetUrlFieldId}
                    name="validationDatasetUrl"
                    type="url"
                    value={validationDataset.url}
                    onChange={(event) =>
                      handleDatasetChange(
                        setValidationDataset,
                        "url",
                        event.target.value
                      )
                    }
                    placeholder="https://storage.example.com/validation_manifest.parquet"
                    className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition placeholder:text-slate-400 focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60"
                  />
                </div>
              )}

              <div className="grid gap-4">
                <div className="space-y-2">
                  <label
                    htmlFor={validationDatasetFormatFieldId}
                    className="text-xs font-semibold uppercase tracking-wide text-slate-500"
                  >
                    Dataset format
                  </label>
                  <SelectDropdown
                    id={validationDatasetFormatFieldId}
                    ariaLabel="Validation dataset format"
                    value={validationDataset.format}
                    onValueChange={(value) =>
                      handleDatasetChange(
                        setValidationDataset,
                        "format",
                        value as DatasetConfig["format"]
                      )
                    }
                    options={datasetFormatOptions.map((option) => ({
                      value: option.value,
                      label: option.label,
                    }))}
                    triggerClassName="w-full"
                  />
                </div>
              </div>
            </div>

            <div className="grid gap-4 rounded-xl border border-slate-200 bg-white p-4 md:grid-cols-3">
              <div className="space-y-2">
                <label
                  htmlFor={hyperparameterEpochsId}
                  className="text-xs font-semibold uppercase tracking-wide text-slate-500"
                >
                  Epochs
                </label>
                <input
                  id={hyperparameterEpochsId}
                  name="trainEpochs"
                  type="number"
                  min={1}
                  value={trainHyperparameters.epochs}
                  onChange={(event) =>
                    setTrainHyperparameters((prev) => ({
                      ...prev,
                      epochs: Number.parseInt(event.target.value, 10) || 1,
                    }))
                  }
                  className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60"
                />
              </div>

              <div className="space-y-2">
                <label
                  htmlFor={hyperparameterBatchSizeId}
                  className="text-xs font-semibold uppercase tracking-wide text-slate-500"
                >
                  Batch size
                </label>
                <input
                  id={hyperparameterBatchSizeId}
                  name="trainBatchSize"
                  type="number"
                  min={1}
                  value={trainHyperparameters.batchSize}
                  onChange={(event) =>
                    setTrainHyperparameters((prev) => ({
                      ...prev,
                      batchSize: Number.parseInt(event.target.value, 10) || 1,
                    }))
                  }
                  className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60"
                />
              </div>

              <div className="space-y-2">
                <label
                  htmlFor={hyperparameterLearningRateId}
                  className="text-xs font-semibold uppercase tracking-wide text-slate-500"
                >
                  Learning rate
                </label>
                <input
                  id={hyperparameterLearningRateId}
                  name="trainLearningRate"
                  type="text"
                  value={trainHyperparameters.learningRate}
                  onChange={(event) =>
                    setTrainHyperparameters((prev) => ({
                      ...prev,
                      learningRate: event.target.value,
                    }))
                  }
                  placeholder="Optional, e.g. 3e-5"
                  className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 shadow-sm transition placeholder:text-slate-400 focus:border-indigo-300 focus:outline-none focus:ring focus:ring-indigo-200/60"
                />
              </div>
            </div>
          </section>
        )}

        <input
          type="hidden"
          name="inferenceBindingsMeta"
          value={JSON.stringify(
            inferenceBindings.map((binding) => ({
              tensorName: binding.tensorName,
              payloadType: binding.payloadType,
              fileName: binding.fileName,
              payload:
                binding.payloadType === "binary"
                  ? undefined
                  : binding.textPayload,
            }))
          )}
        />
        <input
          type="hidden"
          name="trainingDatasetMeta"
          value={JSON.stringify({
            sourceType: trainingDataset.sourceType,
            url: trainingDataset.url,
            fileName: trainingDataset.fileName,
            format: trainingDataset.format,
          })}
        />
        <input
          type="hidden"
          name="validationDatasetMeta"
          value={JSON.stringify({
            sourceType: validationDataset.sourceType,
            url: validationDataset.url,
            fileName: validationDataset.fileName,
            format: validationDataset.format,
          })}
        />
        <input
          type="hidden"
          name="trainHyperparametersMeta"
          value={JSON.stringify(trainHyperparameters)}
        />

        {submissionError ? (
          <div className="rounded-lg border border-rose-300 bg-rose-50/70 px-4 py-3 text-sm text-rose-700 shadow-sm dark:border-rose-900/50 dark:bg-rose-950/50 dark:text-rose-400">
            {submissionError}
          </div>
        ) : null}

        <footer className="flex flex-col gap-3 border-t border-slate-100 pt-6 sm:flex-row sm:items-center sm:justify-end dark:border-slate-700">
          <button
            type="button"
            onClick={onDismiss}
            disabled={isSubmitting}
            className="w-full rounded-lg border border-slate-200 px-4 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-indigo-500 disabled:cursor-not-allowed disabled:bg-indigo-400 sm:w-auto dark:bg-indigo-700 dark:hover:bg-indigo-600 dark:disabled:bg-indigo-800"
          >
            Request execution
          </button>
        </footer>
      </form>
    </DialogShell>
  );
};
