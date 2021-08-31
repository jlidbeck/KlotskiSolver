using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace KlotskiSolverApplication
{
    class KlotskiProblemDefinition
    {
        public int width  { get; private set; } = 0;
        public int height { get; private set; } = 0;

        public KlotskiState goalState  { get; private set; }        // Goal state. Blanks are wildcards; all other values must be matched.
        public KlotskiState startState { get; private set; }
        public string tileIdToTypeMap { get; private set; }         // String representing mapping from each tile ID to its shape type

        public int goalMoves { get; private set; } = 100;

        //  Initializes a problem definition.
        //  Start and goal states are provided as strings representing a 2D array containing tile ID char values and spaces.
        //  tileIdToTypeMap is a string interpreted as char pairs mapping tile IDs (typically alpha chars)
        //  to tile types (typically digits).
        public KlotskiProblemDefinition(int width, int height, string szStart, string szSolution, string tileIdToTypeMap, int goalMoves)
        {
            Trace.Assert(width > 0 && height > 0, "Invalid problem definition size");

            this.width = width;
            this.height = height;
            this.tileIdToTypeMap = tileIdToTypeMap;
            this.goalState = new KlotskiState(this, szSolution);
            this.startState = new KlotskiState(this, szStart);
            this.goalMoves = goalMoves;
        }

        //  Generates canonical version of {state} with tile IDs replaced with type IDs
        public string getCanonicalStateString(string state)
        {
            char[] sz = new char[state.Length];
            for (int i = 0; i < state.Length; ++i)
            {
                int j = tileIdToTypeMap.IndexOf(state[i]);
                if (j >= 0)
                    sz[i] = tileIdToTypeMap[j + 1];
                else
                    sz[i] = state[i];
            }

            return new string(sz);
        }


        //
        // search
        //

        /**
         * Starts a new search from the last state in {history}
         * to the goal state.
         * {history} is used by the search to avoid backtracking to any previous state
         */
        public KlotskiState search(KlotskiState startState, int depth)
        {
            // Keep track of all states visited to avoid backtracking or searching suboptimal paths.
            // These are keyed as canonical strings, because we consider a state visited even if identical tiles
            // are in exchanged positions.
            // We also store the complete state (as the value in the key/value pair), because this contains the
            // history.

            var visitedStates = new Dictionary<string, KlotskiState>();

            // if a history was provided, add all previous states to the visited set
            // not including the last state, which will be the start state for the search.
            for (var st = startState.parentState; st != null; st = st.parentState)
            {
                visitedStates[st.canonicalString] = st;
            }

            // search queue for width-first-search
            var searchQueue = new MS.Internal.PriorityQueue<KlotskiState>(0, new KlotskiState.MoveCountComparer());

            // starting state for search
            searchQueue.Push(startState);

            int maxDepth = startState.depth + depth;

            // for console reporting: report each time a new depth is reached
            int lastReportedDepth = -1;

            int pruneCount1 = 0;
            int pruneCount2 = 0;

            //  Search loop
            //  Continues until goal state is found, or all reachable states have been reached
            while (searchQueue.Count > 0)
            {
                // take one off the queue
                KlotskiState state = searchQueue.Top;
                searchQueue.Pop();

                // output
                if (state.depth > lastReportedDepth)
                {
                    lastReportedDepth = state.depth;
                    Console.WriteLine($"Depth: {state.moveCount} / {state.depth} Queue:{searchQueue.Count} Visited:{visitedStates.Count()}");
                }

                if (state.matchesGoalState(this.goalState))
                {
                    Console.WriteLine($"Pruned: {pruneCount1}, {pruneCount2}");
                    // Solution found: stop search
                    return state;
                }

                if (state.depth > maxDepth)
                {
                    continue;
                }

                if (visitedStates.ContainsKey(state.canonicalString))
                {
                    // If we have already visited this state, we know (due to the ordering of the priority queue) that we have
                    // already found a path to this state no longer than this one so we don't need to add this one to the search queue.

                    Debug.Assert(state.moveCount >= visitedStates[state.canonicalString].moveCount);

                    ++pruneCount1;
                    continue;
                }

                visitedStates[state.canonicalString] = state;

                List<KlotskiState> children = state.getChildStates();

                // add all children to queue, in some order,
                // skipping states isomorphic to states in history or in the queue
                foreach (var childState in children)
                {
                    if (visitedStates.ContainsKey(childState.canonicalString))
                    {
                        // If we have already visited this state, we know (due to the ordering of the priority queue) that we have
                        // already found a path to this state no longer than this one so we don't need to add this one to the search queue.

                        Debug.Assert(childState.moveCount >= visitedStates[childState.canonicalString].moveCount);

                        ++pruneCount2;
                        continue;
                    }

                    searchQueue.Push(childState);

                }   // foreach(childState)

            }   // while(queue not empty)

            // all reachable states have been exhausted, with no solution found
            return null;
        }   // void search()
    }
}
