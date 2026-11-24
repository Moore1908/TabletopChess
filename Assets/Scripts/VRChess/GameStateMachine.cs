using UnityEngine;

namespace VRChess
{
    /// <summary>Base class for every game state (Finite State Machine).</summary>
    public abstract class GameState
    {
        protected readonly ChessGame Game;
        protected GameState(ChessGame game) { Game = game; }

        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    /// <summary>Holds the current state and handles transitions.</summary>
    public class GameStateMachine
    {
        public GameState Current { get; private set; }

        public void ChangeState(GameState next)
        {
            Current?.Exit();
            Current = next;
            Debug.Log($"[FSM] -> {next?.GetType().Name}");
            Current?.Enter();
        }

        public void Tick() => Current?.Tick();
    }

    /// <summary>A human player's turn: only their pieces can be grabbed.</summary>
    public class PlayerTurnState : GameState
    {
        readonly PieceColor side;
        public PlayerTurnState(ChessGame game, PieceColor side) : base(game) { this.side = side; }

        public override void Enter()
        {
            Game.CurrentTurn = side;
            Game.SetGrabbableFor(side);

            string msg = Game.aiPlaysBlack ? "Your turn" : $"{side}'s turn";
            if (Game.IsInCheck(side)) msg += "\nCHECK!";
            Game.SetStatus(msg, Color.white);
        }

        public override void Exit() => Game.SetGrabbableFor(null);
    }

    /// <summary>The computer's turn: nothing is grabbable while it moves.</summary>
    public class AITurnState : GameState
    {
        readonly PieceColor side;
        public AITurnState(ChessGame game, PieceColor side) : base(game) { this.side = side; }

        public override void Enter()
        {
            Game.CurrentTurn = side;
            Game.SetGrabbableFor(null);
            Game.SetStatus("Opponent is thinking...", new Color(1f, 0.85f, 0.5f));
            Game.StartAITurn(side);
        }
    }

    /// <summary>Game finished: show the result and celebrate.</summary>
    public class GameOverState : GameState
    {
        readonly PieceColor winner;
        readonly string reason;

        public GameOverState(ChessGame game, PieceColor winner, string reason) : base(game)
        {
            this.winner = winner;
            this.reason = reason;
        }

        public override void Enter()
        {
            Game.SetGrabbableFor(null);
            Game.OnGameOver(winner, reason);
        }
    }
}
