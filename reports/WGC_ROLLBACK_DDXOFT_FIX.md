# WGC rollback / ddxoft initialization
- Removed WgcAimStabilizer and its output hook. Standard MouseManager response restored (ddxoft readiness wrapper is the only driver-dispatch difference).
- WGC and GDI+ share sensitivity context by slot and input size. Existing saved WGC values remain on disk but are no longer used. Slider label explicitly shows GDI+ profile; tooltip explains shared WGC/GDI+ values. Other capture behavior remains WGC, not silently switched to GDI+.
- Removed duplicate Slot2 ddxoft/Razer selected handlers. Added missing Slot1 ddxoft initialization. Failure reverts only the selected slot, only if ddxoft is still selected.
- ddxoft caches one in-progress initialization task and successful readiness; DLL loaded by absolute path, repeated LoadLibrary calls avoided. Native init runs off UI thread. Movement/buttons ignored before readiness.
- Failure message reports DLL load and DD_btn(0) results instead of declaring PC incompatible.
- Existing ddxoft.dll signature verified Valid. Official README states free version authenticates online on loading: https://github.com/ddxoft/master/blob/master/README.md . No reliable documentation found mapping Error ID7 specifically; timeout is not proven to be PC incompatibility or a specific network cause.
- Release build PASS 0 errors/250 warnings. Regression PASS: shared sensitivity context; pending init shared; pre-ready input suppressed; recoil/48 aim coordinate/UI tests pass.
- NOT TESTED: native driver initialization success/network authentication; WGC aim behavior in game. No security settings, driver binary or licenses changed.
