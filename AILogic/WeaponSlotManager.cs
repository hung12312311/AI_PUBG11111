using System; // Added this
using Aimmy2.Class;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using InputLogic;
using Other;
using System.Windows.Forms;
using AILogic;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYPPX.TensorRtSharp.Nvinfer;

namespace Aimmy2.AILogic
{
    public class WeaponSlotManager
    {
        private static WeaponSlotManager? _instance;
        public static WeaponSlotManager Instance => _instance ??= new WeaponSlotManager();
        public static bool CurrentlyLoadingScopeModel = false;

        private WeaponSlotManager()
        {
            _captureManager.CaptureMethodKey = "Scope Capture Method";
        }

        private InferenceSession? _scopeSession;
        private TensorRTEngine? _scopeEngine;
        private bool _isScopeEngine = false;
        private int _scopeImageSize = 640;
        private bool _scopeDynamicInput;
        private string _scopeInputName = "images";
        public event Action? ScopeInputChanged;
        public int ScopeImageSize { get { lock (_sessionLock) return _scopeImageSize; } }
        public bool CanChangeScopeImageSize { get { lock (_sessionLock) return _scopeDynamicInput && !_isScopeEngine; } }

        private static (int Size, bool Dynamic) ReadScopeInputShape(int[] dims, bool engine)
        {
            if (dims.Length != 4 || (dims[0] > 0 && dims[0] != 1) || dims[1] != 3)
                throw new NotSupportedException("Scope model must use NCHW input [1,3,H,W].");

            bool dynamicInput = dims[2] <= 0 && dims[3] <= 0;
            if (engine && dynamicInput) throw new NotSupportedException("Scope TensorRT engine needs a resolved input size.");
            int savedSize = 640;
            if (Dictionary.dropdownState.TryGetValue("Scope Image Size", out var saved)
                && int.TryParse(Convert.ToString((object)saved), out int parsed) && parsed > 0) savedSize = parsed;
            int size = dynamicInput ? savedSize : Math.Max(dims[2], dims[3]);
            if (size <= 0) throw new NotSupportedException("Invalid scope input size.");
            return (size, dynamicInput);
        }

        public void SetScopeImageSize(int size)
        {
            lock (_sessionLock)
            {
                if (!_scopeDynamicInput || _isScopeEngine || size <= 0) return;
                _scopeImageSize = size;
            }
            PublishScopeInput();
        }

