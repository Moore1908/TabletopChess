using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King }
public enum Side { White, Black }

// Put this on every chess piece and set its type and side in the Inspector.
public class ChessPiece : MonoBehaviour
{
    public PieceType type;
    public Side side;

    [HideInInspector] public Vector2Int square;
    [HideInInspector] public bool hasMoved;
    [HideInInspector] public float height;           // keeps the piece's height above the board
    [HideInInspector] public Quaternion startRotation; // so it stands upright after being dropped

    ChessGame game;
    Rigidbody rb;
    XRGrabInteractable grab;

    void Start()
    {
        game = FindFirstObjectByType<ChessGame>();

        // Physics can only move a mesh collider if it is convex.
        // Without this, captured pieces fall through the board and floor.
        foreach (var mc in GetComponentsInChildren<MeshCollider>()) mc.convex = true;
        if (GetComponentInChildren<Collider>() == null) AddBoxCollider();

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // fast pieces don't pass through things

        // Make sure the piece can be grabbed in VR.
        grab = GetComponent<XRGrabInteractable>();
        if (grab == null) grab = gameObject.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.selectEntered.AddListener(_ => game.Play(game.pickupSound, transform.position, 0.6f));
        grab.selectExited.AddListener(_ => game.OnPieceDropped(this));

        // Runs after XR has moved the held piece, just before each frame is drawn.
        Application.onBeforeRender += KeepAboveBoard;

        game.Register(this);
    }

    void OnDestroy() => Application.onBeforeRender -= KeepAboveBoard;

    void LateUpdate() => KeepAboveBoard();

    // While held, don't let the piece sink into the board (vector math in the board's space).
    void KeepAboveBoard()
    {
        if (grab == null || !grab.isSelected || game == null) return;
        Vector3 local = game.transform.InverseTransformPoint(transform.position);
        if (local.y < height)
        {
            local.y = height;
            transform.position = game.transform.TransformPoint(local);
        }
    }

    // Captured: turn on physics and knock the piece off the board.
    public void KnockOff(Vector3 direction)
    {
        grab.enabled = false;
        rb.isKinematic = false;
        rb.AddForce(direction * 1.5f + Vector3.up * 2f, ForceMode.VelocityChange);
        rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
        Destroy(gameObject, 3f);
    }

    // Knock sound when a flying (captured) piece hits the table, floor or another piece.
    void OnCollisionEnter(Collision hit)
    {
        if (rb.isKinematic) return;
        float speed = hit.relativeVelocity.magnitude;
        if (speed > 0.3f) game.Play(game.hitSound, transform.position, Mathf.Clamp01(speed / 3f));
    }

    // Adds a box collider that fits the model, if the model doesn't have one.
    void AddBoxCollider()
    {
        var box = gameObject.AddComponent<BoxCollider>();
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        Vector3 s = transform.lossyScale;
        box.center = transform.InverseTransformPoint(b.center);
        box.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
    }
}
