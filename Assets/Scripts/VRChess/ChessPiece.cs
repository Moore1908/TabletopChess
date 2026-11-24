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

        // Make sure the piece can be grabbed in VR.
        if (GetComponentInChildren<Collider>() == null) AddBoxCollider();
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        grab = GetComponent<XRGrabInteractable>();
        if (grab == null) grab = gameObject.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.selectExited.AddListener(_ => game.OnPieceDropped(this));

        game.Register(this);
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
