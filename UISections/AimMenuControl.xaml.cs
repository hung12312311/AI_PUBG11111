using Aimmy2.AILogic;
using AILogic;
using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.UILibrary;
using Class;
using InputLogic;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Other;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UILibrary;
using Visuality;

namespace Aimmy2.Controls
{
    public partial class AimMenuControl : UserControl
    {
        //--
        UISections.ColorPicker colorPickerInstance = null;
        UISections.ColorPicker fovColorPickerInstance = null;
        UISections.ColorPicker crosshairColorPickerInstance = null;
        //--
        private MainWindow? _mainWindow;
        private bool _isInitialized;

        // Local minimize state management
        private readonly Dictionary<string, bool> _localMinimizeState = new()
        {
            { "Aim Assist", false },
            { "Aim Config", false },
            { "Aim Config (Slot 1)", false },
            { "Aim Config (Slot 2)", false },
            { "Predictions", false },
            { "Auto Trigger", false },
            { "FOV Config", false },
            { "ESP Config", false },
            { "Weapon Slot System", false },
            { "Recoil Config", false },
            { "Fast Loot Config", false }
        };

        // Public properties for MainWindow access
        public StackPanel AimAssistPanel => AimAssist;
        public StackPanel TriggerBotPanel => TriggerBot;
        public StackPanel ESPConfigPanel => ESPConfig;
        public StackPanel AimConfigSlot1Panel => AimConfigSlot1;
        public StackPanel AimConfigPanel => AimConfig;
        public StackPanel PredictionsPanel => Predictions;
        public StackPanel FOVConfigPanel => FOVConfig;
        public StackPanel WeaponSlotSystemPanel => WeaponSlotSystem;
        public StackPanel LootConfigPanel => LootConfig;
        public StackPanel RecoilConfigPanel => RecoilConfig;
        public ScrollViewer AimMenuScrollViewer => AimMenu;

        public AimMenuControl()
        {
            InitializeComponent();
            Loaded += (_, _) => global::Other.UiLanguage.RefreshTree(this);
        }

