namespace Chess
{
    using Chess.Game;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using System.Diagnostics;
    using System.Linq;
    using Debug = UnityEngine.Debug;
    class MCTSSearch : ISearch
    {
        public event System.Action<Move> onSearchComplete;

        MoveGenerator moveGenerator;
        Board board;

        System.Random random = new();
        Move bestMove;
        float bestEval;
        int numNodes = 1;

        bool abortSearch;


        readonly MCTSSettings settings;
        readonly Evaluation evaluation;

        MCTSNode root;
        int nSimsCount;


        // Diagnostics
        public SearchDiagnostics Diagnostics { get; set; }
        System.Diagnostics.Stopwatch searchStopwatch;

        public MCTSSearch(Board board, MCTSSettings settings)
        {

            this.settings = settings;
            evaluation = new Evaluation();
            moveGenerator = new MoveGenerator();
            this.board = board;
            //root = new(null, board.Clone(), moveGenerator, settings, evaluation);//But why couldn't I use what was already calculated...?
        }

        public void StartSearch()
        {
            root = new(null, board, moveGenerator, settings, evaluation, random);//root has to be recreated every time. So it has no children from the past.
            InitDebugInfo();

            // Initialize search settings
            bestMove = Move.InvalidMove;

            moveGenerator.promotionsToGenerate = settings.promotionsToSearch;
            abortSearch = false;
            Diagnostics = new();

            SearchMoves();
            if (bestMove.Value == Move.InvalidMove.Value) throw new Exception("INVALID");
            onSearchComplete?.Invoke(bestMove);

            if (!settings.useThreading)
            {
                LogDebugInfo();
            }
        }


        public void EndSearch()
        {
            if (settings.useTimeLimit)
            {
                abortSearch = true;
            }
        }
        MCTSNode Select()
        {
            //look for first available move otherwise look for most valuable child.
            MCTSNode node = root;
            while (node.availableMoves.Count == 0)//if its a node with all children we pick highest kid.
            {
   //            if (node.parentMoveName == "f2-d2" && node.parent.parentMoveName=="e5-c4" && node.parent.parent.parentMoveName == "c6-b5")//c6-b5
 //                   Debug.Log("breakpoint");
                float MaxValue = 0;
                var children = node.children;
                if (children.Count == 0) {
                    return node;//this should be a situation in which there is no more moves available. Simulation oughta give it low score and thus it should not repeat
                }
                foreach (MCTSNode child in children)
                {
                    float value = child.UCB();
                    if (MaxValue < value) { node = child; MaxValue = value; }
                }
            }
            return node;//If any kids are missing we gotta pick this one first.

        }

        void SearchMoves()
        {
            nSimsCount = 0;/*
            var dad = board.GetLightweightClone();
            foreach (var kid in root.children)//if we got here in second iteration we should be able to figure out what the opponent did and recycle our old subtree.
            {
                bool same = true;
                var kidboard = kid.board.GetLightweightClone();
                Debug.Log("Good?");
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        
                        if (
                            !(
                                (kidboard[i, j] is null && dad[i, j] is null )
                                ||
                                (kidboard[i, j] is not null && dad[i, j] is not null && kidboard[i, j].code == dad[i, j].code))
                            ) { 
                            same = false;
                            Debug.Log("...no!"+i+" "+j+" - "+ kidboard[i, j].type +" / "+ dad[i, j]);
                            break;
                        }

                    }
                    if (!same) break;
                }
                if (same)
                {
                    Debug.Log("Short");
                    root = kid;
                    nSimsCount = root.nSimulations;

                    break;
                }
            }*/
            List<Move> moves = new(root.availableMoves);
            moves.Reverse();

            while (!abortSearch && (!settings.limitNumOfPlayouts || nSimsCount < settings.maxNumOfPlayouts))
            {
                nSimsCount++;
                //Debug.Log(nSimsCount);
                MCTSNode selected = Select().Expand();//Select picks unfinished node. Expand creates & returns its newly spawned child.
                numNodes++;
                selected.BackPropagate(selected.Simulate());//Simulate assigns value to the new child and backpropagates this value up from the child to the root.
            }
            //Debug.Log("Booooo");
            bestEval = root.children[0].Score;
            bestMove = moves[0];
            //MCTSNode bestChild = root.children[0];
            for (int i =1; i < root.children.Count; i++)
            {
               // if(root.children[i].Score < 0.3)
                    Debug.Log($"{moves[i].Name} -Child:{i} has score {(int)(root.children[i].Score*100)}% cause it has {root.children[i].nWins} nWins and {root.children[i].nSimulations} Sims");
                if (root.children[i].Score < bestEval) {
                    bestMove = moves[i];
                    bestEval = root.children[i].Score; /*bestChild = root.children[i];*/ }//we pick the roots child with the highest score
            }
            //root = bestChild;//This makes sense right? There is nothing unique about the new state. We just take this subtree for future use (will find out what the opponent did in the foreach some 20 lines above)
            //this guy should be on the enemy's turn though
        }

        void LogDebugInfo()
        {

            UnityEngine.Debug.Log($"Best move: {bestMove.Name} Eval: {bestEval} Search time: {searchStopwatch.ElapsedMilliseconds} ms.");
            //UnityEngine.Debug.Log($"Num nodes: {numNodes}");

        }

        void InitDebugInfo()
        {
            searchStopwatch = Stopwatch.StartNew();
            // Optional
        }
    }
}