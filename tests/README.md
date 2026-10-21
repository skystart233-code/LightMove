# Local desktop checks

User-preset checks cover persistence across reopening, point-copy isolation, same-name numbering, separate axes, preview-only loading and undo/redo, deleting/restoring, save failures and retaining corrupt files. Layout checks retain the first release's moderate plot proportions and keep a fixed footer while short windows scroll.

The feel comparison checks cover exact full-profile resume, neutral native output in both axes, concurrent click rejection, pending preview and undo preservation, driver failure and retry, journal failure before activation, corrupted recovery, and saved LUT control points after a paused restart. They use offline callbacks by default.

`render-feel-switch.ps1 -BuildDirectory <build-folder>` creates an offline native eight-state sheet (default, hover, focus, pressed, disabled, loading, error and success) as `feel-switch-states.png`; it does not touch the driver.

With explicit authorization, `CurveEditorTests.exe --live-comparison-roundtrip` temporarily submits a neutral profile, verifies typed driver readback, reopens recovery state and restores the exact starting configuration and settings in a finally block. The starting config is retained in `live-comparison-before.json`. This mode is excluded from the default test script.

Build the application using `build-zh-CN.ps1`, then pass its output folder to:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\run-curve-editor-tests.ps1 -BuildDirectory C:\path\to\build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\run-input-receiver-tests.ps1 -BuildDirectory C:\path\to\build
```

The curve checks use simulated callbacks, not live driver writes. They cover exported/native LUT parity, capped high-speed behavior, editing, preview/apply boundaries, cached points, live-marker freshness and current-curve versus preview positioning, DPI text sizes and scrolling.

Chrome regressions check actual native style flags after title/taskbar and window-state transitions, native hit testing, taskbar-safe maximize and closing cancellation. An opaque-buffer paint check invokes each custom button's foreground paint against a deliberately dirty buffer: the former background-only erase implementation fails this case. These checks do not replace checking the visible desktop. Windows Graphics Capture can time out on this machine, so a clean DrawToBitmap render must not be reported as proof of a clean live window.

Editing checks also cover standard/fine grid snapping, Shift alignment to an off-grid point's exact height, Alt override, arrow nudges, exact numeric input, neighbor bounds, release of alignment guides, and avoiding recalculation inside the same snapped cell. Grid preference changes never apply or dirty the existing curve.

Applied-reference markers are sampled from the actual applied curve at the saved editor speeds. Eight checks verify correct samples, independence from preview presets/dragging, read-only hit behavior, refresh after successful Apply, stable references when Undo changes only the preview, and selected-axis handling. These remain offline callback tests.

Undo/redo checks cover complete multi-move drag grouping, no-op clicks, numeric edits, add/delete, presets, clear-preview reversal, branch replacement, both axes and linking, history retained after Apply while the live baseline stays unchanged, the 100-step limit, keyboard commands from numeric fields, capture loss, and Undo during a drag. None of these history actions writes to the driver.

The input receiver checks compile isolated copies of the application with driver reads replaced by defaults and driver activation/reset disabled. Seven checks verify that the registered Raw Input receiver survives recreation of both form HWNDs, opening/closing the home, and minimize/restore; it is destroyed when the backend is disposed. The old implementation registered the legacy form's HWND once and fails the recreation case. This test queries actual Windows registration and window lifetime, but does not claim to generate physical mouse input.

Actual movement still needs a mouse: verify that the red marker follows slow and fast movement and keeps working after minimize/restore. The optional application argument `--check-live` writes receiver counts and the latest real sample once per second for local diagnosis. It is not enabled by normal startup.

LUT regression: JSON text parsing promotes Single entries to Double and can make exact comparison fail on unchanged coordinates. The default suite reproduces this failure, checks exact typed comparison of a serialized/loaded LUT, and rejects a real change to its float data.

For an explicitly authorized live integration check, run the compiled `CurveEditorTests.exe --live-lut-roundtrip` from its build folder on a machine with the driver installed. This temporarily applies an edited LUT, checks full typed readback, saved settings and ten curve samples, then restores the starting configuration in a finally block. It writes the starting configuration to `live-lut-before.json`; retain this backup if a machine or driver failure interrupts the test. This branch is never used by the default test script.
