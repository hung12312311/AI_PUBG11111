# Five-shot tap and build cleanup

Tap mode now has five configurable shots. Shot 6 and later use shot 5 strength. The existing reset rules and first five stored values are unchanged; legacy values for shots 6–15 remain in config but are ignored. Updated default key creation, UI loop and bilingual guidance. Release build passed (0 errors, 250 warnings); regression tests covered shot 1–20 with legacy stage 6 and 15 values present.

Removed 956.9 MiB of intermediate and test builds (root obj, test project bin/obj, scratch/wgc-smoke) and 18,740,608 bytes of temporary ddxoft download/extraction dependencies. Main executable, runtime DLLs, model/config folders, source tests and backups retained. Exact paths are in test-build-cleanup-result.json and temp-download-cleanup.json. No rebuild was run after cleanup.

WGC investigation: screen-grab timing measures retrieving/copying a frame, not its age or input-to-visible-response latency. WGC supplies frames through a frame pool; the code polls the latest available frames. Movement applies sensitivity per inference update, so capture cadence can affect the control response. No game trace was collected to identify the user's oscillation precisely, and no additional movement filter or gain change was made in this turn. WGC jitter is not claimed fixed.
Source: https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture
