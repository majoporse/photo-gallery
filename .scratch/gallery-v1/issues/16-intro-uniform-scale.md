# 16 — Home intro: the whole strip shrinks at one scale

Status: ready-for-agent
Blocked by: none

## Context
Reported against ticket 08's landing sequence. On `/`, `frontend/app/components/home-intro.tsx` lays ten frames in one row on a 1000vh track. Only the opening frame is built to shrink:

- `FirstFrame` (line 247) starts at cover width — `max(100vw, aspect * 100dvh)` — with the height derived from the photo's ratio, then animates that width down to the shared frame width `62dvh * aspect` over progress 0.04–0.22.
- `Frame` (line 312) is laid out at its final size from the first paint and never scales.

So during the shrink you watch one full-screen photograph collapse next to nine thumbnails that are already at their end size. Two zoom levels are on screen at once, and the strip reads as photographs at different distances from the camera rather than as one strip moving away.

Wanted: at progress 0 the other frames are off-screen but as large as the opener, at the same scale; the row shrinks as a unit and ends exactly where it ends today.

## Scope
- Drive the row from one shared scale instead of per-frame sizing: `rendered height of frame i = 62dvh * s(t)`, `width = frameWidth_i * s(t)`, for every frame including the first. With `s(0) = coverWidth0 / (62dvh * aspect0)` frame 0 still reproduces today's full-bleed opener; with `s(1) = 1` the end state is unchanged.
- Keep frame 0's CRT reveal (clip and brightness) and its `marginLeft` inset cancellation; at full-bleed the opener still starts at `x = 0` and lands 10px in.
- Decide whether the 24px gap and the 10px gutter scale with the row. Scaling them is the smaller change and reads as the camera pulling back; pinning them means dividing the gap and the `boxShadow` spread by `s`. Either way the state at progress 0.22 must match today.
- Fix `sizes` while you are there. `FrameImage` is told `100vw` against a final width of `62dvh * aspect`; once the row starts ~1.7–2.6x larger the browser will pick a derivative that is too small at progress 0 and the opener will look soft. Frame 0's `100vw` is already right.
- The row must stay wide enough that at `s = 1` the last frame's right edge is flush with the viewport's right edge. Travel (`rowX`), `PARALLAX_PCT`, the name wipe and the header phase do not change — but `x` percentages resolve against the untransformed border box, so if the scale lives on the same element, re-check where travel ends.
- `StaticIntro` (reduced motion) is untouched.

## Done when
- At progress 0 only the opening photograph is visible, and it still covers the viewport at its own ratio.
- Sampling 0.04 → 0.22, frame 1's rendered height equals frame 0's at every sample: one scale, not ten.
- At progress 0.22 the composition matches today — 62dvh frame heights, 24px gaps, 10px gutters — and 0.22 → 1 lands the last frame flush right.
- No upscaling blur on the opener at 1440px wide at DPR 2.
- No horizontal scrollbar, no CLS, and the reduced-motion composition is unchanged.

## Comments

Implemented as one transform scale on the row rather than per-frame sizing, so the 24px gaps, 10px gutters and box-shadow spread shrink with the frames and the composition at 0.22 is byte-for-byte today's.

- `useIntroScale` measures the sticky stage and sets `s(0) = max(stageWidth, aspect0 * stageHeight) / (62dvh * aspect0)`, then interpolates to `1` across `SHRINK`. `FirstFrame` and `Frame` both lay out at `62dvh`/`62dvh * aspect`; only the opener keeps its `marginLeft` cancellation and CRT reveal.
- The row is hidden until the scale is measured. The server cannot know the viewport, and an unscaled first paint showed nine thumbnails beside the opener. At progress 0 the stage is blank by design (the CRT clip is collapsed), so nothing visible is delayed.
- `Frame`'s `sizes` is now `max(100vw, 62dvh * aspect)`; `100vw` under-described tall and panoramic frames once the row was larger than its final size.

Verified on the production build with fixtures at 1440x900 DPR2 and at a narrow window:

- 0.04 → 0.22: frame 0 and frame 1 have identical rendered heights at every sample (950px at 1440x900; 931.9 → 577.9 at the narrow window).
- 0.22: 558px (62dvh) frame heights, 24px gaps, 10px gutters, frame 0 left = 10px.
- 0: the opener is 1425×950 with top/bottom −25/925, so it covers the 900px stage; frame 1 starts beyond the right edge and frame 0 left = 0.
- 1: the last frame's right edge is 1440px; `scrollWidth == clientWidth`, so no horizontal scrollbar.
- The opener selects the 2048 AVIF at DPR2, and the console is clean apart from fixture-mode image requests (there is no image host in fixtures mode).
- The reduced-motion composition is unchanged; `StaticIntro` was not touched.
