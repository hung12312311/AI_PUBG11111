using AILogic;
using Aimmy2.Class;
using Class;
using InputLogic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;
using Other;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Visuality;
using static AILogic.MathUtil;
using static Other.LogManager;

namespace Aimmy2.AILogic
{
    public class AIManager : IDisposable
    {
        #region Variables

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private int _slot1ImageSize;
        private int _slot2ImageSize;
        public static int ActiveSlot { get; set; } = 1; // Default to Slot 1
        /*
         * Note: ActiveSlot is modified by WeaponSlotManager when keys are pressed.
         * MouseManager will read this to determine which config to use.
         */
        private readonly object _sizeLock = new object();
        private volatile bool _sizeChangePending = false;

        public void RequestSizeChange(int newSize, int slot)
        {
            if (slot == 1) _slot1ImageSize = newSize;
            else _slot2ImageSize = newSize;

            lock (_sizeLock)
            {
                _sizeChangePending = true;
            }
        }

        // Dynamic properties instead of constants
        public int IMAGE_SIZE => ActiveSlot == 1 ? _slot1ImageSize : _slot2ImageSize;
        internal int GetSlotImageSize(int slot) => slot == 1 ? _slot1ImageSize : _slot2ImageSize;

        private void PublishSlotImageSize(int slot, int size, bool dynamicModel)
        {
            string key = slot == 1 ? "Slot 1 Image Size" : "Slot 2 Image Size";
            Dictionary.dropdownState[key] = size.ToString();
            MouseSensitivityProfiles.NotifyChanged();
            if (ActiveSlot == slot)
            {
                FovSettings.Synchronize(size);
                ImageSizeUpdated?.Invoke(size);
            }
            if (slot == 1) { Slot1IsDynamic = dynamicModel; Slot1FixedSize = size; }
            else { Slot2IsDynamic = dynamicModel; Slot2FixedSize = size; }
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "bin", "dropdown.cfg");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(Dictionary.dropdownState, Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex) { Log(LogLevel.Warning, $"Cannot save model image size: {ex.Message}"); }
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var window = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                window?.SettingsMenuControlInstance?.UpdateImageSizeDropdown((string)Dictionary.dropdownState[key], slot);
            }));
        }
        private int NUM_DETECTIONS { get; set; } = 8400; // Will be set dynamically for dynamic models
        private bool IsDynamicModel { get; set; } = false;
        private bool IsNmsFreeModel { get; set; } = false;
        
        private bool _slot1IsNmsFree = false;
        private bool _slot2IsNmsFree = false;
        private int _slot1NumDetections = 8400;
        private int _slot2NumDetections = 8400;
        private int _slot1FixedSize = 640;
        private int _slot2FixedSize = 640;
        private bool _slot1IsDynamic = false;
        private bool _slot2IsDynamic = false;

        public static bool Slot1IsDynamic { get; set; } = true;
        public static bool Slot2IsDynamic { get; set; } = true;
        public static bool CurrentModelIsDynamic { get; set; } = true;

        public static int Slot1FixedSize { get; private set; } = 640;
        public static int Slot2FixedSize { get; private set; } = 640;
        private int NUM_CLASSES { get; set; } = 1;
        private Dictionary<int, string> _modelClasses = new Dictionary<int, string>
        {
            { 0, "enemy" }
        };
        public Dictionary<int, string> ModelClasses => _modelClasses; // apparently this is better than making _modelClasses public
        public static event Action<Dictionary<int, string>>? ClassesUpdated;
        public static event Action<int>? ImageSizeUpdated;
        public static event Action<bool>? DynamicModelStatusChanged;

        private const int SAVE_FRAME_COOLDOWN_MS = 500;

        private DateTime lastSavedTime = DateTime.MinValue;
        private List<string>? _outputNames;
        private RectangleF LastDetectionBox;
        private KalmanPrediction kalmanPrediction;
        private WiseTheFoxPrediction wtfpredictionManager;
        private ConstantAccelerationPrediction caPrediction;

        private byte[]? _bitmapBuffer; // Reusable buffer for bitmap operations

        // Display-aware properties
        private int ScreenWidth => DisplayManager.ScreenWidth;
        private int ScreenHeight => DisplayManager.ScreenHeight;
        private int ScreenLeft => DisplayManager.ScreenLeft;
        private int ScreenTop => DisplayManager.ScreenTop;

        private readonly RunOptions? _modeloptions;
        private InferenceSession? _onnxModel; // Current Active Model
        private InferenceSession? _onnxModelSlot1;
        private InferenceSession? _onnxModelSlot2;

        private TensorRTEngine? _engineModel;
        private TensorRTEngine? _engineModelSlot1;
        private TensorRTEngine? _engineModelSlot2;
        private bool _isEngineModelSlot1 = false;
        private bool _isEngineModelSlot2 = false;
        private bool _isActiveSlotEngine = false;

        private readonly object _modelLock = new object();
        private volatile bool _isDisposed = false;

        private List<string>? _outputNamesSlot1;
        private List<string>? _outputNamesSlot2;


        public bool Slot1AimHead { get; set; } = true;
        public bool Slot2AimHead { get; set; } = true;

        private Dictionary<int, string> _modelClassesSlot1 = new Dictionary<int, string>{{ 0, "enemy" }};
        private Dictionary<int, string> _modelClassesSlot2 = new Dictionary<int, string>{{ 0, "enemy" }};


        private Thread? _aiLoopThread;
        private volatile bool _isAiLoopRunning;

        // For Auto-Labelling Data System
        private bool PlayerFound = false;

        // Store all predictions for overlay rendering
        private List<Prediction>? _allPredictions = null;
        private Rectangle _currentDetectionBox;
        private Action? _pendingWgcOverlay;
        private int _wgcOverlayScheduled;

        // Sticky-Aim
        private Prediction? _currentTarget = null;
        private int _consecutiveFramesWithoutTarget = 0;
        private const int MAX_FRAMES_WITHOUT_TARGET = 3; // Allow 3 frames of target loss

        // Enhanced Sticky Aim State
        private float _lastTargetVelocityX = 0f;
        private float _lastTargetVelocityY = 0f;
        private float _targetLockScore = 0f;           // Accumulated "stickiness" score
        private const float LOCK_SCORE_DECAY = 0.85f;  // Decay per frame when target not matched
        private const float LOCK_SCORE_GAIN = 15f;     // Gain per frame when target matched
        private const float MAX_LOCK_SCORE = 100f;     // Maximum accumulated score
        private const float REFERENCE_TARGET_SIZE = 10000f; // Reference area for "close" targets (approx 100x100)
        private int _framesWithoutMatch = 0;           // Consecutive frames where current target wasn't found
        private DateTime _targetLockedStartTime = DateTime.MinValue; // When the current target was acquired

        private double CenterXTranslated = 0;
        private double CenterYTranslated = 0;

        // Benchmarking
        private int iterationCount = 0;
        private long totalTime = 0;

        private int detectedX { get; set; }
        private int detectedY { get; set; }

        public double AIConf = 0;
        private static int targetX, targetY;

        // Pre-calculated values - now dynamic
        private float _scaleX => ScreenWidth / (float)IMAGE_SIZE;
        private float _scaleY => ScreenHeight / (float)IMAGE_SIZE;

        // Tensor reuse (model inference)
        private DenseTensor<float>? _reusableTensor;
        private float[]? _reusableInputArray;
        private float[]? _tensorBackingArray;
        private List<NamedOnnxValue>? _reusableInputs;

        // Benchmarking
        private readonly Dictionary<string, BenchmarkData> _benchmarks = new();
        private readonly object _benchmarkLock = new();


        private readonly CaptureManager _captureManager = new();
        #endregion Variables

        #region Benchmarking

        private class BenchmarkData
        {
            public long TotalTime { get; set; }
            public int CallCount { get; set; }
            public long MinTime { get; set; } = long.MaxValue;
            public long MaxTime { get; set; }
            public double AverageTime => CallCount > 0 ? (double)TotalTime / CallCount : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDisposable Benchmark(string name)
        {
            if (!(bool)Dictionary.toggleState["Debug Mode"]) return NoopBenchmark.Instance;
            return new BenchmarkScope(this, name);
        }

        private sealed class NoopBenchmark : IDisposable
        {
            public static readonly NoopBenchmark Instance = new();
            public void Dispose() { }
        }

        private class BenchmarkScope : IDisposable
        {
            private readonly AIManager _manager;
            private readonly string _name;
            private readonly Stopwatch _sw;

            public BenchmarkScope(AIManager manager, string name)
            {
                _manager = manager;
                _name = name;
                _sw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                _manager.RecordBenchmark(_name, _sw.ElapsedMilliseconds);
            }
        }

        private void RecordBenchmark(string name, long elapsedMs)
        {
            lock (_benchmarkLock)
            {
                if (!_benchmarks.TryGetValue(name, out var data))
                {
                    data = new BenchmarkData();
                    _benchmarks[name] = data;
                }

                data.TotalTime += elapsedMs;
                data.CallCount++;
                data.MinTime = Math.Min(data.MinTime, elapsedMs);
                data.MaxTime = Math.Max(data.MaxTime, elapsedMs);
            }
        }

        public void PrintBenchmarks()
        {
            lock (_benchmarkLock)
            {
                var lines = new List<string>
                {
                    "=== AIManager Performance Benchmarks ==="
                };

                foreach (var kvp in _benchmarks.OrderBy(x => x.Key))
                {
                    var data = kvp.Value;
                    lines.Add($"{kvp.Key}: Avg={data.AverageTime:F2}ms, Min={data.MinTime}ms, Max={data.MaxTime}ms, Count={data.CallCount}");
                }

                lines.Add($"Overall FPS: {(iterationCount > 0 ? 1000.0 / (totalTime / (double)iterationCount) : 0):F2}");

                //File.WriteAllLines("AIManager_Benchmarks.txt", lines);

                Log(LogLevel.Info, string.Join(Environment.NewLine, lines));
            }
        }

        #endregion Benchmarking

        public Task Initialization { get; private set; }

        public AIManager(string modelPath)
        {
            // Initialize the cached image size
            _slot1ImageSize = int.Parse(Dictionary.dropdownState["Slot 1 Image Size"]);
            _slot2ImageSize = int.Parse(Dictionary.dropdownState["Slot 2 Image Size"]);

            // Load priority aiming settings from Dictionary
            Slot1AimHead = Dictionary.toggleState.TryGetValue("Slot 1 Priority Aiming", out var s1ah) ? (bool)s1ah : true;
            Slot2AimHead = Dictionary.toggleState.TryGetValue("Slot 2 Priority Aiming", out var s2ah) ? (bool)s2ah : true;

            // Initialize DXGI capture for current display
            if (Dictionary.dropdownState["Screen Capture Method"] == "DirectX")
            {
                try
                {
                    _captureManager.InitializeDxgiDuplication();
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Failed to initialize Screen capture via DirectX: {ex.Message}. Falling back to GDI+.");
                    Dictionary.dropdownState["Screen Capture Method"] = "GDI+";
                    SaveDictionary.WriteJSON(Dictionary.dropdownState, "bin\\dropdown.cfg");

                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                        if (mainWindow?.uiManager?.D_ScreenCaptureMethod?.DropdownBox != null)
                        {
                            var dropdown = mainWindow.uiManager.D_ScreenCaptureMethod;
                            for (int i = 0; i < dropdown.DropdownBox.Items.Count; i++)
                            {
                                if ((dropdown.DropdownBox.Items[i] as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() == "GDI+")
                                {
                                    dropdown.DropdownBox.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                    });
                }
            }

            kalmanPrediction = new KalmanPrediction();
            wtfpredictionManager = new WiseTheFoxPrediction();
            caPrediction = new ConstantAccelerationPrediction();

            _modeloptions = new RunOptions();

            var sessionOptions = new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = false,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = 4
            };

            // Attempt to load via DirectML (else fallback to CPU)
            Initialization = InitializeModel(sessionOptions, modelPath);
        }

        #region Models

        private async Task InitializeModel(SessionOptions sessionOptions, string modelPath)
        {
            using (Benchmark("ModelInitialization"))
            {
                try
                {
                    await LoadModelAsync(sessionOptions, modelPath, useDirectML: true);
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Error starting the model via DirectML: {ex.Message}\n\nFalling back to CPU, performance may be poor.", true);

                    try
                    {
                        await LoadModelAsync(sessionOptions, modelPath, useDirectML: false);
                    }
                    catch (Exception e)
                    {
                        Log(LogLevel.Error, $"Error starting the model via CPU: {e.Message}, you won't be able to aim assist at all.", true);
                    }
                }
            }
        }

        private static async Task<bool> ValidateTensorRTEngineSubprocess(string modelPath)
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    return true;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--validate-engine \"{modelPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    ErrorDialog = false
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        return process.ExitCode == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Validation process failed to start: {ex.Message}");
            }
            return false;
        }

        public Task LoadModelAsync(string modelPath) => LoadModelAsync(new SessionOptions(), modelPath, Dictionary.toggleState["DirectML"]);

        public async Task LoadModelAsync(SessionOptions sessionOptions, string modelPath, bool useDirectML)
        {
            try
            {
                bool isEngine = modelPath.EndsWith(".engine", StringComparison.OrdinalIgnoreCase) || 
                                modelPath.EndsWith(".trt", StringComparison.OrdinalIgnoreCase);

                if (isEngine)
                {
                    // Validate engine file first in a subprocess to avoid native crash on incompatible GPU architecture
                    bool isValid = await ValidateTensorRTEngineSubprocess(modelPath);
                    if (!isValid)
                    {
                        throw new Exception("Kiến trúc của GPU không tương thích với file engine đã chọn. Vui lòng chọn đúng file engine dành cho GPU này.");
                    }

                    TensorRTEngine newEngine;
                    await Dictionary.ModelLoadSemaphore.WaitAsync();
                    try
                    {
                        newEngine = await Task.Run(() => new TensorRTEngine(modelPath));
                    }
                    finally
                    {
                        Dictionary.ModelLoadSemaphore.Release();
                    }

                    lock (_modelLock)
                    {
                        if (_isDisposed)
                        {
                            newEngine.Dispose();
                            return;
                        }

                        var oldEngine = _engineModelSlot1;
                        var oldSession = _onnxModelSlot1;
                        bool wasActiveSlot = ActiveSlot == 1 || (oldSession == null && oldEngine == null);

                        _engineModelSlot1 = newEngine;
                        _isEngineModelSlot1 = true;
                        _onnxModelSlot1 = null;

                        if (wasActiveSlot)
                        {
                            _engineModel = _engineModelSlot1;
                            _onnxModel = null;
                            _isActiveSlotEngine = true;
                            ActiveSlot = 1;
                        }

                        int newSize = 640;
                        if (newEngine.InputDims.Length >= 4)
                        {
                            newSize = newEngine.InputDims[2];
                        }
                        _slot1ImageSize = newSize;

                        _slot1IsNmsFree = false;
                        _slot1NumDetections = 8400;
                        if (newEngine.OutputDims.Length >= 3)
                        {
                            if (newEngine.OutputDims[2] == 6)
                            {
                                _slot1NumDetections = newEngine.OutputDims[1];
                                _slot1IsNmsFree = true;
                            }
                            else
                            {
                                _slot1NumDetections = newEngine.OutputDims[2];
                            }
                        }

                        _slot1IsDynamic = false;
                        _slot1FixedSize = newSize;
                        PublishSlotImageSize(1, newSize, false);

                        if (wasActiveSlot)
                        {
                            ImageSizeUpdated?.Invoke(newSize);
                            NUM_DETECTIONS = _slot1NumDetections;
                            IsNmsFreeModel = _slot1IsNmsFree;
                            IsDynamicModel = false;
                            CurrentModelIsDynamic = false;
                            DynamicModelStatusChanged?.Invoke(false);
                        }

                        _modelClassesSlot1 = LoadClassesForEngine(modelPath, newEngine.OutputDims);
                        if (wasActiveSlot)
                        {
                            _modelClasses = _modelClassesSlot1;
                            NUM_CLASSES = _modelClasses.Count > 0 ? _modelClasses.Keys.Max() + 1 : 1;
                            ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
                        }

                        oldSession?.Dispose();
                        oldEngine?.Dispose();

                        _bitmapBuffer = new byte[3 * IMAGE_SIZE * IMAGE_SIZE];
                    }
                    Log(LogLevel.Info, $"Loaded (Slot 1) TensorRT Engine model: {Path.GetFileName(modelPath)} ({(_slot1IsNmsFree ? "NMS-Free" : "Standard")})", true, 2000);
                }
                else
                {
                    if (useDirectML) { sessionOptions.AppendExecutionProvider_DML(); }
                    else { sessionOptions.AppendExecutionProvider_CPU(); }

                    InferenceSession newSession;
                    await Dictionary.ModelLoadSemaphore.WaitAsync();
                    try
                    {
                        newSession = await Task.Run(() => new InferenceSession(modelPath, sessionOptions));
                    }
                    finally
                    {
                        Dictionary.ModelLoadSemaphore.Release();
                    }
                    
                    lock (_modelLock)
                    {
                        if (_isDisposed)
                        {
                            newSession.Dispose();
                            return;
                        }

                        var oldEngine = _engineModelSlot1;
                        var oldSession = _onnxModelSlot1;
                        var oldOutputNames = _outputNamesSlot1;
                        bool wasActiveSlot = ActiveSlot == 1 || (oldSession == null && oldEngine == null);

                        _onnxModelSlot1 = newSession;
                        _isEngineModelSlot1 = false;
                        _engineModelSlot1 = null;
                        _outputNamesSlot1 = new List<string>(newSession.OutputMetadata.Keys);

                        if (wasActiveSlot)
                        {
                            _onnxModel = _onnxModelSlot1;
                            _engineModel = null;
                            _isActiveSlotEngine = false;
                            _outputNames = _outputNamesSlot1;
                            ActiveSlot = 1;
                        }

                        LoadClasses(1);

                        // Validate the onnx model output shape before disposing the previous valid session.
                        if (!ValidateOnnxShape(1))
                        {
                            newSession.Dispose();
                            _onnxModelSlot1 = oldSession;
                            _outputNamesSlot1 = oldOutputNames;

                            if (wasActiveSlot)
                            {
                                _onnxModel = oldSession;
                                _outputNames = oldOutputNames;
                            }

                            if (oldSession != null)
                            {
                                LoadClasses(1);
                                SetActiveSlot(1);
                            }
                            else
                            {
                                _modelClassesSlot1 = new Dictionary<int, string> { { 0, "enemy" } };
                                _modelClasses = _modelClassesSlot1;
                            }

                            return;
                        }

                        oldSession?.Dispose();
                        oldEngine?.Dispose();

                        // Pre-allocate bitmap buffer
                        _bitmapBuffer = new byte[3 * IMAGE_SIZE * IMAGE_SIZE];
                    }
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading the model: {ex.Message}", true);
                return;
            }

            // Begin the loop
            if (!_isAiLoopRunning)
            {
                _isAiLoopRunning = true;
                lock (_sizeLock) { _sizeChangePending = false; } // Clear flag
                _aiLoopThread = new Thread(AiLoop)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal // Higher priority for AI thread
                };
                _aiLoopThread.Start();
            }
            return;
        }

        private bool ValidateOnnxShape(int slot, bool showNotification = true)
        {
            var targetSession = (slot == 2) ? _onnxModelSlot2 : _onnxModelSlot1;
            if (targetSession == null) return false;

            var inputMetadata = targetSession.InputMetadata;
            var outputMetadata = targetSession.OutputMetadata;

            Log(LogLevel.Info, $"=== Model Metadata (Slot {slot}) ===");
            Log(LogLevel.Info, "Input Metadata:");

            bool isDynamic = false;
            int fixedInputSize = 0;

            foreach (var kvp in inputMetadata)
            {
                string dimensionsStr = string.Join("x", kvp.Value.Dimensions);
                Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}");

                if (kvp.Value.Dimensions.Any(d => d == -1)) isDynamic = true;
                else if (kvp.Value.Dimensions.Length == 4) fixedInputSize = kvp.Value.Dimensions[2];
            }

            Log(LogLevel.Info, "Output Metadata:");
            foreach (var kvp in outputMetadata)
            {
                string dimensionsStr = string.Join("x", kvp.Value.Dimensions);
                Log(LogLevel.Info, $"  Name: {kvp.Key}, Dimensions: {dimensionsStr}");
            }

            if (slot == 1) { _slot1IsDynamic = isDynamic; Slot1IsDynamic = isDynamic; }
            else { _slot2IsDynamic = isDynamic; Slot2IsDynamic = isDynamic; }

            if (ActiveSlot == slot) {
                IsDynamicModel = isDynamic;
                CurrentModelIsDynamic = isDynamic;
                DynamicModelStatusChanged?.Invoke(isDynamic);
            }

            if (isDynamic)
            {
                PublishSlotImageSize(slot, slot == 1 ? _slot1ImageSize : _slot2ImageSize, true);
                int detections = CalculateNumDetections(IMAGE_SIZE);
                if (slot == 1) { _slot1NumDetections = detections; _slot1IsNmsFree = false; }
                else { _slot2NumDetections = detections; _slot2IsNmsFree = false; }
                
                if (ActiveSlot == slot) {
                    NUM_DETECTIONS = detections;
                    IsNmsFreeModel = false;
                    ImageSizeUpdated?.Invoke(IMAGE_SIZE);
                    Log(LogLevel.Info, $"Loaded (Slot {slot}) Dynamic Standard model: {IMAGE_SIZE}x{IMAGE_SIZE}", showNotification, 3000);
                }
            }
            else
            {
                if (slot == 1) { _slot1FixedSize = fixedInputSize; Slot1FixedSize = fixedInputSize; } 
                else { _slot2FixedSize = fixedInputSize; Slot2FixedSize = fixedInputSize; }

                var supportedSizes = new[] { "640", "512", "416", "320", "256", "160" };
                var fixedSizeStr = fixedInputSize.ToString();

                int currentSlotSize = (slot == 1) ? _slot1ImageSize : _slot2ImageSize;
                if (fixedInputSize != currentSlotSize && supportedSizes.Contains(fixedSizeStr))
                {
                    Log(LogLevel.Warning, $"Fixed-size model (Slot {slot}) expects {fixedInputSize}x{fixedInputSize}. Forcing size adjustment.", showNotification, 3000);
                    Dictionary.dropdownState[slot == 1 ? "Slot 1 Image Size" : "Slot 2 Image Size"] = fixedSizeStr;
                    
                    // Update internal field so we don't keep loop-resetting
                    if (slot == 1) _slot1ImageSize = fixedInputSize; else _slot2ImageSize = fixedInputSize;

                    Application.Current?.Dispatcher.BeginInvoke(() => {
                        try {
                            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                            mainWindow?.SettingsMenuControlInstance?.UpdateImageSizeDropdown(fixedSizeStr, slot);
                        } catch { }
                    });
                }
                else if (!supportedSizes.Contains(fixedSizeStr))
                {
                    Log(LogLevel.Error, $"Model (Slot {slot}) requires unsupported size {fixedInputSize}x{fixedInputSize}.", showNotification, 10000);
                    return false;
                }

                PublishSlotImageSize(slot, fixedInputSize, false);
                LoadClasses(slot);

                var temp_num_classes = 1;
                var targetDict = (slot == 2) ? _modelClassesSlot2 : _modelClassesSlot1;
                temp_num_classes = targetDict.Count > 0 ? targetDict.Keys.Max() + 1 : 1;

                int detections_v8 = CalculateNumDetections(fixedInputSize);
                var shape_v8 = new int[] { 1, 4 + temp_num_classes, detections_v8 };
                var shape_v10 = new int[] { 1, 300, 6 };

                bool isV8 = outputMetadata.Values.All(m => m.Dimensions.SequenceEqual(shape_v8));
                bool isV10 = outputMetadata.Values.All(m => m.Dimensions.SequenceEqual(shape_v10));

                if (isV10)
                {
                    if (slot == 1) { _slot1NumDetections = 300; _slot1IsNmsFree = true; }
                    else { _slot2NumDetections = 300; _slot2IsNmsFree = true; }
                    Log(LogLevel.Info, $"Detected (Slot {slot}) NMS-Free Architecture.", false, 3000);
                }
                else if (isV8)
                {
                    if (slot == 1) { _slot1NumDetections = detections_v8; _slot1IsNmsFree = false; }
                    else { _slot2NumDetections = detections_v8; _slot2IsNmsFree = false; }
                }
                else
                {
                    Log(LogLevel.Error, $"Output shape mismatch (Slot {slot}). Use YOLOv8 or YOLOv10/11/26 ONNX.", showNotification, 10000);
                    return false;
                }

                if (ActiveSlot == slot) {
                    NUM_DETECTIONS = (slot == 1) ? _slot1NumDetections : _slot2NumDetections;
                    IsNmsFreeModel = (slot == 1) ? _slot1IsNmsFree : _slot2IsNmsFree;
                }

                bool nmsFree = (slot == 1) ? _slot1IsNmsFree : _slot2IsNmsFree;
                Log(LogLevel.Info, $"Loaded (Slot {slot}) {(nmsFree ? "NMS-Free" : "Standard (NMS)")} model: {fixedInputSize}x{fixedInputSize}", showNotification, 2000);
            }

            if (ActiveSlot == slot) {
                CurrentModelIsDynamic = isDynamic;
                DynamicModelStatusChanged?.Invoke(IsDynamicModel);
            }
            return true;
        }

        private void LoadClasses(int slot = 0)
        {
            var model = (slot == 2) ? _onnxModelSlot2 : _onnxModelSlot1;
            if (model == null && slot != 2) model = _onnxModel;
            if (model == null) return;

            var targetDict = (slot == 2) ? _modelClassesSlot2 : _modelClassesSlot1;
            targetDict.Clear();

            try
            {
                var metadata = model.ModelMetadata;

                if (metadata != null && 
                    metadata.CustomMetadataMap.TryGetValue("names", out string? value) &&
                    !string.IsNullOrEmpty(value))
                {
                    JObject data = JObject.Parse(value);
                    if (data != null && data.Type == JTokenType.Object)
                    {
                        foreach (var item in data)
                        {
                            if (int.TryParse(item.Key, out int classId) && item.Value.Type == JTokenType.String)
                            {
                                targetDict[classId] = item.Value.ToString();
                            }
                        }
                        
                        // Update NUM_CLASSES if this is the active slot
                        if ((slot == 2 && ActiveSlot == 2) || (slot != 2 && ActiveSlot == 1))
                        {
                             NUM_CLASSES = targetDict.Count > 0 ? targetDict.Keys.Max() + 1 : 1;
                             _modelClasses = targetDict;
                        }

                        Log(LogLevel.Info, $"Loaded {targetDict.Count} class(es) from model metadata (Slot {(slot == 0 ? 1 : slot)}): {data.ToString(Newtonsoft.Json.Formatting.None)}", false);
                    }
                    else
                    {
                        Log(LogLevel.Error, "Model metadata 'names' field is not a valid JSON object.", true);
                    }
                }
                else
                {
                    Log(LogLevel.Error, "Model metadata does not contain 'names' field for classes.", true);
                }
                
                if ((slot == 2 && ActiveSlot == 2) || (slot != 2 && ActiveSlot == 1))
                {
                    ClassesUpdated?.Invoke(new Dictionary<int, string>(targetDict));
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Error loading classes: {ex.Message}", true);
            }
        }

        private Dictionary<int, string> LoadClassesForEngine(string enginePath, int[] outputDims)
        {
            var classes = new Dictionary<int, string>();
            
            // 1. Try JSON file of the same name (e.g. mymodel.json next to mymodel.engine)
            string jsonPath = Path.ChangeExtension(enginePath, ".json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    JToken token = JToken.Parse(jsonContent);
                    if (token is JObject obj)
                    {
                        if (obj.TryGetValue("names", out var namesProp) && namesProp != null)
                        {
                            if (namesProp is JObject namesObj)
                            {
                                foreach (var item in namesObj)
                                {
                                    if (int.TryParse(item.Key, out int classId))
                                    {
                                        classes[classId] = item.Value?.ToString() ?? $"Class_{classId}";
                                    }
                                }
                            }
                            else if (namesProp is JArray namesArr)
                            {
                                for (int i = 0; i < namesArr.Count; i++)
                                {
                                    classes[i] = namesArr[i].ToString();
                                }
                            }
                        }
                        else
                        {
                            foreach (var item in obj)
                            {
                                if (int.TryParse(item.Key, out int classId))
                                {
                                    classes[classId] = item.Value?.ToString() ?? $"Class_{classId}";
                                }
                            }
                        }
                    }
                    else if (token is JArray arr)
                    {
                        for (int i = 0; i < arr.Count; i++)
                        {
                            classes[i] = arr[i].ToString();
                        }
                    }
                    
                    if (classes.Count > 0)
                    {
                        Log(LogLevel.Info, $"Loaded {classes.Count} class(es) from JSON: {jsonPath}", false);
                        return classes;
                    }
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Warning, $"Failed to parse classes JSON {jsonPath}: {ex.Message}");
                }
            }

            // 2. Try TXT file of the same name (e.g. mymodel.txt containing one class per line)
            string txtPath = Path.ChangeExtension(enginePath, ".txt");
            if (File.Exists(txtPath))
            {
                try
                {
                    var lines = File.ReadAllLines(txtPath)
                                    .Select(l => l.Trim())
                                    .Where(l => !string.IsNullOrEmpty(l))
                                    .ToList();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        classes[i] = lines[i];
                    }
                    
                    if (classes.Count > 0)
                    {
                        Log(LogLevel.Info, $"Loaded {classes.Count} class(es) from TXT: {txtPath}", false);
                        return classes;
                    }
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Warning, $"Failed to read classes TXT {txtPath}: {ex.Message}");
                }
            }

            // 3. Try ONNX file of the same name (e.g. mymodel.onnx to extract metadata)
            string onnxPath = Path.ChangeExtension(enginePath, ".onnx");
            if (File.Exists(onnxPath))
            {
                try
                {
                    var opt = new SessionOptions();
                    opt.AppendExecutionProvider_CPU();
                    using (var session = new InferenceSession(onnxPath, opt))
                    {
                        var metadata = session.ModelMetadata;
                        if (metadata != null && 
                            metadata.CustomMetadataMap.TryGetValue("names", out string? value) &&
                            !string.IsNullOrEmpty(value))
                        {
                            JObject data = JObject.Parse(value);
                            if (data != null && data.Type == JTokenType.Object)
                            {
                                foreach (var item in data)
                                {
                                    if (int.TryParse(item.Key, out int classId) && item.Value?.Type == JTokenType.String)
                                    {
                                        classes[classId] = item.Value.ToString();
                                    }
                                }
                            }
                        }
                    }
                    
                    if (classes.Count > 0)
                    {
                        Log(LogLevel.Info, $"Loaded {classes.Count} class(es) from ONNX metadata: {onnxPath}", false);
                        return classes;
                    }
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Warning, $"Failed to read classes from ONNX metadata {onnxPath}: {ex.Message}");
                }
            }

            // 4. Try classes.txt in the same directory
            string dir = Path.GetDirectoryName(enginePath) ?? "";
            string commonTxtPath = Path.Combine(dir, "classes.txt");
            if (File.Exists(commonTxtPath))
            {
                try
                {
                    var lines = File.ReadAllLines(commonTxtPath)
                                    .Select(l => l.Trim())
                                    .Where(l => !string.IsNullOrEmpty(l))
                                    .ToList();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        classes[i] = lines[i];
                    }
                    
                    if (classes.Count > 0)
                    {
                        Log(LogLevel.Info, $"Loaded {classes.Count} class(es) from classes.txt: {commonTxtPath}", false);
                        return classes;
                    }
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Warning, $"Failed to read classes.txt {commonTxtPath}: {ex.Message}");
                }
            }

            // 5. Default mapping based on output tensor dimensions
            int calculatedClasses = 1;
            if (outputDims != null)
            {
                if (outputDims.Length == 3)
                {
                    if (outputDims[2] == 6)
                    {
                        calculatedClasses = 2; // Default to 2 classes to support head priority aiming
                    }
                    else if (outputDims[1] > 4)
                    {
                        calculatedClasses = outputDims[1] - 4;
                    }
                }
            }

            if (calculatedClasses == 2)
            {
                classes[0] = "head";
                classes[1] = "body";
                Log(LogLevel.Info, $"No class names file found. Defaulting to 2 classes: 0=head, 1=body", false);
            }
            else
            {
                for (int i = 0; i < calculatedClasses; i++)
                {
                    classes[i] = i == 0 ? "enemy" : $"Class_{i}";
                }
                Log(LogLevel.Info, $"No class names file found. Defaulted to {calculatedClasses} generic classes (e.g. 0=enemy)", false);
            }

            return classes;
        }

        public async Task LoadSecondaryModel(string modelPath, bool showNotification = true)
        {
             bool isEngine = modelPath.EndsWith(".engine", StringComparison.OrdinalIgnoreCase) || 
                             modelPath.EndsWith(".trt", StringComparison.OrdinalIgnoreCase);

             if (isEngine)
             {
                 // Validate engine file first in a subprocess to avoid native crash on incompatible GPU architecture
                 bool isValid = await ValidateTensorRTEngineSubprocess(modelPath);
                 if (!isValid)
                 {
                     Log(LogLevel.Error, "Kiến trúc của GPU không tương thích với file engine đã chọn. Vui lòng chọn đúng file engine dành cho GPU này.", showNotification);
                     return;
                 }

                 TensorRTEngine? newEngine = null;
                 await Dictionary.ModelLoadSemaphore.WaitAsync();
                 try
                 {
                     newEngine = await Task.Run(() => new TensorRTEngine(modelPath));
                 }
                 catch (Exception ex)
                 {
                     Log(LogLevel.Error, $"Error loading Slot 2 TensorRT model: {ex.Message}", true);
                     return;
                 }
                 finally
                 {
                     Dictionary.ModelLoadSemaphore.Release();
                 }

                 lock (_modelLock)
                 {
                     if (_isDisposed)
                     {
                         newEngine?.Dispose();
                         return;
                     }

                     var oldEngine = _engineModelSlot2;
                     var oldSession = _onnxModelSlot2;
                     bool wasActiveSlot = ActiveSlot == 2 || (oldSession == null && oldEngine == null);

                     _engineModelSlot2 = newEngine;
                     _isEngineModelSlot2 = true;
                     _onnxModelSlot2 = null;

                     if (wasActiveSlot)
                     {
                         _engineModel = _engineModelSlot2;
                         _onnxModel = null;
                         _isActiveSlotEngine = true;
                         ActiveSlot = 2;
                     }

                     int newSize = 640;
                     if (newEngine != null && newEngine.InputDims.Length >= 4)
                     {
                         newSize = newEngine.InputDims[2];
                     }
                     _slot2ImageSize = newSize;

                      _slot2IsNmsFree = false;
                      _slot2NumDetections = 8400;
                      if (newEngine != null && newEngine.OutputDims.Length >= 3)
                      {
                          if (newEngine.OutputDims[2] == 6)
                          {
                              _slot2NumDetections = newEngine.OutputDims[1];
                              _slot2IsNmsFree = true;
                          }
                          else
                          {
                              _slot2NumDetections = newEngine.OutputDims[2];
                          }
                      }

                     _slot2IsDynamic = false;
                     _slot2FixedSize = newSize;
                     PublishSlotImageSize(2, newSize, false);

                     if (wasActiveSlot)
                     {
                         ImageSizeUpdated?.Invoke(newSize);
                         NUM_DETECTIONS = _slot2NumDetections;
                         IsNmsFreeModel = _slot2IsNmsFree;
                         IsDynamicModel = false;
                         CurrentModelIsDynamic = false;
                         DynamicModelStatusChanged?.Invoke(false);
                     }

                     _modelClassesSlot2 = LoadClassesForEngine(modelPath, newEngine.OutputDims);
                     if (wasActiveSlot)
                     {
                         _modelClasses = _modelClassesSlot2;
                         NUM_CLASSES = _modelClasses.Count > 0 ? _modelClasses.Keys.Max() + 1 : 1;
                         ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
                     }

                     oldSession?.Dispose();
                     oldEngine?.Dispose();
                 }
                 Log(LogLevel.Info, $"Loaded (Slot 2) TensorRT Engine model: {Path.GetFileName(modelPath)} ({(_slot2IsNmsFree ? "NMS-Free" : "Standard")})", showNotification, 2000);
             }
             else
             {
                 var sessionOptions = new SessionOptions
                 {
                     EnableCpuMemArena = true,
                     EnableMemoryPattern = false,
                     GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                     ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                     InterOpNumThreads = 1,
                     IntraOpNumThreads = 4
                 };

                 InferenceSession? newSession = null;
                 await Dictionary.ModelLoadSemaphore.WaitAsync();
                 try
                 {
                     // Try DirectML
                     try {
                         sessionOptions.AppendExecutionProvider_DML();
                         newSession = await Task.Run(() => new InferenceSession(modelPath, sessionOptions));
                     } catch {
                         sessionOptions.AppendExecutionProvider_CPU();
                         newSession = await Task.Run(() => new InferenceSession(modelPath, sessionOptions));
                     }
                 }
                 finally
                 {
                     Dictionary.ModelLoadSemaphore.Release();
                 }

                 lock (_modelLock)
                 {
                     if (_isDisposed)
                     {
                         newSession?.Dispose();
                         return;
                     }

                     var oldEngine = _engineModelSlot2;
                     var oldSession = _onnxModelSlot2;
                     var oldOutputNames = _outputNamesSlot2;
                     _onnxModelSlot2 = newSession;
                     _isEngineModelSlot2 = false;
                     _engineModelSlot2 = null;

                     if (_onnxModelSlot2 != null)
                     {
                         _outputNamesSlot2 = new List<string>(_onnxModelSlot2.OutputMetadata.Keys);
                         if (!ValidateOnnxShape(2))
                         {
                             _onnxModelSlot2.Dispose();
                             _onnxModelSlot2 = oldSession;
                             _outputNamesSlot2 = oldOutputNames;

                             if (ActiveSlot == 2)
                             {
                                 _onnxModel = oldSession;
                                 _outputNames = oldOutputNames;
                                 _isActiveSlotEngine = false;
                                 if (oldSession != null)
                                 {
                                     LoadClasses(2);
                                     SetActiveSlot(2);
                                 }
                             }

                             return;
                         }
                     }

                     oldSession?.Dispose();
                     oldEngine?.Dispose();

                     if (ActiveSlot == 2)
                     {
                         _onnxModel = _onnxModelSlot2;
                         _engineModel = null;
                         _isActiveSlotEngine = false;
                         _outputNames = _outputNamesSlot2;
                     }
                 }

                 LoadClasses(2);
                 Log(LogLevel.Info, $"Secondary Model (Slot 2) Loaded: {Path.GetFileName(modelPath)}", showNotification);
             }
        }

        public void SetActiveSlot(int slot)
        {
            lock (_modelLock)
            {
                if (_isDisposed) return;

                if (slot == 1)
                {
                    if (_isEngineModelSlot1 && _engineModelSlot1 != null)
                    {
                        _engineModel = _engineModelSlot1;
                        _onnxModel = null;
                        _modelClasses = _modelClassesSlot1;
                        _isActiveSlotEngine = true;
                        ActiveSlot = 1;

                        IsNmsFreeModel = _slot1IsNmsFree;
                        NUM_DETECTIONS = _slot1NumDetections;
                        IsDynamicModel = _slot1IsDynamic;
                        CurrentModelIsDynamic = _slot1IsDynamic;

                        NUM_CLASSES = _modelClasses.Count > 0 ? _modelClasses.Keys.Max() + 1 : 1;
                        Slot1AimHead = Dictionary.toggleState.TryGetValue("Slot 1 Priority Aiming", out var s1ah) ? (bool)s1ah : true;

                        ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
                        DynamicModelStatusChanged?.Invoke(IsDynamicModel);
                    }
                    else if (_onnxModelSlot1 != null) {
                        _onnxModel = _onnxModelSlot1;
                        _engineModel = null;
                        _isActiveSlotEngine = false;
                        _modelClasses = _modelClassesSlot1;
                        _outputNames = _outputNamesSlot1;
                        ActiveSlot = 1;
                        
                        IsNmsFreeModel = _slot1IsNmsFree;
                        NUM_DETECTIONS = _slot1NumDetections;
                        IsDynamicModel = _slot1IsDynamic;
                        CurrentModelIsDynamic = _slot1IsDynamic;

                        NUM_CLASSES = _modelClasses.Count > 0 ? _modelClasses.Keys.Max() + 1 : 1;
                        Slot1AimHead = Dictionary.toggleState.TryGetValue("Slot 1 Priority Aiming", out var s1ah) ? (bool)s1ah : true;

                        ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
                        DynamicModelStatusChanged?.Invoke(IsDynamicModel);
                    }
                }
                else if (slot == 2)
                {
                    if (_isEngineModelSlot2 && _engineModelSlot2 != null)
                    {
                        _engineModel = _engineModelSlot2;
                        _onnxModel = null;
                        _modelClasses = _modelClassesSlot2;
                        _isActiveSlotEngine = true;
                        ActiveSlot = 2;

                        IsNmsFreeModel = _slot2IsNmsFree;
                        NUM_DETECTIONS = _slot2NumDetections;
                        IsDynamicModel = _slot2IsDynamic;
                        CurrentModelIsDynamic = _slot2IsDynamic;

                        NUM_CLASSES = _modelClasses.Count > 0 ? _modelClasses.Keys.Max() + 1 : 1;
                        Slot2AimHead = Dictionary.toggleState.TryGetValue("Slot 2 Priority Aiming", out var s2ah) ? (bool)s2ah : true;

                        ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
                        DynamicModelStatusChanged?.Invoke(IsDynamicModel);
                    }
                    else if (_onnxModelSlot2 != null) {
                        _onnxModel = _onnxModelSlot2;
                        _engineModel = null;
                        _isActiveSlotEngine = false;
                        _modelClasses = _modelClassesSlot2;
                        _outputNames = _outputNamesSlot2;
                        ActiveSlot = 2;

                        IsNmsFreeModel = _slot2IsNmsFree;
                        NUM_DETECTIONS = _slot2NumDetections;
                        IsDynamicModel = _slot2IsDynamic;
                        CurrentModelIsDynamic = _slot2IsDynamic;

                        NUM_CLASSES = _modelClasses.Count > 0 ? _modelClasses.Keys.Max() + 1 : 1;
                        Slot2AimHead = Dictionary.toggleState.TryGetValue("Slot 2 Priority Aiming", out var s2ah) ? (bool)s2ah : true;

                        ClassesUpdated?.Invoke(new Dictionary<int, string>(_modelClasses));
                        DynamicModelStatusChanged?.Invoke(IsDynamicModel);
                    }
                    else
                    {
                        Log(LogLevel.Warning, "Attempted to switch to Slot 2 but no model loaded. Keeping Slot 1.");
                    }
                }
            }
            FovSettings.Synchronize(IMAGE_SIZE);
            ImageSizeUpdated?.Invoke(IMAGE_SIZE);
        }

        #endregion Models

        #region AI

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldPredict() =>
            Dictionary.toggleState["Show Detected Player"] ||
            Dictionary.toggleState["Constant AI Tracking"] ||
            InputBindingManager.IsHoldingBinding("Aim Keybind") ||
            InputBindingManager.IsHoldingBinding("Second Aim Keybind");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldProcess() =>
            Dictionary.toggleState["Aim Assist"] ||
            Dictionary.toggleState["Show Detected Player"] ||
            Dictionary.toggleState["Auto Trigger"];

        private void AiLoop()
        {
            Stopwatch stopwatch = new();
            // Resolve this inside the loop to avoid race conditions with initialization
            DetectedPlayerWindow? DetectedPlayerOverlay = null;

            while (_isAiLoopRunning)
            {
                // Check for pending size changes at the start of each iteration
                lock (_sizeLock)
                {
                    if (_sizeChangePending)
                    {
                        // Skip this iteration to allow clean shutdown
                        continue;
                    }
                }

                stopwatch.Restart();

                // Handle any pending display changes
                _captureManager.HandlePendingDisplayChanges();

                using (Benchmark("AILoopIteration"))
                {
                    try
                    {
                        UpdateFOV();

                        if (ShouldProcess())
                        {
                            // Try to get overlay if we don't have it yet
                            if (DetectedPlayerOverlay == null)
                            {
                                DetectedPlayerOverlay = Dictionary.DetectedPlayerOverlay;
                            }

                            if (ShouldPredict())
                            {
                                Prediction? closestPrediction;
                                using (Benchmark("GetClosestPrediction"))
                                {
                                    closestPrediction = GetClosestPredictionSync();
                                }

                                if ((string)Dictionary.dropdownState["Screen Capture Method"] == "WGC"
                                    && _captureManager.LastCaptureWaitingForFrame)
                                {
                                    // No new observation: do not infer/move again from a stale frame.
                                    // Back off only while WGC has nothing available, not an FPS cap.
                                    Thread.Sleep(1);
                                    continue;
                                }
                                // Update overlay logic - decoupled from whether we have a closest prediction
                                if (Dictionary.toggleState["Show Detected Player"] && DetectedPlayerOverlay != null)
                                {
                                    using (Benchmark("UpdateOverlay"))
                                    {
                                        // UpdateOverlay now handles its own internal checks and null closestPrediction
                                        UpdateOverlay(DetectedPlayerOverlay, closestPrediction);
                                    }
                                }

                                if (closestPrediction == null)
                                {
                                    // Only disable if we actually don't have ANY predictions to show
                                    if ((_allPredictions == null || _allPredictions.Count == 0) && DetectedPlayerOverlay != null)
                                    {
                                        DisableOverlay(DetectedPlayerOverlay);
                                    }
                                }
                                else
                                {
                                    using (Benchmark("AutoTrigger"))
                                    {
                                        AutoTrigger();
                                    }

                                    using (Benchmark("CalculateCoordinates"))
                                    {
                                        // CalculateCoordinates still handles aiming logic
                                        CalculateCoordinates(DetectedPlayerOverlay, closestPrediction, _scaleX, _scaleY);
                                    }

                                    using (Benchmark("HandleAim"))
                                    {
                                        HandleAim(closestPrediction);
                                    }

                                    totalTime += stopwatch.ElapsedMilliseconds;
                                    iterationCount++;
                                }
                            }
                            else
                            {
                                // Processing so we are at the ready but not holding right/click.
                                Thread.Sleep(1);
                            }
                        }
                        else
                        {
                             // No work to do—sleep briefly to free up CPU
                             Thread.Sleep(1);
                        }
                    }
                    catch (ObjectDisposedException) { /* Model disposed */ }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, $"AI Loop Error: {ex.Message}");
                    }
                }

                stopwatch.Stop();
            }
        }

        #region AI Loop Functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AutoTrigger()
        {
            // NEW LOGIC: Only trigger if the toggle is ON AND the specific Auto Click Keybind is held.
            // Holding the Aim Keybind will NOT trigger the shot anymore.
            
            bool isAutoClickHeld = InputBindingManager.IsHoldingBinding("Auto Click Keybind");
            bool shouldTrigger = Dictionary.toggleState["Auto Trigger"] && isAutoClickHeld;

            if (!shouldTrigger)
            {
                CheckSprayRelease();
                return;
            }


            if (Dictionary.toggleState["Spray Mode"])
            {
                MouseManager.DoTriggerClick(LastDetectionBox);
                return;
            }


            if (Dictionary.toggleState["Cursor Check"])
            {
                var mousePos = WinAPICaller.GetCursorPosition();

                if (!DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePos.X, mousePos.Y)))
                {
                    return;
                }

                if (LastDetectionBox.Contains(mousePos.X, mousePos.Y))
                {
                    MouseManager.DoTriggerClick(LastDetectionBox);
                }
            }
            else
            {
                MouseManager.DoTriggerClick();
            }

            if (!Dictionary.toggleState["Aim Assist"] || !Dictionary.toggleState["Show Detected Player"]) return;

        }
        private void CheckSprayRelease()
        {
            if (!Dictionary.toggleState["Spray Mode"]) return;

            bool isAutoClickHeld = InputBindingManager.IsHoldingBinding("Auto Click Keybind");
            bool shouldTrigger = Dictionary.toggleState["Auto Trigger"] && isAutoClickHeld;

            if (!shouldTrigger)
            {
                MouseManager.ResetSprayState();
            }
        }

        private void UpdateFOV()
        {
            if (Dictionary.dropdownState["Detection Area Type"] == "Closest to Mouse" && Dictionary.toggleState["FOV"])
            {
                var mousePosition = WinAPICaller.GetCursorPosition();

                // Check if mouse is on the current display
                if (!DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePosition.X, mousePosition.Y)))
                {
                    // Mouse is on a different display - don't update FOV position
                    return;
                }

                // Translate mouse position relative to current display
                var displayRelativeX = mousePosition.X - DisplayManager.ScreenLeft;
                var displayRelativeY = mousePosition.Y - DisplayManager.ScreenTop;

                Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    if (Dictionary.FOVWindow != null && Dictionary.FOVWindow.FOVStrictEnclosure != null)
                    {
                        Dictionary.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                            Convert.ToInt16(displayRelativeX / WinAPICaller.scalingFactorX) - 320, // this is based off the window size, not the size of the model -whip
                            Convert.ToInt16(displayRelativeY / WinAPICaller.scalingFactorY) - 320, 0, 0);
                    }
                });
            }
        }

        private void QueueOverlayUpdate(Action update)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || _isDisposed) return;
            if ((string)Dictionary.dropdownState["Screen Capture Method"] != "WGC")
            {
                Interlocked.Exchange(ref _pendingWgcOverlay, null);
                dispatcher.BeginInvoke(update);
                return;
            }
            Interlocked.Exchange(ref _pendingWgcOverlay, update);
            ScheduleWgcOverlay();
        }

        private void ScheduleWgcOverlay()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || _isDisposed) return;
            if (Interlocked.CompareExchange(ref _wgcOverlayScheduled, 1, 0) != 0) return;
            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                try
                {
                    var update = Interlocked.Exchange(ref _pendingWgcOverlay, null);
                    if (!_isDisposed && (string)Dictionary.dropdownState["Screen Capture Method"] == "WGC") update?.Invoke();
                }
                finally
                {
                    Interlocked.Exchange(ref _wgcOverlayScheduled, 0);
                    if (Volatile.Read(ref _pendingWgcOverlay) != null) ScheduleWgcOverlay();
                }
            }));
        }

        private void DisableOverlay(DetectedPlayerWindow DetectedPlayerOverlay)
        {
            var showAIConfidence = Dictionary.toggleState["Show AI Confidence"];
            var showTracers = Dictionary.toggleState["Show Tracers"];
            var showDetectedPlayer = Dictionary.toggleState["Show Detected Player"];

            QueueOverlayUpdate(() =>
            {
                if (showDetectedPlayer && DetectedPlayerOverlay != null)
                {
                    if (showAIConfidence)
                    {
                        DetectedPlayerOverlay!.DetectedPlayerConfidence.Opacity = 0;
                    }

                    if (showTracers)
                    {
                        DetectedPlayerOverlay!.DetectedTracers.Opacity = 0;
                    }

                    DetectedPlayerOverlay!.DetectedPlayerFocus.Opacity = 0;
                    
                    // Clear all detection boxes
                    DetectedPlayerOverlay!.ClearAllDetections();
                }
            });
        }

        private void UpdateOverlay(DetectedPlayerWindow DetectedPlayerOverlay, Prediction closestPrediction)
        {
            var lastBoxSnapshot = LastDetectionBox;
            var scalingFactorX = WinAPICaller.scalingFactorX;
            var scalingFactorY = WinAPICaller.scalingFactorY;

            // Convert screen coordinates to display-relative coordinates
            var displayRelativeX = lastBoxSnapshot.X - DisplayManager.ScreenLeft;
            var displayRelativeY = lastBoxSnapshot.Y - DisplayManager.ScreenTop;

            // Calculate center position in display-relative coordinates
            var centerX = Convert.ToInt16(displayRelativeX / scalingFactorX) + (lastBoxSnapshot.Width / 2.0);
            var centerY = Convert.ToInt16(displayRelativeY / scalingFactorY);

            // Snapshot variables for BeginInvoke to ensure thread-safety
            var currentAIConf = AIConf;
            var showAIConfidence = Dictionary.toggleState["Show AI Confidence"];
            var showTracers = Dictionary.toggleState["Show Tracers"];
            var tracerPosition = Dictionary.dropdownState["Tracer Position"];
            var opacity = Dictionary.sliderSettings["Opacity"];
            var allPredsSnapshot = _allPredictions != null ? new List<Prediction>(_allPredictions) : null;
            var currentDetBoxSnapshot = _currentDetectionBox;
            bool isEngineSnapshot = _isActiveSlotEngine;
            bool isSingleClassSnapshot = NUM_CLASSES == 1;
            bool isWgcSnapshot = Dictionary.dropdownState["Screen Capture Method"] == "WGC";
            int capturedDisplayLeft = DisplayManager.ScreenLeft;
            int capturedDisplayTop = DisplayManager.ScreenTop;

            QueueOverlayUpdate(() =>
            {
                if (isWgcSnapshot && Dictionary.dropdownState["Screen Capture Method"] != "WGC") return;
                if (showAIConfidence && closestPrediction != null)
                {
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 1;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Content = $"{closestPrediction.ClassName}: {Math.Round((currentAIConf * 100), 2)}%";

                    var labelEstimatedHalfWidth = DetectedPlayerOverlay.DetectedPlayerConfidence.ActualWidth / 2.0;
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Margin = new Thickness(
                        centerX - labelEstimatedHalfWidth,
                        centerY - DetectedPlayerOverlay.DetectedPlayerConfidence.ActualHeight - 2, 0, 0);
                }
                else
                {
                    DetectedPlayerOverlay.DetectedPlayerConfidence.Opacity = 0;
                }
                var showTracerLines = showTracers && closestPrediction != null;
                DetectedPlayerOverlay.DetectedTracers.Opacity = showTracerLines ? 1 : 0;
                if (showTracerLines && closestPrediction != null)
                {
                    var boxTop = centerY;
                    var boxBottom = centerY + lastBoxSnapshot.Height;
                    var boxHorizontalCenter = centerX;
                    var boxVerticalCenter = centerY + (lastBoxSnapshot.Height / 2.0);
                    var boxLeft = centerX - (lastBoxSnapshot.Width / 2.0);
                    var boxRight = centerX + (lastBoxSnapshot.Width / 2.0);

                    switch (tracerPosition)
                    {
                        case "Top":
                            DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                            DetectedPlayerOverlay.DetectedTracers.Y2 = boxTop;
                            break;

                        case "Bottom":
                            DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                            DetectedPlayerOverlay.DetectedTracers.Y2 = boxBottom;
                            break;

                        case "Middle":
                            var screenHorizontalCenter = DisplayManager.ScreenWidth / (2.0 * WinAPICaller.scalingFactorX);
                            if (boxHorizontalCenter < screenHorizontalCenter)
                            {
                                // if the box is on the left half of the screen, aim for the right-middle of the box
                                DetectedPlayerOverlay.DetectedTracers.X2 = boxRight;
                                DetectedPlayerOverlay.DetectedTracers.Y2 = boxVerticalCenter;
                            }
                            else
                            {
                                // if the box is on the right half, aim for the left-middle
                                DetectedPlayerOverlay.DetectedTracers.X2 = boxLeft;
                                DetectedPlayerOverlay.DetectedTracers.Y2 = boxVerticalCenter;
                            }
                            break;

                        default:
                            // default to the bottom-center if the setting is unrecognized
                            DetectedPlayerOverlay.DetectedTracers.X2 = boxHorizontalCenter;
                            DetectedPlayerOverlay.DetectedTracers.Y2 = boxBottom;
                            break;
                    }
                }

                DetectedPlayerOverlay.Opacity = opacity;

                // Draw all detected predictions with different colors
                if (allPredsSnapshot != null && allPredsSnapshot.Count > 0)
                {
                    // Apply NMS to remove overlapping boxes
                    var filteredPredictions = ApplyNMS(allPredsSnapshot, 0.45f);
                    
                    var detectionList = new List<(string ClassName, double X, double Y, double Width, double Height, double Confidence)>();
                    
                    foreach (var prediction in filteredPredictions)
                    {
                        // Use same logic as UpdateDetectionBox
                        // Translate from model coordinates to screen coordinates
                        float translatedXMin = prediction.Rectangle.X + currentDetBoxSnapshot.Left;
                        float translatedYMin = prediction.Rectangle.Y + currentDetBoxSnapshot.Top;
                        
                        // Convert to display-relative coordinates
                        var predDisplayX = translatedXMin - (isWgcSnapshot ? capturedDisplayLeft : DisplayManager.ScreenLeft);
                        var predDisplayY = translatedYMin - (isWgcSnapshot ? capturedDisplayTop : DisplayManager.ScreenTop);
                        
                        // Apply scaling
                        var predScreenX = predDisplayX / scalingFactorX;
                        var predScreenY = predDisplayY / scalingFactorY;
                        var predWidth = prediction.Rectangle.Width / scalingFactorX;
                        var predHeight = prediction.Rectangle.Height / scalingFactorY;
                        
                        detectionList.Add((
                            prediction.ClassName ?? "Unknown",
                            predScreenX,
                            predScreenY,
                            predWidth,
                            predHeight,
                            prediction.Confidence
                        ));
                    }
                    
                    DetectedPlayerOverlay.DrawAllDetections(detectionList, showAIConfidence, isEngineSnapshot, isSingleClassSnapshot);
                }
                else if (Dictionary.dropdownState["Screen Capture Method"] == "WGC")
                {
                    DetectedPlayerOverlay.DrawAllDetections(new List<(string ClassName, double X, double Y, double Width, double Height, double Confidence)>(), showAIConfidence, isEngineSnapshot, isSingleClassSnapshot);
                }

                // Hide the old single-box overlay since we now draw all boxes via Canvas
                DetectedPlayerOverlay.DetectedPlayerFocus.Opacity = 0;
            });
        }

        private void CalculateCoordinates(DetectedPlayerWindow DetectedPlayerOverlay, Prediction closestPrediction, float scaleX, float scaleY)
        {
            AIConf = closestPrediction.Confidence;

            if (Dictionary.toggleState["Aim Assist"] || Dictionary.DetectedPlayerOverlay != null)
            {
                // We don't call UpdateOverlay here anymore, it's called directly in AiLoop for better responsiveness and ESP
                if (!Dictionary.toggleState["Aim Assist"]) return;
            }

            double YOffset = Dictionary.sliderSettings[ActiveSlot == 1 ? "Slot 1 Y Offset (Up/Down)" : "Y Offset (Up/Down)"];
            double XOffset = Dictionary.sliderSettings[ActiveSlot == 1 ? "Slot 1 X Offset (Left/Right)" : "X Offset (Left/Right)"];

            double YOffsetPercentage = Dictionary.sliderSettings[ActiveSlot == 1 ? "Slot 1 Y Offset (%)" : "Y Offset (%)"];
            double XOffsetPercentage = Dictionary.sliderSettings[ActiveSlot == 1 ? "Slot 1 X Offset (%)" : "X Offset (%)"];

            var rect = closestPrediction.Rectangle;
            
            bool useXPercent = Dictionary.toggleState[ActiveSlot == 1 ? "Slot 1 X Axis Percentage Adjustment" : "X Axis Percentage Adjustment"];
            bool useYPercent = Dictionary.toggleState[ActiveSlot == 1 ? "Slot 1 Y Axis Percentage Adjustment" : "Y Axis Percentage Adjustment"];

            if (useXPercent)
            {
                detectedX = (int)((rect.X + (rect.Width * (XOffsetPercentage / 100))) * scaleX);
            }
            else
            {
                detectedX = (int)((rect.X + rect.Width / 2) * scaleX + XOffset);
            }

            if (useYPercent)
            {
                detectedY = (int)((rect.Y + rect.Height - (rect.Height * (YOffsetPercentage / 100))) * scaleY + YOffset);
            }
            else
            {
                detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalculateDetectedY(float scaleY, double YOffset, Prediction closestPrediction)
        {
            var rect = closestPrediction.Rectangle;
            float yBase = rect.Y;
            float yAdjustment = 0;

            switch (Dictionary.dropdownState["Aiming Boundaries Alignment"])
            {
                case "Center":
                    yAdjustment = rect.Height / 2;
                    break;

                case "Top":
                    // yBase is already at the top
                    break;

                case "Bottom":
                    yAdjustment = rect.Height;
                    break;
            }

            return (int)((yBase + yAdjustment) * scaleY + YOffset);
        }

        private void HandleAim(Prediction closestPrediction)
        {
            if (Dictionary.toggleState["Aim Assist"] &&
                (Dictionary.toggleState["Constant AI Tracking"] ||
                 Dictionary.toggleState["Aim Assist"] && InputBindingManager.IsHoldingBinding("Aim Keybind") ||
                 Dictionary.toggleState["Aim Assist"] && InputBindingManager.IsHoldingBinding("Second Aim Keybind")))
            {
                if (Dictionary.toggleState["Predictions"])
                {
                    HandlePredictions(kalmanPrediction, closestPrediction, detectedX, detectedY);
                }
                else
                {
                    MouseManager.MoveCrosshair(detectedX, detectedY);
                }
            }
        }

        private void HandlePredictions(KalmanPrediction kalmanPrediction, Prediction closestPrediction, int detectedX, int detectedY)
        {
            var predictionMethod = Dictionary.dropdownState["Prediction Method"];
            switch (predictionMethod)
            {
                case "Kalman Filter":
                    KalmanPrediction.Detection detection = new()
                    {
                        X = detectedX,
                        Y = detectedY,
                        Timestamp = DateTime.UtcNow
                    };

                    kalmanPrediction.UpdateKalmanFilter(detection);
                    var predictedPosition = kalmanPrediction.GetKalmanPosition();

                    MouseManager.MoveCrosshair(predictedPosition.X, predictedPosition.Y);
                    break;

                case "Shall0e's Prediction":
                    // Update position (calculates velocity internally)
                    ShalloePredictionV2.UpdatePosition(detectedX, detectedY);

                    // Get predicted position
                    MouseManager.MoveCrosshair(ShalloePredictionV2.GetSPX(), ShalloePredictionV2.GetSPY());
                    break;

                case "wisethef0x's EMA Prediction":
                    WiseTheFoxPrediction.WTFDetection wtfdetection = new()
                    {
                        X = detectedX,
                        Y = detectedY,
                        Timestamp = DateTime.UtcNow
                    };

                    wtfpredictionManager.UpdateDetection(wtfdetection);
                    var wtfpredictedPosition = wtfpredictionManager.GetEstimatedPosition();

                    // Use both predicted X and Y
                    MouseManager.MoveCrosshair(wtfpredictedPosition.X, wtfpredictedPosition.Y);
                    break;

                case "Constant Acceleration":
                    caPrediction.Update(detectedX, detectedY);
                    var (caX, caY) = caPrediction.GetPrediction();
                    MouseManager.MoveCrosshair(caX, caY);
                    break;
            }
        }

        private Prediction? GetClosestPredictionSync(bool useMousePosition = true)
        {
            //whats these variables for? - taylor 
            //int adjustedTargetX, adjustedTargetY;
            int activeSlot;
            int imageSize;
            int numDetections;
            bool isNmsFreeModel;
            int numClasses;
            List<string>? outputNames;
            Dictionary<int, string> modelClasses;

            lock (_modelLock)
            {
                if (_isDisposed) return null;
                if (_isActiveSlotEngine)
                {
                    if (_engineModel == null) return null;
                }
                else
                {
                    if (_onnxModel == null || _outputNames == null) return null;
                }

                activeSlot = ActiveSlot;
                imageSize = IMAGE_SIZE;
                numDetections = NUM_DETECTIONS;
                isNmsFreeModel = IsNmsFreeModel;
                numClasses = NUM_CLASSES;
                outputNames = _outputNames != null ? new List<string>(_outputNames) : null;
                modelClasses = new Dictionary<int, string>(_modelClasses);
            }

            if (Dictionary.dropdownState["Detection Area Type"] == "Closest to Mouse")
            {
                var mousePos = WinAPICaller.GetCursorPosition();

                // Check if mouse is on the current display
                if (DisplayManager.IsPointInCurrentDisplay(new System.Windows.Point(mousePos.X, mousePos.Y)))
                {
                    // Mouse is on current display, use its position
                    targetX = mousePos.X;
                    targetY = mousePos.Y;
                }
                else
                {
                    // Mouse is on different display, use center of current display
                    targetX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                    targetY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
                }
            }
            else
            {
                // Center of current display
                targetX = DisplayManager.ScreenLeft + (DisplayManager.ScreenWidth / 2);
                targetY = DisplayManager.ScreenTop + (DisplayManager.ScreenHeight / 2);
            }

            Rectangle detectionBox = new(targetX - imageSize / 2, targetY - imageSize / 2, imageSize, imageSize); // Detection box dynamic size
            _currentDetectionBox = detectionBox; // Store for overlay rendering

            Bitmap? frame = null;
            bool collectData = Dictionary.toggleState.TryGetValue("Collect Data While Playing", out var cd) && (bool)cd;
            bool useDirectX = Dictionary.dropdownState.TryGetValue("Screen Capture Method", out var scm) && scm.ToString() == "DirectX";

            if (useDirectX && !collectData)
            {
                // Fast path: direct copy from DX mapped VRAM to float array!
                if (_reusableInputArray == null || _reusableInputArray.Length != 3 * imageSize * imageSize)
                {
                    _reusableInputArray = new float[3 * imageSize * imageSize];
                }
                
                using (Benchmark("ScreenGrab"))
                {
                    bool success = _captureManager.CaptureAndConvertDirectX(detectionBox, _reusableInputArray, imageSize, Dictionary.toggleState["Third Person Support"]);
                    if (!success) return null;
                }
            }
            else
            {
                // WGC writes directly into the reusable tensor unless image collection needs a Bitmap.
                if (_reusableInputArray == null || _reusableInputArray.Length != 3 * imageSize * imageSize)
                    _reusableInputArray = new float[3 * imageSize * imageSize];
                bool useWgcTensor = !collectData && Dictionary.dropdownState["Screen Capture Method"] == "WGC";
                using (Benchmark("ScreenGrab"))
                {
                    frame = _captureManager.ScreenGrab(detectionBox, useWgcTensor ? _reusableInputArray : null);
                }

                if (frame == null && !_captureManager.LastCaptureConverted)
                {
                    if (_captureManager.LastCaptureWaitingForFrame) return null;
                    if (Dictionary.dropdownState["Screen Capture Method"] == "WGC")
                        _allPredictions = null; // Do not draw old model coordinates against a new ROI.
                    return null;
                }

                if (frame != null)
                using (Benchmark("BitmapToFloatArray"))
                {
                    if (_reusableInputArray == null || _reusableInputArray.Length != 3 * imageSize * imageSize)
                    {
                        _reusableInputArray = new float[3 * imageSize * imageSize];
                    }
                    BitmapToFloatArrayInPlace(frame, _reusableInputArray, imageSize);
                }
            }

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? results = null;
            Tensor<float>? outputTensor = null;

            try
            {
                if (_isActiveSlotEngine)
                {
                    lock (_modelLock)
                    {
                        if (_engineModel == null || _isDisposed || ActiveSlot != activeSlot || IMAGE_SIZE != imageSize ||
                            NUM_DETECTIONS != numDetections || IsNmsFreeModel != isNmsFreeModel || NUM_CLASSES != numClasses)
                        {
                            return null;
                        }

                        using (Benchmark("ModelInference"))
                        {
                            float[] outputData = _engineModel.RunInference(_reusableInputArray);
                            outputTensor = new DenseTensor<float>(outputData, _engineModel.OutputDims);
                        }
                    }
                }
                else
                {
                    // Reuse tensor and inputs - recreate if size changed
                    if (_reusableTensor == null || _reusableTensor.Dimensions[2] != imageSize || !ReferenceEquals(_tensorBackingArray, _reusableInputArray))
                    {
                        _reusableTensor = new DenseTensor<float>(_reusableInputArray, new int[] { 1, 3, imageSize, imageSize });
                        _tensorBackingArray = _reusableInputArray;
                        _reusableInputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", _reusableTensor) };
                    }


                    lock (_modelLock)
                    {
                        if (_onnxModel == null || _isDisposed || ActiveSlot != activeSlot || IMAGE_SIZE != imageSize ||
                            NUM_DETECTIONS != numDetections || IsNmsFreeModel != isNmsFreeModel || NUM_CLASSES != numClasses)
                        {
                            return null;
                        }

                        using (Benchmark("ModelInference"))
                        {
                            results = _onnxModel.Run(_reusableInputs, outputNames, _modeloptions);
                            outputTensor = results[0].AsTensor<float>();
                        }
                    }
                }

                if (outputTensor == null)
                {
                    Log(LogLevel.Error, "Model inference returned null output tensor.", true, 2000);
                    if (frame != null) SaveFrame(frame);
                    return null;
                }

                // Calculate the FOV boundaries
                float FovSize = (float)Dictionary.sliderSettings["FOV Size"];
                float fovMinX = (imageSize - FovSize) / 2.0f;
                float fovMaxX = (imageSize + FovSize) / 2.0f;
                float fovMinY = (imageSize - FovSize) / 2.0f;
                float fovMaxY = (imageSize + FovSize) / 2.0f;

                List<Prediction> KDPredictions;
                using (Benchmark("PrepareKDTreeData"))
                {
                    // Get ALL predictions for ESP without FOV filtering
                    KDPredictions = PrepareKDTreeData(outputTensor, detectionBox, fovMinX, fovMaxX, fovMinY, fovMaxY,
                        activeSlot, imageSize, numDetections, isNmsFreeModel, numClasses, modelClasses, false);
                    _allPredictions = KDPredictions; // Store all for overlay rendering
                }

                if (KDPredictions.Count == 0)
                {
                    if (frame != null) SaveFrame(frame);
                    return null;
                }

                // Filter KDPredictions for aiming based on FOV
                var aimCandidates = KDPredictions.Where(p => {
                    float x_min = p.Rectangle.X;
                    float y_min = p.Rectangle.Y;
                    float x_max = p.Rectangle.X + p.Rectangle.Width;
                    float y_max = p.Rectangle.Y + p.Rectangle.Height;
                    return !(x_min < fovMinX || x_max > fovMaxX || y_min < fovMinY || y_max > fovMaxY);
                }).ToList();

                Prediction? bestCandidate = null;
                double bestDistSq = double.MaxValue;
                double center = imageSize / 2.0;
                List<Prediction> trackingCandidates = aimCandidates;

                using (Benchmark("LinearSearch"))
                {
                    var candidates = aimCandidates;
                    
                    if (activeSlot == 1 && modelClasses.Count > 1 && IsPriorityAimingEnabled(1))
                    {
                        bool searchHead = ShouldPrioritizeHead(1);
                        candidates = GetPriorityCandidates(aimCandidates, searchHead);
                    }
                    else if (activeSlot == 2 && modelClasses.Count > 1 && IsPriorityAimingEnabled(2))
                    {
                        bool searchHead = ShouldPrioritizeHead(2);
                        candidates = GetPriorityCandidates(aimCandidates, searchHead);
                    }

                    trackingCandidates = candidates;

                    foreach (var p in candidates)
                    {
                        var dx = p.CenterXTranslated * imageSize - center;
                        var dy = p.CenterYTranslated * imageSize - center;
                        double d2 = dx * dx + dy * dy;

                        if (d2 < bestDistSq) { bestDistSq = d2; bestCandidate = p; }
                    }
                    
                    // Store all predictions for overlay rendering
                    _allPredictions = KDPredictions;
                }

                Prediction? finalTarget = HandleStickyAim(bestCandidate, trackingCandidates);
                if (finalTarget != null)
                {
                    UpdateDetectionBox(finalTarget, detectionBox);
                    if (frame != null) SaveFrame(frame, finalTarget);
                    return finalTarget;
                }

                return null;
            }
            finally
            {
                results?.Dispose();
            }
        }

        private static int ParseKeyToVirtualKeyCode(string keyStr)
        {
            if (string.IsNullOrEmpty(keyStr) || string.Equals(keyStr, "None", StringComparison.OrdinalIgnoreCase))
                return 0;

            // Handle common modifier names
            if (string.Equals(keyStr, "Shift", StringComparison.OrdinalIgnoreCase)) return 16; // VK_SHIFT
            if (string.Equals(keyStr, "Control", StringComparison.OrdinalIgnoreCase)) return 17; // VK_CONTROL
            if (string.Equals(keyStr, "Alt", StringComparison.OrdinalIgnoreCase)) return 18; // VK_MENU

            // Handle custom name mappings
            if (string.Equals(keyStr, "Left Shift", StringComparison.OrdinalIgnoreCase) || string.Equals(keyStr, "LShiftKey", StringComparison.OrdinalIgnoreCase)) return 160; // VK_LSHIFT
            if (string.Equals(keyStr, "Right Shift", StringComparison.OrdinalIgnoreCase) || string.Equals(keyStr, "RShiftKey", StringComparison.OrdinalIgnoreCase)) return 161; // VK_RSHIFT
            if (string.Equals(keyStr, "Left Ctrl", StringComparison.OrdinalIgnoreCase) || string.Equals(keyStr, "LControlKey", StringComparison.OrdinalIgnoreCase)) return 162; // VK_LCONTROL
            if (string.Equals(keyStr, "Right Ctrl", StringComparison.OrdinalIgnoreCase) || string.Equals(keyStr, "RControlKey", StringComparison.OrdinalIgnoreCase)) return 163; // VK_RCONTROL

            // Try standard enum parse
            if (System.Enum.TryParse<System.Windows.Forms.Keys>(keyStr, true, out var key))
            {
                return (int)key;
            }

            return 0;
        }

        private static bool ReadBoolSetting(string key, bool fallback)
        {
            if (!Dictionary.toggleState.TryGetValue(key, out var value) || value == null)
                return fallback;

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return fallback;
            }
        }

        private bool IsPriorityAimingEnabled(int slot)
        {
            string key = slot == 1 ? "Slot 1 Priority Aiming" : "Slot 2 Priority Aiming";
            bool fallback = slot == 1 ? Slot1AimHead : Slot2AimHead;
            return ReadBoolSetting(key, fallback);
        }

        private static string GetPriorityBindingId(int slot) => slot == 1 ? "Slot 1 Priority Key" : "Slot 2 Priority Key";

        private static string GetPriorityKey(int slot)
        {
            string bindingId = GetPriorityBindingId(slot);
            return Dictionary.bindingSettings.TryGetValue(bindingId, out var key) ? key?.ToString() ?? "None" : "None";
        }

        private static bool IsPriorityKeyHeld(int slot, string key)
        {
            string bindingId = GetPriorityBindingId(slot);

            if (InputBindingManager.IsHoldingBinding(bindingId))
                return true;

            if (string.IsNullOrWhiteSpace(key) || string.Equals(key, "None", StringComparison.OrdinalIgnoreCase))
                return false;

            int vk = ParseKeyToVirtualKeyCode(key);
            return vk != 0 && (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private bool ShouldPrioritizeHead(int slot)
        {
            string priorityKey = GetPriorityKey(slot);
            return IsPriorityKeyHeld(slot, priorityKey);
        }

        private List<Prediction> GetPriorityCandidates(List<Prediction> candidates, bool prioritizeHead)
        {
            var preferred = candidates.Where(p => IsPriorityTargetSlot1(p, prioritizeHead)).ToList();
            if (preferred.Count > 0)
                return preferred;

            var fallback = candidates.Where(p => !IsPriorityTargetSlot1(p, prioritizeHead)).ToList();
            return fallback.Count > 0 ? fallback : candidates;
        }

        private bool IsPriorityTargetSlot1(Prediction p, bool prioritizeHead)
        {
            if (string.IsNullOrEmpty(p.ClassName)) return false;
            string name = p.ClassName.ToLower();
            
            // Check if class is head
            bool isHead = name.Contains("head") || name.Contains("dau") || name.Contains("đầu") || 
                           name.Contains("sọ") || name.Contains("face") || name.Contains("mặt") ||
                           name.Contains("cap") || name.Contains("mũ") || name.Contains("brain") ||
                           name.Contains("helmet") || name.Contains("nón");

            if (prioritizeHead)
            {
                return isHead;
            }
            else
            {
                // Prioritize Body: return true if it is NOT head
                return !isHead;
            }
        }

        private bool IsPriorityTargetSlot2(Prediction p, bool prioritizeHead)
        {
            if (string.IsNullOrEmpty(p.ClassName)) return false;
            string name = p.ClassName.ToLower();
            
            // Check if class is head
            bool isHead = name.Contains("head") || name.Contains("dau") || name.Contains("đầu") || 
                           name.Contains("sọ") || name.Contains("face") || name.Contains("mặt") ||
                           name.Contains("cap") || name.Contains("mũ") || name.Contains("brain") ||
                           name.Contains("helmet") || name.Contains("nón");

            if (prioritizeHead)
            {
                return isHead;
            }
            else
            {
                // Prioritize Body: return true if it is NOT head
                return !isHead;
            }
        }

        private Prediction? HandleStickyAim(Prediction? bestCandidate, List<Prediction> KDPredictions)
        {
            if (!Dictionary.toggleState["Sticky Aim"])
            {
                _currentTarget = bestCandidate;
                ResetStickyAimState();
                return bestCandidate;
            }

            // No detections available
            if (bestCandidate == null || KDPredictions == null || KDPredictions.Count == 0)
            {
                return HandleNoDetections();
            }

            _consecutiveFramesWithoutTarget = 0;

            // Screen center (where user is aiming)
            float screenCenterX = IMAGE_SIZE / 2f;
            float screenCenterY = IMAGE_SIZE / 2f;

            // STEP 1: Find what the user is aiming at (closest to crosshair)
            Prediction? aimTarget = null;
            float nearestToCrosshairDistSq = float.MaxValue;

            foreach (var candidate in KDPredictions)
            {
                float distSq = GetDistanceSq(candidate.ScreenCenterX, candidate.ScreenCenterY, screenCenterX, screenCenterY);
                if (distSq < nearestToCrosshairDistSq)
                {
                    nearestToCrosshairDistSq = distSq;
                    aimTarget = candidate;
                }
            }

            if (aimTarget == null)
            {
                return HandleNoDetections();
            }

            // No current target - acquire what user is aiming at
            if (_currentTarget == null)
            {
                return AcquireNewTarget(aimTarget);
            }

            // STEP 2: Is the aim target the SAME as our current target?
            float lastX = _currentTarget.ScreenCenterX;
            float lastY = _currentTarget.ScreenCenterY;
            float targetArea = _currentTarget.Rectangle.Width * _currentTarget.Rectangle.Height;
            float targetSize = MathF.Sqrt(targetArea);
            float sizeFactor = GetSizeFactor(targetArea);

            // Distance from aim target to our current target's last position
            float aimToCurrentDistSq = GetDistanceSq(aimTarget.ScreenCenterX, aimTarget.ScreenCenterY, lastX, lastY);

            // Tracking radius based on target size - larger targets have larger radius
            float trackingRadius = targetSize * 3f;
            float trackingRadiusSq = trackingRadius * trackingRadius;

            // Check size similarity
            float aimTargetArea = aimTarget.Rectangle.Width * aimTarget.Rectangle.Height;
            float sizeRatio = MathF.Min(targetArea, aimTargetArea) / MathF.Max(targetArea, aimTargetArea);

            // Is the aim target the same as our current target?
            // Same if: close to last position AND similar size
            bool isSameTarget = (aimToCurrentDistSq < trackingRadiusSq) && (sizeRatio > 0.5f);

            // TARGET LOCK DURATION CHECK
            // If we are within the lock duration, we force the target to stay on the current target
            // UNLESS the current target is completely lost (handled by framesWithoutMatch later if needed, but here we just check time)
            if (_targetLockedStartTime != DateTime.MinValue && Dictionary.sliderSettings.ContainsKey("Target Lock Duration"))
            {
                double lockDurationMs = Dictionary.sliderSettings["Target Lock Duration"];
                if (lockDurationMs > 0 && (DateTime.UtcNow - _targetLockedStartTime).TotalMilliseconds < lockDurationMs)
                {
                    // effectively force it to be "same target" if we are still tracking something valid
                    // We only do this if we actually found a match that *could* be the current target
                    // logic: if isSameTarget is true, we good.
                    // if isSameTarget is false, but we are locked, we check if the "aimTarget" is actually just a Better target.
                    // We want to preventing switching to a Better target.

                    // Actually, simpler logic:
                    // If we are locked, only allow finding the *current* target.
                    // We Iterate all candidates again? No, we just need to find the one matching _currentTarget.
                    
                    // Let's refine:
                    // logic above found "aimTarget" which is the CLOSEST to crosshair.
                    // If "aimTarget" != "_currentTarget" (isSameTarget == false), normally we might switch.
                    // BUT if we are locked, we should ignore "aimTarget" and search for "_currentTarget" in the list.
                    
                    if (!isSameTarget)
                    {
                        // Try to find our original target in the list
                        Prediction? originalTargetCandidate = null;
                        float bestDistSq = float.MaxValue;
                        
                        foreach (var candidate in KDPredictions)
                        {
                            // Check if this candidate looks like our current target
                            float distSq = GetDistanceSq(candidate.ScreenCenterX, candidate.ScreenCenterY, lastX, lastY);
                             float candArea = candidate.Rectangle.Width * candidate.Rectangle.Height;
                             float candSizeRatio = MathF.Min(targetArea, candArea) / MathF.Max(targetArea, candArea);
                             
                             if (distSq < trackingRadiusSq && candSizeRatio > 0.5f)
                             {
                                 if (distSq < bestDistSq)
                                 {
                                     bestDistSq = distSq;
                                     originalTargetCandidate = candidate;
                                 }
                             }
                        }

                        if (originalTargetCandidate != null)
                        {
                            // We found our locked target! Ignore the "better" aimTarget.
                            aimTarget = originalTargetCandidate;
                            isSameTarget = true;
                        }
                    }
                }
            }

            if (isSameTarget)
            {
                // User is still aiming at current target - update and continue
                _framesWithoutMatch = 0;
                UpdateVelocity(aimTarget, sizeFactor);
                _targetLockScore = Math.Min(MAX_LOCK_SCORE, _targetLockScore + LOCK_SCORE_GAIN);
                _currentTarget = aimTarget;
                return aimTarget;
            }

            // STEP 3: User is aiming at a DIFFERENT target
            // But we need hysteresis - don't switch on single-frame jitter
            _framesWithoutMatch++;

            // Quick switch if aim target is very close to crosshair (user clearly aiming at it)
            // BUT NOT if we are still locked (logic above handles lock by forcing isSameTarget = true if found)
            // So if we reach here, it means we are either NOT locked, OR we are locked but the locked target is GONE.
            
            float stickyThreshold = (float)Dictionary.sliderSettings["Sticky Aim Threshold"];
            bool aimTargetVeryCentered = nearestToCrosshairDistSq < (stickyThreshold * stickyThreshold * 0.25f);
            
            // If we're locked, we should be much more hesitant to switch, effectively "losing" the lock only if target is gone.
            // The code above already did that: if locked & found, isSameTarget=true.
            // If we are here, we didn't find the locked target.
            
            if (aimTargetVeryCentered || _framesWithoutMatch >= 3)
            {
                // User has clearly moved to new target - switch
                return AcquireNewTarget(aimTarget);
            }

            // Not ready to switch yet - return null to avoid flicking
            // (Don't return old target position, don't return new target position)
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetDistanceSq(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Returns a scaling factor based on target size. Smaller targets (further away) get higher factors
        /// to make thresholds more forgiving and filtering more aggressive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetSizeFactor(float targetArea)
        {
            // sizeFactor: 1.0 for large/close targets, up to 3.0 for small/distant targets
            // This makes distant targets more "sticky" to compensate for detection jitter
            float ratio = REFERENCE_TARGET_SIZE / Math.Max(targetArea, 100f);
            return Math.Clamp(ratio, 1.0f, 3.0f);
        }

        private Prediction? HandleNoDetections()
        {
            if (_currentTarget != null && ++_consecutiveFramesWithoutTarget <= MAX_FRAMES_WITHOUT_TARGET)
            {
                // Decay lock score during grace period
                _targetLockScore *= LOCK_SCORE_DECAY;

                // Return predicted position instead of stale position
                var predicted = new Prediction
                {
                    ScreenCenterX = _currentTarget.ScreenCenterX + _lastTargetVelocityX * _consecutiveFramesWithoutTarget,
                    ScreenCenterY = _currentTarget.ScreenCenterY + _lastTargetVelocityY * _consecutiveFramesWithoutTarget,
                    Rectangle = _currentTarget.Rectangle,
                    Confidence = _currentTarget.Confidence * (1f - _consecutiveFramesWithoutTarget * 0.2f),
                    ClassId = _currentTarget.ClassId,
                    ClassName = _currentTarget.ClassName,
                    CenterXTranslated = _currentTarget.CenterXTranslated,
                    CenterYTranslated = _currentTarget.CenterYTranslated
                };
                return predicted;
            }

            ResetStickyAimState();
            return null;
        }

        private Prediction AcquireNewTarget(Prediction target)
        {
            _lastTargetVelocityX = 0f;
            _lastTargetVelocityY = 0f;
            _targetLockScore = LOCK_SCORE_GAIN; // Start with some lock score
            _framesWithoutMatch = 0;
            _currentTarget = target;
            _targetLockedStartTime = DateTime.UtcNow;
            return target;
        }

        private void UpdateVelocity(Prediction newTarget, float sizeFactor)
        {
            if (_currentTarget != null)
            {
                // EMA smoothing on velocity to reduce noise
                // Use heavier smoothing for smaller/distant targets (more weight on old velocity)
                // sizeFactor 1.0 -> 0.7/0.3, sizeFactor 3.0 -> 0.9/0.1
                float smoothing = Math.Clamp(0.6f + (sizeFactor * 0.1f), 0.7f, 0.9f);
                float newWeight = 1f - smoothing;

                float newVelX = newTarget.ScreenCenterX - _currentTarget.ScreenCenterX;
                float newVelY = newTarget.ScreenCenterY - _currentTarget.ScreenCenterY;
                _lastTargetVelocityX = _lastTargetVelocityX * smoothing + newVelX * newWeight;
                _lastTargetVelocityY = _lastTargetVelocityY * smoothing + newVelY * newWeight;
            }
        }

        private void ResetStickyAimState()
        {
            _currentTarget = null;
            _consecutiveFramesWithoutTarget = 0;
            _framesWithoutMatch = 0;
            _lastTargetVelocityX = 0f;
            _lastTargetVelocityY = 0f;
            _targetLockScore = 0f;
        }

        private void UpdateDetectionBox(Prediction target, Rectangle detectionBox)
        {
            float translatedXMin = target.Rectangle.X + detectionBox.Left;
            float translatedYMin = target.Rectangle.Y + detectionBox.Top;
            LastDetectionBox = new(translatedXMin, translatedYMin,
                target.Rectangle.Width, target.Rectangle.Height);

            CenterXTranslated = target.CenterXTranslated;
            CenterYTranslated = target.CenterYTranslated;
        }
        // is it really kdtreedata though....
        private List<Prediction> PrepareKDTreeData(
            Tensor<float> outputTensor,
            Rectangle detectionBox,
            float fovMinX, float fovMaxX, float fovMinY, float fovMaxY,
            int activeSlot,
            int imageSize,
            int numDetections,
            bool isNmsFreeModel,
            int numClasses,
            Dictionary<int, string> modelClasses,
            bool filterByFOV = true)
        {
            float minConfidence = (float)Dictionary.sliderSettings[activeSlot == 2 ? "Slot 2 AI Minimum Confidence" : "AI Minimum Confidence"] / 100.0f;
            string targetClassKey = activeSlot == 1 ? "Slot 1 Target Class" : "Slot 2 Target Class";
            string selectedClass = Dictionary.dropdownState[targetClassKey];
            int selectedClassId = selectedClass == "Best Confidence" ? -1 : modelClasses.FirstOrDefault(c => c.Value == selectedClass).Key;

            var KDpredictions = new List<Prediction>(numDetections);

            DenseTensor<float>? denseOutput = outputTensor as DenseTensor<float>;
            bool hasSpan = denseOutput != null;
            ReadOnlySpan<float> span = hasSpan ? denseOutput!.Buffer.Span : ReadOnlySpan<float>.Empty;
            int[] outputDims = outputTensor.Dimensions.ToArray();

            if (outputDims.Length != 3)
            {
                Log(LogLevel.Warning, $"Unexpected model output rank: {string.Join("x", outputDims)}");
                return KDpredictions;
            }

            bool parseAsNmsFree = isNmsFreeModel || (outputDims[2] == 6 && outputDims[1] <= 1000);
            int actualDetections;
            int actualClasses = numClasses;
            int detectionStride = outputDims[2];

            if (parseAsNmsFree)
            {
                if (outputDims[2] < 6)
                {
                    Log(LogLevel.Warning, $"Invalid NMS-Free output shape: {string.Join("x", outputDims)}");
                    return KDpredictions;
                }

                actualDetections = Math.Min(numDetections, outputDims[1]);
            }
            else
            {
                if (outputDims[1] < 5)
                {
                    Log(LogLevel.Warning, $"Invalid standard output shape: {string.Join("x", outputDims)}");
                    return KDpredictions;
                }

                actualDetections = Math.Min(numDetections, outputDims[2]);
                actualClasses = Math.Max(1, Math.Min(numClasses, outputDims[1] - 4));
            }

            for (int i = 0; i < actualDetections; i++)
            {
                float x_center, y_center, width, height, bestConfidence;
                float x_min, y_min, x_max, y_max;
                int bestClassId;

                if (parseAsNmsFree)
                {
                    if (hasSpan)
                    {
                        int baseIdx = i * detectionStride;
                        x_min = span[baseIdx + 0];
                        y_min = span[baseIdx + 1];
                        x_max = span[baseIdx + 2];
                        y_max = span[baseIdx + 3];
                        bestConfidence = span[baseIdx + 4];
                        bestClassId = (int)span[baseIdx + 5];
                    }
                    else
                    {
                        // Fallback to slow indexer if tensor is not dense
                        x_min = outputTensor[0, i, 0];
                        y_min = outputTensor[0, i, 1];
                        x_max = outputTensor[0, i, 2];
                        y_max = outputTensor[0, i, 3];
                        bestConfidence = outputTensor[0, i, 4];
                        bestClassId = (int)outputTensor[0, i, 5];
                    }

                    // Filter by selected target class if not "Best Confidence"
                    if (selectedClassId != -1 && bestClassId != selectedClassId)
                        continue;

                    x_center = (x_min + x_max) / 2f;
                    y_center = (y_min + y_max) / 2f;
                    width = x_max - x_min;
                    height = y_max - y_min;
                    
                    if (x_max <= 1.05f && y_max <= 1.05f) {
                        x_center *= imageSize;
                        y_center *= imageSize;
                        width *= imageSize;
                        height *= imageSize;
                    }
                }
                else
                {
                    // YOLOv8 format: [1, 4+nc, 8400]
                    if (hasSpan)
                    {
                        x_center = span[0 * detectionStride + i];
                        y_center = span[1 * detectionStride + i];
                        width = span[2 * detectionStride + i];
                        height = span[3 * detectionStride + i];
                    }
                    else
                    {
                        x_center = outputTensor[0, 0, i];
                        y_center = outputTensor[0, 1, i];
                        width = outputTensor[0, 2, i];
                        height = outputTensor[0, 3, i];
                    }

                    bestClassId = 0;
                    bestConfidence = 0f;

                    if (actualClasses == 1)
                    {
                        bestConfidence = hasSpan ? span[4 * detectionStride + i] : outputTensor[0, 4, i];
                    }
                    else
                    {
                        if (selectedClassId == -1)
                        {
                            for (int classId = 0; classId < actualClasses; classId++)
                            {
                                float classConfidence = hasSpan ? span[(4 + classId) * detectionStride + i] : outputTensor[0, 4 + classId, i];
                                if (classConfidence > bestConfidence)
                                {
                                    bestConfidence = classConfidence;
                                    bestClassId = classId;
                                }
                            }
                        }
                        else
                        {
                            if (selectedClassId >= actualClasses)
                                continue;

                            bestConfidence = hasSpan ? span[(4 + selectedClassId) * detectionStride + i] : outputTensor[0, 4 + selectedClassId, i];
                            bestClassId = selectedClassId;
                        }
                    }
                }

                if (bestConfidence < minConfidence) continue;

                x_min = x_center - width / 2;
                y_min = y_center - height / 2;
                x_max = x_center + width / 2;
                y_max = y_center + height / 2;

                if (filterByFOV)
                {
                    if (x_min < fovMinX || x_max > fovMaxX || y_min < fovMinY || y_max > fovMaxY) continue;
                }

                RectangleF rect = new(x_min, y_min, width, height);
                Prediction prediction = new()
                {
                    Rectangle = rect,
                    Confidence = bestConfidence,
                    ClassId = bestClassId,
                    ClassName = modelClasses.GetValueOrDefault(bestClassId, $"Class_{bestClassId}"),
                    CenterXTranslated = x_center / imageSize,
                    CenterYTranslated = y_center / imageSize,
                    ScreenCenterX = detectionBox.Left + x_center,
                    ScreenCenterY = detectionBox.Top + y_center
                };

                KDpredictions.Add(prediction);
            }

            return KDpredictions;
        }

        // NMS (Non-Maximum Suppression) to remove overlapping boxes
        private List<Prediction> ApplyNMS(List<Prediction> predictions, float iouThreshold = 0.45f)
        {
            if (predictions == null || predictions.Count == 0)
                return predictions;

            // Sort by confidence (highest first)
            var sortedPredictions = predictions.OrderByDescending(p => p.Confidence).ToList();
            var result = new List<Prediction>();

            while (sortedPredictions.Count > 0)
            {
                // Take the prediction with highest confidence
                var best = sortedPredictions[0];
                result.Add(best);
                sortedPredictions.RemoveAt(0);

                // Remove all predictions that overlap significantly with the best one
                sortedPredictions.RemoveAll(p => CalculateIoU(best.Rectangle, p.Rectangle) > iouThreshold);
            }

            return result;
        }

        // Calculate Intersection over Union (IoU) between two rectangles
        private float CalculateIoU(RectangleF rect1, RectangleF rect2)
        {
            // Calculate intersection
            float x1 = Math.Max(rect1.Left, rect2.Left);
            float y1 = Math.Max(rect1.Top, rect2.Top);
            float x2 = Math.Min(rect1.Right, rect2.Right);
            float y2 = Math.Min(rect1.Bottom, rect2.Bottom);

            float intersectionWidth = Math.Max(0, x2 - x1);
            float intersectionHeight = Math.Max(0, y2 - y1);
            float intersectionArea = intersectionWidth * intersectionHeight;

            // Calculate union
            float area1 = rect1.Width * rect1.Height;
            float area2 = rect2.Width * rect2.Height;
            float unionArea = area1 + area2 - intersectionArea;

            // Avoid division by zero
            if (unionArea == 0)
                return 0;

            return intersectionArea / unionArea;
        }

        #endregion AI Loop Functions

        #endregion AI

        #region Screen Capture

        private void SaveFrame(Bitmap frame, Prediction? DoLabel = null)
        {
            // Only save frames if "Collect Data While Playing" is enabled
            if (!Dictionary.toggleState["Collect Data While Playing"]) return;

            // Skip if we're in constant tracking mode (unless auto-labeling is enabled)
            if (Dictionary.toggleState["Constant AI Tracking"] && !Dictionary.toggleState["Auto Label Data"]) return;

            // Cooldown check
            if ((DateTime.Now - lastSavedTime).TotalMilliseconds < SAVE_FRAME_COOLDOWN_MS) return;

            try
            {
                // Validate bitmap is still usable
                if (frame == null) return;

                // Accessing Width/Height will throw if bitmap is disposed
                int width = frame.Width;
                int height = frame.Height;
                if (width <= 0 || height <= 0) return;

                lastSavedTime = DateTime.Now;
                string uuid = Guid.NewGuid().ToString();
                string imagePath = Path.Combine("bin", "images", $"{uuid}.jpg");

                // Save synchronously to avoid "Object is currently in use elsewhere" error
                frame.Save(imagePath, ImageFormat.Jpeg);

                if (Dictionary.toggleState["Auto Label Data"] && DoLabel != null)
                {
                    var labelPath = Path.Combine("bin", "labels", $"{uuid}.txt");

                    float x = (DoLabel!.Rectangle.X + DoLabel.Rectangle.Width / 2) / width;
                    float y = (DoLabel!.Rectangle.Y + DoLabel.Rectangle.Height / 2) / height;
                    float labelWidth = DoLabel.Rectangle.Width / width;
                    float labelHeight = DoLabel.Rectangle.Height / height;

                    File.WriteAllText(labelPath, $"{DoLabel.ClassId} {x} {y} {labelWidth} {labelHeight}");
                }
            }
            catch (ArgumentException)
            {
                // Bitmap was disposed or invalid - silently ignore
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"SaveFrame failed: {ex.Message}");
            }
        }



        #endregion Screen Capture

        public void Dispose()
        {
            lock (_modelLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;
            }

            // Signal that we're shutting down
            lock (_sizeLock)
            {
                _sizeChangePending = true;
            }

            // Stop the loop
            _isAiLoopRunning = false;
            if (_aiLoopThread != null && _aiLoopThread.IsAlive)
            {
                if (!_aiLoopThread.Join(TimeSpan.FromSeconds(1)))
                {
                    try { _aiLoopThread.Interrupt(); }
                    catch { }
                }
            }

            // Print final benchmarks
            PrintBenchmarks();

            // Dispose DXGI objects
            _captureManager.Dispose();

            // Clean up other resources
            _reusableInputArray = null;
            _reusableInputs = null;

            lock (_modelLock)
            {
                _onnxModel?.Dispose();
                _onnxModel = null;
                _onnxModelSlot1?.Dispose();
                _onnxModelSlot1 = null;
                _onnxModelSlot2?.Dispose();
                _onnxModelSlot2 = null;

                _engineModel?.Dispose();
                _engineModel = null;
                _engineModelSlot1?.Dispose();
                _engineModelSlot1 = null;
                _engineModelSlot2?.Dispose();
                _engineModelSlot2 = null;
            }
            _modeloptions?.Dispose();
            _bitmapBuffer = null;
        }

        public static bool IsTensorRTAvaliable()
        {
            try
            {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrEmpty(pathEnv)) return false;

                var paths = pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);
                bool hasCuda = false;
                bool hasTrt = false;

                foreach (var p in paths)
                {
                    if (!Directory.Exists(p)) continue;

                    if (!hasCuda)
                    {
                        var files = Directory.GetFiles(p, "cudart64_*.dll");
                        if (files.Length > 0) hasCuda = true;
                    }

                    if (!hasTrt)
                    {
                        var files = Directory.GetFiles(p, "nvinfer*.dll");
                        if (files.Length > 0) hasTrt = true;
                    }

                    if (hasCuda && hasTrt) return true;
                }

                return hasCuda && hasTrt;
            }
            catch
            {
                return false;
            }
        }
    }
    public class Prediction
    {
        public RectangleF Rectangle { get; set; }
        public float Confidence { get; set; }
        public int ClassId { get; set; } = 0;
        public string ClassName { get; set; } = "Enemy";
        public float CenterXTranslated { get; set; }
        public float CenterYTranslated { get; set; }
        public float ScreenCenterX { get; set; }  // Absolute screen position
        public float ScreenCenterY { get; set; }
    }
}


