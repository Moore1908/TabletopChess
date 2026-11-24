using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
// XR Interaction Toolkit 3.x (Unity 6). On XRI 2.x, delete the next two lines.
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRChess
{
    /// <summary>
    /// Put this on each of your chess piece models and set Type and Side in the Inspector.
    /// Place the piece on its starting square in the scene. When the game starts it is
    /// snapped to the nearest square and made grabbable in VR. A Rigidbody and an
    /// XRGrabInteractable are added automatically if missing, plus a box collider if the
    /// model has no collider.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChessPiece : MonoBehaviour
    {
        [SerializeField] PieceType type = PieceType.Pawn;
        [SerializeField] PieceColor side = PieceColor.White;

        public PieceType Type => type;
        public PieceColor Side => side;
        public Vector2Int Square { get; set; }
        public Vector2Int StartSquare { get; private set; }
        public bool HasMoved { get; set; }
        public bool Captured { get; private set; }

        /// <summary>Rotation relative to the board, kept from how you placed it.</summary>
        public Quaternion RotationOnBoard { get; private set; }
        /// <summary>Height of the pivot above the board surface, kept from how you placed it.</summary>
        public float HeightOffset { get; private set; }

        ChessGame game;
        Rigidbody rb;
        XRGrabInteractable grab;
        XRBaseInputInteractor lastHand;
        PieceType startType;
        Vector3 startScale;

        /// <summary>Used when the game spawns grey-box pieces from code.</summary>
        public void Configure(PieceType t, PieceColor c)
        {
            type = t;
            side = c;
        }

        public void Register(ChessGame owner, Vector2Int square, Quaternion rotationOnBoard, float heightOffset)
        {
            game = owner;
            Square = StartSquare = square;
            RotationOnBoard = rotationOnBoard;
            HeightOffset = heightOffset;
            startType = type;
            startScale = transform.localScale;
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (GetComponentInChildren<Collider>() == null) AddFittedBoxCollider();

            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // stays put on the board until captured
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            grab = GetComponent<XRGrabInteractable>();
            if (grab == null) grab = gameObject.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true; // hold the piece where your hand grabbed it
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        void AddFittedBoxCollider()
        {
            var box = gameObject.AddComponent<BoxCollider>();
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Vector3 s = transform.lossyScale;
            box.center = transform.InverseTransformPoint(b.center);
            box.size = new Vector3(b.size.x / Mathf.Abs(s.x), b.size.y / Mathf.Abs(s.y), b.size.z / Mathf.Abs(s.z));
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            lastHand = args.interactorObject as XRBaseInputInteractor;
            Buzz(0.2f, 0.05f);
            if (game != null) game.OnPieceGrabbed(this);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            if (!Captured && game != null) game.OnPieceReleased(this);
        }

        /// <summary>Controller vibration on the hand that last held this piece.</summary>
        public void Buzz(float amplitude, float duration)
        {
            if (lastHand != null) lastHand.SendHapticImpulse(amplitude, duration);
        }

        public void SetGrabbable(bool on)
        {
            if (grab != null && !Captured) grab.enabled = on;
        }

        /// <summary>Switch to physics and push in the given direction (velocity change).</summary>
        public void Capture(Vector3 push)
        {
            Captured = true;
            if (grab != null) grab.enabled = false;
            rb.isKinematic = false;
            rb.maxDepenetrationVelocity = 1f; // stop overlaps launching it into orbit
            rb.AddForce(push, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.VelocityChange);
            Invoke(nameof(Hide), 4f);
        }

        /// <summary>Victory explosion for the losing side.</summary>
        public void Blast(Vector3 centre, float force)
        {
            Captured = true;
            if (grab != null) grab.enabled = false;
            rb.isKinematic = false;
            rb.AddExplosionForce(force, centre, 1.5f, 0.6f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
            Invoke(nameof(Hide), 6f);
        }

        void Hide()
        {
            rb.isKinematic = true;
            gameObject.SetActive(false); // hidden, not destroyed, so Reset can bring it back
        }

        public void Promote()
        {
            type = PieceType.Queen;
            transform.localScale = startScale * 1.2f; // promoted pawns are bigger
        }

        public void ResetToStart()
        {
            CancelInvoke();
            gameObject.SetActive(true);
            Captured = false;
            if (rb != null) rb.isKinematic = true;
            if (grab != null) grab.enabled = false;
            type = startType;
            transform.localScale = startScale;
            Square = StartSquare;
            HasMoved = false;
        }
    }
}
