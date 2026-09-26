# Video branch mixed mound

Scene: Assets/Scenes/MainScene.unity. Branch: video_tcg_3.

- 4,959 existing cards and 110 existing packs form one irregular mound near world (2.07, 0.0665, -1.7).
- Peak: approximately 2.13 m above the shop floor. Broad skirt, random headings, tilted overlapping layers; original item scales retained.
- Card identities, definitions, pack assignments and existing gameplay components are unchanged. No duplicate items were generated.
- Startup no longer invokes VideoShelfPreparation.FillHalfRoutine. New Game uses authored poses; Continue/Load intentionally uses the saved poses.
- No running mound controller, mass rigidbody simulation, or per-frame settling was added. The layout is authored in the scene.
- Editor menu: TCG Card Chaos > Video > Rebuild Mixed Card Mountain. Uses deterministic editor-only swept box placement with angular settling. Tilt is adjusted to lower each item onto its support instead of freezing its first corner contact; the dense centre compensates for the more compact layers. Undo is available; save the scene after intentional regeneration.

## Verification on 2026-09-26

- Unity 6000.0.80f1 compiled the editor tool and runtime startup change.
- Editor and Play Mode images inspected. Previews: Recordings/VideoMoundSupportPreview.png (close view), Recordings/VideoMoundPreview.png (recordings are not committed).
- Independent geometry check: 40,189 nearby oriented box pairs, zero penetrations; lowest collider point 0.06770 m; top 2.19699 m.
- Play startup retained 4,959 cards and 110 packs, with no automatic redistribution to shelves.
- All authored card positions stayed stationary over a four-second check.
- Card pickup/throw and pack pickup/throw passed. Thrown objects remained in the world.
- Save collector + JSON serialization/deserialization retained all items. This was a memory/temporary-file check, not an actual user-slot load cycle.
- Save manager writes were disabled only in the temporary test session; existing user save slots were not written. Test tooling has been removed from Assets.
- Game-only near-mound Editor measurement: Latest support-fix test: 7.7 FPS average over 10 seconds after warmup (prior layout test: 29.1 FPS). Editor conditions were not controlled between runs; this is not evidence of a runtime cost caused by the editor-only settling code. Low Editor performance remains a limitation. Not a standalone-build benchmark, long-duration performance test, or guarantee of 60 FPS recording.

Unity reserializes unrelated scene data when saving. The final diff retains only existing item transforms/parents and affected parent child lists, preserving unrelated scene components and serialized data.
