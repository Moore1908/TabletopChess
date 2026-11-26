using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.XR.CoreUtils;

// Put this on an empty object at the centre of the board's top surface.
// White starts on the side nearest the player. Capture the King to win.
public class ChessGame : MonoBehaviour
{
    public float squareSize = 0.07f;   // width of one square in metres
    public TMP_Text statusText;         // optional: a TextMeshPro text to show messages
    // Sounds: if left empty, they load automatically from Assets/Resources/ChessAudio.
    public AudioClip pickupSound, moveSound, captureSound, illegalSound, hitSound, winSound;

    [Header("Piece colours")]
    public Color whiteColour = new Color(0.92f, 0.92f, 0.9f);
    public Color blackColour = new Color(0.1f, 0.1f, 0.12f);

    [Header("Player view")]
    public XROrigin playerRig;           // found automatically if left empty
    public float viewDistance = 0.45f;   // metres from the board centre to the player
    public bool switchSidesEachTurn = true;

    Material whiteMaterial, blackMaterial;

    // Finite State Machine: whose turn it is, or game over.
    enum State { WhiteTurn, BlackTurn, GameOver }
    State state;

    ChessPiece[,] board = new ChessPiece[8, 8];

    void Awake()
    {
        if (pickupSound == null)  pickupSound  = LoadSound("pickup");
        if (moveSound == null)    moveSound    = LoadSound("move");
        if (captureSound == null) captureSound = LoadSound("capture");
        if (illegalSound == null) illegalSound = LoadSound("illegal");
        if (hitSound == null)     hitSound     = LoadSound("hit");
        if (winSound == null)     winSound     = LoadSound("win");

        MakeMaterials();
        if (playerRig == null) playerRig = FindFirstObjectByType<XROrigin>();
    }

    void Start() => ChangeState(State.WhiteTurn);

    void ChangeState(State next)
    {
        state = next;
        if (state == State.WhiteTurn) { Say("White's turn"); MovePlayerTo(Side.White); }
        if (state == State.BlackTurn) { Say("Black's turn"); MovePlayerTo(Side.Black); }
    }

    // ---- player view ----

    // Stand the player on their own side of the board, facing it.
    void MovePlayerTo(Side side)
    {
        if (playerRig == null) return;
        if (side == Side.Black && !switchSidesEachTurn) return;

        // Vector math: a point behind the board edge on this side, on the floor.
        Vector3 towardsPlayer = side == Side.White ? -transform.forward : transform.forward;
        Vector3 spot = transform.position + towardsPlayer * viewDistance;
        spot.y = playerRig.transform.position.y;

        playerRig.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(-towardsPlayer, Vector3.up));

        // No headset (testing in the editor): tilt the view down so the board is in sight.
        if (!UnityEngine.XR.XRSettings.isDeviceActive && playerRig.CameraFloorOffsetObject != null)
        {
            Transform offset = playerRig.CameraFloorOffsetObject.transform;
            Vector3 eye = playerRig.Camera.transform.position;
            Vector3 toBoard = transform.position - eye;
            float pitch = Mathf.Atan2(-toBoard.y, new Vector2(toBoard.x, toBoard.z).magnitude) * Mathf.Rad2Deg;
            offset.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }

    // ---- piece colours ----

    // The chess pack's material uses an old shader that doesn't work in URP,
    // so each side gets a plain URP material in its colour.
    void MakeMaterials()
    {
        var probe = GameObject.CreatePrimitive(PrimitiveType.Cube); // its default material is URP Lit
        Material baseMat = probe.GetComponent<Renderer>().sharedMaterial;
        if (Application.isPlaying) Destroy(probe); else DestroyImmediate(probe);

        whiteMaterial = new Material(baseMat) { name = "White Piece", color = whiteColour };
        blackMaterial = new Material(baseMat) { name = "Black Piece", color = blackColour };
    }

