using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VRChess
{
    /// <summary>
    /// Testing aid for when there is no headset or mouse: tap/click a piece to pick it up,
    /// then tap/click a square (or an enemy piece) to move there. Tap the piece again to
    /// put it back. Uses the same move checks as VR grabbing.
    /// Added automatically by ChessGame when "Desktop Click To Move" is ticked.
    /// </summary>
    public class DesktopTestInput : MonoBehaviour
    {
        public float liftHeight = 0.04f;

        ChessGame game;
        ChessPiece selected;

        [Tooltip("Without a headset, tilt the XR camera rig so it looks at the board.")]
        public bool aimCameraAtBoard = true;

        void Awake() => game = GetComponent<ChessGame>();

        System.Collections.IEnumerator Start()
        {
            // Wait a frame so the XR Origin has set up its camera height.
            yield return null;
            if (!aimCameraAtBoard || UnityEngine.XR.XRSettings.isDeviceActive) yield break;

            var cam = Camera.main;
            if (cam == null || cam.transform.parent == null) yield break;

            // Point the camera's parent ("Camera Offset") so the camera looks at the board.
            Vector3 boardCentre = (game.SquareToWorld(new Vector2Int(3, 3)) + game.SquareToWorld(new Vector2Int(4, 4))) * 0.5f;
            Vector3 target = boardCentre + Vector3.up * 0.08f + Vector3.forward * 0.05f;
            Vector3 dir = target - cam.transform.position;
            if (dir.sqrMagnitude < 1e-6f) yield break;

            Transform offset = cam.transform.parent;
            offset.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up)
                            * Quaternion.Inverse(cam.transform.localRotation);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 screenPos = mouse.position.ReadValue();
#else
            if (!Input.GetMouseButtonDown(0)) return;
            Vector2 screenPos = Input.mousePosition;
#endif
            var cam = Camera.main;
            if (cam == null) return;

            // If the selected piece was captured or reset, forget it.
            if (selected != null && (selected.Captured || !game.CanMove(selected))) Deselect();

            if (!Physics.Raycast(cam.ScreenPointToRay(screenPos), out RaycastHit hit, 10f)) return;
            var clickedPiece = hit.collider.GetComponentInParent<ChessPiece>();

            if (selected == null)
            {
                if (clickedPiece != null && game.CanMove(clickedPiece)) Select(clickedPiece);
                return;
            }

            if (clickedPiece == selected)
            {
                Deselect();
                return;
            }

            // Clicking one of your own pieces switches the selection.
            if (clickedPiece != null && clickedPiece.Side == selected.Side)
            {
                Deselect();
                if (game.CanMove(clickedPiece)) Select(clickedPiece);
                return;
            }

            // Otherwise, try to move to the clicked square (or to the enemy piece's square).
            Vector2Int target;
            if (clickedPiece != null) target = clickedPiece.Square;
            else if (!game.WorldToSquare(hit.point, out target)) return;

            var piece = selected;
            selected = null;
            piece.transform.position = game.RestPosition(piece, target);
            game.OnPieceReleased(piece); // checks legality, snaps back if illegal
        }

        void Select(ChessPiece p)
        {
            selected = p;
            p.transform.position = game.RestPosition(p, p.Square) + Vector3.up * liftHeight;
            game.OnPieceGrabbed(p); // shows the move highlights
        }

        void Deselect()
        {
            if (selected == null) return;
            var p = selected;
            selected = null;
            if (p != null && !p.Captured) game.OnPieceReleased(p); // dropped on its own square: snaps back
        }
    }
}
