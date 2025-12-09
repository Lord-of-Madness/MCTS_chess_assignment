namespace Chess
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    using Debug = UnityEngine.Debug;

    public class MCTSNode
    {
        public override string ToString()
        {
            return parentMoveName + " - " + (int)(Score*100)+"%" ;//for debug purposes
        }
        const float explorationParameter = 1;
        public MCTSNode parent;
        public float Score
        {
            get => nWins / nSimulations;
        }
        public Board board;
        public List<MCTSNode> children;
        public List<Move> availableMoves;
        public readonly MoveGenerator moveGenerator;
        private readonly MCTSSettings settings;
        public int nSimulations = 0;
        public float nWins = 0;
        private readonly Evaluation evaluation;
        private readonly System.Random random;//Unity's Random was getting the code stuck. I couldn't figure out how and why.
        public string parentMoveName; //for debuging purposes
        enum Result
        {
            StateIsMated,
            StateIsWon,
            Stalemate,
            Playing
        }
        Result preresult = Result.Playing;
        public MCTSNode(MCTSNode parent, Board board, MoveGenerator moveGenerator, MCTSSettings settings, Evaluation evaluation, System.Random random)
        //Makes more sense to have a reference to Settings and evaluation rather than making more objects. Unless we'd like to do some paralel shenanigans that is.
        {
            this.parent = parent;
            this.board = board;
            this.moveGenerator = moveGenerator;
            this.settings = settings;
            this.evaluation = evaluation;
            availableMoves = moveGenerator.GenerateMoves(board, true);
            bool inCheck = moveGenerator.InCheck();
            if (availableMoves.Count == 0)
            {
                if (inCheck) preresult = Result.StateIsMated;
                else preresult = Result.Stalemate;
            }
            else
                foreach (var move in availableMoves)
                {
                    if (board.KingSquare[1 - board.ColourToMoveIndex] == move.TargetSquare) preresult = Result.StateIsWon;
                }
            children = new(availableMoves.Count);
            this.random = random;
        }

        public void BackPropagate(float score)
        {
            nSimulations++;
            nWins += score;
            parent?.BackPropagate(1 - score);

        }
        public MCTSNode Expand()
        {
            if (preresult != Result.Playing) return this;// this is an end state. Has no children. Game over
            Board childboard = board.Clone();
            childboard.MakeMove(availableMoves[^1]);
            MCTSNode childNode = new(this, childboard, moveGenerator, settings, evaluation, random)
            {
                parentMoveName = availableMoves[^1].Name//for debug purposes
            };
            availableMoves.RemoveAt(availableMoves.Count - 1);
            children.Add(childNode);
            return childNode;

        }

        public float Simulate()
        {
            if (preresult == Result.StateIsWon) return 1;
            else if (preresult == Result.StateIsMated)return 0;
            else if (preresult == Result.Stalemate) return 0.5f;//umm this is a loss for us but also not a win for the other guy sooooo its as good as middleground?


            var SimBoard = board.GetLightweightClone();
            bool whiteToMove = board.WhiteToMove;
            

            


            for (int depth = 0; depth < settings.playoutDepthLimit; depth++)
            {
                List<SimMove> moves = moveGenerator.GetSimMoves(SimBoard, whiteToMove);
                if (moves.Count == 0) return 0.5f;//Stalemate and thats technically a failure for either) (Oughta check if we aren't in check for that would be a loss)
                var move = moves[random.Next(moves.Count)];//we pick moves at random

                //bool OwnKingPresent = false;
                //bool OppKingPresent = false;
                /*(int, int) OwnKingCoord = (0, 0);
                (int, int) OppKingCoord = (0, 0);
                for (int i = 0; i < 8; i++) for (int j = 0; j < 8; j++)
                    {
                        SimPiece p = SimBoard[i, j];
                        if (p != null)
                        {
                            if (p.team == board.WhiteToMove)
                            {
                                if (p.type == SimPieceType.King)
                                {
                                    //OwnKingPresent = true;
                                    OwnKingCoord = (i, j);
                                }
                            }
                            else
                            {
                                if (p.type == SimPieceType.King)
                                {
                                    //OppKingPresent = true;
                                    OppKingCoord = (i, j);
                                }
                            }
                        }
                    }*/
                //if (!OppKingPresent) return 1;
                //if (!OwnKingPresent) return 0;
                //(int, int) TarKingCoord;
                //if (board.WhiteToMove == whiteToMove) TarKingCoord = OppKingCoord;
                //else TarKingCoord = OwnKingCoord;
                if (moves.Count == 1
                    && SimBoard[move.endCoord1, move.endCoord2] is not null
                    && SimBoard[move.endCoord1, move.endCoord2].type == SimPieceType.King)
                {

                    if (board.WhiteToMove == whiteToMove)
                    {
                        return 1;
                    }
                    return 0;


                }

                whiteToMove = !whiteToMove;
                SimBoard[move.endCoord1, move.endCoord2] = SimBoard[move.startCoord1, move.startCoord2];
                SimBoard[move.startCoord1, move.startCoord2] = null;
            }

            return evaluation.EvaluateSimBoard(SimBoard, board.WhiteToMove);//always with respect to the white player
        }
        public float UCB()
        {
            //if (nSimulations == 0) return float.PositiveInfinity;//so that we don't divide by zero, also making it preferable //shouldn't ever happen(we don't ask for UCB if we haven't seen the node yet)
            return 1 - nWins / nSimulations + explorationParameter * Mathf.Sqrt(Mathf.Log(parent.nSimulations) / nSimulations);
        }
    }
}