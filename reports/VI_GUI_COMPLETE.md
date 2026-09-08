# Vietnamese interface repair

Fixed lazy pages that retained English after selecting Vietnamese. Added explicit localization after each UI section loads, live updates of rendered dynamic text, and translated dropdown presentation while preserving original option values and setting keys.

Language now uses the same ADropdown control inside Settings Menu as the surrounding controls. Slider labels use remaining column width instead of a fixed 172-pixel width. Added translations for aim, recoil, ESP, settings, model tabs, key labels, units, selectable options, startup, tray menu and app dialogs. Model filenames and technology names stay intact. External DLL dialogs and raw diagnostic details remain owned by their respective providers.

Validation: Release build and UI regression checks. Real WPF test windows verified switching EN/VI, pages loaded after switching, unchanged dropdown selections, dynamic text updates, original English restoration and Vietnamese dialog Yes result. Rendered Settings, Aim, Model and About pages inspected. No driver initialization or game mouse input during localization tests.

Open bin/Build/CouldBeAimmyV2.exe for the rebuilt version; an already running process cannot acquire the new interface code.

## Model naming and rebuilt controls
- Replaced numbered ô labels with Model 1 / Model 2, and numbered scope labels with Model scope 1–6.
- Labels and units bind their translations during construction, including controls created after their parent page has loaded.
- Dropdown presentation now also handles items added after initial construction, preserving stored values.
- Verified the screenshot selections SendInput, Linear, Closest to Mouse and Center on a real WPF page, as well as late-created controls and options. Release build and regression tests passed.
- Latest cropped verification: reports/vi-model1-panel.png.

## Language menu, English guidance and scope overlay
- Language dropdown items use explicit dark backgrounds and high-contrast text, including highlight state.
- Added English counterparts for Vietnamese guidance and notices, plus reverse translations for existing labels. Both tooltip objects and inline tooltips follow language selection. Text bindings use a converter instead of an indexer path so commas and punctuation cannot break guidance bindings.
- Removed numbered Model prefixes from child labels; section headings retain model identity.
- Scope overlay labels and active scope now use Model scope 1/2. Increased overlay and label widths to prevent overlap.
- Real WPF checks passed for open language popup colors, English guidance including punctuation, English notices, language switching, scope overlay labels, and unchanged option IDs. Release: 0 errors, 250 warnings.
- Screenshots: reports/language-popup.png and reports/scope-model-labels.png.
