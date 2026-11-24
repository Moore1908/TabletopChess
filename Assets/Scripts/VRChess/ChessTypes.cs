namespace VRChess
{
    public enum PieceType { Pawn, Knight, Bishop, Rook, Queen, King }

    public enum PieceColor { White, Black }

    public static class PieceColorExtensions
    {
        public static PieceColor Opposite(this PieceColor c) =>
            c == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}
