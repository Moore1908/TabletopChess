using System.Collections.Generic;
using UnityEngine;

namespace VRChess
{
    /// <summary>
    /// Simplified chess movement rules. Board squares are Vector2Int(file, rank),
    /// 0..7 each, with White starting on ranks 0-1. Moves are expressed as direction
    /// vectors ("vector math"): sliding pieces repeat a direction until blocked,
    /// stepping pieces apply it once.
    /// Not implemented (by design, to keep scope small): castling, en passant,
    /// check legality. The game is won by capturing the King.
    /// </summary>
    public static class MoveRules
    {
        static readonly Vector2Int[] Straight =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        static readonly Vector2Int[] Diagonal =
        {
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        static readonly Vector2Int[] KnightJumps =
        {
            new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
            new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2)
        };

        public static bool InBounds(Vector2Int s) => s.x >= 0 && s.x < 8 && s.y >= 0 && s.y < 8;

        public static List<Vector2Int> GetMoves(ChessPiece[,] grid, ChessPiece piece)
        {
            var moves = new List<Vector2Int>();
            switch (piece.Type)
            {
                case PieceType.Pawn:   AddPawnMoves(grid, piece, moves); break;
                case PieceType.Knight: AddSteps(grid, piece, KnightJumps, moves); break;
                case PieceType.Bishop: AddSlides(grid, piece, Diagonal, moves); break;
                case PieceType.Rook:   AddSlides(grid, piece, Straight, moves); break;
                case PieceType.Queen:
                    AddSlides(grid, piece, Straight, moves);
                    AddSlides(grid, piece, Diagonal, moves);
                    break;
                case PieceType.King:
                    AddSteps(grid, piece, Straight, moves);
                    AddSteps(grid, piece, Diagonal, moves);
                    break;
            }
            return moves;
        }

        static bool IsEnemy(ChessPiece[,] grid, Vector2Int s, PieceColor side)
        {
            var other = grid[s.x, s.y];
            return other != null && other.Side != side;
        }

        static bool IsEmpty(ChessPiece[,] grid, Vector2Int s) => grid[s.x, s.y] == null;

        static void AddSteps(ChessPiece[,] grid, ChessPiece p, Vector2Int[] dirs, List<Vector2Int> moves)
        {
            foreach (var d in dirs)
            {
                var t = p.Square + d;
                if (InBounds(t) && (IsEmpty(grid, t) || IsEnemy(grid, t, p.Side)))
                    moves.Add(t);
            }
        }

        static void AddSlides(ChessPiece[,] grid, ChessPiece p, Vector2Int[] dirs, List<Vector2Int> moves)
        {
            foreach (var d in dirs)
            {
                var t = p.Square + d;
                while (InBounds(t))
                {
                    if (IsEmpty(grid, t))
                    {
                        moves.Add(t);
                    }
                    else
                    {
                        if (IsEnemy(grid, t, p.Side)) moves.Add(t);
                        break; // blocked
                    }
                    t += d;
                }
            }
        }

        static void AddPawnMoves(ChessPiece[,] grid, ChessPiece p, List<Vector2Int> moves)
        {
            int dir = p.Side == PieceColor.White ? 1 : -1;

            var one = p.Square + new Vector2Int(0, dir);
            if (InBounds(one) && IsEmpty(grid, one))
            {
                moves.Add(one);
                var two = p.Square + new Vector2Int(0, 2 * dir);
                if (!p.HasMoved && InBounds(two) && IsEmpty(grid, two))
                    moves.Add(two);
            }

            for (int dx = -1; dx <= 1; dx += 2)
            {
                var diag = p.Square + new Vector2Int(dx, dir);
                if (InBounds(diag) && IsEnemy(grid, diag, p.Side))
                    moves.Add(diag);
            }
        }
    }
}