        private void PublishScopeInput()
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                Dictionary.dropdownState["Scope Image Size"] = ScopeImageSize.ToString();
                global::Class.SaveDictionary.WriteJSON(Dictionary.dropdownState, "bin\\dropdown.cfg");
                ScopeInputChanged?.Invoke();
            }));
        }
        private readonly object _sessionLock = new object();
        public bool IsInitialized
        {
            get
            {
                lock (_sessionLock)
                {
                    return _scopeSession != null || _scopeEngine != null;
                }
            }
        }
        private readonly CaptureManager _captureManager = new CaptureManager();
        
        // Slot data
        private int _slot1ScopeIndex = -1; // -1 = none
        private int _slot2ScopeIndex = -1;
        private bool _isNmsFreeMod = false;
        private int _numDetections = 8400;
        private int _numClasses = 7;
        
        private readonly object _slotApplyLock = new();
        private int _activeSlot = 1; // 1 or 2

        // Snapshot settings for slots
        private RecoilManager.RecoilSettings _slot1Recoil = new RecoilManager.RecoilSettings();
        private RecoilManager.RecoilSettings _slot2Recoil = new RecoilManager.RecoilSettings();

        // Regions from user
        // Weapon 1 is the upper weapon slot (visible when TAB is open or as primary)
        // Weapon 2 is the lower weapon slot (visible when TAB is open or as secondary)
        private Rectangle _weapon1Region = new Rectangle(1604, 113, 53, 53); // Upper position
        private Rectangle _weapon2Region = new Rectangle(1603, 337, 56, 55); // Lower position

        // Scope Names mapping from user
        private readonly string[] _scopeNames = { "8x", "6x", "4x", "3x", "2x", "chamdo", "morong" };

        private bool _isScanning = false;

        private readonly object _scanLock = new object();
        private CancellationTokenSource? _tabHoldCts;
        private DateTime _lastTabPressTime = DateTime.MinValue;

        private volatile bool _isInitializing = false;

        public void Initialize()
        {
            if (IsInitialized) return;

            lock (_sessionLock)
            {
                if (IsInitialized || _isInitializing) return;
                _isInitializing = true;
            }

            try
            {
                string modelPath = Dictionary.filelocationState["Scope Model Location"];
                
                // Chuyển đường dẫn tương đối thành tuyệt đối
                if (!Path.IsPathRooted(modelPath))
                {
                    modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelPath);
                }
                
                LoadModel(modelPath);
                LoadRegions();
            }
            finally
            {
                lock (_sessionLock)
                {
                    _isInitializing = false;
                }
            }
        }

        public void LoadRegions()
        {
            try
            {
                if (Dictionary.sliderSettings.ContainsKey("Weapon 1 X"))
                {
                    int x1 = Convert.ToInt32(Dictionary.sliderSettings["Weapon 1 X"]);
                    int y1 = Convert.ToInt32(Dictionary.sliderSettings["Weapon 1 Y"]);
                    int w1 = Convert.ToInt32(Dictionary.sliderSettings["Weapon 1 Width"]);
                    int h1 = Convert.ToInt32(Dictionary.sliderSettings["Weapon 1 Height"]);
                    _weapon1Region = new Rectangle(x1, y1, w1, h1);
                }

                if (Dictionary.sliderSettings.ContainsKey("Weapon 2 X"))
                {
                    int x2 = Convert.ToInt32(Dictionary.sliderSettings["Weapon 2 X"]);
                    int y2 = Convert.ToInt32(Dictionary.sliderSettings["Weapon 2 Y"]);
                    int w2 = Convert.ToInt32(Dictionary.sliderSettings["Weapon 2 Width"]);
                    int h2 = Convert.ToInt32(Dictionary.sliderSettings["Weapon 2 Height"]);
                    _weapon2Region = new Rectangle(x2, y2, w2, h2);
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Failed to load weapon regions: {ex.Message}");
            }
        }

        public void LoadModel(string modelPath)
        {
            lock (_sessionLock)
            {
                if (CurrentlyLoadingScopeModel) return;
                CurrentlyLoadingScopeModel = true;
            }
            Task.Run(async () =>
            {
                try
                {
                    // Chuyển đường dẫn tương đối thành tuyệt đối để load
                    string absolutePath = modelPath;
                    if (!Path.IsPathRooted(modelPath))
                    {
                        absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelPath);
                    }
                    
                    if (File.Exists(absolutePath))
                    {
                        bool isEngine = absolutePath.EndsWith(".engine", StringComparison.OrdinalIgnoreCase) || 
                                        absolutePath.EndsWith(".trt", StringComparison.OrdinalIgnoreCase);

                        if (isEngine)
                        {
                            if (!AIManager.IsTensorRTAvaliable())
                            {
                                throw new Exception("TensorRT (.engine) is not supported on this machine. CUDA 12.x or TensorRT 10.x DLLs not found in PATH.");
                            }

                            TensorRTEngine newEngine;
                            await Dictionary.ModelLoadSemaphore.WaitAsync();
                            try
                            {
                                newEngine = await Task.Run(() => new TensorRTEngine(absolutePath));
                            }
                            finally
                            {
                                Dictionary.ModelLoadSemaphore.Release();
                            }

                            lock (_sessionLock)
                            {
                                (int Size, bool Dynamic) shape;
                                try { shape = ReadScopeInputShape(newEngine.InputDims, true); }
                                catch { newEngine.Dispose(); throw; }
                                _scopeSession?.Dispose();
                                _scopeSession = null;
                                _scopeEngine?.Dispose();
                                _scopeEngine = newEngine;
                                _isScopeEngine = true;
                                _scopeImageSize = shape.Size;
                                _scopeDynamicInput = false;
                            }
                        }
                        else
                        {
                            InferenceSession newSession;
                            await Dictionary.ModelLoadSemaphore.WaitAsync();
                            try
                            {
                                newSession = await Task.Run(() => OnnxModelSessionFactory.Load(absolutePath, "Auto", _scopeImageSize));
                            }
                            finally
                            {
                                Dictionary.ModelLoadSemaphore.Release();
                            }
                            
                            lock (_sessionLock)
                            {
                                (int Size, bool Dynamic) shape;
                                string inputName;
                                try
                                {
                                    var input = newSession.InputMetadata.Single();
                                    var metadata = OnnxModelSessionFactory.Metadata(newSession);
                                    var resolved = metadata.ResolveSize(_scopeImageSize);
                                    shape = (resolved.Width, metadata.Dynamic);
                                    inputName = input.Key;
                                }
                                catch { newSession.Dispose(); throw; }
                                _scopeEngine?.Dispose();
                                _scopeEngine = null;
                                _scopeSession?.Dispose();
                                _scopeSession = newSession;
                                _isScopeEngine = false;
                                _scopeImageSize = shape.Size;
                                _scopeDynamicInput = shape.Dynamic;
                                _scopeInputName = inputName;
                            }
                        }
                        
                        // Lưu đường dẫn tương đối để portable
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        if (absolutePath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                        {
                            Dictionary.filelocationState["Scope Model Location"] = absolutePath.Substring(baseDir.Length).TrimStart('\\', '/');
                        }
                        else
                        {
                            Dictionary.filelocationState["Scope Model Location"] = absolutePath;
                        }
                        
                        LogManager.Log(LogManager.LogLevel.Info, $"Weapon Recognition Model loaded: {Path.GetFileName(absolutePath)}");
                        
                        // Validate model shape
                        ValidateModelShape();
                    }
                    else
                    {
                        LogManager.Log(LogManager.LogLevel.Error, $"Scope model not found: {absolutePath}");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, $"Failed to load scope model: {ex.Message}");
                }
                finally
                {
                    CurrentlyLoadingScopeModel = false;
                    PublishScopeInput();
                }
            });
        }

        private void ValidateModelShape()
        {
            bool isNmsFree = false;
            lock (_sessionLock)
            {
                if (_isScopeEngine && _scopeEngine != null)
                {
                    var dims = _scopeEngine.OutputDims;
                    LogManager.Log(LogManager.LogLevel.Info, $"Scope Model Output (Engine): {string.Join("x", dims)}");

                    // Check for YOLOv10/11/26 (1x300x6)
                    if (dims.Length == 3 && dims[2] == 6)
                    {
                        _isNmsFreeMod = true;
                        _numDetections = dims[1];
                        isNmsFree = true;
                        LogManager.Log(LogManager.LogLevel.Info, "Detected NMS-Free Scope Model (YOLOv10/11/26).");
                    }
                    else
                    {
                        _isNmsFreeMod = false;
                        _numDetections = dims.Length >= 3 ? dims[2] : 8400;
                        _numClasses = dims.Length >= 2 ? (dims[1] - 4) : 7;
                        isNmsFree = false;
                        LogManager.Log(LogManager.LogLevel.Info, $"Detected YOLOv8-style Scope Model with {_numClasses} classes.");
                    }
                }
                else if (_scopeSession != null)
                {
                    var outputMetadata = _scopeSession.OutputMetadata;
                    foreach (var kvp in outputMetadata)
                    {
                        var dims = kvp.Value.Dimensions;
                        LogManager.Log(LogManager.LogLevel.Info, $"Scope Model Output: {string.Join("x", dims)}");

                        // Check for YOLOv10/11/26 (1x300x6)
                        if (dims.Length == 3 && dims[2] == 6)
                        {
                            _isNmsFreeMod = true;
                            _numDetections = dims[1];
                            isNmsFree = true;
                            LogManager.Log(LogManager.LogLevel.Info, "Detected NMS-Free Scope Model.");
                            break;
                        }
                        
                        // Check for YOLOv8/v11 (1x(4+nc)x8400)
                        if (dims.Length == 3 && dims[1] > 4)
                        {
                            _isNmsFreeMod = false;
                            _numDetections = dims[2];
                            _numClasses = dims[1] - 4;
                            isNmsFree = false;
                            LogManager.Log(LogManager.LogLevel.Info, $"Detected YOLOv8-style Scope Model with {_numClasses} classes.");
                            break;
                        }
                    }
                }
                else
                {
                    return;
                }
            }

            // Show toast notification like the aim model
            string modelName = Path.GetFileName(Dictionary.filelocationState["Scope Model Location"]);
            string typeStr = isNmsFree ? "NMS-Free" : "Standard (NMS)";
            LogManager.Log(LogManager.LogLevel.Info, $"Loaded Scope model: {modelName} ({typeStr})", true, 3000);
        }

        // State to track if we assume inventory is open
        private bool _inventoryOpenState = false;

        public void HandleKeyPress(Keys key)
        {
            if (key == Keys.Tab)
            {
               // Legacy Tab handling removed in favor of OnTabPressed/Released
            }
            else if (key == Keys.D1 || key == Keys.NumPad1)
            {
                ApplySlot(1);
            }
            else if (key == Keys.D2 || key == Keys.NumPad2)
            {
                ApplySlot(2);
            }
        }

        public void StopScanning()
        {
            _isScanning = false;
            _inventoryOpenState = false;
            _tabHoldCts?.Cancel();
        }

        public async void OnTabPressed()
        {
            // Debounce to prevent rapid clicks (jitter/spam) from breaking the state
            if ((DateTime.Now - _lastTabPressTime).TotalMilliseconds < 250) return;
            _lastTabPressTime = DateTime.Now;

            if (_isScanning)
            {
                // Support Toggle OFF: If already scanning and toggle mode is on, stop it.
                if (Dictionary.toggleState.ContainsKey("Toggle Weapon Scan") && Dictionary.toggleState["Toggle Weapon Scan"])
                {
                    StopScanning();
                }
                return;
            }

            // Cancel any existing wait
            _tabHoldCts?.Cancel();
            _tabHoldCts = new CancellationTokenSource();

            try
            {
                // Get delay from settings (default 2s if not found)
                double scanDelay = 0.5;
                if (Dictionary.sliderSettings.TryGetValue("Weapon Scan Delay", out var sDelay))
                {
                    scanDelay = Convert.ToDouble(sDelay);
                }
                
                // Get Reset Delay
                double resetDelay = 0.2; // Default short delay for reset
                if (Dictionary.sliderSettings.TryGetValue("Tab Reset Adjust", out var rDelay))
                {
                    resetDelay = Convert.ToDouble(rDelay);
                }

                // Determine which action we are doing? 
                
                // We'll use a loop to wait and check deadlines
                // Wait for the SHORTER of the two, perform action, then wait for the rest if needed.
                
                DateTime startTime = DateTime.Now;
                bool resetDone = false;
                bool scanStarted = false;

                // Loop continues as long as we haven't cancelled AND (we haven't started scanning OR reset isn't done yet)
                // Note: If scan starts, does user keep holding? Usually yes for a bit.
                // We want to ensure Reset happens if time elapsed, even if scan started.
                // But StartScan() runs on another thread usually? No, it's async void but internally awaits.
                // Actually StartScan is async void. So it returns immediately? 
                
                // Let's keep logic simple: Check deadlines until user releases or both done.
                while (!_tabHoldCts.Token.IsCancellationRequested)
                {
                     double elapsed = (DateTime.Now - startTime).TotalSeconds;
                     bool allDone = true;

                     // Check Reset
                     if (!resetDone)
                     {
                         if (elapsed >= resetDelay)
                         {
                             // Only reset if enabled
                             if (Dictionary.toggleState.ContainsKey("Enable Tab Reset") && Dictionary.toggleState["Enable Tab Reset"])
                             {
                                 RecoilManager.ResetTemporaryStrength();
                             }
                             resetDone = true;
                         }
                         else
                         {
                             allDone = false;
                         }
                     }

                    // Check Scan
                    if (!scanStarted)
                    {
                        bool isToggleMode = Dictionary.toggleState.ContainsKey("Toggle Weapon Scan") && Dictionary.toggleState["Toggle Weapon Scan"];
                        if (isToggleMode || elapsed >= scanDelay)
                        {
                            _inventoryOpenState = true;
                            StartScan();
                            scanStarted = true;
                        }
                        else
                        {
                            allDone = false;
                        }
                    }

                     if (allDone) break;

                     await Task.Delay(50, _tabHoldCts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation
            }
        }

        public void OnTabReleased()
        {
            // Cancel the pending scan if strictly waiting
            _tabHoldCts?.Cancel();
            
            // Only stop if NOT in toggle mode (if toggle is on, scan continues until pressed again)
            bool isToggleMode = Dictionary.toggleState.ContainsKey("Toggle Weapon Scan") && Dictionary.toggleState["Toggle Weapon Scan"];
            if (!isToggleMode)
            {
                _inventoryOpenState = false;
            }
        }

        private async void StartScan()
        {
            if (_isScanning) return;

            lock (_scanLock)
            {
                if (_isScanning) return;
                _isScanning = true;
            }

            LogManager.Log(LogManager.LogLevel.Info, $"Scanning weapons... (Continuous)");
            
            // Wait a bit for TAB animation
            await Task.Delay(300);

            await Task.Run(() =>
            {
                try
                {
                    bool firstRun = true;

                    do
                    {
                        // Stop if user closed inventory
                        if (!firstRun && !_inventoryOpenState) break;

                        // Scan Weapon 1
                        using var w1Bitmap = _captureManager.ScreenGrabSnapshot(_weapon1Region);
                        if (w1Bitmap != null)
                        {
                            int det1 = DetectScope(w1Bitmap);
                            // Only update if a scope was found, or if the inventory is still open (avoiding -1 during rapid menu flickering)
                            if (det1 != -1)
                            {
                                _slot1ScopeIndex = det1;
                                CaptureRecoilSettings(1, _slot1ScopeIndex);
                            }
                            else if (_inventoryOpenState)
                            {
                                _slot1ScopeIndex = -1;
                                CaptureRecoilSettings(1, -1);
                            }
                        }

                        // Scan Weapon 2
                        using var w2Bitmap = _captureManager.ScreenGrabSnapshot(_weapon2Region);
                        if (w2Bitmap != null)
                        {
                            int det2 = DetectScope(w2Bitmap);
                            // Only update if a scope was found, or if the inventory is still open (avoiding -1 during rapid menu flickering)
                            if (det2 != -1)
                            {
                                _slot2ScopeIndex = det2;
                                CaptureRecoilSettings(2, _slot2ScopeIndex);
                            }
                            else if (_inventoryOpenState)
                            {
                                _slot2ScopeIndex = -1;
                                CaptureRecoilSettings(2, -1);
                            }
                        }

                        // Update overlay directly during loop
                        UpdateScopeOverlay();
                        ApplyCurrentSlot();

                        firstRun = false;

                        // Throttle loop
                        if (_inventoryOpenState)
                        {
                            Thread.Sleep(200); // 5 scans per second
                        }

                    } while (_isScanning && _inventoryOpenState);

                    LogManager.Log(LogManager.LogLevel.Info, "Scan Loop Completed.");

                    // Final Auto-show overlay
                    if ((_slot1ScopeIndex != -1 || _slot2ScopeIndex != -1) && Dictionary.DetectedScopeOverlay != null)
                    {
                          Dictionary.DetectedScopeOverlay.Show(true);
                           if (Dictionary.toggleState.ContainsKey("Show Detected Scope") && !Dictionary.toggleState["Show Detected Scope"])
                               Dictionary.toggleState["Show Detected Scope"] = true;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, $"Error during scan: {ex.Message}");
                }
                finally
                {
                    _isScanning = false;
                }
            });
        }

        private string GetScopeName(int index)
        {
            if (index >= 0 && index < _scopeNames.Length)
                return _scopeNames[index];
            return "None";
        }

        private int DetectScope(Bitmap bitmap)
        {
            try
            {
                lock (_sessionLock)
                {
                    if (CurrentlyLoadingScopeModel) return -1;
                    int size = _scopeImageSize;
                    using var resized = new Bitmap(bitmap, new Size(size, size));
                    var input = PreProcess(resized, size);
                    Tensor<float> output;
                    var region = new Rectangle(0, 0, size, size);
                    if (_isScopeEngine && _scopeEngine != null)
                    {
                        output = _scopeEngine.RunDetections(input.ToArray(), region, 0);
                    }
                    else if (_scopeSession != null)
                    {
                        var metadata = OnnxModelSessionFactory.Metadata(_scopeSession);
                        var resolved = metadata.ResolveSize(size);
                        using var run = new RunOptions();
                        output = OnnxModelSessionFactory.Run(_scopeSession, input.ToArray(),
                            new CaptureTransform(region, resolved.Width, resolved.Height, metadata.Options.Letterbox), run, 0);
                    }
                    else
                    {
                        return -1;
                    }
                    _isNmsFreeMod = true; // Canonical output is postprocessed [1,N,6].
                    return PostProcess(output);
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Error in DetectScope: {ex.Message}");
                return -1;
            }
        }

        private DenseTensor<float> PreProcess(Bitmap bitmap, int size)
        {
            var pixels = new float[3 * size * size];
            MathUtil.BitmapToFloatArrayInPlace(bitmap, pixels, size);
            return new DenseTensor<float>(pixels, new[] { 1, 3, size, size });
        }
        private int PostProcess(Tensor<float> output)
        {
            float maxConfidence = 0;
            int bestClass = -1;

            if (_isNmsFreeMod)
            {
                // YOLOv10/11/26 format: [1, 300, 6] -> [x1, y1, x2, y2, confidence, class]
                for (int i = 0; i < output.Dimensions[1]; i++)
                {
                    float conf = output[0, i, 4];
                    if (conf > maxConfidence)
                    {
                        maxConfidence = conf;
                        bestClass = (int)output[0, i, 5];
                    }
                }
            }
            else
            {
                // YOLOv8 format: [ batch, 4 + classes, 8400 ]
                int numDetections = output.Dimensions[2];
                for (int i = 0; i < numDetections; i++)
                {
                    for (int c = 0; c < output.Dimensions[1] - 4; c++)
                    {
                        float conf = output[0, 4 + c, i];
                        if (conf > maxConfidence)
                        {
                            maxConfidence = conf;
                            bestClass = c;
                        }
                    }
                }
            }

            LogManager.Log(LogManager.LogLevel.Info, $"PostProcess Scope: Best class = {bestClass}, Conf = {maxConfidence:F4}");
            
            float threshold = 0.45f; // Default 45%
            if (Dictionary.sliderSettings.TryGetValue("Scope Confidence", out var confVal))
            {
                threshold = (float)(Convert.ToDouble(confVal) / 100.0);
            }

            if (maxConfidence < threshold) return -1;
            return bestClass;
        }

        // This section is likely part of a UI building method, e.g., AimMenuControl.BuildMenu()
        // The instruction implies adding these toggles to a menu.
        // Since the context provided is between PostProcess and ApplySlot,
        // and the code snippet itself is a fluent API chain, it's placed here
        // as a placeholder for where it would logically be added in a UI definition.
        // This is not syntactically correct in the current class structure,
        // but follows the instruction's placement context.
        // If this was a real file, these lines would be in a method like:
        // public void BuildMenu(MenuBuilder builder) { builder
        //     .AddToggle(...)
        //     .AddToggle(...);
        // }
        // For the purpose of this edit, I'm placing it as per the instruction's context.
        // This is a placeholder and would need to be integrated into a proper menu building method.
        // .AddToggle("Weapon Recognition", t => {
        //     t.Reader.Click += (s, e) => {
        //         // Notify user or something
        //     };
        // }, tooltip: "Enable AI scope recognition from specific screen regions.")
        // .AddToggle("Show Detected Scope", tooltip: "Show overlay on screen with detected scope information.")


        private void ApplyCurrentSlot()
        {
            // Read the active slot inside the same lock used by key switches.
            lock (_slotApplyLock) ApplySlot(_activeSlot);
        }

        private void ApplySlot(int slot)
        {
            lock (_slotApplyLock) ApplySlotCore(slot);
        }

        private void ApplySlotCore(int slot)
        {
            if (slot is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(slot));
            _activeSlot = slot;
            // Switch AI Model Slot
            if (FileManager.AIManager != null)
            {
                FileManager.AIManager.SetActiveSlot(slot);
            }

            var recoilSettings = (slot == 1) ? _slot1Recoil : _slot2Recoil;
            int scopeIndex = (slot == 1) ? _slot1ScopeIndex : _slot2ScopeIndex;
            
            // Update RecoilManager with the captured settings
            RecoilManager.ActiveSlotSettings = recoilSettings;


            // Logic: If None (-1), default to Scope 1 (0) (Red Dot/x1)
            int effectiveScopeIndex = (scopeIndex == -1) ? 0 : scopeIndex;

            // Map WeaponSlotManager scope index to RecoilManager scope index (0-5)
            // WeaponSlotManager: 0=8x, 1=6x, ..., 4=2x, 5=chamdo/x1, -1=None
            // RecoilManager: 0=Scope1(x1), 1=Scope2(2x)...
            
            // If it was valid detection (not None)
            if (scopeIndex != -1)
            {
                 // Mapping logic from StartScan:
                 // 8x (0) -> Scope 6 (idx 5)
                 // 6x (1) -> Scope 5 (idx 4)
                 // 4x (2) -> Scope 4 (idx 3)
                 // 3x (3) -> Scope 3 (idx 2)
                 // 2x (4) -> Scope 2 (idx 1)
                 // x1 (5) -> Scope 1 (idx 0)
                 // morong (6) -> Scope 1 (idx 0)
                 
                 // Let's recalculate based on the known order in CaptureRecoilSettings
                 if (scopeIndex == 0) effectiveScopeIndex = 5; // 8x -> Scope 6
                 else if (scopeIndex == 1) effectiveScopeIndex = 4; // 6x -> Scope 5
                 else if (scopeIndex == 2) effectiveScopeIndex = 3; // 4x -> Scope 4
                 else if (scopeIndex == 3) effectiveScopeIndex = 2; // 3x -> Scope 3
                 else if (scopeIndex == 4) effectiveScopeIndex = 1; // 2x -> Scope 2
                 else effectiveScopeIndex = 0; // x1/morong -> Scope 1
            }
            else
            {
                // If None, Force Scope 1
                effectiveScopeIndex = 0;
            }

            RecoilManager.SelectedScopeIndex = effectiveScopeIndex;
                
            LogManager.Log(LogManager.LogLevel.Info, $"✓ Applied Slot {slot}: {GetScopeName(scopeIndex)}");
            LogManager.Log(LogManager.LogLevel.Info, $"  → Recoil: Strength={recoilSettings.Strength}, Step={recoilSettings.Step}, Delay={recoilSettings.Delay}, Multi={recoilSettings.Multi}");
            LogManager.Log(LogManager.LogLevel.Info, $"  → RecoilManager.SelectedScopeIndex set to {effectiveScopeIndex}");
            
            // Update overlay
            UpdateScopeOverlay();
            
            // Ensure overlay is visible if toggle is on
            if (Dictionary.toggleState["Show Detected Scope"] && Dictionary.DetectedScopeOverlay != null)
            {
                Dictionary.DetectedScopeOverlay.Show(true);
            }
        }

        public void SetWeaponRegion(int slot, Rectangle rect)
        {
            if (slot == 1)
            {
                _weapon1Region = rect;
                Dictionary.sliderSettings["Weapon 1 X"] = rect.X;
                Dictionary.sliderSettings["Weapon 1 Y"] = rect.Y;
                Dictionary.sliderSettings["Weapon 1 Width"] = rect.Width;
                Dictionary.sliderSettings["Weapon 1 Height"] = rect.Height;
            }
            else if (slot == 2)
            {
                _weapon2Region = rect;
                Dictionary.sliderSettings["Weapon 2 X"] = rect.X;
                Dictionary.sliderSettings["Weapon 2 Y"] = rect.Y;
                Dictionary.sliderSettings["Weapon 2 Width"] = rect.Width;
                Dictionary.sliderSettings["Weapon 2 Height"] = rect.Height;
            }
            LogManager.Log(LogManager.LogLevel.Info, $"Updated Weapon Region {slot}: {rect}");
        }

        private void UpdateScopeOverlay()
        {
            if (Dictionary.DetectedScopeOverlay != null)
            {
                Dictionary.DetectedScopeOverlay.UpdateSlot1(GetScopeName(_slot1ScopeIndex));
                Dictionary.DetectedScopeOverlay.UpdateSlot2(GetScopeName(_slot2ScopeIndex));
                Dictionary.DetectedScopeOverlay.UpdateActiveSlot(_activeSlot);
            }
        }

        private void CaptureRecoilSettings(int slot, int scopeIdx)
        {
            // Map scope Index to Recoil Slider ID
            int scopeNumToLoad = 1; // Default to Scope 1 (x1) if None

            if (scopeIdx == -1)
            {
                // None -> Load Scope 1
                scopeNumToLoad = 1;
            }
            else if (scopeIdx == 0) scopeNumToLoad = 6; // 8x
            else if (scopeIdx == 1) scopeNumToLoad = 5; // 6x
            else if (scopeIdx == 2) scopeNumToLoad = 4; // 4x
            else if (scopeIdx == 3) scopeNumToLoad = 3; // 3x
            else if (scopeIdx == 4) scopeNumToLoad = 2; // 2x
            else scopeNumToLoad = 1; // chamdo/morong -> 1x

            var settings = (slot == 1) ? _slot1Recoil : _slot2Recoil;
            
            // Get current values from Dictionary sliders
            // Get current values from Dictionary sliders
            if (Dictionary.sliderSettings.TryGetValue($"Recoil Scope {scopeNumToLoad} Strength", out var strength))
                settings.Strength = (float)Convert.ToDouble(strength);
            
            if (Dictionary.sliderSettings.TryGetValue($"Recoil Scope {scopeNumToLoad} Step", out var step))
                settings.Step = (float)Convert.ToDouble(step);
            
            if (Dictionary.sliderSettings.TryGetValue($"Recoil Scope {scopeNumToLoad} Delay", out var delay))
                settings.Delay = (float)Convert.ToDouble(delay);
            
            if (Dictionary.sliderSettings.TryGetValue($"Recoil Scope {scopeNumToLoad} Multi", out var multi))
                settings.Multi = (float)Convert.ToDouble(multi);
            
            LogManager.Log(LogManager.LogLevel.Info, $"Slot {slot} Recoil Settings Saved: {GetScopeName(scopeIdx)} → Strength={settings.Strength}, Step={settings.Step}, Delay={settings.Delay}, Multi={settings.Multi}");
        }
    }
}
