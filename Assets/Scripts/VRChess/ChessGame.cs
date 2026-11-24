using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
// XR Interaction Toolkit 3.x (Unity 6). On XRI 2.x, delete the next line.
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRChess
{
    /// <summary>
    /// Runs a VR chess game.
    /// Put this GameObject at the CENTRE OF THE BOARD'S TOP SURFACE (e.g. (0, 0.75, 0.55)).
    /// Your own pieces are any objects in the scene with a ChessPiece component, placed on
    /// their starting squares. If there are none, grey-box pieces are spawned for testing.
    /// </summary>
    public class ChessGame : MonoBehaviour
    {
        [Header("Board")]
        [Tooltip("Size of one square in metres. Match this to your board model.")]
        public float squareSize = 0.07f;
        [Tooltip("Build a grey-box board. Untick when you use your own board model.")]
        public bool generateBoard = true;
        [Tooltip("Build a grey-box table and floor under the board.")]
        public bool generateTableAndFloor = true;

        [Header("Pieces")]
        [Tooltip("If the scene has no ChessPiece objects, spawn grey-box pieces for testing.")]
        public bool spawnGreyBoxIfNoPieces = true;

        [Header("Opponent")]
        public bool aiPlaysBlack = true;
        public float aiThinkTime = 0.8f;
        public float aiMoveDuration = 0.6f;

        [Header("Testing")]
        [Tooltip("Tap/click a piece, then a square, to move without a headset.")]
        public bool desktopClickToMove = true;

        [Header("Colours")]
        public Color lightSquare = new Color(0.82f, 0.82f, 0.82f);
        public Color darkSquare = new Color(0.35f, 0.35f, 0.35f);
        public Color whitePiece = new Color(0.95f, 0.95f, 0.95f);
        public Color blackPiece = new Color(0.12f, 0.12f, 0.12f);
        public Color moveHighlight = new Color(0.3f, 0.85f, 0.4f);
        public Color captureHighlight = new Color(0.9f, 0.3f, 0.3f);
        public Color selectedHighlight = new Color(0.95f, 0.85f, 0.3f);
        public Color hoverHighlight = new Color(0.4f, 0.7f, 1f);

        [Header("Audio (optional)")]
        public AudioClip moveClip;
        public AudioClip captureClip;
        public AudioClip invalidClip;
        public AudioClip winClip;

        public PieceColor CurrentTurn { get; set; }
        public float SquareSize => squareSize;

        readonly GameStateMachine fsm = new GameStateMachine();
        readonly ChessPiece[,] grid = new ChessPiece[8, 8];
        readonly List<ChessPiece> allPieces = new List<ChessPiece>(); // every registered piece
        readonly List<ChessPiece> pieces = new List<ChessPiece>();    // pieces still in play
        readonly Renderer[,] highlightTiles = new Renderer[8, 8];

        Transform boardRoot;
        TextMesh statusText;
        string statusBase = "";
        Color statusBaseColor = Color.white;
        Coroutine flashRoutine;
        bool isResetting;
        PieceColor? movableSide;

        Material matLight, matDark, matWhite, matBlack, matMove, matCapture, matSelected, matHover;
        Material matTable, matFloor, matPanel, matReset;

        ChessPiece heldPiece;
        readonly List<Vector2Int> heldMoves = new List<Vector2Int>();
        Vector2Int? hoverSquare;

        static readonly PieceType[] BackRank =
        {
            PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
            PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
        };

        // ------------------------------------------------------------------ setup

        void Start()
        {
            boardRoot = transform;
            CreateMaterials();

            BuildEnvironment();
            BuildBoard();
            BuildHighlightTiles();
            BuildStatusText();
            BuildResetButton();
            RegisterPieces();

            if (desktopClickToMove && GetComponent<DesktopTestInput>() == null)
                gameObject.AddComponent<DesktopTestInput>();

            StartNewGame();
        }

        void CreateMaterials()
        {
            // Copy the default material of a primitive, so this works in URP and Built-in.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var baseMat = probe.GetComponent<Renderer>().sharedMaterial;
            if (Application.isPlaying) Destroy(probe); else DestroyImmediate(probe);

            Material Make(Color c) => new Material(baseMat) { color = c };

            matLight = Make(lightSquare);
            matDark = Make(darkSquare);
            matWhite = Make(whitePiece);
            matBlack = Make(blackPiece);
            matMove = Make(moveHighlight);
            matCapture = Make(captureHighlight);
            matSelected = Make(selectedHighlight);
            matHover = Make(hoverHighlight);
            matTable = Make(new Color(0.55f, 0.55f, 0.58f));
            matFloor = Make(new Color(0.45f, 0.45f, 0.45f));
            matPanel = Make(new Color(0.15f, 0.15f, 0.18f));
            matReset = Make(new Color(0.85f, 0.2f, 0.2f));
        }

        List<GameObject> BuildEnvironment()
        {
            var created = new List<GameObject>();
            if (!generateTableAndFloor) return created;

            if (transform.position.y < 0.3f)
            {
                Debug.LogWarning("[Chess] ChessGame should sit at table height (board surface). " +
                                 "Raising it to Y = 0.75. Set this in the Inspector to stop this message.", this);
                transform.position = new Vector3(transform.position.x, 0.75f, transform.position.z);
            }

            Vector3 p = transform.position;
            Quaternion yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            float tableTop = p.y - 0.032f;

            var floor = PieceFactory.Part(null, PrimitiveType.Plane, new Vector3(p.x, 0f, p.z), new Vector3(2f, 1f, 2f), matFloor);
            floor.name = "Floor";
            floor.transform.rotation = yaw;

            var table = PieceFactory.Part(null, PrimitiveType.Cube, new Vector3(p.x, tableTop / 2f, p.z),
                                          new Vector3(10f * squareSize + 0.3f, tableTop, 10f * squareSize), matTable);
            table.name = "Table";
            table.transform.rotation = yaw;

            created.Add(floor);
            created.Add(table);
            return created;
        }

        GameObject BuildBoardObjects()
        {
            var board = new GameObject("Board");
            board.transform.SetParent(boardRoot, false);

            PieceFactory.Part(board.transform, PrimitiveType.Cube, new Vector3(0, -0.017f, 0),
                              new Vector3(8.6f * squareSize, 0.03f, 8.6f * squareSize), matPanel).name = "Frame";

            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var local = SquareToLocal(new Vector2Int(x, y));
                local.y = -0.005f; // square top surface sits at y = 0
                var sq = PieceFactory.Part(board.transform, PrimitiveType.Cube, local,
                                           new Vector3(squareSize, 0.01f, squareSize),
                                           (x + y) % 2 == 0 ? matDark : matLight);
                sq.name = $"Square {SquareName(new Vector2Int(x, y))}";
            }
            return board;
        }

        void BuildBoard()
        {
            if (generateBoard)
            {
                BuildBoardObjects();
            }
            else
            {
                // Invisible surface so the desktop test mode can click squares on your own board.
                var surface = new GameObject("Board Click Surface");
                surface.transform.SetParent(boardRoot, false);
                var box = surface.AddComponent<BoxCollider>();
                box.center = new Vector3(0, -0.005f, 0);
                box.size = new Vector3(8f * squareSize, 0.01f, 8f * squareSize);
            }
        }

        /// <summary>Thin coloured tiles just above each square, shown only when highlighted.</summary>
        void BuildHighlightTiles()
        {
            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var local = SquareToLocal(new Vector2Int(x, y));
                local.y = 0.0015f;
                var tile = PieceFactory.Part(boardRoot, PrimitiveType.Cube, local,
                                             new Vector3(0.9f * squareSize, 0.003f, 0.9f * squareSize), matMove);
                tile.name = "Highlight";
                Destroy(tile.GetComponent<Collider>());
                var r = tile.GetComponent<Renderer>();
                r.enabled = false;
                highlightTiles[x, y] = r;
            }
        }

        void BuildStatusText()
        {
            float z = 4.8f * squareSize;
            PieceFactory.Part(boardRoot, PrimitiveType.Cube, new Vector3(0, 0.32f, z + 0.01f),
                              new Vector3(0.7f, 0.2f, 0.01f), matPanel).name = "Status Panel";

            statusText = CreateText("Status Text", boardRoot, new Vector3(0, 0.32f, z - 0.002f), 0.005f);
        }

        void BuildResetButton()
        {
            var pos = new Vector3(5.3f * squareSize, 0.02f, -3f * squareSize);
            var button = PieceFactory.Part(boardRoot, PrimitiveType.Cube, pos, new Vector3(0.08f, 0.04f, 0.08f), matReset);
            button.name = "Reset Button";
            var interactable = button.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => StartNewGame());

            var label = CreateText("Reset Label", boardRoot, pos + new Vector3(0, 0.07f, 0), 0.003f);
            label.text = "RESET";
        }

        TextMesh CreateText(string name, Transform parent, Vector3 localPos, float charSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.color = Color.white;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return tm;
        }

        // ------------------------------------------------------------------ pieces

        void RegisterPieces()
        {
            var found = FindObjectsByType<ChessPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (found.Length == 0)
            {
                if (spawnGreyBoxIfNoPieces) SpawnGreyBoxSet();
                else Debug.LogWarning("[Chess] No ChessPiece objects found in the scene.", this);
                return;
            }

            var taken = new HashSet<Vector2Int>();
            foreach (var p in found)
            {
                if (!WorldToSquare(p.transform.position, out Vector2Int sq))
                {
                    Debug.LogWarning($"[Chess] '{p.name}' is not on the board, so it was ignored. Move it onto a square.", p);
                    continue;
                }
                if (!taken.Add(sq))
                {
                    Debug.LogWarning($"[Chess] '{p.name}' is on {SquareName(sq)}, which already has a piece, so it was ignored.", p);
                    continue;
                }
                RegisterPiece(p, sq);
            }

            foreach (PieceColor side in new[] { PieceColor.White, PieceColor.Black })
            {
                int kings = allPieces.FindAll(p => p.Side == side && p.Type == PieceType.King).Count;
                if (kings != 1)
                    Debug.LogWarning($"[Chess] {side} has {kings} kings. The game is won by capturing the King, so each side needs exactly one.", this);
            }
            Debug.Log($"[Chess] Registered {allPieces.Count} pieces from the scene.");
        }

        void RegisterPiece(ChessPiece p, Vector2Int sq)
        {
            float height = Mathf.Max(0f, boardRoot.InverseTransformPoint(p.transform.position).y);
            Quaternion rotationOnBoard = Quaternion.Inverse(boardRoot.rotation) * p.transform.rotation;
            p.Register(this, sq, rotationOnBoard, height);
            allPieces.Add(p);
        }

        void SpawnGreyBoxSet()
        {
            var parent = new GameObject("Grey-box Pieces").transform;
            parent.SetParent(boardRoot, false);

            for (int x = 0; x < 8; x++)
            {
                SpawnGreyBox(parent, BackRank[x], PieceColor.White, new Vector2Int(x, 0));
                SpawnGreyBox(parent, PieceType.Pawn, PieceColor.White, new Vector2Int(x, 1));
                SpawnGreyBox(parent, PieceType.Pawn, PieceColor.Black, new Vector2Int(x, 6));
                SpawnGreyBox(parent, BackRank[x], PieceColor.Black, new Vector2Int(x, 7));
            }
        }

        void SpawnGreyBox(Transform parent, PieceType type, PieceColor side, Vector2Int sq)
        {
            RegisterPiece(CreateGreyBoxPiece(parent, type, side, sq), sq);
        }

        ChessPiece CreateGreyBoxPiece(Transform parent, PieceType type, PieceColor side, Vector2Int sq)
        {
            var go = new GameObject($"{side} {type} {SquareName(sq)}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(SquareToWorld(sq), boardRoot.rotation);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            if (side == PieceColor.Black) visual.localRotation = Quaternion.Euler(0, 180f, 0);
            PieceFactory.Build(visual, type, squareSize, side == PieceColor.White ? matWhite : matBlack);

            var piece = go.AddComponent<ChessPiece>();
            piece.Configure(type, side);
            return piece;
        }

        // ------------------------------------------------------------------ editor tools
        // Use these from the ⋮ menu on the Chess Game component (no need to press Play).

        [ContextMenu("Build Board In Scene")]
        void BuildBoardInScene()
        {
            boardRoot = transform;
#if UNITY_EDITOR
            Undo.RecordObject(this, "Build Chess Board");
            Undo.RecordObject(transform, "Build Chess Board");
#endif
            CreateMaterials();
            var created = BuildEnvironment();
            created.Add(BuildBoardObjects());

            // They now exist in the scene, so don't build them again at runtime.
            generateBoard = false;
            generateTableAndFloor = false;
#if UNITY_EDITOR
            foreach (var go in created) Undo.RegisterCreatedObjectUndo(go, "Build Chess Board");
            EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Spawn Grey-box Pieces In Scene")]
        void SpawnPiecesInScene()
        {
            if (FindObjectsByType<ChessPiece>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
            {
                Debug.LogWarning("[Chess] The scene already has Chess Pieces. Delete them first if you want a fresh set.", this);
                return;
            }

            boardRoot = transform;
            CreateMaterials();
            var parent = new GameObject("Pieces").transform;
            parent.SetParent(transform, false);

            for (int x = 0; x < 8; x++)
            {
                CreateGreyBoxPiece(parent, BackRank[x], PieceColor.White, new Vector2Int(x, 0));
                CreateGreyBoxPiece(parent, PieceType.Pawn, PieceColor.White, new Vector2Int(x, 1));
                CreateGreyBoxPiece(parent, PieceType.Pawn, PieceColor.Black, new Vector2Int(x, 6));
                CreateGreyBoxPiece(parent, BackRank[x], PieceColor.Black, new Vector2Int(x, 7));
            }
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(parent.gameObject, "Spawn Chess Pieces");
#endif
        }

        /// <summary>Draws the 8x8 grid in the Scene view, even when not playing.</summary>
        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            bool hasBoard = transform.Find("Board") != null;

            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                Vector3 c = SquareToLocal(new Vector2Int(x, y));
                if (!hasBoard)
                {
                    Gizmos.color = (x + y) % 2 == 0 ? new Color(0.25f, 0.25f, 0.25f, 0.7f)
                                                    : new Color(0.9f, 0.9f, 0.9f, 0.7f);
                    Gizmos.DrawCube(c, new Vector3(squareSize, 0.001f, squareSize));
                }
                Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
                Gizmos.DrawWireCube(c + Vector3.up * 0.002f, new Vector3(squareSize, 0f, squareSize));
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(8f * squareSize, 0.002f, 8f * squareSize));
            Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
            Handles.color = Color.white;
            Handles.Label(transform.TransformPoint(new Vector3(0, 0.02f, -4.8f * squareSize)), "WHITE (player side)");
            Handles.Label(transform.TransformPoint(new Vector3(0, 0.02f, 4.8f * squareSize)), "BLACK");
            for (int x = 0; x < 8; x++)
                Handles.Label(transform.TransformPoint(SquareToLocal(new Vector2Int(x, 0)) + new Vector3(0, 0.01f, -0.6f * squareSize)),
                              ((char)('a' + x)).ToString());
            for (int y = 0; y < 8; y++)
                Handles.Label(transform.TransformPoint(SquareToLocal(new Vector2Int(0, y)) + new Vector3(-0.6f * squareSize, 0.01f, 0)),
                              (y + 1).ToString());
#endif
        }

        // ------------------------------------------------------------------ game flow

        public void StartNewGame()
        {
            StopAllCoroutines();
            flashRoutine = null;
            heldPiece = null;
            heldMoves.Clear();
            hoverSquare = null;

            isResetting = true;
            foreach (var p in allPieces)
                if (p != null) p.ResetToStart();
            isResetting = false;

            pieces.Clear();
            System.Array.Clear(grid, 0, grid.Length);
            foreach (var p in allPieces)
            {
                if (p == null) continue;
                pieces.Add(p);
                grid[p.Square.x, p.Square.y] = p;
                SnapToSquare(p, p.Square);
            }

            RefreshHighlights();
            fsm.ChangeState(new PlayerTurnState(this, PieceColor.White));
        }

        void Update()
        {
            fsm.Tick();

            // Live hover highlight while a piece is held
            if (heldPiece != null)
            {
                Vector2Int? h = WorldToSquare(heldPiece.transform.position, out var s) ? s : (Vector2Int?)null;
                if (h != hoverSquare)
                {
                    hoverSquare = h;
                    RefreshHighlights();
                }
            }

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame) StartNewGame();
#endif
        }

        public void OnPieceGrabbed(ChessPiece p)
        {
            if (isResetting || !pieces.Contains(p)) return;
            heldPiece = p;
            heldMoves.Clear();
            heldMoves.AddRange(MoveRules.GetMoves(grid, p));
            RefreshHighlights();
        }

        public void OnPieceReleased(ChessPiece p)
        {
            if (isResetting || !pieces.Contains(p)) return;

            heldPiece = null;
            hoverSquare = null;
            heldMoves.Clear();
            RefreshHighlights();

            bool onBoard = WorldToSquare(p.transform.position, out Vector2Int target);
            bool movedAway = !onBoard || target != p.Square;

            if (onBoard && movedAway && p.Side == CurrentTurn && MoveRules.GetMoves(grid, p).Contains(target))
            {
                p.Buzz(0.3f, 0.08f);
                ExecuteMove(p, target);
            }
            else
            {
                SnapToSquare(p, p.Square);
                if (movedAway)
                {
                    p.Buzz(0.8f, 0.25f);
                    Play(invalidClip, p.transform.position);
                    Flash("Illegal move!", new Color(1f, 0.4f, 0.4f));
                }
            }
        }

        void ExecuteMove(ChessPiece p, Vector2Int to)
        {
            Vector2Int from = p.Square;
            Vector3 fromWorld = SquareToWorld(from);
            Vector3 toWorld = SquareToWorld(to);
            bool kingTaken = false;

            var victim = grid[to.x, to.y];
            if (victim != null)
            {
                // Vector math: knock the captured piece along the attack direction, plus up.
                Vector3 dir = Vector3.ProjectOnPlane(toWorld - fromWorld, boardRoot.up);
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : boardRoot.forward;
                victim.Capture(dir * 1.2f + boardRoot.up * 1.5f);

                pieces.Remove(victim);
                kingTaken = victim.Type == PieceType.King;
                Play(captureClip, toWorld);
            }
            else
            {
                Play(moveClip, toWorld);
            }

            grid[from.x, from.y] = null;
            grid[to.x, to.y] = p;
            p.Square = to;
            p.HasMoved = true;
            SnapToSquare(p, to);

            if (p.Type == PieceType.Pawn && (to.y == 7 || to.y == 0))
            {
                p.Promote();
                Flash("Pawn promoted to Queen!", new Color(0.6f, 0.9f, 1f));
            }

            EndTurn(p.Side, kingTaken);
        }

        void EndTurn(PieceColor mover, bool kingTaken)
        {
            if (kingTaken)
            {
                fsm.ChangeState(new GameOverState(this, mover, "The King has fallen!"));
                return;
            }

            PieceColor next = mover.Opposite();
            if (!HasAnyMove(next))
            {
                fsm.ChangeState(new GameOverState(this, mover, $"{next} has no moves left"));
                return;
            }

            if (next == PieceColor.Black && aiPlaysBlack)
                fsm.ChangeState(new AITurnState(this, next));
            else
                fsm.ChangeState(new PlayerTurnState(this, next));
        }

        // ------------------------------------------------------------------ called by states / input

        /// <summary>True if this piece belongs to the side currently allowed to move.</summary>
        public bool CanMove(ChessPiece p) =>
            p != null && !p.Captured && pieces.Contains(p) && movableSide.HasValue && p.Side == movableSide.Value;

        public void SetGrabbableFor(PieceColor? side)
        {
            movableSide = side;
            foreach (var p in pieces)
                if (p != null) p.SetGrabbable(side.HasValue && p.Side == side.Value);
        }

        public void SetStatus(string text, Color color)
        {
            statusBase = text;
            statusBaseColor = color;
            if (flashRoutine == null && statusText != null)
            {
                statusText.text = text;
                statusText.color = color;
            }
        }

        public void StartAITurn(PieceColor side) => StartCoroutine(AITurnRoutine(side));

        public void OnGameOver(PieceColor winner, string reason)
        {
            string headline = aiPlaysBlack
                ? (winner == PieceColor.White ? "YOU WIN!" : "YOU LOSE!")
                : $"{winner.ToString().ToUpper()} WINS!";
            SetStatus($"{headline}\n{reason}\nPress RESET to play again", new Color(0.5f, 1f, 0.5f));
            Play(winClip, boardRoot.position);
            StartCoroutine(Celebrate(winner));
        }

        // ------------------------------------------------------------------ rules helpers

        public bool IsSquareAttacked(Vector2Int sq, PieceColor by)
        {
            foreach (var e in pieces)
            {
                if (e == null || e.Side != by || grid[e.Square.x, e.Square.y] != e) continue;
                if (MoveRules.GetMoves(grid, e).Contains(sq)) return true;
            }
            return false;
        }

        public bool IsInCheck(PieceColor side)
        {
            var king = pieces.Find(p => p != null && p.Side == side && p.Type == PieceType.King);
            return king != null && IsSquareAttacked(king.Square, side.Opposite());
        }

        bool HasAnyMove(PieceColor side)
        {
            foreach (var p in pieces)
                if (p != null && p.Side == side && MoveRules.GetMoves(grid, p).Count > 0) return true;
            return false;
        }

        static int Value(PieceType t)
        {
            switch (t)
            {
                case PieceType.Pawn: return 1;
                case PieceType.Knight:
                case PieceType.Bishop: return 3;
                case PieceType.Rook: return 5;
                case PieceType.Queen: return 9;
                default: return 100; // King
            }
        }

        // ------------------------------------------------------------------ AI

        /// <summary>
        /// Greedy opponent: prefers the most valuable capture and avoids moving onto
        /// attacked squares (looks one move ahead). Adds a little randomness.
        /// </summary>
        IEnumerator AITurnRoutine(PieceColor side)
        {
            yield return new WaitForSeconds(aiThinkTime);

            ChessPiece bestPiece = null;
            Vector2Int bestTarget = default;
            int bestScore = int.MinValue;

            foreach (var p in pieces.ToArray())
            {
                if (p == null || p.Side != side) continue;
                Vector2Int from = p.Square;

                foreach (var t in MoveRules.GetMoves(grid, p))
                {
                    var victim = grid[t.x, t.y];
                    int score = victim != null ? Value(victim.Type) * 10 : 0;

                    // Try the move and see if the piece would be attacked.
                    grid[from.x, from.y] = null;
                    grid[t.x, t.y] = p;
                    p.Square = t;
                    bool unsafeSquare = IsSquareAttacked(t, side.Opposite());
                    p.Square = from;
                    grid[from.x, from.y] = p;
                    grid[t.x, t.y] = victim;

                    if (unsafeSquare) score -= Value(p.Type) * 9;
                    score += Random.Range(0, 4);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPiece = p;
                        bestTarget = t;
                    }
                }
            }

            if (bestPiece == null) yield break; // EndTurn already handles "no moves"

            // Animate along an arc: Lerp across the board plus a sine-wave lift.
            Vector3 start = bestPiece.transform.position;
            Vector3 end = RestPosition(bestPiece, bestTarget);
            float t01 = 0f;
            while (t01 < 1f)
            {
                t01 = Mathf.Min(1f, t01 + Time.deltaTime / aiMoveDuration);
                float e = Mathf.SmoothStep(0f, 1f, t01);
                bestPiece.transform.position = Vector3.Lerp(start, end, e)
                                             + boardRoot.up * (Mathf.Sin(e * Mathf.PI) * squareSize * 1.2f);
                yield return null;
            }

            ExecuteMove(bestPiece, bestTarget);
        }

        // ------------------------------------------------------------------ feedback

        IEnumerator Celebrate(PieceColor winner)
        {
            yield return new WaitForSeconds(0.5f);

            // The losing side gets blown off the table (rigidbody physics).
            Vector3 centre = boardRoot.position - boardRoot.up * 0.1f;
            foreach (var p in pieces.ToArray())
                if (p != null && p.Side != winner) p.Blast(centre, 2.5f);

            // The winners do a victory bounce.
            var winners = pieces.FindAll(p => p != null && p.Side == winner && !p.Captured);
            float time = 0f;
            while (time < 2.5f)
            {
                time += Time.deltaTime;
                float h = Mathf.Abs(Mathf.Sin(time * Mathf.PI * 2f)) * squareSize * 0.5f;
                foreach (var w in winners)
                    if (w != null) w.transform.position = RestPosition(w, w.Square) + boardRoot.up * h;
                yield return null;
            }
            foreach (var w in winners)
                if (w != null) SnapToSquare(w, w.Square);
        }

        void Flash(string message, Color color)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(message, color));
        }

        IEnumerator FlashRoutine(string message, Color color)
        {
            statusText.text = message;
            statusText.color = color;
            yield return new WaitForSeconds(1.2f);
            flashRoutine = null;
            statusText.text = statusBase;
            statusText.color = statusBaseColor;
        }

        void Play(AudioClip clip, Vector3 pos)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, pos);
        }

        void RefreshHighlights()
        {
            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var v = new Vector2Int(x, y);
                Material m = null;

                if (heldPiece != null)
                {
                    bool legal = heldMoves.Contains(v);
                    if (legal) m = grid[x, y] != null ? matCapture : matMove;
                    if (v == heldPiece.Square) m = matSelected;
                    if (legal && hoverSquare.HasValue && hoverSquare.Value == v) m = matHover;
                }

                var tile = highlightTiles[x, y];
                tile.enabled = m != null;
                if (m != null) tile.sharedMaterial = m;
            }
        }

        // ------------------------------------------------------------------ coordinates (vector math)

        static string SquareName(Vector2Int s) => $"{(char)('a' + s.x)}{s.y + 1}";

        Vector3 SquareToLocal(Vector2Int s) =>
            new Vector3((s.x - 3.5f) * squareSize, 0f, (s.y - 3.5f) * squareSize);

        /// <summary>Centre of a square, on the board surface.</summary>
        public Vector3 SquareToWorld(Vector2Int s) => boardRoot.TransformPoint(SquareToLocal(s));

        /// <summary>Where a particular piece rests on a square (keeps its own pivot height).</summary>
        public Vector3 RestPosition(ChessPiece p, Vector2Int s) =>
            boardRoot.TransformPoint(SquareToLocal(s) + Vector3.up * p.HeightOffset);

        /// <summary>Converts a world position to the nearest board square.</summary>
        public bool WorldToSquare(Vector3 world, out Vector2Int square)
        {
            Vector3 local = boardRoot.InverseTransformPoint(world);
            square = new Vector2Int(Mathf.RoundToInt(local.x / squareSize + 3.5f),
                                    Mathf.RoundToInt(local.z / squareSize + 3.5f));
            bool nearSurface = local.y > -0.05f && local.y < 0.35f;
            return MoveRules.InBounds(square) && nearSurface;
        }

        void SnapToSquare(ChessPiece p, Vector2Int s) =>
            p.transform.SetPositionAndRotation(RestPosition(p, s), boardRoot.rotation * p.RotationOnBoard);
    }
}
