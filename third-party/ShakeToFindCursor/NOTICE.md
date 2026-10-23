# ShakeToFindCursor

- Upstream: https://github.com/0cch/ShakeToFindCursor
- Revision: `972287b11c35646645c7c31716c8e591f5f51eea`
- License: MIT, Copyright (c) 2024 0CCh. Full text: [LICENSE](LICENSE).
- Reused parts: `main.cpp`'s `MouseMoveDetector` movement-history / reversal detection and `CursorUtils::ScaleCursor` / `LargeCursor` / `LargeCursorManager` native cursor replacement and restoration, ported to C# in `ShakeDetector.cs`, `CursorPicture.cs` and `NativeCursorEffect.cs`.
- LightMove changes: time-bounded history, small-movement filtering, per-axis reversal threshold, button-held suppression, bounded grow/hold/shrink animation, preference and tray integration.
- Rendering: replace the standard system cursors temporarily with `SetSystemCursor`, so there is exactly one native pointer and no overlay. Render the stock Windows Aero arrow using geometry at 4x sampling and the default text caret as antialiased geometry. Prefer high-resolution cursor files for custom themes. Do not write cursor registry values, install hooks, or inject input.
- Recovery: original cursors are copied before each effect. Restore after 220 ms without a fresh shake reversal, when dragging starts, on disable, or on exit. `LightMove.CursorGuard.exe` keeps independent original copies and restores them if the owner exits or stops sending a heartbeat for 600 ms. This helper uses the same compiled application with a dedicated entry point; it has no driver, input-hook or network functionality.
- Scope: ordinary interactive Windows desktop. Secure desktops, exclusive fullscreen applications and application-owned cursor images may not enlarge. Busy animations can appear static briefly while enlarged; stopping restores the saved native cursors.

The distributed EXE includes the upstream MIT text and this attribution in `开源组件许可.txt`. See [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md) for all third-party components and distribution requirements. The existing 0.5.0-preview EXE includes this ShakeToFindCursor text; Newtonsoft.Json's full license is supplied by the release companion license ZIP.
