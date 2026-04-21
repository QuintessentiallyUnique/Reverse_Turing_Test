# Unity Level + HUD Upgrade Setup

## Quick build in scene

1. Open the scene `ReverseTuringTest`.
2. In Unity menu, click `Tools > Reverse Turing > Build HUD Canvas In Active Scene`.
3. The canvas and HUD elements are created (or updated) and auto-wired to `TuringGameManager`.
4. The old IMGUI overlay is disabled automatically (`showTurnOverlay = false`).

## 1) Turn on Canvas HUD (replace IMGUI overlay)

1. Create a Canvas in Screen Space - Overlay.
2. Add a panel anchored top-left for game state.
3. Add TMP text fields for:
   - Phase
   - Round
   - Turn label
   - Instruction
   - Identity
4. Add an Image for progress fill (set Image Type to Filled, Horizontal).
5. Add a CanvasGroup on the HUD root.
6. Add `TuringHudController` to the HUD root object.
7. Assign fields in the inspector:
   - Game Manager -> `TuringGameManager`
   - Canvas Group -> HUD root CanvasGroup
   - Text fields -> your TMP elements
   - Round Progress Fill -> progress image
8. On `TuringGameManager`, set `showTurnOverlay` to false to hide old IMGUI box.

## 2) Use the level layout guide

1. Create empty object: `ConversationLayoutGuide`.
2. Add `ArenaConversationLayoutGuide`.
3. Assign:
   - Center: arena center transform
   - Player Spawn: player start transform
   - Npc Anchors: one transform per NPC position
4. Tune values:
   - Npc Radius
   - Player Distance
   - Anchor Height
   - Face Center
5. In component context menu, run `Apply Circular Layout`.
6. Use Scene view gizmos to fine-tune composition and sight lines.

## 3) Fast quality pass checklist

- Keep all NPC faces visible from player spawn.
- Ensure nearest NPC is not clipping camera at normal FOV.
- Keep HUD instruction lines below 110 characters when possible.
- Verify important text remains readable at 1280x720 and 1920x1080.
- Test one full round and voting for pacing and camera comfort.
