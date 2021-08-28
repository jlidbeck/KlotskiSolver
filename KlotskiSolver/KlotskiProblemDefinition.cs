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

        //  Determine whether {state} satisfies {goalState}, that is,
        //  tile IDs match all non-blank values in {goalState}
        public bool matchesGoalState(KlotskiState state)
        {
            for (int i = 0; i < goalState.stateString.Length; ++i)
            {
                if (goalState.stateString[i] != ' ' && state.stateString[i] != goalState.stateString[i])
                    return false;
            }
            return true;
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

            // keep track of state signatures for all states in both history and queue,
            // so that the same state is never searched twice.
            // this avoids loops and backtracking as well as searching multiple paths to the same state
            var visitedStates = new HashSet<string>();

            // search queue for width-first-search
            var searchQueue = new Queue<KlotskiState>();

            foreach (KlotskiState st in history)
            {
                visitedStates.Add(st.canonicalString);
            }

            // starting state for search
            searchQueue.Enqueue(history[history.Count() - 1]);

            Random rand = new Random();
            int maxDepth = history.Count() + depth;

            // for console reporting: report each time a new depth is reached
            int lastReportedDepth = -1;

            while (searchQueue.Count()>0)
            {
                // take one off the queue
                KlotskiState state = searchQueue.Dequeue();

                // output
                if (state.depth > lastReportedDepth)
                {
                    lastReportedDepth = state.depth;
                    Console.WriteLine("Depth:"+state.depth+" Queue:" + searchQueue.Count() + " Unique:" + visitedStates.Count());
                }


                if (this.matchesGoalState(state))
                {
                    return state;
                }
                else
                {
                    if (state.depth < maxDepth)
                    {
                        List<KlotskiState> children = state.getChildStates();

                        if (children.Count() > 0)
                        {
                            // add all children to queue, in some order,
                            // skipping states isomorphic to states in history or in the queue

                            // first priority: find children that move the same piece
                            // just moved
                            for (int k = 0; k < children.Count(); ++k)
                            {
                                if (children[k].movedPiece == state.movedPiece)
                                {
                                    string isoString = children[k].canonicalString;
                                    if (!visitedStates.Contains(isoString))
                                    {
                                        searchQueue.Enqueue(children[k]);
                                        visitedStates.Add(isoString);
                                    }
                                }
                            }

                            // now add all the rest of the child states which have not been added
                            // and are not duplicates or repeats
                            // they are added in a semi-random order.
                            // todo: use heuristic
                            int j = rand.Next() % children.Count();
                            for (int m = 0; m < children.Count(); ++m)
                            {
                                int k = (j + m) % children.Count();

                                string isoString = children[k].canonicalString;
                                if (!visitedStates.Contains(isoString))
                                {
                                    searchQueue.Enqueue(children[k]);
                                    visitedStates.Add(isoString);
                                }
                                
                            }
                        }

                    }
                }
            }   // while(queue not empty)

            // all reachable states have been exhausted, with no solution found
            return null;
        }   // void search()
    }
}
