from pathlib import Path

p = Path('AILogic/AIManager.cs')
s = p.read_text(encoding='utf-8-sig')
def replace(old, new):
    global s
    assert old in s, old[:100]
    s = s.replace(old, new)

replace('if (useDirectML) { sessionOptions.AppendExecutionProvider_DML(); }\n                    else { sessionOptions.AppendExecutionProvider_CPU(); }', '')
replace('newSession = await Task.Run(() => new InferenceSession(modelPath, sessionOptions));', 'newSession = await Task.Run(() => OnnxModelSessionFactory.Load(modelPath, RequestedProvider(useDirectML)));')
start = s.index('                     // Try DirectML')
end = s.index('\n                 }\n                 finally', start)
s = s[:start] + '                     newSession = await Task.Run(() => OnnxModelSessionFactory.Load(modelPath, RequestedProvider(Dictionary.toggleState["DirectML"])));' + s[end:]
start = s.index('        private bool ValidateOnnxShape(')
end = s.index('        private void LoadClasses(', start)
s = s[:start] + '''        private static string RequestedProvider(bool directML) => Dictionary.dropdownState.TryGetValue("ONNX Provider", out var p)
            && p.ToString() != "Legacy" ? p.ToString() : directML ? "DirectML" : "CPU";

        public ModelMetadata? GetModelMetadata(int slot)
        {
            lock (_modelLock)
            {
                var session = slot == 2 ? _onnxModelSlot2 : _onnxModelSlot1;
                return session != null ? OnnxModelSessionFactory.Metadata(session) : (slot == 2 ? _engineModelSlot2 : _engineModelSlot1)?.Metadata;
            }
        }

        private bool ValidateOnnxShape(int slot, bool showNotification = true)
        {
            var session = slot == 2 ? _onnxModelSlot2 : _onnxModelSlot1;
            if (session == null) return false;
            try
            {
                var metadata = OnnxModelSessionFactory.Metadata(session);
                var size = metadata.ResolveSize(GetSlotImageSize(slot));
                bool dynamic = metadata.Dynamic;
                if (slot == 1) { _slot1ImageSize = size.Width; _slot1IsDynamic = dynamic; _slot1FixedSize = size.Width; _slot1IsNmsFree = true; _slot1NumDetections = 0; }
                else { _slot2ImageSize = size.Width; _slot2IsDynamic = dynamic; _slot2FixedSize = size.Width; _slot2IsNmsFree = true; _slot2NumDetections = 0; }
                PublishSlotImageSize(slot, size.Width, dynamic);
                if (ActiveSlot == slot)
                {
                    IsDynamicModel = CurrentModelIsDynamic = dynamic;
                    IsNmsFreeModel = true; NUM_DETECTIONS = 0;
                    DynamicModelStatusChanged?.Invoke(dynamic);
                }
                Log(LogLevel.Info, $"Slot {slot}: {metadata.Backend}, {metadata.Input.DataType}, {size.Width} × {size.Height}, {(dynamic ? "dynamic" : "fixed")}", showNotification);
                return true;
            }
            catch (Exception ex) { Log(LogLevel.Error, $"Model không tương thích: {ex.Message}", showNotification); return false; }
        }

''' + s[end:]
start = s.index('                    // Reuse tensor and inputs - recreate if size changed')
end = s.index('\n                }\n\n                if (outputTensor == null)', start)
s = s[:start] + '''                    lock (_modelLock)
                    {
                        if (_onnxModel == null || _isDisposed || ActiveSlot != activeSlot || IMAGE_SIZE != imageSize) return null;
                        var metadata = OnnxModelSessionFactory.Metadata(_onnxModel);
                        var size = metadata.ResolveSize(GetSlotImageSize(activeSlot));
                        var transform = new CaptureTransform(detectionBox, size.Width, size.Height, metadata.Options.Letterbox);
                        using (Benchmark("ModelInference"))
                            outputTensor = OnnxModelSessionFactory.Run(_onnxModel, _reusableInputArray, transform, _modeloptions,
                                (float)Dictionary.sliderSettings[activeSlot == 2 ? "Slot 2 AI Minimum Confidence" : "AI Minimum Confidence"] / 100f);
                    }''' + s[end:]
