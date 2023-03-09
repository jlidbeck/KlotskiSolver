using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KlotskiSolverApplication
{
    class KlotskiSearch
    {
        //
        // search
        //

        [DebuggerDisplay("Solutions={solutionStates.Count}, Visited={visitedStates}, MaxDepth={maxDepthReached}")]
        public class SearchContext
        {
            public bool canceled { get; internal set; } = false;

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
            internal void addToVisitedStates(KlotskiState state)
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

            public override string ToString()
            {
                if (canceled)
                    return $"[SEARCH CANCELED] Visited={visitedStates}, MaxDepth={maxDepthReached}, MaxMoves={maxMovesReached}";
                return $"Solutions={solutionStates.Count}, Visited={visitedStates}, MaxDepth={maxDepthReached}, MaxMoves={maxMovesReached}, AllStatesVisited={allStatesVisited}";
            }
        }
        //  Finds all states at the greatest distance from any solution state
        public static SearchContext findDeepestStates(KlotskiProblemDefinition pd, int depth)
        {
            // First, do a full graph traversal to find all reachable solution states
            var searchOptions = new SearchOptions
            {
                startStates = new List<KlotskiState> { pd.startState },
                searchComparer = new KlotskiState.MoveCountComparer(),
                maxMoves = depth,
                stopAtFirst = false
            };

            var searchResults = search(pd, searchOptions);

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
            searchOptions.startStates = searchResults.solutionStates;
            searchResults = search(pd, searchOptions);

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
        //public SearchContext search(SearchOptions searchOptions)
        //{
        //    searchOptions.startStates = new List<KlotskiState> { state };

        //    // if a history was provided, add all previous states to the visited set
        //    // not including the last state, which will be the start state for the search.
        //    var excludeStates = new List<KlotskiState>();
        //    for (var st = startState.parentState; st != null; st = st.parentState)
        //    {
        //        excludeStates.Add(st);
        //    }
        //    searchOptions.excludeStates = excludeStates;

        //    return search(searchOptions);
        //}

        public class SearchOptions
        {
            public IEnumerable<KlotskiState> startStates;
            public IEnumerable<KlotskiState> excludeStates = null;
            public IComparer<KlotskiState> searchComparer;
            public int maxMoves = 200;
            public bool stopAtFirst = true;
            public bool optimizePath = true;
            public bool storeVisitedStates = false;
        }

        public static SearchContext search(KlotskiProblemDefinition pd, SearchOptions searchOptions)
        {
            Trace.Assert(searchOptions.startStates?.Count() > 0, "One or more start states required");

            // if a history was provided, add all previous states to the visited set
            // not including the last state, which will be the start state for the search.
            if (searchOptions.excludeStates == null)
            {
                var excludeStates = new List<KlotskiState>();
                foreach (var startState in searchOptions.startStates)
                {
                    for (var st = startState.parentState; st != null; st = st.parentState)
                    {
                        excludeStates.Add(st);
                    }
                }
                searchOptions.excludeStates = excludeStates;
            }

            // Keep track of all states visited to avoid backtracking or searching suboptimal paths.
            // States are keyed as their canonical strings, because we consider a state visited even if identical tiles are exchanged.
            // We also store the complete state (as the value in the key/value pair), because this contains the
            // history.

            var visitedStates = new Dictionary<string, KlotskiState>();

            var searchContext = new SearchContext();

            // if a history was provided, add all previous states to the visited set
            // not including the last state, which will be the start state for the search.
            if (searchOptions.excludeStates != null)
            {
                foreach (var st in searchOptions.excludeStates)
                {
                    visitedStates[st.canonicalString] = st;
                }
            }

            // search queue for width-first-search
            var searchQueue = new MS.Internal.PriorityQueue<KlotskiState>(0, searchOptions.searchComparer);

            // starting states for search
            foreach (var st in searchOptions.startStates)
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
                    searchContext.canceled = true;
                    return searchContext;
                }

                // take one off the queue
                KlotskiState state = searchQueue.Top;
                searchQueue.Pop();

                // periodically output to console
                if (stopwatch.ElapsedMilliseconds - lastReport > 100)
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

                    if (searchOptions.stopAtFirst)
                    {
                        if (searchOptions.optimizePath)
                            while (optimizePath(state)) ;
                        return searchContext;
                    }

                    // Note: though this is a solution state, we continue to add it to the search queue and
                    // therefore we will be traversing paths that travel *through* the solution state.
                    // This is necessary for a full BFS enumeration as it will prune other longer paths to the
                    // same solution state.
                }

                //  There's a small problem with this pruning logic.
                //  It will sometimes prevent a more optimal solution from being found:
                //  If this history is, say, A,B,C and another history, A,C,B already in the queue
                //  leads to the same state, A,B,C will not be searched.
                //  In this situation, if the optimal solution is A,B,C,C..., it won't be found--
                //  A,C,B,C... will be found instead.
                //  The found solution is still guaranteed have minimal depth, but not necessarily the
                //  smallest move count.
                if (visitedStates.ContainsKey(state.canonicalString))
                {
                    ++searchContext.prunedAfterPop;
                    continue;
                }

                // {state} has now officially been visited: add to stats, history

                visitedStates[state.canonicalString] = state;

                if (searchOptions.storeVisitedStates)
                {
                    searchContext.addToVisitedStates(state);
                }

                if (state.moveCount >= searchOptions.maxMoves)
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

                        //Debug.Assert(childState.moveCount >= visitedStates[childState.canonicalString].moveCount);

                        ++searchContext.prunedBeforePush;
                        continue;
                    }*/

                    searchQueue.Push(childState);

                }   // foreach(childState)

            }   // while(queue not empty)

            searchContext.allStatesVisited = true;
            return searchContext;
        }   // void search()

        // search path for moves that can be combined if the steps are reordered
        private static bool optimizePath(KlotskiState finalState)
        {
            for (KlotskiState state = finalState; state != null; state = state.parentState)
            {
                var prevState = state.parentState;
                if (prevState == null)
                    break;

                // find the previous step that moved tile that moved in {state}.
                // this will be the swap candidate.
                // if it's immediately preceding {state}, there's no need to test this swap--continue the outer loop

                if (prevState.tileMove.tile == state.tileMove.tile)
                    continue;

                var swapCandidate = prevState.parentState;
                while (swapCandidate != null && swapCandidate.tileMove.tile != state.tileMove.tile)
                    swapCandidate = swapCandidate.parentState;
                if (swapCandidate == null)
                    continue;

                // if the previous moves were    S, A', B, C, ..., Y, Z,  A'',
                // see if they can be reordered  S, B,  C, ..., Y, Z, A', A''

                // apply the moves SC+1, SC+2, ... ST-1
                // in order
                List<KlotskiState.TileMove> moves = new List<KlotskiState.TileMove>();
                for (var st = prevState; st != swapCandidate; st = st.parentState)
                    moves.Add(st.tileMove);
                moves.Reverse();
                moves.Add(swapCandidate.tileMove);

                var newStates = new List<KlotskiState>();
                var newState = swapCandidate.parentState;
                foreach (var move in moves)
                {
                    newState = newState?.moveTile(move.tile, move.direction);
                    newStates.Add(newState);
                }

                if (newState != null)
                {
                    if (newState?.stateString == prevState.stateString)
                    {
                        // the last 2 moves can be reordered.
                        //Console.WriteLine($"Rotating moves {swapCandidate.depth}-{state.depth}:\n{swapCandidate} ...\n{state}");
                        //Console.WriteLine($"After:\n{newStates[0]}\n{newState}");

                        newStates[0].parentState = swapCandidate.parentState;
                        state.parentState = newState;

                        // todo: nextState and beyond will have to be re-parented, and the move counts updated

                        //nextState.parentState = state;
                        state = swapCandidate;
                    }
                }
            }

            Dictionary<string, KlotskiState> states = new Dictionary<string, KlotskiState>();
            for (KlotskiState state = finalState; state != null; state = state.parentState)
            {
                if (states.ContainsKey(state.stateString))
                {
                    // repeated state--cut out the intermediate loop
                    states[state.stateString].parentState = state.parentState;
                }
                else
                {
                    states[state.stateString] = state;
                }
            }

            int moveCountOld = finalState.moveCount;
            updateMoveCounts(finalState);
            Console.WriteLine($"Optimized path: {moveCountOld} --> {finalState.moveCount}");
            return (finalState.moveCount < moveCountOld);
        }


        static void updateMoveCounts(KlotskiState finalState)
        {
            var history = new List<KlotskiState>();
            for (KlotskiState state = finalState; state != null; state = state.parentState)
            {
                history.Add(state);
            }
            history.Reverse();

            foreach (var state in history)
            {
                if (state.parentState == null)
                    continue;
                state.moveCount = (state.parentState.tileMove.tile == state.tileMove.tile)
                    ? state.parentState.moveCount
                    : state.parentState.moveCount + 1;
            }
        }


        //  Finds a random state {depth} steps from {startState}.
        //  Shuffle is implemented as a random walk of length {depth} from {startState}, avoiding loops.
        //  This is nearly the search algorithm reversed, using a stack instead of a queue, for a depth-first search.
        //  Longer paths are preferred. The first valid path of length {depth} without a loop is returned.
        //  The algorithm uses the same dist-sq heuristic used by the quick search. Here higher values are used to
        //  guide the search further from the {startState}.
        public static KlotskiState randomWalk(KlotskiState startState, int depth)
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
                    Console.WriteLine($"Depth: {state.moveCount} / {state.depth} Score:{state.getDistanceEstimate()} Stack:{searchStack.Count} Visited:{visitedStates.Count()}");
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
                children.Sort(new KlotskiState.DistanceHeuristicComparer(random.Next(10), 2));

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
