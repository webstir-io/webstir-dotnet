# HMR Validation Checklist

Follow these steps after touching the frontend hot-update pipeline.

## Automated Smoke
- Run `./utilities/format-build.sh` (invokes TypeScript build + node tests).
- Start `webstir watch` in a clean workspace and confirm the daemon boots without errors.

## JavaScript/Edit Loop
1. Launch `webstir watch` with `WEBSTIR_FRONTEND_HMR_VERBOSE=1`.
2. Modify `Engine/Resources/src/frontend/pages/home/index.ts` (e.g., change a string).
3. Verify the browser console logs:
   - `Applied hot update…` message with module/style counts.
   - `Totals — applied: <n>, fallbacks: <m>` increments without forcing a reload.
4. Repeat with additional JS edits to observe counters climbing without page refreshes.

## CSS Refresh
1. Edit `Engine/Resources/src/frontend/pages/home/index.css`.
2. Confirm the DOM injects a fresh stylesheet and console totals increment.

## Fallback Scenario
1. From the browser console run `window.__webstirAccept = () => false;`.
2. Edit the page script again.
3. Confirm:
   - Console warning announces fallback with reasons and totals.
   - SSE status switches to `hmr-fallback`, followed by a full reload.
   - Daemon logs show `Hot update totals — …` and `frontend.watch.pipeline.hmrfallback`.

## HTML/Manifest Change
1. Modify `Engine/Resources/src/frontend/app/app.html`.
2. Observe the daemon logging a reload requirement and the browser performing a full refresh.

## Performance Spot Check
- Capture `frontend.watch.javascript.build.stats` and `frontend.watch.hmr.summary` timings; ensure hot updates complete in <200 ms for the seed project.

Document any deviations (especially fallback rates >10%) before shipping.
