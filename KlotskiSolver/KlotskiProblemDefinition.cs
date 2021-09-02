using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace KlotskiSolverApplication
{
    class KlotskiProblemDefinition
    {
        public string name { get; private set; } = null;

        public int width   { get; private set; } = 0;
        public int height  { get; private set; } = 0;

        public KlotskiState goalState  { get; private set; }        // Goal state. Blanks are wildcards; all other values must be matched.
        public KlotskiState startState { get; private set; }
        public Dictionary<char, char>         tileIdToTypeMap  { get; private set; }    // Maps tile IDs to type IDs
        public Dictionary<char, ConsoleColor> tileIdToColorMap { get; private set; }

        public int goalMoves { get; private set; } = 100;

        //  Initializes a problem definition.
        //  Start and goal states are provided as strings of length width x height representing a 2D array of spaces and
        //  tile ID char values and spaces.
        //  isomorphicTileGroups: defines which tiles are considered identical for the purpose of comparing states.
        //  This string is a space-separated list of strings where each string is a set of tile IDs.
        //  e.g. "ABC IJ" indicates that tiles A, B, and C are identical, tiles I and J are identical, and any other tiles
        //  are unique.
        public KlotskiProblemDefinition(string name, int goalMoves, int width, int height, string szStartState, string szGoalState, string isomorphicTileGroups)
        {
            Trace.Assert(width > 0 && height > 0, "Invalid problem definition size");

            this.name = name;
            this.goalMoves = goalMoves;
            this.width = width;
            this.height = height;
            this.goalState = new KlotskiState(this, szGoalState);
            this.startState = new KlotskiState(this, szStartState);

            parseTileIdTypeGroups(isomorphicTileGroups);
            initializeColors();
        }

        public static KlotskiProblemDefinition createFifteenPuzzle(string name, int width, int height, int goalMoves)
        {
            var random = new Random();
            int n = width * height;

            var szGoalState = new char[n];
            for (int i = 0; i < n - 1; ++i)
            {
                szGoalState[i] = (char)('A' + i);
            }
            szGoalState[n - 1] = ' ';

            var szStartState = (char[])szGoalState.Clone();

            // this loop ensures we always do an even number of swaps (not including the empty square swap)
            for (int i = n - 2; i > n % 2; --i)
            {
                int j = random.Next(i);
                char c = szStartState[i];
                szStartState[i] = szStartState[j];
                szStartState[j] = c;
            }

            return new KlotskiProblemDefinition(name, goalMoves, width, height, new string(szStartState), new string(szGoalState), "");
        }

        private void parseTileIdTypeGroups(string isomorphicTiles)
        {
            tileIdToTypeMap = new Dictionary<char, char>();

            char typeId = '0';
            var groups = isomorphicTiles.Split(' ');
            foreach (var group in groups)
            {
                // get a new tile type id char
                // also make sure it's not any tileId
                for (++typeId; startState.stateString.IndexOf(typeId) >= 0; ++typeId) ;

                foreach (var tileId in group)
                    tileIdToTypeMap[tileId] = typeId;
            }
        }

        private void initializeColors()
        {
            tileIdToColorMap = new Dictionary<char, ConsoleColor>();

            // cycle thru these 15 colors.
            // the first tile ID (by ASCII value) is assigned the color white.

            var colors = new ConsoleColor[15] {
                ConsoleColor.White,
                ConsoleColor.Magenta,
                ConsoleColor.Red,
                ConsoleColor.Cyan,
                ConsoleColor.Green,
                ConsoleColor.Blue,
                ConsoleColor.DarkGray,
                ConsoleColor.Gray,
                ConsoleColor.DarkYellow,
                ConsoleColor.DarkMagenta,
                ConsoleColor.DarkRed,
                ConsoleColor.DarkCyan,
                ConsoleColor.DarkGreen,
                ConsoleColor.DarkBlue,
                ConsoleColor.Yellow,
                };

            tileIdToColorMap[' '] = ConsoleColor.Black;

            // get all unique tileIDs, sorted
            var tileIds = startState.stateString.Distinct().ToArray();
            Array.Sort(tileIds);

            int colorIdx = 0;
            foreach (var c in tileIds)
            {
                if (!tileIdToColorMap.ContainsKey(c))
                {
                    tileIdToColorMap[c] = colors[colorIdx];
                    if (++colorIdx == colors.Length)
                    {
                        colorIdx = 1;
                    }
                }
            }
        }

        //  Generates canonical version of {state} with tile IDs replaced with type IDs
        public string getCanonicalStateString(string state)
        {
            char[] sz = state.ToCharArray();
            for (int i = 0; i < state.Length; ++i)
            {
                if (tileIdToTypeMap.ContainsKey(state[i]))
                {
                    sz[i] = tileIdToTypeMap[state[i]];
                }
            }

            return new string(sz);
        }


        //
        // search
        //

        //  Searches for {startState.context.goalState} starting at {startState},
        //  using a breadth-first search up to the given depth.
        //  If a solution is found, returns a reference to the end state, which is isomorphic to the goal state.
        //  The returned state has a linked-list path back to {startState} found by traversing KlotskiState.parent.
        //
        public KlotskiState search(KlotskiState startState, IComparer<KlotskiState> searchComparer, int depth)
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
            var searchQueue = new MS.Internal.PriorityQueue<KlotskiState>(0, searchComparer);

            // starting state for search
            searchQueue.Push(startState);

            int maxDepth = startState.depth + depth;

            // for console reporting: report each time a new depth is reached
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long lastReport = -1000;

            int pruneCount1 = 0;
            int pruneCount2 = 0;

            //  Search loop
            //  Continues until goal state is found, or all reachable states have been reached
            while (searchQueue.Count > 0)
            {
                if (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                    Console.WriteLine("Search canceled.");
                    return null;
                }

                // take one off the queue
                KlotskiState state = searchQueue.Top;
                searchQueue.Pop();

                // periodically output to console
                if (stopwatch.ElapsedMilliseconds - lastReport > 100 )
                {
                    lastReport = stopwatch.ElapsedMilliseconds;
                    Console.WriteLine($"Depth: {state.moveCount} / {state.depth} Queue:{searchQueue.Count} Visited:{visitedStates.Count()}");
                }

                if (state.matchesGoalState())
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
                    ++pruneCount1;
                    continue;
                }

                visitedStates[state.canonicalString] = state;

                List<KlotskiState> children = state.getChildStates();

                // add all children to queue, in some order,
                // skipping states isomorphic to states in history or in the queue
                foreach (var childState in children)
                {
/*                    if (visitedStates.ContainsKey(childState.canonicalString))
                    {
                        // If we have already visited this state, we know (due to the ordering of the priority queue) that we have
                        // already found a path to this state no longer than this one so we don't need to add this one to the search queue.

                        Debug.Assert(childState.moveCount >= visitedStates[childState.canonicalString].moveCount);

                        ++pruneCount2;
                        continue;
                    }
*/
                    searchQueue.Push(childState);

                }   // foreach(childState)

            }   // while(queue not empty)

            // all reachable states have been exhausted, with no solution found
            return null;
        }   // void search()


        //  Finds a random state {depth} steps from {startState}.
        //  This is nearly the search algorithm reversed, using a stack instead of a queue, for a depth-first search.
        //  Longer paths are preferred, in fact, the first valid path of length {depth} without a loop is returned.
        //  The algorithm uses the same dist-sq heuristic used by the quick search. Here higher values are used to
        //  guide the search further from the {startState}.
        public KlotskiState shuffle(KlotskiState startState, int depth)
        {
            var random = new Random();

            // Keep track of all states visited to avoid backtracking or searching suboptimal paths.
            // These are keyed as canonical strings, because we consider a state visited even if identical tiles
            // are in exchanged positions.

            var visitedStates = new HashSet<string>();

            // search queue for depth-first-search
            var searchStack = new Stack<KlotskiState>();

            // starting state for search
            searchStack.Push(startState);

            // for console reporting: report each time a new depth is reached
            int lastReportedDepth = -1;

            //  Search loop
            //  Continues until goal state is found, or all reachable states have been reached
            while (searchStack.Count > 0)
            {
                // take one off the queue
                KlotskiState state = searchStack.Pop();

                // output
                if (state.depth > lastReportedDepth)
                {
                    lastReportedDepth = state.depth;
                    Console.WriteLine($"Depth: {state.moveCount} / {state.depth} Score:{state.getDistanceSquareScore()} Stack:{searchStack.Count} Visited:{visitedStates.Count()}");
                }

                if (visitedStates.Contains(state.canonicalString))
                {
                    continue;
                }

                if (state.depth - startState.depth >= depth)
                {
                    return state;
                }

                visitedStates.Add(state.canonicalString);

                List<KlotskiState> children = state.getChildStates();

                // add all children to stack, in some order.

                // since this is a last-in-first-out stack, the same heuristic can be used in reverse
                // to give priority to states further from the goal.
                children.Sort(new KlotskiState.DistanceSquaredHeuristicComparer(random.Next(10), 2));

                foreach (var childState in children)
                {
                    searchStack.Push(childState);
                }

            }   // while(stack not empty)

            // all reachable states have been exhausted, with no solution found
            return null;
        }   // void shuffle()
    }
}
