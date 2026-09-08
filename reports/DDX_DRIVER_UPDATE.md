# ddxoft check — 2026-09-08

Official archive: https://github.com/ddxoft/master/blob/master/2026.DD.EV.HVCI.63xxx.7z

Replaced bin/Build/ddxoft.dll (dd32695) with official x64 1.simple/dd63330.dll; Authenticode Valid. SHA256: 72FBA9C7721AC394A0803D0C97835B0DA21C9D6DDA8076FC0584A566CD7B2A9D.
Old DLL: reports/ddxoft-backup/ddxoft-dd32695.dll. Removed automatic download from the obsolete third-party source.

All seven required exports present. Administrator initialization test sent no mouse movement or button presses. Windows System event 7045 recorded installation; sc query dd63330 reports RUNNING with both exit codes 0. DD_btn(0) still failed: first 0, retry -3. Meaning of -3 unverified. Mouse input is NOT confirmed working. Driver remains installed and running; Windows security settings unchanged.

Release build: 0 errors, 250 warnings. UiRegressionChecks passed. Model notification now says kích thước cố định / kích thước động instead of fixed / dynamic.

## Follow-up: installation/internal error screenshots
- Checked enabled CodeIntegrity and Defender operational logs for the preceding hour: no matching block events returned.
- dd63330 was STOP_PENDING on the first query, then absent (SC 1060) on the next. Earlier RUNNING does not imply persistent successful initialization.
- Current runtime DLL is DD63330; Windows 10 build 19045.6466.
- No evidence currently identifies a Windows protection rule to unblock. No Defender, firewall, signature enforcement, or Memory Integrity changes made.
- Driver initialization remains unresolved; use the existing Mouse Event fallback until a functioning vendor driver is available.
