namespace Chess
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class MCTSNode
    {
        public MCTSNode parent;
        public float score = float.MaxValue;
        public Board board;
        public List<MCTSNode> children;
        public List<Move> availableMoves;
        public readonly MoveGenerator moveGenerator;
        private readonly MCTSSettings settings;
        public int nSimulations = 1;//so that we don't divide by zero
        public int nWins;
        private readonly Evaluation evaluation;
        public float SimValue;//Holds the result of the last Simulation with respect to the player on turn in this state (not the player running the simulation)
        public MCTSNode(MCTSNode parent, Board board, MoveGenerator moveGenerator, MCTSSettings settings, Evaluation evaluation)
        //Makes more sense to have a reference to Settings and evaluation rather than making more objects. Unless we'd like to do some paralel shenanigans that is.
        {
            this.parent = parent;
            this.board = board;
            this.moveGenerator = moveGenerator;
            this.settings = settings;
            this.evaluation = evaluation;

        }
        public void BeginBackPropagation()
        {
            BackPropagate(SimValue);
        }
        public void BackPropagate(float score)
        {
            if (this.score > score)
            {
                this.score = score;
                parent?.BackPropagate(1-score);//parent is other player therefore the SimValue is opposite for him
            }
        }
        void CreateChildren()
        {
            availableMoves = moveGenerator.GenerateMoves(board, parent is null);
            children = new(availableMoves.Count);
            foreach (Move move in availableMoves)
            {
                Board childboard = board.Clone();
                childboard.MakeMove(move);
                children.Add(new(this, childboard, moveGenerator, settings, evaluation));
            }
        }
        public MCTSNode Expand()
        {
            availableMoves ??= moveGenerator.GenerateMoves(board, parent is null);
            if (availableMoves.Count == 0) return null;//TODO: Gotta check and remove parent from open nodes after
            children ??= new();
            Board childboard = board.Clone();
            childboard.MakeMove(availableMoves.Last());
            MCTSNode childNode = new(this, childboard, moveGenerator, settings, evaluation);
            children.Add(childNode);
            availableMoves.RemoveAt(availableMoves.Count - 1);
            return childNode;

        }

        public void Simulate()
        {
            nSimulations++;

            int depth = 0;
            var SimBoard = board.GetLightweightClone();
            bool whiteToMove = board.WhiteToMove;
            while (settings.playoutDepthLimit > depth)
            {
                List<SimMove> moves = new();
                for (int i = 0; i < 8; i++)
                    for (int j = 0; j < 8; j++)
                    {
                        var piece = SimBoard[j, j];
                        if (piece is not null && piece.team == whiteToMove)
                        {
                            moves.Concat(piece.GetMoves(SimBoard, j, j));
                        }
                    }
                if(moves.Count == 0)//Cannot do anything
                {
                    if (whiteToMove != board.WhiteToMove) //evaluated with respect to the state we started in
                    {
                        nWins++;
                        SimValue = 1;//Win
                    }
                    else SimValue = 0;//Fail
                    return;
                }
                var move = moves[UnityEngine.Random.Range(0, moves.Count)];//we pick moves at random
                if (SimBoard[move.endCoord1, move.endCoord2].type == SimPieceType.King)//If we are on turn while opponents is in Check we win (in normal chess its the turn before but this will certainly not pose a problem later (easier to check))
                {
                    if (whiteToMove == board.WhiteToMove) //evaluated with respect to the state we started in
                    {
                        nWins++;
                        SimValue = 1;//Win
                    }
                    else SimValue = 0;//Fail
                    return;
                }
                depth++;
                whiteToMove = !whiteToMove;
            }

            SimValue = evaluation.EvaluateSimBoard(SimBoard, board.WhiteToMove);//evaluated with respect to the state we started in
        }
        public float UCB(int totalSimCount, float c)
        {
            return nWins / nSimulations + c * Mathf.Sqrt(Mathf.Log(totalSimCount) / nSimulations);
        }
    }
}