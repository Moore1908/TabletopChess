# VR Chess (grey-box prototype)

A tabletop chess game for VR, built in Unity with the XR Interaction Toolkit.
You grab pieces, drop them on squares, and play against a simple AI or a second player.
**Goal: capture the opponent's King.**

## Setup (about 20 minutes)

1. **Project:** Unity 6 (or 2022.3 LTS), using the *3D (URP)* or *VR* template.
2. **Packages** (Window > Package Manager):
   - **XR Interaction Toolkit** (3.x). From its **Samples** tab, import **Starter Assets** and **XR Device Simulator** (on newer versions it may be called *XR Interaction Simulator*).
   - **XR Plug-in Management** (Edit > Project Settings > XR Plug-in Management): enable **OpenXR** on PC and Android. You need this later for the headset; the simulator works either way.
3. **Copy scripts:** put the `Assets/Scripts/VRChess` folder into your project's `Assets` folder.
4. **Scene:**
   - Delete the default Main Camera.
   - From *Samples > XR Interaction Toolkit > ... > Starter Assets > Prefabs*, drag in **XR Interaction Setup**. This includes the XR Origin and the Interaction Manager. Set its position to (0, 0, 0).
   - From the simulator sample's folder, drag in the **XR Device Simulator** prefab (or *XR Interaction Simulator*).
   - Create an empty GameObject named **ChessGame** at position **(0, 0.75, 0.55)** (the centre of the board's top surface), with rotation 0 and scale 1. Add the **Chess Game** component to it.
5. Press **Play**. The table, board, pieces, status panel and reset button are all generated at runtime.

> **XRI 2.x:** if Unity reports that `UnityEngine.XR.Interaction.Toolkit.Interactables` doesn't exist, delete that `using` line in `ChessPiece.cs` and `ChessGame.cs`.

## Using your own pieces and board

- **Pieces:** for each piece model, add the **Chess Piece** component and set **Type** and **Side**. Place it on its starting square; it only needs to be roughly in the middle of the square. When the game starts, every Chess Piece in the scene is snapped to its square and made grabbable in VR. A Rigidbody, an XR Grab Interactable and, if the model has none, a box collider are added automatically. Tip: set up one piece as a prefab and drag copies onto the board.
- White starts on the side nearest the player (ranks 1-2). A knight's or king's facing is kept from how you placed it.
- If the scene has no Chess Piece objects, grey-box pieces are spawned so you can still test.
- **Own board model:** untick **Generate Board**. Put the ChessGame object at the centre of your board's top surface and set **Square Size** to the width of one square in metres.

## Playing without a headset

The device simulator lets you control the head and hands with mouse and keyboard. Its on-screen panel shows the key bindings. Select a hand, move it over a piece and press **grip** to grab it. Release grip over a square to drop the piece.

- Legal squares light up **green**, captures **red**, the square the piece came from **yellow**, and the legal square under the held piece **blue**.
- An illegal drop sends the piece back to its square and shows "Illegal move!".
- **Reset:** grab the red RESET cube, or press **F5**.
- `Ai Plays Black` on the component switches between playing the AI and two players taking turns.

## Rules (simplified on purpose)

- Normal movement for all pieces, including the pawn's two-square first move and diagonal captures.
- Pawns reaching the last rank are promoted to Queens (shown by a small crown).
- "CHECK!" is shown as a warning, but moving into check is allowed. **You win by capturing the King**, or if the opponent has no moves left.
- Not implemented: castling and en passant.

## How it maps to the marking rubric

| Rubric item | Where it is |
|---|---|
| Goal and feedback | Capture the King. Square highlights, status panel, check warning, illegal-move message, win screen, victory bounce and explosion |
| Tutorial techniques (XRI) | `XRGrabInteractable` pieces, `XRSimpleInteractable` reset button, XR Origin and simulator |
| Finite State Machine | `GameStateMachine.cs`: `PlayerTurnState`, `AITurnState` and `GameOverState`, each with Enter, Tick and Exit |
| Vector math | Move directions in `MoveRules.cs`; world-to-square snapping in `WorldToSquare`; capture knock-back direction (`ProjectOnPlane`); AI move arc (Lerp + sine) |
| Rigidbody simulation | Captured pieces become dynamic and get knocked off the board; the losing side is blown away with `AddExplosionForce` |
| AI | Greedy one-move-ahead opponent in `AITurnRoutine` |

## Scripts

- `ChessGame.cs`: builds the scene and runs the game (moves, AI, feedback)
- `ChessPiece.cs`: grab and release handling, capture physics
- `MoveRules.cs`: legal move generation
- `GameStateMachine.cs`: the FSM and its states
- `PieceFactory.cs`: grey-box piece shapes made from primitives
- `ChessTypes.cs`: enums

## Credits

Code: written for this prototype. Assets: Unity primitives (grey box). Add credits for Kenney, Freesound and other assets here when you use them.
