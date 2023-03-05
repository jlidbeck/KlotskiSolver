using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace KlotskiSolverApplication
{
    [DebuggerDisplay("Start:{startState.stateString} Goal:{goalState.stateString}")]
    class KlotskiProblemDefinition
    {
        public string name { get; private set; } = null;

        public int width   { get; private set; } = 0;
        public int height  { get; private set; } = 0;

        public KlotskiState goalState  { get; private set; }        // Goal state. Blanks are wildcards; all other values must be matched.
        public KlotskiState startState { get; private set; }
        public Dictionary<char, char>         tileTypeIdMap { get; private set; }
        public Dictionary<char, ConsoleColor> tileColorMap  { get; private set; }

        public int goalMoves { get; private set; } = 100;

        //  Initializes a problem definition.
        //  Start and goal states are provided as strings of length width x height representing a 2D array of
        //  case-sensitive tile IDs and spaces.
        //  isomorphicTileGroups: defines which tiles are considered interchangeable for the purpose of comparing states.
        //  This string is a space-separated list of strings where each string is a set of tile IDs.
        //  e.g. "ABC IJ" indicates that tiles A, B, and C are identical, tiles I and J are identical, and any other tiles
        //  are unique.
        public KlotskiProblemDefinition(
            string name, 
            int goalMoves, 
            int width, int height, 
            string startState, 
            string goalState, 
            string isomorphicTileGroups)
        {
            Trace.Assert(width > 0 && height > 0, "Invalid problem definition size");

            this.name = name;
            this.goalMoves = goalMoves;
            this.width = width;
            this.height = height;
            this.goalState = new KlotskiState(this, goalState);
            this.startState = new KlotskiState(this, startState);

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
            tileTypeIdMap = new Dictionary<char, char>();

            char typeId = '0';
            var groups = isomorphicTiles.Split(' ');
            foreach (var group in groups)
            {
                // get a new tile type id char
                // also make sure it's not any tileId
                for (++typeId; startState.stateString.IndexOf(typeId) >= 0; ++typeId) ;

                foreach (var tileId in group)
                    tileTypeIdMap[tileId] = typeId;
            }
        }

        private void initializeColors()
        {
            tileColorMap = new Dictionary<char, ConsoleColor>();

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

            tileColorMap[' '] = ConsoleColor.Black;

            // get all unique tileIDs, sorted
            var tileIds = startState.stateString.Distinct().ToArray();
            Array.Sort(tileIds);

            int colorIdx = 0;
            foreach (var c in tileIds)
            {
                if (!tileColorMap.ContainsKey(c))
                {
                    tileColorMap[c] = colors[colorIdx];
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
                if (tileTypeIdMap.ContainsKey(state[i]))
                {
                    sz[i] = tileTypeIdMap[state[i]];
                }
            }

            return new string(sz);
        }


        //
        // search
        //

        [DebuggerDisplay("Solutions={solutionStates.Count}, Visited={visitedStates}, MaxDepth={maxDepthReached}")]
        public class SearchContext
        {
            public readonly List<KlotskiState> solutionStates = new List<KlotskiState>();

            // count of all states reached by the search
            public int visitedStates { get; internal set; } = 0;

            // count of all states reached, binned by moveCount
            public List<int> visitedStatesByMoveCount = new List<int>();

            // indicates that all reachable states (up to maximum depth) have been visited
            public bool allStatesVisited { get; internal set; } = false;

            // indicates the max depth reached by the search
            public int maxDepthReached { get; internal set; } = 0;

            // indicates the max moves reached by the search
            public int maxMovesReached { get; internal set; } = 0;

            // list of all states reached at the maximum moveCount seen so far
            public List<KlotskiState> deepestStates = null;

            // update {visitedStatesByMoveCount} and {deepestStates}
            internal void incrementDepthCounter(KlotskiState state)
            {
                if (visitedStatesByMoveCount.Count() <= state.moveCount)
                {
                    // reached new depth: reset the deepest state list
                    deepestStates = new List<KlotskiState>();

                    // expand the bin counter list until we have at least {state.moveCount+1} bins
                    while (visitedStatesByMoveCount.Count() <= state.moveCount)
                        visitedStatesByMoveCount.Add(0);
                }

                visitedStatesByMoveCount[state.moveCount]++;
                if (state.moveCount == visitedStatesByMoveCount.Count() - 1)
                    deepestStates.Add(state);
            }

            // additional reporting for debugging
            public int prunedAfterPop { get; internal set; } = 0;
            public int prunedBeforePush { get; internal set; } = 0;
        };

        //  Finds all states at the greatest distance from any solution state
        public SearchContext findDeepestStates(int depth)
        {
            // First, do a full graph traversal to find all reachable solution states
            var searchResults = search(startState, new KlotskiState.MoveCountComparer(), depth, false);

            if (searchResults == null)
                return null;

            Console.WriteLine();
            Console.WriteLine($"*** BFS traversal complete: {searchResults?.solutionStates?.Count()} solution states found ***");
            Console.WriteLine();

            // Now reset and do a second traversal, starting with all the solution states.
            // This guarantees a minimal moveCount is set on all reachable states.

            // Each solution state node must be detached from the previous search's traversal tree
            // so the next search starts fresh
            foreach (var st in searchResults.solutionStates)
            {
                st.detach();
            }

            // Search 2: another full traversal with multiple start states
            searchResults = search(searchResults.solutionStates, null, new KlotskiState.MoveCountComparer(), depth, false);

            Console.WriteLine($"*** 2nd BFS traversal complete: {searchResults.deepestStates.Count()} states found with moveCount={searchResults.deepestStates.First().moveCount} ***");
            Console.WriteLine();
            return searchResults;
        }

        //  
        //  For a full enumeration of shortest paths to all reachable states at depth {depth},
        //  {searchComparer} should order states on depth, and {stopAtFirst} should be false.
        //  If {stopAtFirst} is true:
        //  Searches for {startState.context.goalState} starting at {startState}, using a breadth-first search up to the given depth.
        //  The first solution found is returned.
        //  If {stopAtFirst} is false:
        //  A full traversal is done up to {depth}.
        //  Solutions are returned as references to an end state which satisfies the goal state criteria.
        //  Each end state is the last member of a linked-list path back to {startState}
        //  which can be enumerated by traversing KlotskiState.parent.
        //
        public SearchContext search(KlotskiState startState, IComparer<KlotskiState> searchComparer, int depth, bool stopAtFirst)
        {
            var startStates = new List<KlotskiState>();
            startStates.Add(startState);

            // if a history was provided, add all previous states to the visited set
            // not including the last state, which will be the start state for the search.
            var excludeStates = new List<KlotskiState>();
            for (var st = startState.parentState; st != null; st = st.parentState)
            {
                excludeStates.Add(st);
            }

            var searchResults = search(startStates, excludeStates, searchComparer, depth, stopAtFirst);
            return searchResults;
        }

        SearchContext search(
            IEnumerable<KlotskiState> startStates, 
            IEnumerable<KlotskiState> excludeStates, 
            IComparer<KlotskiState> searchComparer, 
            int maxMoves, 
            bool stopAtFirst)
        {
            Trace.Assert(startStates?.Count() > 0, "One or more start states required");

            // Keep track of all states visited to avoid backtracking or searching suboptimal paths.
            // States are keyed as their canonical strings, because we consider a state visited even if identical tiles are exchanged.
            // We also store the complete state (as the value in the key/value pair), because this contains the
            // history.

            var visitedStates = new Dictionary<string, KlotskiState>();

            var searchContext = new SearchContext();

            // if a history was provided, add all previous states to the visited set
            // not including the last state, which will be the start state for the search.
            if (excludeStates != null)
            {
                foreach (var st in excludeStates)
                {
                    visitedStates[st.canonicalString] = st;
                }
            }

            // search queue for width-first-search
            var searchQueue = new MS.Internal.PriorityQueue<KlotskiState>(0, searchComparer);

            // starting states for search
            foreach (var st in startStates)
                searchQueue.Push(st);


            // for console reporting
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long lastReport = -1000;

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
                    Console.WriteLine($"Moves: {state.moveCount}  Depth: {state.depth}  Queue: {searchQueue.Count}  Visited: {visitedStates.Count()}");
                }

                // update maxDepthReached stat
                if (state.depth > searchContext.maxDepthReached)
                    searchContext.maxDepthReached = state.depth;
                if (state.moveCount > searchContext.maxMovesReached)
                    searchContext.maxMovesReached = state.moveCount;

                searchContext.visitedStates++;

                // check for end conditions

                if (state.matchesGoalState())
                {
                    // Solution found
                    searchContext.solutionStates.Add(state);

                    if (stopAtFirst)
                        return searchContext;

                    // Note: though this is a solution state, we continue to add it to the search queue and
                    // therefore we will be traversing paths that travel *through* the solution state.
                    // This is necessary for a full BFS enumeration as it will prune other longer paths to the
                    // same solution state.
                }

                //  There's a small problem with this pruning logic.
                //  It will sometimes prevent a more optimal solution from being found:
                //  If this history is, say, A,B,C and another history, A,C,B already in the queue
                //  leads to the same state, A,B,C will not be searched.
                //  However, in cases where the optimal solution is A,B,C,C..., it may not be found
                //  if the longer solution A,C,B,C is found first--4 moves instead of 3.
                //  The found solution will have minimal depth, but not necessarily the smallest move count.
                if (visitedStates.ContainsKey(state.canonicalString))
                {
                    ++searchContext.prunedAfterPop;
                    continue;
                }

                // {state} has now officially been visited: add to stats, history

                visitedStates[state.canonicalString] = state;

                searchContext.incrementDepthCounter(state);

                if (state.moveCount >= maxMoves)
                {
                    continue;
                }

                // enumerate all child states and add to queue

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

                        ++searchContext.prunedBeforePush;
                        continue;
                    }
*/
                    searchQueue.Push(childState);

                }   // foreach(childState)

            }   // while(queue not empty)

            searchContext.allStatesVisited = true;
            return searchContext;
        }   // void search()


        //  Finds a random state {depth} steps from {startState}.
        //  Shuffle is implemented as a random walk of length {depth} from {startState}, avoiding loops.
        //  This is nearly the search algorithm reversed, using a stack instead of a queue, for a depth-first search.
        //  Longer paths are preferred. The first valid path of length {depth} without a loop is returned.
        //  The algorithm uses the same dist-sq heuristic used by the quick search. Here higher values are used to
        //  guide the search further from the {startState}.
        public KlotskiState randomWalk(KlotskiState startState, int depth)
        {
            var random = new Random();

            // Keep track of all states visited to avoid backtracking or searching suboptimal paths.
            // These are keyed as canonical strings, because we consider a state visited even if identical tiles
            // are in exchanged positions.

            var visitedStates = new HashSet<string>();

            // stack is used for depth-first-search
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

                // use heuristic to prioritize states (presumably) further from the goal.
                // since a stack is last-in-first-out, the same heuristic used in solution searches
                // can be used here for the opposite effect, ordering near-goal states LATER in the search.
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