        public void Initialize(MainWindow mainWindow)
        {
            if (_isInitialized) return;

            _mainWindow = mainWindow;
            _isInitialized = true;

            // Load minimize states from global dictionary if they exist
            LoadMinimizeStatesFromGlobal();

            AIManager.ImageSizeUpdated += OnImageSizeChanged;

            // Load all sections with error handling
            try { LoadAimAssist(); } catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Error, "Error loading AimAssist: " + ex.Message); }
            try { LoadAimConfig(); } catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Error, "Error loading AimConfig: " + ex.Message); }
            try { LoadPredictions(); } catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Error, "Error loading Predictions: " + ex.Message); }
            try { LoadTriggerBot(); } catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Error, "Error loading TriggerBot: " + ex.Message); }
            try { LoadFOVConfig(); } catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Error, "Error loading FOVConfig: " + ex.Message); }
            OnImageSizeChanged(FovSettings.ImageSize);
            try { LoadESPConfig(); } catch (Exception ex) { LogManager.Log(LogManager.LogLevel.Error, "Error loading ESPConfig: " + ex.Message); }
            
            try 
            { 
                LoadWeaponSlotSystem(); 
            } 
            catch (Exception ex) 
            { 
                LogManager.Log(LogManager.LogLevel.Error, "Error loading WeaponSlotSystem: " + ex.Message); 
            }

            try 
            { 
                LoadRecoilConfig(); 
            } 
            catch (Exception ex) 
            { 
                LogManager.Log(LogManager.LogLevel.Error, "Error loading RecoilConfig: " + ex.Message); 
            }

            try 
            { 
                LoadLootConfig(); 
            } 
            catch (Exception ex) 
            { 
                LogManager.Log(LogManager.LogLevel.Error, "Error loading LootConfig: " + ex.Message); 
            }

            // Apply minimize states after loading
            ApplyMinimizeStates();
        }

        #region Minimize State Management

        private void LoadMinimizeStatesFromGlobal()
        {
            foreach (var key in _localMinimizeState.Keys.ToList())
            {
                if (Dictionary.minimizeState.ContainsKey(key))
                {
                    _localMinimizeState[key] = Dictionary.minimizeState[key];
                }
            }
        }

        private void SaveMinimizeStatesToGlobal()
        {
            foreach (var kvp in _localMinimizeState)
            {
                Dictionary.minimizeState[kvp.Key] = kvp.Value;
            }
        }

        private void ApplyMinimizeStates()
        {
            ApplyPanelState("Aim Assist", AimAssistPanel);
            ApplyPanelState("Aim Config (Slot 1)", AimConfigSlot1Panel);
            ApplyPanelState("Aim Config (Slot 2)", AimConfigPanel);
            ApplyPanelState("Predictions", PredictionsPanel);
            ApplyPanelState("Auto Trigger", TriggerBotPanel);
            ApplyPanelState("FOV Config", FOVConfigPanel);
            ApplyPanelState("ESP Config", ESPConfigPanel);
            ApplyPanelState("Weapon Slot System", WeaponSlotSystemPanel);
            ApplyPanelState("Fast Loot Config", LootConfigPanel);
            ApplyPanelState("Recoil Config", RecoilConfigPanel);
        }

        private void ApplyPanelState(string stateName, StackPanel panel)
        {
            if (_localMinimizeState.TryGetValue(stateName, out bool isMinimized))
            {
                SetPanelVisibility(panel, !isMinimized);
            }
        }

        private void SetPanelVisibility(StackPanel panel, bool isVisible)
        {
            foreach (UIElement child in panel.Children)
            {
                // Keep titles, spacers, and bottom rectangles always visible
                bool shouldStayVisible = child is ATitle || child is ASpacer || child is ARectangleBottom;

                child.Visibility = shouldStayVisible
                    ? Visibility.Visible
                    : (isVisible ? Visibility.Visible : Visibility.Collapsed);
            }
        }

        private void TogglePanel(string stateName, StackPanel panel)
        {
            if (!_localMinimizeState.ContainsKey(stateName)) return;

            // Toggle the state
            _localMinimizeState[stateName] = !_localMinimizeState[stateName];

            // Apply the new visibility
            SetPanelVisibility(panel, !_localMinimizeState[stateName]);

            // Save to global dictionary
            SaveMinimizeStatesToGlobal();
        }



        #endregion

        #region Menu Section Loaders

        private void LoadAimAssist()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, AimAssist);

            builder
                .AddTitle("Aim Assist", true, t =>
                {
                    uiManager.AT_Aim = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Aim Assist", AimAssistPanel);
                        if (_mainWindow != null) MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                    };
                })
                .AddToggle("Aim Assist", t =>
                {
                    uiManager.T_AimAligner = t;
                    t.Reader.Click += (s, e) =>
                    {
                        _mainWindow?.CancelDelayedActivation("Aim Assist");
                        if (Dictionary.toggleState["Aim Assist"] && Dictionary.lastLoadedModel == "N/A")
                        {
                            Dictionary.toggleState["Aim Assist"] = false;
                            _mainWindow.UpdateToggleUI(t, false);
                            LogManager.Log(LogManager.LogLevel.Warning, "Please load a model first", true);
                        }
                        else if (Dictionary.toggleState["Aim Assist"] && Dictionary.lastLoadedModelSlot2 == "N/A")
                        {
                             LogManager.Log(LogManager.LogLevel.Warning, "Please choose model slot 2", true);
                        }
                    };
                }, tooltip: "Bật/Tắt hỗ trợ nhắm. Bạn cần tải Model trước.")
                .AddToggle("Constant AI Tracking", t =>
                {
                    uiManager.T_ConstantAITracking = t;
                    t.Reader.Click += (s, e) =>
                    {
                        if (Dictionary.toggleState["Constant AI Tracking"])
                        {
                            if (Dictionary.lastLoadedModel == "N/A")
                            {
                                Dictionary.toggleState["Constant AI Tracking"] = false;
                                _mainWindow.UpdateToggleUI(t, false);
                            }
                            else
                            {
                                Dictionary.toggleState["Aim Assist"] = true;
                                _mainWindow.UpdateToggleUI(uiManager.T_AimAligner, true);
                            }
                        }
                    };
                }, tooltip: "Luôn theo dõi mục tiêu mà không cần giữ phím. Khi tắt, bạn phải giữ phím aim.")
                .AddToggle("Sticky Aim", t => 
                {
                    uiManager.T_StickyAim = t;
                    t.Reader.Click += (s, e) =>
                    {
                         bool isSticky = Dictionary.toggleState["Sticky Aim"];
                         if (uiManager.S_StickyAimThreshold != null)
                             uiManager.S_StickyAimThreshold.Visibility = isSticky ? Visibility.Visible : Visibility.Collapsed;
                         
                         if (uiManager.S_TargetLockDuration != null)
                             uiManager.S_TargetLockDuration.Visibility = isSticky ? Visibility.Visible : Visibility.Collapsed;
                    };
                },
                    tooltip: "Khóa vào một mục tiêu cho đến khi nó ra khỏi phạm vi thay vì chuyển sang mục tiêu khác.")
                .AddSlider("Sticky Aim Threshold", "Pixels", 1, 1, 0, 100, s =>
                {
                    uiManager.S_StickyAimThreshold = s;
                    // Set initial visibility based on toggle state
                    s.Visibility = Dictionary.toggleState["Sticky Aim"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Khoảng cách mục tiêu di chuyển để chuyển sang mục tiêu mới. Cao hơn = khóa lâu hơn.")
                .AddSlider("Target Lock Duration", "ms", 10, 10, 0, 2000, s =>
                {
                    uiManager.S_TargetLockDuration = s;
                     // Set initial visibility based on toggle state
                    s.Visibility = Dictionary.toggleState["Sticky Aim"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Thời gian (ms) giữ mục tiêu sau khi khóa trước khi cho phép đổi mục tiêu. Giúp tránh aim nhảy loạn xạ.")
                .AddKeyChanger("Aim Keybind", k => uiManager.C_Keybind = k,
                    tooltip: "Phím bạn giữ để kích hoạt hỗ trợ nhắm.")
                .AddKeyChanger("Second Aim Keybind", tooltip: "Phím thay thế để kích hoạt hỗ trợ nhắm.")
                .AddSeparator();
        }

        private void LoadAimConfig()
        {
            try
            {
                LoadAimConfigSlot1();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, "Error loading AimConfigSlot1: " + ex.Message);
                AimConfigSlot1.Children.Add(new TextBlock 
                { 
                    Text = "Error loading Slot 1 Config: " + ex.Message + "\n" + ex.StackTrace, 
                    Foreground = Brushes.Red, 
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.Bold
                });
            }

            try
            {
                LoadAimConfigSlot2();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, "Error loading AimConfigSlot2: " + ex.Message);
                 AimConfig.Children.Add(new TextBlock 
                { 
                    Text = "Error loading Slot 2 Config: " + ex.Message + "\n" + ex.StackTrace, 
                    Foreground = Brushes.Red, 
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.Bold
                });
            }
        }

        private void LoadAimConfigSlot1()
        {
            var uiManager = _mainWindow!.uiManager;
            // Ensure child is cleared if reloaded
            AimConfigSlot1.Children.Clear();
            
            var builder = new SectionBuilder(this, AimConfigSlot1);

            builder.AddTitle("Aim Config (Slot 1)", true, t =>
            {
                t.Minimize.Click += (s, e) => 
                {
                    TogglePanel("Aim Config (Slot 1)", AimConfigSlot1Panel);
                    if (_mainWindow != null) MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                };
            })
            // Add Dropdowns for Slot 1
            .AddDropdown("Slot 1 Mouse Movement Method", d =>
            {
                uiManager.D_Slot1MouseMovementMethod = d;
                d.DropdownBox.SelectedIndex = -1;
                _mainWindow.AddDropdownItem(d, "Mouse Event");
                _mainWindow.AddDropdownItem(d, "SendInput");
                _mainWindow.AddDropdownItem(d, "LG HUB");
                _mainWindow.AddDropdownItem(d, "Razer Synapse (Require Razer Peripheral)");
                var dd = _mainWindow.AddDropdownItem(d, "ddxoft Virtual Input Driver");
                dd.Selected += async (_, _) =>
                {
                    if (!await DdxoftMain.Load() && d.DropdownBox.SelectedItem == dd)
                        d.DropdownBox.SelectedIndex = 0;
                };
            }, tooltip: "Phương thức di chuyển chuột cho Slot 1.")
            .AddDropdown("Slot 1 Movement Path", d =>
            {
                uiManager.D_Slot1MovementPath = d;
                _mainWindow.AddDropdownItem(d, "Cubic Bezier");
                _mainWindow.AddDropdownItem(d, "Exponential");
                _mainWindow.AddDropdownItem(d, "Linear");
                _mainWindow.AddDropdownItem(d, "Adaptive");
                _mainWindow.AddDropdownItem(d, "Perlin Noise");
                d.DropdownBox.SelectedIndex = 2;
            }, tooltip: "Đường đi chuyển chuột cho Slot 1.")
             .AddDropdown("Slot 1 Detection Area Type", d =>
            {
                uiManager.D_Slot1DetectionAreaType = d;
                d.DropdownBox.SelectedIndex = -1;
                _mainWindow.AddDropdownItem(d, "Closest to Center Screen");
                _mainWindow.AddDropdownItem(d, "Closest to Mouse");
            }, tooltip: "Loại vùng phát hiện cho Slot 1.")
             .AddDropdown("Slot 1 Aiming Boundaries Alignment", d =>
            {
                uiManager.D_Slot1AimingBoundariesAlignment = d;
                d.DropdownBox.SelectedIndex = -1;
                _mainWindow.AddDropdownItem(d, "Center");
                _mainWindow.AddDropdownItem(d, "Top");
                _mainWindow.AddDropdownItem(d, "Bottom");
            }, tooltip: "Căn chỉnh biên ngắm cho Slot 1.");

            AddSlot1ConfigSliders(builder, uiManager);
            
            builder.AddSeparator();
        }

        private void LoadAimConfigSlot2()
        {
            var uiManager = _mainWindow!.uiManager;
            // Ensure child is cleared if reloaded
            AimConfig.Children.Clear();

            var builder = new SectionBuilder(this, AimConfig);

            builder
                .AddTitle("Aim Config (Slot 2)", true, t =>
                {
                    uiManager.AT_AimConfig = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Aim Config (Slot 2)", AimConfigPanel);
                        if (_mainWindow != null) MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                    };
                })
                .AddDropdown("Mouse Movement Method", d =>
                {
                    uiManager.D_MouseMovementMethod = d;
                    d.DropdownBox.SelectedIndex = -1;  // Prevent auto-selection
                    _mainWindow.AddDropdownItem(d, "Mouse Event");
                    _mainWindow.AddDropdownItem(d, "SendInput");
                    uiManager.DDI_LGHUB = _mainWindow.AddDropdownItem(d, "LG HUB");
                    uiManager.DDI_RazerSynapse = _mainWindow.AddDropdownItem(d, "Razer Synapse (Require Razer Peripheral)");
                    uiManager.DDI_ddxoft = _mainWindow.AddDropdownItem(d, "ddxoft Virtual Input Driver");

                    // Setup handlers
                    uiManager.DDI_LGHUB.Selected += async (s, e) => { if (!new LGHubMain().Load()) await ResetToMouseEvent(); };


                    uiManager.DDI_RazerSynapse.Selected += async (s, e) => { if (!await RZMouse.Load()) await ResetToMouseEvent(); };
                    var dd = uiManager.DDI_ddxoft;
                    dd.Selected += async (_, _) =>
                    {
                        if (!await DdxoftMain.Load() && d.DropdownBox.SelectedItem == dd)
                            d.DropdownBox.SelectedIndex = 0;
                    };
                }, tooltip: "Cách thức gửi tín hiệu di chuyển chuột (Dùng chung).")
                .AddDropdown("Movement Path", d =>
                {
                    uiManager.D_MovementPath = d;
                    _mainWindow.AddDropdownItem(d, "Cubic Bezier");
                    _mainWindow.AddDropdownItem(d, "Exponential");
                    _mainWindow.AddDropdownItem(d, "Linear");
                    _mainWindow.AddDropdownItem(d, "Adaptive");
                    _mainWindow.AddDropdownItem(d, "Perlin Noise");
                    d.DropdownBox.SelectedIndex = 2; // Default to Linear
                }, tooltip: "Kiểu đường cong khi di chuyển tới mục tiêu (Dùng chung).")
                .AddDropdown("Detection Area Type", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_DetectionAreaType = d;
                    uiManager.DDI_ClosestToCenterScreen = _mainWindow.AddDropdownItem(d, "Closest to Center Screen");
                    _mainWindow.AddDropdownItem(d, "Closest to Mouse");

                    uiManager.DDI_ClosestToCenterScreen.Selected += async (s, e) =>
                    {
                        await Task.Delay(100);
                        MainWindow.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                            Convert.ToInt16((WinAPICaller.ScreenWidth / 2) / WinAPICaller.scalingFactorX) - 320,
                            Convert.ToInt16((WinAPICaller.ScreenHeight / 2) / WinAPICaller.scalingFactorY) - 320,
                            0, 0);
                    };
                }, tooltip: "Cách ưu tiên mục tiêu (Dùng chung).")
                .AddDropdown("Aiming Boundaries Alignment", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_AimingBoundariesAlignment = d;
                    _mainWindow.AddDropdownItem(d, "Center");
                    _mainWindow.AddDropdownItem(d, "Top");
                    _mainWindow.AddDropdownItem(d, "Bottom");
                }, tooltip: "Vị trí ngắm trên ô mục tiêu được phát hiện (Dùng chung).");

            // Add sliders for Slot 2
            AddConfigSliders(builder, uiManager);
            builder.AddSeparator();
        }

        private void AddSlot1ConfigSliders(SectionBuilder builder, UI uiManager)
        {
             // These keys MUST match what we added to Dictionary.cs
            builder
                .AddSlider("Slot 1 Mouse Sensitivity", "Sens", 0.01, 0.01, 0.01, 1, s =>
                {
                    uiManager.S_Slot1MouseSensitivity = s;
                }, tooltip: "Độ nhạy chuột cho Slot 1.")
                .AddSlider("Slot 1 Mouse Jitter", "Jitter", 1, 1, 0, 15, s => 
                {
                    uiManager.S_Slot1MouseJitter = s;
                }, tooltip: "Độ rung chuột cho Slot 1.")
                .AddToggle("Slot 1 Y Axis Percentage Adjustment", t => 
                {
                    uiManager.T_Slot1YAxisPercentageAdjustment = t;
                    t.Reader.Click += (s, e) => _mainWindow?.Toggle_Action("Slot 1 Y Axis Percentage Adjustment");
                }, tooltip: "Bật điều chỉnh trục Y theo % cho Slot 1.")
                .AddToggle("Slot 1 X Axis Percentage Adjustment", t => 
                {
                    uiManager.T_Slot1XAxisPercentageAdjustment = t;
                    t.Reader.Click += (s, e) => _mainWindow?.Toggle_Action("Slot 1 X Axis Percentage Adjustment");
                }, tooltip: "Bật điều chỉnh trục X theo % cho Slot 1.")
                .AddSlider("Slot 1 Y Offset (Up/Down)", "Offset", 1, 1, -150, 150, s =>
                {
                    uiManager.S_Slot1YOffset = s;
                    s.Visibility = Dictionary.toggleState["Slot 1 Y Axis Percentage Adjustment"]
                        ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: "Độ lệch pixel trục Y cho Slot 1.")
                .AddSlider("Slot 1 Y Offset (%)", "Percent", 1, 1, 0, 100, s =>
                {
                    uiManager.S_Slot1YOffsetPercent = s;
                    s.Visibility = Dictionary.toggleState["Slot 1 Y Axis Percentage Adjustment"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Độ lệch phần trăm trục Y cho Slot 1.")
                .AddSlider("Slot 1 X Offset (Left/Right)", "Offset", 1, 1, -150, 150, s =>
                {
                    uiManager.S_Slot1XOffset = s;
                    s.Visibility = Dictionary.toggleState["Slot 1 X Axis Percentage Adjustment"]
                        ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: "Độ lệch pixel trục X cho Slot 1.")
                .AddSlider("Slot 1 X Offset (%)", "Percent", 1, 1, 0, 100, s =>
                {
                    uiManager.S_Slot1XOffsetPercent = s;
                    s.Visibility = Dictionary.toggleState["Slot 1 X Axis Percentage Adjustment"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Độ lệch phần trăm trục X cho Slot 1.");
        }

        private void AddConfigSliders(SectionBuilder builder, UI uiManager)
        {
            builder
                .AddSlider("Mouse Sensitivity (+/-)", "Sensitivity", 0.01, 0.01, 0.01, 1, s =>
                {
                    uiManager.S_MouseSensitivity = s;
                }, tooltip: "Độ nhạy cho Slot 2.")
                .AddSlider("Mouse Jitter", "Jitter", 1, 1, 0, 15, s => uiManager.S_MouseJitter = s,
                    tooltip: "Độ rung cho Slot 2.")
                .AddToggle("Y Axis Percentage Adjustment", t => uiManager.T_YAxisPercentageAdjustment = t,
                    tooltip: "Bật điều chỉnh trục Y theo % cho Slot 2.")
                .AddToggle("X Axis Percentage Adjustment", t => uiManager.T_XAxisPercentageAdjustment = t,
                    tooltip: "Bật điều chỉnh trục X theo % cho Slot 2.")
                .AddSlider("Y Offset (Up/Down)", "Offset", 1, 1, -150, 150, s =>
                {
                    uiManager.S_YOffset = s;
                    s.Visibility = Dictionary.toggleState["Y Axis Percentage Adjustment"]
                        ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: "Độ lệch pixel trục Y cho Slot 2.")
                .AddSlider("Y Offset (%)", "Percent", 1, 1, 0, 100, s =>
                {
                    uiManager.S_YOffsetPercent = s;
                    s.Visibility = Dictionary.toggleState["Y Axis Percentage Adjustment"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Độ lệch phần trăm trục Y cho Slot 2.")
                .AddSlider("X Offset (Left/Right)", "Offset", 1, 1, -150, 150, s =>
                {
                    uiManager.S_XOffset = s;
                    s.Visibility = Dictionary.toggleState["X Axis Percentage Adjustment"]
                        ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: "Độ lệch pixel trục X cho Slot 2.")
                .AddSlider("X Offset (%)", "Percent", 1, 1, 0, 100, s =>
                {
                    uiManager.S_XOffsetPercent = s;
                    s.Visibility = Dictionary.toggleState["X Axis Percentage Adjustment"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: "Độ lệch phần trăm trục X cho Slot 2.");
        }

        private void LoadPredictions()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, Predictions);

            builder
                .AddTitle("Predictions", true, t =>
                {
                    uiManager.AT_Predictions = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Predictions", PredictionsPanel);
                        if (_mainWindow != null) MainWindow.UpdatePredictionSliderVisibility(_mainWindow.uiManager);
                    };
                })
                .AddToggle("Predictions", t => uiManager.T_Predictions = t,
                    tooltip: "Dự đoán vị trí mục tiêu di chuyển. Giúp theo dõi mục tiêu di chuyển nhanh.")
                .AddDropdown("Prediction Method", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_PredictionMethod = d;
                    _mainWindow.AddDropdownItem(d, "Kalman Filter");
                    _mainWindow.AddDropdownItem(d, "Shall0e's Prediction");
                    _mainWindow.AddDropdownItem(d, "wisethef0x's EMA Prediction");
                    _mainWindow.AddDropdownItem(d, "Constant Acceleration");
 
                    // Update slider visibility when prediction method changes
                    d.DropdownBox.SelectionChanged += (s, e) => 
                    {
                        if (_mainWindow != null) MainWindow.UpdatePredictionSliderVisibility(_mainWindow.uiManager);
                    };
                }, tooltip: "Thuật toán dự đoán chuyển động mục tiêu. Hãy thử các loại khác nhau để xem cái nào tốt nhất.")
                .AddSlider("CA Lead Multiplier", "Multiplier", 0.01, 0.01, 0.01, 0.50, s =>
                {
                    uiManager.S_CALeadMultiplier = s;
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: "Hệ số dự đoán cho gia tốc. Cao hơn = dự đoán xa hơn, giúp bám đuổi tốt khi đổi hướng.")
                .AddSlider("Kalman Lead Time", "Seconds", 0.01, 0.01, 0.02, 0.30, s =>
                {
                    uiManager.S_KalmanLeadTime = s;
                    // Start collapsed - visibility will be set by LoadDropdownStates
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: "Thời gian dự đoán trước. Cao hơn = dự đoán xa hơn, có thể bị vọt lố.")
                .AddSlider("WiseTheFox Lead Time", "Seconds", 0.01, 0.01, 0.02, 0.30, s =>
                {
                    uiManager.S_WiseTheFoxLeadTime = s;
                    // Start collapsed - visibility will be set by LoadDropdownStates
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: "Thời gian dự đoán trước. Cao hơn = dự đoán xa hơn, có thể bị vọt lố.")
                .AddSlider("Shalloe Lead Multiplier", "Frames", 0.5, 0.5, 1, 10, s =>
                {
                    uiManager.S_ShalloeLeadMultiplier = s;
                    // Start collapsed - visibility will be set by LoadDropdownStates
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: "Số khung hình dự đoán trước. Cao hơn = dự đoán xa hơn, có thể bị vọt lố.")
                .AddToggle("EMA Smoothening", t => uiManager.T_EMASmoothing = t,
                    tooltip: "Làm mượt chuyển động ngắm để giảm rung và giúp theo dõi ổn định hơn.")
                .AddSlider("EMA Smoothening", "Amount", 0.01, 0.01, 0.01, 1, s =>
                {
                    uiManager.S_EMASmoothing = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        if (Dictionary.toggleState["EMA Smoothening"])
                        {
                            MouseManager.smoothingFactor = s.Slider.Value;
                        }
                    };
                }, tooltip: "Mức độ làm mượt. Thấp = mượt hơn nhưng chậm hơn, cao = nhanh hơn nhưng rung.")
                .AddSeparator();
        }

        private void LoadTriggerBot()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, TriggerBot);

            builder
                .AddTitle("Auto Trigger", true, t =>
                {
                    uiManager.AT_TriggerBot = t;
                    t.Minimize.Click += (s, e) => 
                    {
                        TogglePanel("Auto Trigger", TriggerBotPanel);
                        if (_mainWindow != null) MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                    };
                })
                .AddToggle("Auto Trigger", t => uiManager.T_AutoTrigger = t,
                    tooltip: "Tự động click khi phát hiện mục tiêu trong vùng hồng tâm.")
                .AddToggle("Cursor Check", t => uiManager.T_CursorCheck = t,
                    tooltip: "Chỉ kích hoạt khi con trỏ chuột nằm trực tiếp trên mục tiêu. Chính xác hơn nhưng có thể bỏ lỡ.")
                .AddToggle("Spray Mode", t => uiManager.T_SprayMode = t,
                    tooltip: "Giữ chuột thay vì click từng cái. Tốt cho súng tự động.")
                //.AddToggle("Only When Held", t => uiManager.T_OnlyWhenHeld = t)
                .AddSlider("Auto Trigger Delay", "Seconds", 0.01, 0.1, 0.01, 1, s => uiManager.S_AutoTriggerDelay = s,
                    tooltip: "Thời gian chờ trước khi bắn sau khi phát hiện mục tiêu. Giúp tránh bắn nhầm.")
                .AddKeyChanger("Auto Click Keybind", tooltip: "Phím giữ để auto click liên tục khi tâm vào địch.")
                .AddSeparator();
        }

        private void LoadWeaponSlotSystem()
        {
            try
            {
                var uiManager = _mainWindow!.uiManager;
                var builder = new SectionBuilder(this, WeaponSlotSystem);

                builder.AddTitle("Weapon Slot System", true, t =>
                {
                    t.Minimize.Click += (s, e) => 
                {
                    TogglePanel("Weapon Slot System", WeaponSlotSystemPanel);
                    if (_mainWindow != null) MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                };
                })
                .AddToggle("Weapon Recognition", t => {
                        uiManager.T_WeaponRecognition = t;
                        t.Reader.Click += (s, e) => {
                             _mainWindow?.CancelDelayedActivation("Weapon Recognition");
                             if(Dictionary.toggleState["Weapon Recognition"] && !WeaponSlotManager.Instance.IsInitialized)
                            {
                                 WeaponSlotManager.Instance.Initialize();
                                 // Re-check just in case
                                 if(!WeaponSlotManager.Instance.IsInitialized)
                                 {
                                     Dictionary.toggleState["Weapon Recognition"] = false;
                                     _mainWindow.UpdateToggleUI(t, false);
                                     global::Other.LocalizedMessageBox.Show("Failed to initialize Weapon Recognition. Please check your model settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                 }
                            }
                        };
                    }, tooltip: "Tự động phát hiện vũ khí đang cầm để chuyển đổi cài đặt.")
                    .AddToggle("Toggle Weapon Scan", tooltip: "Nếu BẬT, nhấn phím scan (Tab) để Bật/Tắt scan. Nếu TẮT, bạn phải GIỮ phím để scan.")
                    .AddToggle("Show Detected Scope", tooltip: "Hiển thị lớp phủ thông tin scope đã phát hiện lên màn hình.");

                // Add Toggle for Tab Reset Feature
                if (!Dictionary.toggleState.ContainsKey("Enable Tab Reset"))
                    Dictionary.toggleState["Enable Tab Reset"] = true; // Default to true
                builder.AddToggle("Enable Tab Reset", tooltip: "Giữ Tab để đặt lại các điều chỉnh tạm thời.");

                // Add Toggle for Mouse Wheel Adjust (Shortcut)
                if (!Dictionary.toggleState.ContainsKey("Mouse Wheel Adjust"))
                    Dictionary.toggleState["Mouse Wheel Adjust"] = true;
                builder.AddToggle("Mouse Wheel Adjust", tooltip: "Điều chỉnh độ giật bằng lăn chuột (Chỉ khi con trỏ bị ẩn/trong game).");

                try
                {
                    builder.AddFileLocator("Scope Model Location", fl => {
                        fl.FileChanged += (path) => {
                            try {
                                bool isEngine = path.EndsWith(".engine", StringComparison.OrdinalIgnoreCase) || 
                                                path.EndsWith(".trt", StringComparison.OrdinalIgnoreCase);

                                if (isEngine && !AIManager.IsTensorRTAvaliable())
                                {
                                    LogManager.Log(LogManager.LogLevel.Error, "TensorRT (.engine) is not supported on this machine. CUDA 12.x or TensorRT 10.x DLLs not found in PATH.", true, 8000);
                                    
                                    // Reset file locator to previous path
                                    fl.SetFilePath(Dictionary.filelocationState["Scope Model Location"].ToString());
                                    return;
                                }

                                WeaponSlotManager.Instance.LoadModel(path);
                            }
                            catch { }
                        };
                    }, filter: "Model Files (*.onnx;*.engine;*.trt)|*.onnx;*.engine;*.trt|ONNX Model (*.onnx)|*.onnx|TensorRT Engine (*.engine;*.trt)|*.engine;*.trt", dlExtension: "\\bin\\scope_models");
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogManager.LogLevel.Error, "Error adding FileLocator: " + ex.Message);
                }

                builder.AddDropdown("Scope Image Size", d =>
                {
                    var manager = WeaponSlotManager.Instance;
                    bool updating = false;
                    void RefreshScopeSize()
                    {
                        updating = true;
                        try
                        {
                            int size = manager.ScopeImageSize;
                            var sizes = new[] { 160, 256, 320, 416, 480, 512, 640, 768, 800, 960, 1024, 1280, 1536, 1920, 2048, size }
                                .Distinct().OrderBy(value => value).ToArray();
                            d.DropdownBox.Items.Clear();
                            foreach (int value in sizes) d.DropdownBox.Items.Add(new ComboBoxItem { Content = value.ToString() });
                            d.DropdownBox.SelectedIndex = Array.IndexOf(sizes, size);
                            d.DropdownBox.IsEnabled = manager.CanChangeScopeImageSize && !WeaponSlotManager.CurrentlyLoadingScopeModel;
                            d.ToolTip = manager.CanChangeScopeImageSize
                                ? "Model scope động: chọn kích thước đầu vào nhận diện scope."
                                : "Kích thước theo model scope cố định; chọn model khác để thay đổi.";
                        }
                        finally { updating = false; }
                    }
                    d.Loaded += (s, e) => { manager.ScopeInputChanged -= RefreshScopeSize; manager.ScopeInputChanged += RefreshScopeSize; RefreshScopeSize(); };
                    d.Unloaded += (s, e) => manager.ScopeInputChanged -= RefreshScopeSize;
                    d.DropdownBox.SelectionChanged += (s, e) =>
                    {
                        if (updating) return;
                        if (int.TryParse((d.DropdownBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out int size))
                            manager.SetScopeImageSize(size);
                    };
                    RefreshScopeSize();
                }, tooltip: "Image Size riêng cho model scope; không thay đổi kích thước model Slot 1/2.");

                builder.AddDropdown("Scope Capture Method", d =>
                {
                    uiManager.D_ScopeCaptureMethod = d;
                    _mainWindow.AddDropdownItem(d, "DirectX");
                    _mainWindow.AddDropdownItem(d, "GDI+");
                    _mainWindow.AddDropdownItem(d, "WGC");
                    d.DropdownBox.SelectedIndex = 1; // GDI+ default; saved selection is restored later.

                    int captureSelectionVersion = 0;
                    d.DropdownBox.SelectionChanged += async (s, e) =>
                    {
                        int selectionVersion = ++captureSelectionVersion;
                        var method = (d.DropdownBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                        if (!string.IsNullOrEmpty(method))
                        {
                            if (method == "DirectX")
                            {
                                bool isDirectXSupported = false;
                                string? supportError = null;
                                await Task.Run(() =>
                                {
                                    try
                                    {
                                        var testManager = new CaptureManager();
                                        testManager.CaptureMethodKey = "Scope Capture Method";
                                        try { testManager.InitializeDxgiDuplication(updateSettingsOnFailure: false); }
                                        finally { testManager.Dispose(); }
                                        isDirectXSupported = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        supportError = ex.Message;
                                    }
                                });

                                if (selectionVersion != captureSelectionVersion) return;

                                if (!isDirectXSupported)
                                {
                                    CaptureManager.ReportCaptureFailure("Scope Capture Method", "DirectX", new NotSupportedException(supportError));
                                    // Revert selection to GDI+
                                    for (int i = 0; i < d.DropdownBox.Items.Count; i++)
                                    {
                                        if ((d.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == "GDI+")
                                        {
                                            d.DropdownBox.SelectedIndex = i;
                                            break;
                                        }
                                    }
                                    return;
                                }
                            }
                            Dictionary.dropdownState["Scope Capture Method"] = method;
                            _mainWindow.SaveAllConfigurations();
                            await CaptureManager.ReleaseInactiveWgcSessionsAsync();
                        }
                    };
                }, tooltip: "Phương thức chụp màn hình để nhận diện scope. WGC dùng Windows Graphics Capture; lựa chọn này độc lập với Screen Capture Method.");

                builder.AddSlider("Scope Confidence", "% Confidence", 1, 1, 1, 100, s => uiManager.S_ScopeConfidence = s, tooltip: "Độ tin cậy tối thiểu để phát hiện scope.")
                       .AddSlider("Weapon Scan Delay", "Seconds", 0.1, 0.1, 0, 5, s => uiManager.S_WeaponScanDelay = s, tooltip: "Thời gian giữ phím scan (Tab) trước khi bắt đầu quét.")
                       .AddSlider("Tab Reset Adjust", "Seconds", 0.1, 0.1, 0, 5, tooltip: "Thời gian giữ Tab để đặt lại các điều chỉnh recoil tạm thời.")
                       .AddKeyChanger("Weapon Scan Keybind", tooltip: "Phím tắt để kích hoạt AI scan.")
                       .AddKeyChanger("Weapon Slot 1 Keybind", tooltip: "Phím tắt để áp dụng cài đặt vũ khí slot 1.")
                       .AddKeyChanger("Weapon Slot 2 Keybind", tooltip: "Phím tắt để áp dụng cài đặt vũ khí slot 2.");
                       
                // Add Region Selectors manually with better styling
                // Wrap in a Border to match the style of other items (AToggle, etc.)
                // This ensures the vertical side lines continue seamlessly.
                var containerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(63, 60, 60, 60)), // #3F3C3C3C
                    BorderThickness = new Thickness(1, 0, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255)), // #3FFFFFFF
                    Padding = new Thickness(10, 5, 10, 5) // Add padding inside the border
                };

                var btnGrid = new Grid(); // No margin on grid, handled by padding
                btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Spacer
                btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Style for buttons to match app theme with Rounded Corners
                var btnStyle = new Style(typeof(Button));
                btnStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(114, 46, 209)))); // Theme Color
                btnStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                btnStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
                btnStyle.Setters.Add(new Setter(Button.HeightProperty, 30.0));
                btnStyle.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));
                btnStyle.Setters.Add(new Setter(Button.FontSizeProperty, 12.0));
                
                // Create ControlTemplate for Rounded Corners
                var btnTemplate = new ControlTemplate(typeof(Button));
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));

                var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                
                borderFactory.AppendChild(contentPresenter);
                btnTemplate.VisualTree = borderFactory;
                
                btnStyle.Setters.Add(new Setter(Button.TemplateProperty, btnTemplate));

                var btnRegion1 = new Button { 
                    Content = "Vùng Vũ Khí 1",
                    Style = btnStyle,
                    ToolTip = "Chọn vùng màn hình để nhận diện scope cho vũ khí 1 (phía trên)"
                };
                
                btnRegion1.Click += (s, e) => {
                   var selector = new RegionSelectorWindow("Chọn vùng Scope Vũ khí 1");
                   selector.ShowDialog();
                   if (selector.IsConfirmed) {
                       WeaponSlotManager.Instance.SetWeaponRegion(1, selector.SelectedRegion);
                   }
                };

                var btnRegion2 = new Button { 
                    Content = "Vùng Vũ Khí 2",
                    Style = btnStyle, 
                    ToolTip = "Chọn vùng màn hình để nhận diện scope cho vũ khí 2 (phía dưới)"
                };
                
                btnRegion2.Click += (s, e) => {
                   var selector = new RegionSelectorWindow("Chọn vùng Scope Vũ khí 2");
                   selector.ShowDialog();
                   if (selector.IsConfirmed) {
                       WeaponSlotManager.Instance.SetWeaponRegion(2, selector.SelectedRegion);
                   }
                };

                Grid.SetColumn(btnRegion1, 0);
                Grid.SetColumn(btnRegion2, 2);

                btnGrid.Children.Add(btnRegion1);
                btnGrid.Children.Add(btnRegion2);
                
                containerBorder.Child = btnGrid;
                
                // Add to parent panel
                WeaponSlotSystem.Children.Add(containerBorder);
                
                // Ensure separator added at the end for clean bottom
                builder.AddSeparator();
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, "Critical Error inside LoadWeaponSlotSystem: " + ex.Message);
                // Displays error on UI
                var errorText = new TextBlock
                {
                    Text = $"Error loading Weapon Slot System:\n{ex.Message}\n{ex.StackTrace}",
                    Foreground = Brushes.Red,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(5)
                };
                WeaponSlotSystem.Children.Add(errorText);
            }
        }

        private void LoadFOVConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, FOVConfig);

            builder
                .AddTitle("FOV Config", true, t =>
                {
                    uiManager.AT_FOV = t;
                    t.Minimize.Click += (s, e) => 
                    {
                        TogglePanel("FOV Config", FOVConfigPanel);
                        if (_mainWindow != null) MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                    };
                })
                .AddToggle("FOV", t => uiManager.T_FOV = t,
                    tooltip: "Hiển thị vòng tròn trên màn hình biểu thị khu vực phát hiện.")
                .AddToggle("Dynamic FOV", t => uiManager.T_DynamicFOV = t,
                    tooltip: "Thay đổi kích thước FOV khi giữ phím. Hữu ích khi bật ống ngắm.")
                .AddToggle("Third Person Support", t => uiManager.T_ThirdPersonSupport = t,
                    tooltip: "Điều chỉnh vị trí FOV cho game góc nhìn thứ ba.")
                .AddToggle("Virtual Crosshair", t => uiManager.T_VirtualCrosshair = t,
                    tooltip: "Hiển thị chấm trắng nhỏ ở chính giữa màn hình làm tâm ảo. Sẽ ẩn đi khi nhấn giữ đồng thời cả chuột trái và chuột phải.")
                .AddColorChanger("Crosshair Color", c =>
                {
                    uiManager.CC_CrosshairColor = c;
                    c.Reader.Click += (s, e) =>
                    {
                        if (crosshairColorPickerInstance != null && crosshairColorPickerInstance.IsVisible)
                        {
                            crosshairColorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        crosshairColorPickerInstance = new UISections.ColorPicker(initialColor, "Crosshair Color");

                        crosshairColorPickerInstance.ColorChanged += (color) =>
                        {
                            // Update the color square
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            // Save to dictionary for persistence
                            Dictionary.colorState["Crosshair Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                            PropertyChanger.PostCrosshairColor(color);
                        };

                        crosshairColorPickerInstance.Closed += (sender, args) =>
                        {
                            crosshairColorPickerInstance = null;
                        };

                        crosshairColorPickerInstance.Show();
                    };
                })
                .AddSlider("Crosshair Size", "Size", 1, 1, 1, 30, s =>
                {
                    uiManager.S_CrosshairSize = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        PropertyChanger.PostCrosshairSize(s.Slider.Value);
                    };
                }, tooltip: "Kích thước của chấm tâm ảo. (Mặc định: 6)")
                .AddKeyChanger("Crosshair Hide Key 1", k => uiManager.C_CrosshairHideKey1 = k,
                    tooltip: "Phím ẩn tâm ảo thứ nhất (Mặc định: Chuột phải).")
                .AddKeyChanger("Dynamic FOV Keybind", k => uiManager.C_DynamicFOV = k,
                    tooltip: "Phím giữ để chuyển sang kích thước FOV động.")
                .AddDropdown("FOV Style", d =>
                {
                    uiManager.D_FOVSTYLE = d;

                    var circleItem = _mainWindow.AddDropdownItem(d, "Circle");
                    var rectangleItem = _mainWindow.AddDropdownItem(d, "Rectangle");

                    circleItem.Selected += (s, e) =>
                    {
                        MainWindow.FOVWindow.Circle.Visibility = Visibility.Visible;
                        MainWindow.FOVWindow.RectangleShape.Visibility = Visibility.Collapsed;
                    };

                    rectangleItem.Selected += (s, e) =>
                    {
                        MainWindow.FOVWindow.Circle.Visibility = Visibility.Collapsed;
                        MainWindow.FOVWindow.RectangleShape.Visibility = Visibility.Visible;
                    };
                }, tooltip: "Shape of the FOV overlay. Circle is most common.")
                .AddColorChanger("FOV Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (fovColorPickerInstance != null && fovColorPickerInstance.IsVisible)
                        {
                            fovColorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        fovColorPickerInstance = new UISections.ColorPicker(initialColor, "FOV Color");

                        fovColorPickerInstance.ColorChanged += (color) =>
                        {
                            // Update the color square
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            // Save to dictionary for persistence
                            Dictionary.colorState["FOV Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                            PropertyChanger.PostColor(color);
                        };

                        fovColorPickerInstance.Closed += (sender, args) =>
                        {
                            fovColorPickerInstance = null;
                        };

                        fovColorPickerInstance.Show();
                    };
                })
                .AddSlider("FOV Size", "Size", 1, 1, 10, 640, s =>
                {
                    uiManager.S_FOVSize = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        _mainWindow.ActualFOV = s.Slider.Value;
                        PropertyChanger.PostNewFOVSize(_mainWindow.ActualFOV);
                    };
                }, tooltip: "Size of the detection area. Smaller = more precise, larger = wider coverage.")
                .AddSlider("Dynamic FOV Size", "Size", 1, 1, 10, 640, s =>
                {
                    uiManager.S_DynamicFOVSize = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        if (Dictionary.toggleState["Dynamic FOV"])
                            PropertyChanger.PostNewFOVSize(s.Slider.Value);
                    };
                }, tooltip: "FOV size when holding the Dynamic FOV key. Usually smaller for scoped aim.")
                .AddSeparator();
        }

        private void LoadESPConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, ESPConfig);

            builder
                .AddTitle("ESP Config", true, t =>
                {
                    uiManager.AT_DetectedPlayer = t;
                    t.Minimize.Click += (s, e) => 
                    {
                        TogglePanel("ESP Config", ESPConfigPanel);
                        if (_mainWindow != null) MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                    };
                })
                .AddToggle("Show Detected Player", t => uiManager.T_ShowDetectedPlayer = t,
                    tooltip: "Vẽ một khung bao quanh các mục tiêu bị phát hiện trên màn hình.")
                .AddToggle("Show Detection Performance", t =>
                {
                    t.ToggleTitle.Content = "Show FPS / Inference";
                    t.IsEnabled = Dictionary.toggleState["Show Detected Player"];
                    t.Loaded += (_, _) => t.IsEnabled = Dictionary.toggleState["Show Detected Player"];
                }, tooltip: "Hiển thị FPS và thời gian suy luận trên ESP. Chỉ dùng khi bật Show Detected Player.")
                .AddToggle("Show AI Confidence", t => uiManager.T_ShowAIConfidence = t,
                    tooltip: "Hiển thị mức độ tin cậy của AI đối với từng phát hiện (0-100%).")
                .AddToggle("Show Tracers", t => uiManager.T_ShowTracers = t,
                    tooltip: "Vẽ các đường kẻ từ cạnh màn hình đến các mục tiêu được phát hiện.");

            builder.AddDropdown("Tracer Position", d =>
            {
                d.DropdownBox.SelectedIndex = 0;
                uiManager.D_TracerPosition = d;
                // Changed the positions of these as top is above middle & bottom - ts (this) bothered me so i had to
                _mainWindow.AddDropdownItem(d, "Top");
                _mainWindow.AddDropdownItem(d, "Middle");
                _mainWindow.AddDropdownItem(d, "Bottom");
                d.DropdownBox.SelectionChanged += (s, e) =>
                {
                    if (Dictionary.toggleState["Show Detected Player"])
                    {
                        // simulate a click to turn it off - this is to force a reload of the ui cause tracer doesn't update otherwise - helz
                        uiManager.T_ShowDetectedPlayer.Reader.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        // simulate a click to turn it back on - same as before ^ - helz
                        uiManager.T_ShowDetectedPlayer.Reader.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    else
                    {
                        if (Dictionary.DetectedPlayerOverlay != null)
                        {
                            Dictionary.DetectedPlayerOverlay.ForceReposition();
                        }
                    }
                };
            }, tooltip: "Vị trí bắt đầu của các đường kẻ (tracers) trên màn hình.");

            builder
                .AddColorChanger("Detected Player Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (colorPickerInstance != null && colorPickerInstance.IsVisible)
                        {
                            colorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        colorPickerInstance = new UISections.ColorPicker(initialColor, "ESP Color");

                        colorPickerInstance.ColorChanged += (color) =>
                        {
                            // Update the color square
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            // Save to dictionary for persistence
                            Dictionary.colorState["Detected Player Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                            PropertyChanger.PostDPColor(color);
                        };

                        colorPickerInstance.Closed += (sender, args) =>
                        {
                            colorPickerInstance = null;
                        };

                        colorPickerInstance.Show();
                    };
                })
                .AddColorChanger("Single Class Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (colorPickerInstance != null && colorPickerInstance.IsVisible)
                        {
                            colorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        colorPickerInstance = new UISections.ColorPicker(initialColor, "Single Class Color");

                        colorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["Single Class Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                        };

                        colorPickerInstance.Closed += (sender, args) =>
                        {
                            colorPickerInstance = null;
                        };

                        colorPickerInstance.Show();
                    };
                })
                .AddColorChanger("ONNX Head Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (colorPickerInstance != null && colorPickerInstance.IsVisible)
                        {
                            colorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        colorPickerInstance = new UISections.ColorPicker(initialColor, "ONNX Head Color");

                        colorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["ONNX Head Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                        };

                        colorPickerInstance.Closed += (sender, args) =>
                        {
                            colorPickerInstance = null;
                        };

                        colorPickerInstance.Show();
                    };
                })
                .AddColorChanger("ONNX Body Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (colorPickerInstance != null && colorPickerInstance.IsVisible)
                        {
                            colorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        colorPickerInstance = new UISections.ColorPicker(initialColor, "ONNX Body Color");

                        colorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["ONNX Body Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                        };

                        colorPickerInstance.Closed += (sender, args) =>
                        {
                            colorPickerInstance = null;
                        };

                        colorPickerInstance.Show();
                    };
                })
                .AddColorChanger("Engine Head Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (colorPickerInstance != null && colorPickerInstance.IsVisible)
                        {
                            colorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        colorPickerInstance = new UISections.ColorPicker(initialColor, "Engine Head Color");

                        colorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["Engine Head Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                        };

                        colorPickerInstance.Closed += (sender, args) =>
                        {
                            colorPickerInstance = null;
                        };

                        colorPickerInstance.Show();
                    };
                })
                .AddColorChanger("Engine Body Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (colorPickerInstance != null && colorPickerInstance.IsVisible)
                        {
                            colorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        colorPickerInstance = new UISections.ColorPicker(initialColor, "Engine Body Color");

                        colorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["Engine Body Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                        };

                        colorPickerInstance.Closed += (sender, args) =>
                        {
                            colorPickerInstance = null;
                        };

                        colorPickerInstance.Show();
                    };
                })
                .AddSlider("AI Confidence Font Size", "Size", 1, 1, 1, 30, s =>
                {
                    uiManager.S_DPFontSize = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPFontSize((int)s.Slider.Value);
                }, tooltip: "Kích thước văn bản cho phần hiển thị tỷ lệ phần trăm độ tin cậy.")
                .AddSlider("Corner Radius", "Radius", 1, 1, 0, 100, s =>
                {
                    uiManager.S_DPCornerRadius = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWCornerRadius((int)s.Slider.Value);
                }, tooltip: "Độ bo góc của khung phát hiện. 0 = góc nhọn.")
                .AddSlider("Border Thickness", "Thickness", 0.1, 1, 0.1, 10, s =>
                {
                    uiManager.S_DPBorderThickness = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWBorderThickness(s.Slider.Value);
                }, tooltip: "Độ dày viền của khung phát hiện.")
                .AddSlider("Opacity", "Opacity", 0.1, 0.1, 0, 1, s =>
                {
                    uiManager.S_DPOpacity = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWOpacity(s.Slider.Value);
                }, tooltip: "Độ trong suốt của khung phát hiện. 0 = trong suốt hoàn toàn, 1 = đặc.")
                .AddSeparator();
        }

        private void LoadRecoilConfig()
        {
            try
            {
                var uiManager = _mainWindow!.uiManager;
                RecoilConfig.Children.Clear(); 

                // 1. Title
                var title = new ATitle("Recoil Config", true);
                title.Minimize.Click += (s, e) => 
                {
                    TogglePanel("Recoil Config", RecoilConfigPanel);
                    if (_mainWindow != null) MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                };
                RecoilConfig.Children.Add(title);

                // 2. Main Toggle
                if (!Dictionary.toggleState.ContainsKey("Scope Recoil Control"))
                    Dictionary.toggleState["Scope Recoil Control"] = false;

                var toggleScopeRecoil = CreateToggle("Scope Recoil Control", "Tự động ghì tâm chuột xuống khi bắn.");
                uiManager.T_ScopeRecoil = toggleScopeRecoil;
                RecoilConfig.Children.Add(toggleScopeRecoil);

                // 3. Keybind
                string toggleKey = Dictionary.bindingSettings.ContainsKey("Recoil Toggle Keybind") ? Dictionary.bindingSettings["Recoil Toggle Keybind"] : "None";
                var keybind = CreateKeyChanger("Recoil Toggle", toggleKey, "Phím tắt để bật/tắt kiểm soát độ giật.");
                uiManager.C_RecoilKeybind = keybind;
                RecoilConfig.Children.Add(keybind);

                // 4. Mouse Wheel Toggle
                if (!Dictionary.toggleState.ContainsKey("Mouse Wheel Adjust"))
                    Dictionary.toggleState["Mouse Wheel Adjust"] = true;

                var toggleWheel = CreateToggle("Mouse Wheel Adjust", "Sử dụng con lăn chuột để điều chỉnh độ mạnh của độ giật ngay tức thì.");
                RecoilConfig.Children.Add(toggleWheel);

                // Mouse Wheel Step
                if (!Dictionary.sliderSettings.ContainsKey("Mouse Wheel Adjust Step"))
                    Dictionary.sliderSettings["Mouse Wheel Adjust Step"] = 2.0;

                var sliderWheelStep = CreateSlider("Mouse Wheel Adjust Step", "Độ nhạy con lăn", 0.1, 0.5, 0.1, 10.0, "Độ thay đổi của lực ghì tâm mỗi khi lăn chuột.");
                uiManager.S_MouseWheelAdjustStep = sliderWheelStep;
                RecoilConfig.Children.Add(sliderWheelStep);

                // 5. Scope Dropdown
                var scopeDropdown = CreateDropdown("Select Scope", "Cấu hình độ giật cho từng loại ống ngắm.");
                string[] scopeNames = { "Scope 1 (Red Dot/1x)", "Scope 2 (2x)", "Scope 3 (3x)", "Scope 4 (4x)", "Scope 5 (6x)", "Scope 6 (8x)" };

                scopeDropdown.DropdownBox.Foreground = Brushes.White;
                var itemStyle = new Style(typeof(ComboBoxItem));
                itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
                itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
                scopeDropdown.DropdownBox.ItemContainerStyle = itemStyle;

                foreach (var name in scopeNames)
                {
                    scopeDropdown.DropdownBox.Items.Add(new ComboBoxItem { Content = name });
                }
                scopeDropdown.DropdownBox.SelectedIndex = Math.Clamp(RecoilManager.SelectedScopeIndex, 0, 5);
                RecoilConfig.Children.Add(scopeDropdown);
                // 6. Dynamic Panel
                var dynamicSettingsPanel = new StackPanel();
                RecoilConfig.Children.Add(dynamicSettingsPanel);

                void UpdateScopeSettings(int scopeIndex)
                {
                    try
                    {
                        dynamicSettingsPanel.Children.Clear();
                        int scopeNum = scopeIndex + 1;

                        string tapKey = $"Recoil Scope {scopeNum} Tap";
                        string tapDistanceKey = $"Recoil Scope {scopeNum} Tap Distance";
                        if (!Dictionary.toggleState.ContainsKey(tapKey)) Dictionary.toggleState[tapKey] = false;
                        if (!Dictionary.sliderSettings.ContainsKey(tapDistanceKey)) Dictionary.sliderSettings[tapDistanceKey] = 40.0;
                        var tapToggle = CreateToggle(tapKey,
                            "Súng tap: khi giữ chuột phải, mỗi lần nhấn chuột trái kéo xuống một lần. Thay thế 4 giai đoạn ghì liên tục cho scope này.");
                        dynamicSettingsPanel.Children.Add(tapToggle);
                        var tapSettingsPanel = new StackPanel
                        {
                            Visibility = (bool)Dictionary.toggleState[tapKey] ? Visibility.Visible : Visibility.Collapsed
                        };
                        dynamicSettingsPanel.Children.Add(tapSettingsPanel);
                        tapToggle.Reader.Click += (s, e) => tapSettingsPanel.Visibility =
                            (bool)Dictionary.toggleState[tapKey] ? Visibility.Visible : Visibility.Collapsed;
                        tapSettingsPanel.Children.Add(CreateSlider($"Recoil Scope {scopeNum} Tap Reset Time", "Nghỉ để về phát 1 (s)", 0.1, 0.1, 0.1, 10,
                            "Ngừng bắn đủ thời gian này thì lần bấm tiếp theo dùng mức phát 1. Nhả ngắm hoặc đổi scope/slot cũng về phát 1."));
                        for (int shot = 1; shot <= InputLogic.RecoilManager.TapShotCount; shot++)
                        {
                            string shotKey = $"Recoil Scope {scopeNum} Tap Shot {shot}";
                            Dictionary.sliderSettings[shotKey] = (double)RecoilManager.GetTapShotDistance(scopeNum, shot);
                            tapSettingsPanel.Children.Add(CreateSlider(shotKey, $"Khoảng kéo phát {shot}", 1, 1, 0, 2000,
                                "Kéo một lần khi nhấn bắn trong lúc ngắm. Phát 6 trở đi dùng mức phát 5. Ngừng bắn đủ thời gian nghỉ, nhả ngắm, đổi scope/slot hoặc tắt Tap để về phát 1."));
                        }
                        // Function to create a section header that connects seamlessly with ASlider borders
                        Border CreateHeader(string text)
                        {
                            return new Border
                            {
                                Height = 30, // Adjust height as needed
                                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C")),
                                BorderThickness = new Thickness(1, 0, 1, 0),
                                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FFFFFFF")),
                                Child = new TextBlock
                                {
                                    Text = text,
                                    Foreground = Brushes.Cyan,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                    Margin = new Thickness(10, 0, 0, 0),
                                    FontWeight = FontWeights.Bold
                                }
                            };
                        }

                        // Helper to add sliders
                        void AddStage(string headerText, string forceKey, string timeKey, string tip, bool hasTime = true)
                        {
                            // Add Header
                            dynamicSettingsPanel.Children.Add(CreateHeader(headerText));

                            // Force Slider
                            var forceSlider = CreateSlider(forceKey, "Lực ghì", 0.01, 0.01, 0, 200, tip + " - Lực ghì giảm 50%; có thể nhập 0,01 / 0,1 / 0,5. Giá trị 0 không kéo.");
                            forceSlider.Slider.ValueChanged += (_, _) => RecoilManager.SetStageForce(scopeNum, forceKey, forceSlider.Slider.Value);
                            dynamicSettingsPanel.Children.Add(forceSlider);

                            if (hasTime)
                            {
                                // Time Slider
                                var timeSlider = CreateSlider(timeKey, "Thời gian (s)", 0.05, 0.1, 0, 5, tip + " - Thời gian");
                                dynamicSettingsPanel.Children.Add(timeSlider);
                            }
                            else
                            {
                                // Info for End Stage
                                // Wrap in border to maintain line continuity if needed, or just use a disabled slider labeled 'Infinite'
                                // A simple TextBlock might break the line again.
                                // Let's use a "fake" slider or just a container that looks like one.
                                // Simplest is to just have the header and force slider. Use tooltip on slider to explain.
                                // Adding a text block inside a slider-like border:
                                var infoBorder = new Border
                                {
                                    Height = 30,
                                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C")),
                                    BorderThickness = new Thickness(1, 0, 1, 0),
                                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FFFFFFF")),
                                    Child = new TextBlock
                                    {
                                        Text = "Tiếp tục chạy cho đến khi nhả chuột.",
                                        Foreground = Brushes.Gray,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        FontSize = 10
                                    }
                                };
                                dynamicSettingsPanel.Children.Add(infoBorder);
                            }
                        }

                        AddStage("Giai đoạn 1: Bắt đầu", $"Recoil Scope {scopeNum} S1 Force", $"Recoil Scope {scopeNum} S1 Time", "Giai đoạn đầu");
                        AddStage("Giai đoạn 2: Giữa", $"Recoil Scope {scopeNum} S2 Force", $"Recoil Scope {scopeNum} S2 Time", "Giai đoạn giữa");
                        AddStage("Giai đoạn 3: Cuối", $"Recoil Scope {scopeNum} S3 Force", $"Recoil Scope {scopeNum} S3 Time", "Giai đoạn gần kết thúc");
                        AddStage("Giai đoạn 4: Lặp lại", $"Recoil Scope {scopeNum} S4 Force", "", "Giai đoạn cuối", false);
                    }
                    catch (Exception iex)
                    {
                         LogManager.Log(LogManager.LogLevel.Error, "Error UpdateScopeSettings: " + iex.Message);
                    }
                }

                UpdateScopeSettings(scopeDropdown.DropdownBox.SelectedIndex);

                scopeDropdown.DropdownBox.SelectionChanged += (s, e) =>
                {
                    if (scopeDropdown.DropdownBox.SelectedIndex != -1)
                        UpdateScopeSettings(scopeDropdown.DropdownBox.SelectedIndex);
                };

                RecoilConfig.Children.Add(new ARectangleBottom());
                RecoilConfig.Children.Add(new ASpacer());

                // Apply minimize state to the newly loaded controls
                ApplyPanelState("Recoil Config", RecoilConfigPanel);
                if (_mainWindow != null)
                {
                    MainWindow.UpdateSliderVisibility(_mainWindow.uiManager);
                }
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, "Critical Error LoadRecoilConfig: " + ex.Message);
                RecoilConfig.Children.Add(new TextBlock { Text = "ERROR: " + ex.Message, Foreground = Brushes.Red });
            }
        }

        private void DeleteLootItem(int itemNumToDelete)
        {
            int itemCount = 6;
            if (Dictionary.sliderSettings.ContainsKey("Fast Loot Item Count"))
            {
                itemCount = (int)Convert.ToDouble(Dictionary.sliderSettings["Fast Loot Item Count"]);
            }

            // Shift remaining items
            for (int i = itemNumToDelete; i < itemCount; i++)
            {
                int nextItem = i + 1;
                
                string nextXKey = $"Loot Item {nextItem} X";
                string currentXKey = $"Loot Item {i} X";
                if (Dictionary.sliderSettings.ContainsKey(nextXKey))
                    Dictionary.sliderSettings[currentXKey] = Dictionary.sliderSettings[nextXKey];
                
                string nextYKey = $"Loot Item {nextItem} Y";
                string currentYKey = $"Loot Item {i} Y";
                if (Dictionary.sliderSettings.ContainsKey(nextYKey))
                    Dictionary.sliderSettings[currentYKey] = Dictionary.sliderSettings[nextYKey];
                    
                string nextActionKey = $"Loot Item {nextItem} Action";
                string currentActionKey = $"Loot Item {i} Action";
                if (Dictionary.sliderSettings.ContainsKey(nextActionKey))
                    Dictionary.sliderSettings[currentActionKey] = Dictionary.sliderSettings[nextActionKey];
                else
                    Dictionary.sliderSettings[currentActionKey] = (double)((i % 2 == 1) ? 0 : 1);
            }

            // Remove last item keys
            Dictionary.sliderSettings.Remove($"Loot Item {itemCount} X");
            Dictionary.sliderSettings.Remove($"Loot Item {itemCount} Y");
            Dictionary.sliderSettings.Remove($"Loot Item {itemCount} Action");

            // Decrement item count
            Dictionary.sliderSettings["Fast Loot Item Count"] = (double)(itemCount - 1);

            _mainWindow?.SaveAllConfigurations();
            LoadLootConfig();
        }

        private void LoadLootConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            LootConfig.Children.Clear();

            var builder = new SectionBuilder(this, LootConfig);

            builder.AddTitle("Fast Loot Config", true, t =>
            {
                t.Minimize.Click += (s, e) =>
                {
                    TogglePanel("Fast Loot Config", LootConfigPanel);
                };
            })
            .AddToggle("Fast Loot", t =>
            {
                uiManager.T_FastLoot = t;
            }, tooltip: "Bật/Tắt chức năng Fast Loot. Nhấn phím loot để tự động kéo item vào inventory.")
            .AddKeyChanger("Fast Loot Keybind", k =>
            {
                uiManager.C_FastLootKeybind = k;
            }, tooltip: "Phím tắt để kích hoạt Fast Loot (mặc định: Nút giữa chuột).")
            .AddSlider("Fast Loot Delay", "ms", 1, 1, 1, 50, tooltip: "Thời gian chờ giữa các lần kéo item (ms). Nhỏ hơn = nhanh hơn nhưng có thể bị lỗi.");

            // Create button template for rounded corners
            var btnTemplate = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cp);
            btnTemplate.VisualTree = borderFactory;

            // Get dynamic item count
            int itemCount = 6;
            if (Dictionary.sliderSettings.ContainsKey("Fast Loot Item Count"))
            {
                itemCount = (int)Convert.ToDouble(Dictionary.sliderSettings["Fast Loot Item Count"]);
            }
            else
            {
                Dictionary.sliderSettings["Fast Loot Item Count"] = 6.0;
            }

            // Render items
            for (int i = 0; i < itemCount; i++)
            {
                int itemNum = i + 1;

                string posName = $"Item {itemNum}";
                if (i < InputLogic.LootManager.PositionNames.Length)
                {
                    posName = InputLogic.LootManager.PositionNames[i];
                }

                int currentX = Dictionary.sliderSettings.ContainsKey($"Loot Item {itemNum} X") ? (int)Convert.ToDouble(Dictionary.sliderSettings[$"Loot Item {itemNum} X"]) : 0;
                int currentY = Dictionary.sliderSettings.ContainsKey($"Loot Item {itemNum} Y") ? (int)Convert.ToDouble(Dictionary.sliderSettings[$"Loot Item {itemNum} Y"]) : 0;

                string actionKey = $"Loot Item {itemNum} Action";
                int actionValue = 1; // default to Drag, matching the original Loot.ahk behavior
                if (Dictionary.sliderSettings.ContainsKey(actionKey))
                {
                    actionValue = (int)Convert.ToDouble(Dictionary.sliderSettings[actionKey]);
                }
                else
                {
                    Dictionary.sliderSettings[actionKey] = (double)actionValue;
                }

                var containerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(63, 60, 60, 60)),
                    BorderThickness = new Thickness(1, 0, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255)),
                    Padding = new Thickness(10, 6, 10, 6)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Label
                var label = new TextBlock
                {
                    Text = posName,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = (FontFamily?)TryFindResource("Atkinson Hyperlegible") ?? new FontFamily("Segoe UI")
                };
                Grid.SetColumn(label, 0);

                // Coordinates
                var coordText = new TextBlock
                {
                    Text = $"X:{currentX} Y:{currentY}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 150)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    FontFamily = (FontFamily?)TryFindResource("Atkinson Hyperlegible") ?? new FontFamily("Segoe UI")
                };
                Grid.SetColumn(coordText, 1);

                // Action Toggle Button
                var actionBtn = new Button
                {
                    Content = actionValue == 0 ? "R-Click" : "Drag",
                    Width = 55,
                    Height = 24,
                    Background = actionValue == 0 ? new SolidColorBrush(Color.FromRgb(38, 108, 200)) : new SolidColorBrush(Color.FromRgb(38, 166, 91)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 10,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = btnTemplate,
                    Margin = new Thickness(0, 0, 5, 0),
                    ToolTip = "Click để đổi hành động: Right Click (Loot nhanh) hoặc Drag (Kéo thả vào inventory)"
                };
                Grid.SetColumn(actionBtn, 2);

                actionBtn.Click += (s, e) =>
                {
                    int currentVal = (int)Convert.ToDouble(Dictionary.sliderSettings[actionKey]);
                    int newVal = currentVal == 0 ? 1 : 0;
                    Dictionary.sliderSettings[actionKey] = (double)newVal;
                    actionBtn.Content = newVal == 0 ? "R-Click" : "Drag";
                    actionBtn.Background = newVal == 0 ? new SolidColorBrush(Color.FromRgb(38, 108, 200)) : new SolidColorBrush(Color.FromRgb(38, 166, 91));
                    _mainWindow?.SaveAllConfigurations();
                };

                // Set Button
                var setBtn = new Button
                {
                    Content = "Set",
                    Width = 35,
                    Height = 24,
                    Background = new SolidColorBrush(Color.FromRgb(114, 46, 209)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 11,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = btnTemplate,
                    Margin = new Thickness(0, 0, 5, 0),
                    ToolTip = $"Click rồi chọn vị trí {posName} trên màn hình"
                };
                Grid.SetColumn(setBtn, 3);

                setBtn.Click += (s, e) =>
                {
                    LogManager.Log(LogManager.LogLevel.Info, $"Click chuột trái vào vị trí {posName} trên màn hình...", true, 5000);
                    Task.Run(() =>
                    {
                        while ((GetAsyncKeyState(0x01) & 0x8000) == 0)
                        {
                            Thread.Sleep(10);
                        }
                        GetCursorPos(out POINT pt);
                        int newX = pt.X;
                        int newY = pt.Y;
                        while ((GetAsyncKeyState(0x01) & 0x8000) != 0)
                        {
                            Thread.Sleep(10);
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Dictionary.sliderSettings[$"Loot Item {itemNum} X"] = (double)newX;
                            Dictionary.sliderSettings[$"Loot Item {itemNum} Y"] = (double)newY;
                            coordText.Text = $"X:{newX} Y:{newY}";
                            LogManager.Log(LogManager.LogLevel.Info, $"{posName}: X={newX} Y={newY}", true, 3000);
                            _mainWindow?.SaveAllConfigurations();
                        });
                    });
                };

                // Delete Button
                var delBtn = new Button
                {
                    Content = "X",
                    Width = 24,
                    Height = 24,
                    Background = new SolidColorBrush(Color.FromRgb(211, 47, 47)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = btnTemplate,
                    ToolTip = $"Xóa {posName}"
                };
                Grid.SetColumn(delBtn, 4);

                delBtn.Click += (s, e) =>
                {
                    DeleteLootItem(itemNum);
                };

                grid.Children.Add(label);
                grid.Children.Add(coordText);
                grid.Children.Add(actionBtn);
                grid.Children.Add(setBtn);
                grid.Children.Add(delBtn);
                containerBorder.Child = grid;
                LootConfig.Children.Add(containerBorder);
            }

            // Inventory row container
            {
                int invX = Dictionary.sliderSettings.ContainsKey("Loot Inventory X") ? (int)Convert.ToDouble(Dictionary.sliderSettings["Loot Inventory X"]) : 0;
                int invY = Dictionary.sliderSettings.ContainsKey("Loot Inventory Y") ? (int)Convert.ToDouble(Dictionary.sliderSettings["Loot Inventory Y"]) : 0;

                var containerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(63, 60, 60, 60)),
                    BorderThickness = new Thickness(1, 0, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255)),
                    Padding = new Thickness(10, 6, 10, 6)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // placeholder for action
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Set
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // delete placeholder/empty

                var label = new TextBlock
                {
                    Text = "Inventory",
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = (FontFamily?)TryFindResource("Atkinson Hyperlegible") ?? new FontFamily("Segoe UI")
                };
                Grid.SetColumn(label, 0);

                var coordText = new TextBlock
                {
                    Text = $"X:{invX} Y:{invY}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 150)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    FontFamily = (FontFamily?)TryFindResource("Atkinson Hyperlegible") ?? new FontFamily("Segoe UI")
                };
                Grid.SetColumn(coordText, 1);

                var setBtn = new Button
                {
                    Content = "Set",
                    Width = 35,
                    Height = 24,
                    Background = new SolidColorBrush(Color.FromRgb(114, 46, 209)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 11,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = btnTemplate,
                    Margin = new Thickness(0, 0, 29, 0), // Shift to align with other rows since no delete button (24 width + 5 margin = 29)
                    ToolTip = "Click rồi chọn vị trí Inventory trên màn hình"
                };
                Grid.SetColumn(setBtn, 3);

                setBtn.Click += (s, e) =>
                {
                    LogManager.Log(LogManager.LogLevel.Info, "Click chuột trái vào vị trí Inventory trên màn hình...", true, 5000);
                    Task.Run(() =>
                    {
                        while ((GetAsyncKeyState(0x01) & 0x8000) == 0)
                        {
                            Thread.Sleep(10);
                        }
                        GetCursorPos(out POINT pt);
                        int newX = pt.X;
                        int newY = pt.Y;
                        while ((GetAsyncKeyState(0x01) & 0x8000) != 0)
                        {
                            Thread.Sleep(10);
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Dictionary.sliderSettings["Loot Inventory X"] = (double)newX;
                            Dictionary.sliderSettings["Loot Inventory Y"] = (double)newY;
                            coordText.Text = $"X:{newX} Y:{newY}";
                            LogManager.Log(LogManager.LogLevel.Info, $"Inventory: X={newX} Y={newY}", true, 3000);
                            _mainWindow?.SaveAllConfigurations();
                        });
                    });
                };

                grid.Children.Add(label);
                grid.Children.Add(coordText);
                grid.Children.Add(setBtn);
                containerBorder.Child = grid;
                LootConfig.Children.Add(containerBorder);
            }

            // Add Item button container
            {
                var containerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(63, 60, 60, 60)),
                    BorderThickness = new Thickness(1, 0, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255)),
                    Padding = new Thickness(10, 6, 10, 6)
                };

                var addBtn = new Button
                {
                    Content = "+ Add Loot Item",
                    Height = 24,
                    Background = new SolidColorBrush(Color.FromRgb(38, 166, 91)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = btnTemplate,
                    ToolTip = "Thêm một vật phẩm mới vào danh sách Fast Loot"
                };

                addBtn.Click += (s, e) =>
                {
                    int currentCount = 6;
                    if (Dictionary.sliderSettings.ContainsKey("Fast Loot Item Count"))
                    {
                        currentCount = (int)Convert.ToDouble(Dictionary.sliderSettings["Fast Loot Item Count"]);
                    }

                    int newCount = currentCount + 1;
                    Dictionary.sliderSettings["Fast Loot Item Count"] = (double)newCount;
                    
                    Dictionary.sliderSettings[$"Loot Item {newCount} X"] = 0.0;
                    Dictionary.sliderSettings[$"Loot Item {newCount} Y"] = 0.0;
                    Dictionary.sliderSettings[$"Loot Item {newCount} Action"] = 1.0;

                    _mainWindow?.SaveAllConfigurations();
                    LoadLootConfig();
                };

                containerBorder.Child = addBtn;
                LootConfig.Children.Add(containerBorder);
            }

            builder.AddSeparator();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        #region Helper Methods

        private void OnImageSizeChanged(int imageSize)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                imageSize = FovSettings.ImageSize;
                FovSettings.Synchronize(imageSize);
                if (_mainWindow?.uiManager.S_FOVSize != null && _mainWindow.uiManager.S_DynamicFOVSize != null)
                {
                    UpdateFovSizeSlider(_mainWindow.uiManager.S_FOVSize, imageSize);
                    UpdateFovSizeSlider(_mainWindow.uiManager.S_DynamicFOVSize, imageSize);
                    _mainWindow.uiManager.S_FOVSize.Slider.Value = Convert.ToDouble(Dictionary.sliderSettings["FOV Size"]);
                    _mainWindow.uiManager.S_DynamicFOVSize.Slider.Value = Convert.ToDouble(Dictionary.sliderSettings["Dynamic FOV Size"]);
                    _mainWindow.ActualFOV = Convert.ToDouble(Dictionary.sliderSettings["FOV Size"]);
                    PropertyChanger.PostNewFOVSize(_mainWindow.ActualFOV);
                }
            }));
        }        private void UpdateFovSizeSlider(ASlider slider, int imageSize = 640)
        {
            if (slider.Slider == null) return;
            if (imageSize < slider.Slider.Value)
            {
                slider.Slider.Value = imageSize;
            }
            slider.Slider.Maximum = imageSize;
        }

        private async Task ResetToMouseEvent()
        {
            await Task.Delay(500);
            _mainWindow!.uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = 0;
        }

        private void HandleColorChange(AColorChanger colorChanger, string settingKey, Action<Color> updateAction)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                colorChanger.ColorChangingBorder.Background = new SolidColorBrush(color);
                Dictionary.colorState[settingKey] = color.ToString();
                updateAction(color);
            }
        }

        public void RefreshRecoilConfig()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadRecoilConfig();
            });
        }

        public void Dispose()
        {
            // Save minimize states before disposing
            SaveMinimizeStatesToGlobal();
        }

        #endregion

        #region Section Builder

        private class SectionBuilder
        {
            private readonly AimMenuControl _parent;
            private readonly StackPanel _panel;

            public SectionBuilder(AimMenuControl parent, StackPanel panel)
            {
                _parent = parent;
                _panel = panel;
            }

            public SectionBuilder AddTitle(string title, bool canMinimize, Action<ATitle>? configure = null)
            {
                var titleControl = new ATitle(title, canMinimize);
                configure?.Invoke(titleControl);
                _panel.Children.Add(titleControl);
                return this;
            }

            public SectionBuilder AddToggle(string title, Action<AToggle>? configure = null, string? tooltip = null)
            {
                var toggle = _parent.CreateToggle(title, tooltip);
                configure?.Invoke(toggle);
                _panel.Children.Add(toggle);
                return this;
            }

            public SectionBuilder AddKeyChanger(string title, Action<AKeyChanger>? configure = null, string? defaultKey = null, string? tooltip = null)
            {
                var key = defaultKey ?? Dictionary.bindingSettings[title];
                var keyChanger = _parent.CreateKeyChanger(title, key, tooltip);
                configure?.Invoke(keyChanger);
                _panel.Children.Add(keyChanger);
                return this;
            }

            public SectionBuilder AddSlider(string title, string label, double frequency, double buttonSteps,
                double min, double max, Action<ASlider>? configure = null, string? tooltip = null)
            {
                var slider = _parent.CreateSlider(title, label, frequency, buttonSteps, min, max, tooltip);
                configure?.Invoke(slider);
                _panel.Children.Add(slider);
                return this;
            }

            public SectionBuilder AddDropdown(string title, Action<ADropdown>? configure = null, string? tooltip = null)
            {
                var dropdown = _parent.CreateDropdown(title, tooltip);
                configure?.Invoke(dropdown);
                _panel.Children.Add(dropdown);
                return this;
            }

            public SectionBuilder AddColorChanger(string title, Action<AColorChanger>? configure = null)
            {
                var colorChanger = _parent.CreateColorChanger(title);
                configure?.Invoke(colorChanger);
                _panel.Children.Add(colorChanger);
                return this;
            }

            public SectionBuilder AddButton(string title, Action<APButton>? configure = null, string? tooltip = null)
            {
                var button = new APButton(title, tooltip);
                configure?.Invoke(button);
                _panel.Children.Add(button);
                return this;
            }

            public SectionBuilder AddFileLocator(string title, Action<AFileLocator>? configure = null,
                string filter = "All files (*.*)|*.*", string dlExtension = "")
            {
                var fileLocator = new AFileLocator(title, title, filter, dlExtension);
                configure?.Invoke(fileLocator);
                _panel.Children.Add(fileLocator);
                return this;
            }

            public SectionBuilder AddSeparator()
            {
                _panel.Children.Add(new ARectangleBottom());
                _panel.Children.Add(new ASpacer());
                return this;
            }

            public SectionBuilder AddCustomElement(Func<UIElement> createElement)
            {
                var element = createElement();
                _panel.Children.Add(element);
                return this;
            }
        }

        #endregion

        #region Control Creation Methods

        private AToggle CreateToggle(string title, string? tooltip = null)
        {
            var toggle = new AToggle(title, tooltip);
            _mainWindow!.toggleInstances[title] = toggle;

            // Set initial state
            if (Dictionary.toggleState[title])
                toggle.EnableSwitch();
            else
                toggle.DisableSwitch();

            // Handle click
            toggle.Reader.Click += (sender, e) =>
            {
                Dictionary.toggleState[title] = !Dictionary.toggleState[title];
                _mainWindow.UpdateToggleUI(toggle, Dictionary.toggleState[title]);
                _mainWindow.Toggle_Action(title);
            };

            return toggle;
        }

        private AKeyChanger CreateKeyChanger(string title, string keybind, string? tooltip = null)
        {
            var keyChanger = new AKeyChanger(title, keybind, tooltip);

            keyChanger.Reader.Click += (sender, e) =>
            {
                keyChanger.KeyNotifier.Content = "...";
                _mainWindow!.bindingManager.StartListeningForBinding(title);

                Action<string, string>? bindingSetHandler = null;
                bindingSetHandler = (bindingId, key) =>
                {
                    if (bindingId == title)
                    {
                        keyChanger.KeyNotifier.Content = KeybindNameManager.ConvertToRegularKey(key);
                        Dictionary.bindingSettings[bindingId] = key;
                        _mainWindow.bindingManager.OnBindingSet -= bindingSetHandler;
                    }
                };

                _mainWindow.bindingManager.OnBindingSet += bindingSetHandler;
            };

            return keyChanger;
        }

        private ASlider CreateSlider(string title, string label, double frequency, double buttonSteps,
            double min, double max, string? tooltip = null)
        {
            var slider = new ASlider(title, label, buttonSteps, tooltip)
            {
                Slider = { Minimum = min, Maximum = max, TickFrequency = frequency }
            };

            slider.Slider.Value = Dictionary.sliderSettings.TryGetValue(title, out var value) ? value : min;
            if (title is "Slot 1 Mouse Sensitivity" or "Mouse Sensitivity (+/-)")
                MouseSensitivityProfiles.Bind(slider, title == "Slot 1 Mouse Sensitivity" ? 1 : 2);
            else
                slider.Slider.ValueChanged += (s, e) => Dictionary.sliderSettings[title] = slider.Slider.Value;

            return slider;
        }

        private ADropdown CreateDropdown(string title, string? tooltip = null) => new(title, title, tooltip);

        private AColorChanger CreateColorChanger(string title)
        {
            var colorChanger = new AColorChanger(title);
            colorChanger.ColorChangingBorder.Background =
                (Brush)new BrushConverter().ConvertFromString(Dictionary.colorState[title]);
            return colorChanger;
        }

        // Scope info panel with public TextBlocks for updates
        public static TextBlock? Slot1ScopeText;
        public static TextBlock? Slot2ScopeText;
        public static TextBlock? ActiveSlotText;

        private UIElement CreateScopeInfoPanel()
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 5, 0, 5),
                Background = new SolidColorBrush(Color.FromArgb(40, 114, 46, 209))
            };

            // Title
            var titleText = new TextBlock
            {
                Text = "Detected Scopes",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10, 5, 10, 2),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(titleText);

            // Slot 1
            var slot1Panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 2, 10, 2)
            };
            var slot1Label = new TextBlock
            {
                Text = "Slot 1: ",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 11,
                Width = 50
            };
            Slot1ScopeText = new TextBlock
            {
                Text = "None",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            slot1Panel.Children.Add(slot1Label);
            slot1Panel.Children.Add(Slot1ScopeText);
            panel.Children.Add(slot1Panel);

            // Slot 2
            var slot2Panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 2, 10, 2)
            };
            var slot2Label = new TextBlock
            {
                Text = "Slot 2: ",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 11,
                Width = 50
            };
            Slot2ScopeText = new TextBlock
            {
                Text = "None",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            slot2Panel.Children.Add(slot2Label);
            slot2Panel.Children.Add(Slot2ScopeText);
            panel.Children.Add(slot2Panel);

            // Active Slot
            var activePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 2, 10, 5)
            };
            var activeLabel = new TextBlock
            {
                Text = "Active: ",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 11,
                Width = 50
            };
            ActiveSlotText = new TextBlock
            {
                Text = "Slot 1",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            activePanel.Children.Add(activeLabel);
            activePanel.Children.Add(ActiveSlotText);
            panel.Children.Add(activePanel);

            return panel;
        }

        #endregion
    }
}


