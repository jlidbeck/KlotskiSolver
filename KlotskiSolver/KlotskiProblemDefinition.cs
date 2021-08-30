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
        public KlotskiState search(List<KlotskiState> history, int depth)
        {
            // init search

            // keep track of canonical state strings for all states in search history
            // so that the same state is never searched twice.
            // this avoids loops and backtracking as well as searching suboptimal paths to the same state

            var visitedStates = new Dictionary<string, KlotskiState>();

            // if a history was provided, add all previous states to the visited set
            // not including the last state, which will be the start state for the search.
            for(int i=0; i<history.Count()-1; ++i)
            {
                var st = history[i];
                visitedStates[st.canonicalString] = st;
            }

            // search queue for width-first-search
            var searchQueue = new MS.Internal.PriorityQueue<KlotskiState>(0, new KlotskiState.MoveCountComparer());

            // starting state for search
            searchQueue.Push(history.Last());

            //Random rand = new Random();
            int maxDepth = history.Count() + depth;

            // for console reporting: report each time a new depth is reached
            int lastReportedDepth = -1;

            int pruneCount = 0;
            int superCount = 0;

            while (searchQueue.Count > 0)
            {
                // take one off the queue
                KlotskiState state = searchQueue.Top;
                searchQueue.Pop();

                // output
                if (state.depth > lastReportedDepth)
                {
                    lastReportedDepth = state.depth;
                    if (searchQueue.Count > 0)
                    {
                        Console.WriteLine($"Depth: {state.moveCount} / {state.depth} Queue:{searchQueue.Count} Visited:{visitedStates.Count()} ...");// + searchQueue.moveCount);
                    }
                }

                if (state.matchesGoalState(this.goalState))
                {
                    return state;
                }

                if (state.depth > maxDepth)
                {
                    continue;
                }

                // if we have already visited this state:
                if (visitedStates.ContainsKey(state.canonicalString))
                {
                    // we already know that, due to the ordering of the priority queue,
                    // this child state must have at least as many moves to get there,
                    // so we don't need to add this one to the search queue.

                    if (state.moveCount >= visitedStates[state.canonicalString].moveCount)
                    {
                        string sz = $"{state.getHistoryString()} longer than {visitedStates[state.canonicalString].getHistoryString()}";
                        ++pruneCount;
                        continue;
                    }

                    // found a shorter path? how is that possible?
                    ++superCount;
                    Console.WriteLine("what the hell?");
                }

                visitedStates[state.canonicalString] = state;

                List<KlotskiState> children = state.getChildStates();

                // add all children to queue, in some order,
                // skipping states isomorphic to states in history or in the queue
                foreach (var childState in children)
                {
                    string isoString = childState.canonicalString;

                    // if we have already visited this state:
                    if (visitedStates.ContainsKey(isoString))
                    {
                        // we already know that, due to the ordering of the priority queue,
                        // this child state must have at least as many moves to get there,
                        // so we don't need to add this one to the search queue.

                        if (childState.moveCount >= visitedStates[isoString].moveCount)
                        {
                            string sz = $"{childState.getHistoryString()} longer than {visitedStates[isoString].getHistoryString()}";
                            ++pruneCount;
                            continue;
                        }

                        // found a shorter path? how is that possible?
                        ++superCount;
                        Console.WriteLine("what the hell?");
                    }

                    searchQueue.Push(childState);

                }   // foreach(childState)

            }   // while(queue not empty)

            // all reachable states have been exhausted, with no solution found
            return null;
        }   // void search()
    }
}
