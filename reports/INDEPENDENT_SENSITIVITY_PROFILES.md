# Independent sensitivity profiles

Removed the WGC-to-GDI+ alias in MouseSensitivityProfiles.Current. Lookup and saving now use capture method → actual model image size → model slot, with WGC, GDI+ and DirectX independent. Existing stored profiles are reused; the user's profile file was not rewritten by this fix.

Added sensitivity UI refresh after an image-size selection even when no AI manager is loaded. Loaded models still use their actual size, and PublishSlotImageSize refreshes sensitivity after load. Updated the bilingual tooltip to describe independent profiles.

Validated 3 capture methods × 2 image sizes × 2 models: editing, switching back, JSON persistence, clearing in-memory state and reloading from disk, and different image sizes between the models. Real WPF sliders matched the values returned to the movement code. Tests used an isolated output directory and sent no input. General UI/aim/recoil regression checks also passed. Release build: 0 errors, 250 warnings.

Runtime file: bin/Build/bin/mouse-sensitivity.cfg. Values save automatically after adjustment (300 ms debounce) and flush on normal shutdown.
