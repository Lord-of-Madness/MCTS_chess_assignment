namespace Chess
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using UnityEngine;
    using static System.Math;
    class MCTSSearch : ISearch
    {
        public event System.Action<Move> onSearchComplete;

        MoveGenerator moveGenerator;

        Move bestMove;
        int bestEval;
        bool abortSearch;
        const float explorationParameter = 1;

        MCTSSettings settings;
        Board board;
        Evaluation evaluation;

        MCTSNode root;
        List<MCTSNode> activeNodes;
        int nSimsCount;


        // Diagnostics
        public SearchDiagnostics Diagnostics { get; set; }
        System.Diagnostics.Stopwatch searchStopwatch;

        public MCTSSearch(Board board, MCTSSettings settings)
        {
            this.board = board;
            this.settings = settings;
            evaluation = new Evaluation();
            moveGenerator = new MoveGenerator();
            root = new(null, board, moveGenerator,settings, evaluation);
            activeNodes = new() { root };
        }

        public void StartSearch()
        {
            InitDebugInfo();

            // Initialize search settings
            bestEval = 0;
            bestMove = Move.InvalidMove;

            moveGenerator.promotionsToGenerate = settings.promotionsToSearch;
            abortSearch = false;
            Diagnostics = new SearchDiagnostics();

            SearchMoves();

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
            nSimsCount++;
            (MCTSNode,float) chosen = (null,0);
            if (activeNodes.Count == 0) throw new Exception("Ran out of nodes to go through");
            foreach (MCTSNode node in activeNodes)
            {
                float value = node.UCB(nSimsCount, explorationParameter);
                if (chosen.Item1 is null ||chosen.Item2 < value)chosen = (node, value);
            }
            return chosen.Item1;
        }

        void SearchMoves()
        {
            var moves = root.moveGenerator.GenerateMoves(board, true);
            while (!abortSearch)
            {// Don't forget to end the search once the abortSearch parameter gets set to true.
                MCTSNode selected = Select();
                selected.Expand();
                selected.Simulate();
                selected.BeginBackPropagation();
                if (root.score == 1)break;//found the win path
            }
            for (int i = 0; i < root.children.Count; i++)
            {
                if (root.children[i].score == root.score) bestMove = moves[i];
            }
        }

        void LogDebugInfo()
        {
            // Optional
        }

        void InitDebugInfo()
        {
            searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Optional
        }
    }
}