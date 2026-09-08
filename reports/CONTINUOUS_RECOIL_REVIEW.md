# Continuous recoil review

User reports continuous fire stops pulling; tap still works.

Confirmed code defects: continuous force applied TemporaryStrengthOffset even with Mouse Wheel Adjust disabled; offset survived scope/model changes. A sufficiently negative offset cancels force. Switching tap to continuous while holding buttons also retained the previous firing clock.

RecoilManager now ignores wheel offset when its toggle is off, clears temporary adjustment when the scope/model/mode/enable context changes, and restarts the continuous stage clock and fractional accumulator on that transition. Stage selection is shared with the regression check. Loop exceptions are rate-limited in the existing diagnostic logger (Debug Mode must be enabled).

Saved forces and sensitivity profiles were not edited. Continuous gain remains 0.5; tap retains five stages. No input backend or Windows security changes.

Validation: Release build succeeded, 0 errors / 250 existing warnings. ContinuousRecoilChecks read all 24 saved stage forces from bin/Build/bin/configs/Default.cfg and passed disabled-wheel, scope/model/mode/enable transitions and zero-force checks. General UI regression checks also passed, including 48 original aim-coordinate cases and five-shot tap behavior. No game input was sent. This identifies and fixes reproducible state bugs; it does not establish that stale wheel offset was the sole cause of the user's in-game symptom.

Release output: bin/Build/CouldBeAimmyV2.exe. Removed regenerated root obj and UiRegressionChecks bin/obj after validation; retained test source and reports.
