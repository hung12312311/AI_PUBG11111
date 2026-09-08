# WGC aim stability
- WGC-only post-filter after smoothing/jitter: keep command away from target; deadband <=0.75 capture pixels per axis; suppress wrong-direction commands; near-center sign reversal within12 pixels reduces command to40% for80ms. Reset on idle>150ms, slot/model/size/sensitivity/path change, and other capture methods.
- Thresholds are conservative initial tuning, not calibrated by game measurements. No claim of identical behavior to GDI+.
- Fresh-frame capture policy retained; no repeated stale-frame input introduced.
- ScreenGrab timing excludes WGC calls waiting for a new frame.
- Recoil, GDI+ output logic, saved sensitivity settings unchanged.
- Tests PASS: far output unchanged, center deadband, reverse damping, wrong-direction rejection, slot/idle reset. Existing recoil and48 coordinate checks PASS. Final Release build PASS0 errors/250 warnings.
- NOT TESTED: actual WGC aiming in the user's game; latency and overshoot comparison against GDI+ on a moving target.