    public void ColourPiece(ChessPiece p)
    {
        Material m = p.side == Side.White ? whiteMaterial : blackMaterial;
        foreach (var r in p.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    // Use from the ⋮ menu on this component to see the colours in the Scene view without pressing Play.
    [ContextMenu("Apply Piece Colours In Scene")]
    void ApplyColoursInScene()
    {
        MakeMaterials();
        foreach (var p in FindObjectsByType<ChessPiece>(FindObjectsSortMode.None))
        {
#if UNITY_EDITOR
            foreach (var r in p.GetComponentsInChildren<Renderer>())
                UnityEditor.Undo.RecordObject(r, "Apply Piece Colours");
#endif
            ColourPiece(p);
        }
    }

    // Called by each piece when the game starts.
    public void Register(ChessPiece p)
    {
        p.square = WorldToSquare(p.transform.position);
        if (!OnBoard(p.square)) { Debug.LogWarning(p.name + " is not on the board"); return; }

        p.height = transform.InverseTransformPoint(p.transform.position).y;
        p.startRotation = p.transform.rotation;
        board[p.square.x, p.square.y] = p;
        Snap(p);
    }

    // Called by a piece when the player lets go of it.
    public void OnPieceDropped(ChessPiece p)
    {
        Vector2Int target = WorldToSquare(p.transform.position);
        Side turn = state == State.WhiteTurn ? Side.White : Side.Black;

        if (state != State.GameOver && p.side == turn && IsLegal(p, target))
        {
            MovePiece(p, target);
        }
        else
        {
            Snap(p); // put it back where it was
            if (state != State.GameOver && target != p.square)
            {
                Say("Illegal move! " + turn + "'s turn");
                Play(illegalSound, p.transform.position);
            }
            else Play(moveSound, p.transform.position, 0.5f);
        }
    }

    void MovePiece(ChessPiece p, Vector2Int to)
    {
        ChessPiece victim = board[to.x, to.y];
        if (victim != null)
        {
            // Vector math: knock the captured piece away from the attacker.
            Vector3 direction = victim.transform.position - SquareToWorld(p.square);
            direction.y = 0;
            victim.KnockOff(direction.normalized);
            Play(captureSound, victim.transform.position);
        }
        else Play(moveSound, SquareToWorld(to));

        board[p.square.x, p.square.y] = null;
        board[to.x, to.y] = p;
        p.square = to;
        p.hasMoved = true;
        Snap(p);

        if (p.type == PieceType.Pawn && (to.y == 0 || to.y == 7)) p.type = PieceType.Queen; // promotion

        if (victim != null && victim.type == PieceType.King)
        {
            state = State.GameOver;
            Say(p.side + " wins!");
            Play(winSound, transform.position);
        }
        else ChangeState(p.side == Side.White ? State.BlackTurn : State.WhiteTurn);
    }

    bool IsLegal(ChessPiece p, Vector2Int to)
    {
        if (!OnBoard(to) || to == p.square) return false;
        ChessPiece target = board[to.x, to.y];
        if (target != null && target.side == p.side) return false; // can't take your own piece

        Vector2Int d = to - p.square; // the move as a vector
        int ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);

        switch (p.type)
        {
            case PieceType.King:   return ax <= 1 && ay <= 1;
            case PieceType.Knight: return ax * ay == 2; // L-shape: 1x2 or 2x1
            case PieceType.Rook:   return (d.x == 0 || d.y == 0) && PathClear(p.square, to);
            case PieceType.Bishop: return ax == ay && PathClear(p.square, to);
            case PieceType.Queen:  return (d.x == 0 || d.y == 0 || ax == ay) && PathClear(p.square, to);
            case PieceType.Pawn:
                int forward = p.side == Side.White ? 1 : -1;
                if (target != null) return ax == 1 && d.y == forward; // capture diagonally
                if (d.x != 0) return false;
                if (d.y == forward) return true;
                return d.y == 2 * forward && !p.hasMoved && board[p.square.x, p.square.y + forward] == null;
        }
        return false;
    }

    // Steps one square at a time towards the target, checking nothing is in the way.
    bool PathClear(Vector2Int from, Vector2Int to)
    {
        Vector2Int step = new Vector2Int(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y));
        for (Vector2Int s = from + step; s != to; s += step)
            if (board[s.x, s.y] != null) return false;
        return true;
    }

    // ---- converting between world positions and board squares ----

    Vector2Int WorldToSquare(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        return new Vector2Int(Mathf.RoundToInt(local.x / squareSize + 3.5f),
                              Mathf.RoundToInt(local.z / squareSize + 3.5f));
    }

    Vector3 SquareToWorld(Vector2Int s) =>
        transform.TransformPoint(new Vector3((s.x - 3.5f) * squareSize, 0, (s.y - 3.5f) * squareSize));

    bool OnBoard(Vector2Int s) => s.x >= 0 && s.x < 8 && s.y >= 0 && s.y < 8;

    void Snap(ChessPiece p)
    {
        p.transform.position = SquareToWorld(p.square) + transform.up * p.height;
        p.transform.rotation = p.startRotation;
    }

    // ---- feedback ----

    void Say(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }

    // Looks in Assets/Resources/ChessAudio (or a ChessAudio folder inside it).
    AudioClip LoadSound(string name)
    {
        AudioClip clip = Resources.Load<AudioClip>("ChessAudio/" + name);
        if (clip == null) clip = Resources.Load<AudioClip>("ChessAudio/ChessAudio/" + name);
        if (clip == null) Debug.LogWarning("Sound not found: " + name + ".wav in Assets/Resources/ChessAudio");
        return clip;
    }

    // 3D sound at a position, so in VR you hear it from where it happened.
    public void Play(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    // Hook this up to a VR button to start again.
    public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