# Both backends now return canonical postprocessed capture-coordinate detections.
replace('activeSlot, imageSize, numDetections, isNmsFreeModel, numClasses, modelClasses, false);', 'activeSlot, imageSize, outputTensor.Dimensions[1], true, numClasses, modelClasses, false);')
start = s.index('                    if (x_max <= 1.05f && y_max <= 1.05f)')
end = s.index('\n                }\n                else', start)
s = s[:start] + s[end:]
replace('var filteredPredictions = ApplyNMS(allPredsSnapshot, 0.45f);', 'var filteredPredictions = allPredsSnapshot; // ModelOutputAdapter already applied the correct postprocess once.')
replace('sortedPredictions.RemoveAll(p => CalculateIoU(best.Rectangle, p.Rectangle) > iouThreshold);', 'sortedPredictions.RemoveAll(p => p.ClassId == best.ClassId && CalculateIoU(best.Rectangle, p.Rectangle) > iouThreshold);')
# Capture pixel coordinates map by translation, not display/crop scaling.
replace('            AIConf = closestPrediction.Confidence;', '            AIConf = closestPrediction.Confidence;\n            scaleX = scaleY = 1;')
replace('                detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);\n            }', '                detectedY = CalculateDetectedY(scaleY, YOffset, closestPrediction);\n            }\n            detectedX += _currentDetectionBox.Left - DisplayManager.ScreenLeft;\n            detectedY += _currentDetectionBox.Top - DisplayManager.ScreenTop;')
replace('float screenCenterX = IMAGE_SIZE / 2f;\n            float screenCenterY = IMAGE_SIZE / 2f;', 'float screenCenterX = _currentDetectionBox.Left + IMAGE_SIZE / 2f;\n            float screenCenterY = _currentDetectionBox.Top + IMAGE_SIZE / 2f;')
# Capture size is optional and independent; legacy zero follows slot width.
replace('public int IMAGE_SIZE => ActiveSlot == 1 ? _slot1ImageSize : _slot2ImageSize;', 'public int IMAGE_SIZE => Dictionary.sliderSettings.TryGetValue("Capture Size", out var capture) && Convert.ToInt32(capture) > 0\n            ? Convert.ToInt32(capture) : ActiveSlot == 1 ? _slot1ImageSize : _slot2ImageSize;')
replace('        public void RequestSizeChange(int newSize, int slot)\n        {', '        public void RequestSizeChange(int newSize, int slot)\n        {\n            if (newSize <= 0) throw new ArgumentOutOfRangeException(nameof(newSize));\n            var metadata = GetModelMetadata(slot);\n            if (metadata != null && !metadata.Dynamic) return;')
replace('                            float[] outputData = _engineModel.RunInference(_reusableInputArray);\n                            outputTensor = new DenseTensor<float>(outputData, _engineModel.OutputDims);', '                            outputTensor = _engineModel.RunDetections(_reusableInputArray, detectionBox,\n                                (float)Dictionary.sliderSettings[activeSlot == 2 ? "Slot 2 AI Minimum Confidence" : "AI Minimum Confidence"] / 100f);')
p.write_text(s, encoding='utf-8')

p = Path('UISections/SettingsMenuControl.xaml.cs'); s = p.read_text(encoding='utf-8-sig')
needle = '                for (int i = 0; i < dropdown.DropdownBox.Items.Count; i++)'
assert needle in s
s = s.replace(needle, '''                if (!dropdown.DropdownBox.Items.OfType<ComboBoxItem>().Any(x => x.Content?.ToString() == newSize))
                    dropdown.DropdownBox.Items.Add(new ComboBoxItem { Content = newSize });
''' + needle)
p.write_text(s, encoding='utf-8')
