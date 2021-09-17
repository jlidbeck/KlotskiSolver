using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;

namespace KlotskiSolverApplication
{
    //  Represents a board state of a specific KlotskiProblemDefinition.
    //  States make up a tree data structure, with each state linked to its parent state
    //  and potentially multiple child states.

    [DebuggerDisplay("{stateString} [{movedPiece.ToString()} {movedPieceDirection}] Move={moveCount} Depth={depth}")]
    class KlotskiState
    {
        public const char EMPTY = ' ';

        // serialized state descriptor
        public String stateString { get; private set; } = null;

        // serialized state descriptor with tile IDs replaced with type IDs
        String _canonicalString = null;

		[JsonIgnore]
        public KlotskiProblemDefinition context { get; private set; } = null;

        [JsonIgnore]
        public KlotskiState parentState { get; private set; } = null;
		
        List<KlotskiState> _children = null;

		// number of steps from startState
        public int depth { get; private set; } = 0;

        // piece move count. this can be less than {depth} because consecutive moves of the same tile are only counted once
        public int moveCount { get; private set; } = 0;
        
        // data describing the move transforming parent state to this state
		public char movedPiece { get; private set; } = '\0';
        public enum Direction { DOWN=1, RIGHT=2, UP=3, LEFT=4 };
        public Direction movedPieceDirection { get; private set; }


        #region Construction

        public KlotskiState(KlotskiProblemDefinition context, String stateString)
        {
            Trace.Assert(context != null, "Context cannot be null");
            Trace.Assert(stateString != null && stateString.Length == context.width * context.height,
                        "Invalid state string: string length must be equal to width x height");

            this.context = context;
            this.stateString = stateString;
        }

        //  Creates an orphan state object, without a history
        public KlotskiState clone()
        {
            return new KlotskiState(context, stateString);
        }

        #endregion

        #region Comparison

        public override int GetHashCode()
        {
            return stateString.GetHashCode();
        }

        //  Comparison override: true if states are identical (not just isomorphic)
        //  Allows functionality such as Array.IndexOf, etc.
        public override bool Equals(object obj)
        {
            KlotskiState state = obj as KlotskiState;

            return (this.stateString.Equals(state?.stateString));
        }

        //  Returns true if this state is equivalent to {state},
        //  allowing for identical (same type) tiles to be exchanged
        public bool isIsomorphicTo(KlotskiState state)
        {
            string sz1 = this.canonicalString;
            string sz2 = state.canonicalString;
            return (sz1.Equals(sz2));
        }

        //  Returns a canonical form for the state, with tile IDs replaced with tile type IDs,
        //  for isomorphic state comparisons
        [JsonIgnore]
        public string canonicalString
        {
            get
            {
                if (_canonicalString == null)
                    _canonicalString = context.getCanonicalStateString(stateString);
                return _canonicalString;
            }
        }

        #endregion

        #region Comparers

        //  Comparers used by priority queues, stacks, Sort, etc.

        //  MoveCountComparer:
        //  This compare function guarantees that an optimal shortest-path solution is found,
        //  because shorter paths (by move count) are always given higher priority.
        //  In case of tie, depth is used, giving priority to shorter tile movements.
        public class MoveCountComparer : IComparer<KlotskiState>
        {
            public int Compare(KlotskiState x, KlotskiState y)
            {
                if (x.moveCount == y.moveCount)
                    return x.depth - y.depth;
                return x.moveCount - y.moveCount;
            }
        }

        //  DistanceSquaredHeuristicComparer
        //  This heuristic comparison function estimates the distance from the goal state using the sum of the
        //  distance squared for each tile cell specified in the goal state. It is not guaranteed to find an
        //  optimal solution, but it will probably find a solution much faster.
        //  Note that for best performance a larger depth cutoff should be used, well more than the minimum.
        //  This comparer does especially well when the goal state specifies multiple tiles, such as the 15-slider
        //  puzzle.
        //  The move count is given some weight, to help avoid getting stuck in local minima.
        //
        public class DistanceSquaredHeuristicComparer : IComparer<KlotskiState>
        {
            public int distSqWeight { get; private set; } = 7;
            public int moveCountWeight { get; private set; } = 2;

            public DistanceSquaredHeuristicComparer(int distSqWeight, int moveCountWeight)
            {
                this.distSqWeight = distSqWeight;
                this.moveCountWeight = moveCountWeight;
            }

            public override string ToString()
            {
                return $"DistanceSquaredHeuristic {distSqWeight}/{moveCountWeight}";
            }

            public int Compare(KlotskiState x, KlotskiState y)
            {
                return distSqWeight    * ( x.getDistanceSquareScore() - y.getDistanceSquareScore() )
                     + moveCountWeight * ( x.moveCount                - y.moveCount                );
            }
        }

        #endregion

        #region Goal state checking / heuristics

        //  Determine whether this state satisfies {goalState}, that is,
        //  all non-blank values in {goalStateString} match values in this.stateString.
        public bool matchesGoalState()
        {
            var goal = context.goalState.stateString;
            for (int i = 0; i < goal.Length; ++i)
            {
                if (goal[i] != ' ' && this.stateString[i] != goal[i])
                    return false;
            }

            return true;
        }

        //  Estimate distance for all tiles indicated in goal state.
        //  Lower scores indicate this state is expected to be closer to the goal state.
        public int getDistanceSquareScore()
        {
            int diffCount = 0;
            var goal = context.goalState.stateString;

            // for each non-blank in the goal state..
            for (int i = 0; i < goal.Length; ++i)
            {
                char c = goal[i];
                if (c == ' ') continue;

                // find the nearest matching square (by Euclidean distance)

                int ix = i % context.width;
                int iy = i / context.width;

                int minDist = int.MaxValue;
                for (int j = 0; j < stateString.Length; ++j)
                {
                    if (stateString[j] == c)
                    {
                        int jx = j % context.width;
                        int jy = j / context.width;
                        int dist = (ix - jx) * (ix - jx) + (iy - jy) * (iy - jy);
                        if (dist < minDist)
                            minDist = dist;
                    }
                }

                diffCount += minDist;
            }

            return diffCount;
        }

        #endregion

        #region serialization

        public override string ToString()
        {
            StringBuilder sz = new StringBuilder();
            for (int row = 0; row < context.height; ++row)
            {
                sz.AppendLine(stateString.Substring(row * context.width, context.width));
            }
            return sz.ToString();
        }

        public List<string> ToStrings()
        {
            var asz = new List<string>();
            for (int row = 0; row < context.height; ++row)
            {
                asz.Add(stateString.Substring(row * context.width, context.width));
            }
            return asz;
        }


        // Renders the state to the console output
        public void write()
        {
            var fg = Console.ForegroundColor;
            var bg = Console.BackgroundColor;

            Console.ForegroundColor = ConsoleColor.Black;

            // only print each tile ID char once
            var tileIdsPrinted = new HashSet<char>();

            for (int row = 0; row < context.height; ++row)
            {
                Console.BackgroundColor = ConsoleColor.Black;
                Console.Write("     ");
                for (int col = 0; col < context.width; ++col)
                {
                    char tileId = this.tileAt(row, col);

                    Console.BackgroundColor = context.tileIdToColorMap[tileId];
                    if (tileIdsPrinted.Contains(tileId))
                    {
                        Console.Write("  ");
                    }
                    else
                    {
                        tileIdsPrinted.Add(tileId);
                        Console.Write(tileId);
                        Console.Write(' ');
                    }
                }
                Console.BackgroundColor = ConsoleColor.Black;
                Console.WriteLine();
            }

            Console.BackgroundColor = bg;
            Console.ForegroundColor = fg;
        }

        // Renders the state to the console output
        public void writeToConsole(int cx, int cy)
        {
            if(cy + context.height >= Console.BufferHeight)
            {
                // not enough room in buffer...
                for (int i = 0; i < context.height; ++i)
                    Console.WriteLine();
            }

            var fg = Console.ForegroundColor;
            var bg = Console.BackgroundColor;

            Console.ForegroundColor = ConsoleColor.Black;

            // only print each tile ID char once
            var tileIdsPrinted = new HashSet<char>();

            for (int row = 0; row < context.height; ++row)
            {
                Console.CursorLeft = cx;
                Console.CursorTop = cy + row;

                for (int col = 0; col < context.width; ++col)
                {
                    char tileId = this.tileAt(row, col);

                    Console.BackgroundColor = context.tileIdToColorMap[tileId];
                    if (tileIdsPrinted.Contains(tileId))
                    {
                        Console.Write("  ");
                    }
                    else
                    {
                        tileIdsPrinted.Add(tileId);
                        Console.Write(tileId);
                        Console.Write(' ');
                    }
                }
                Console.BackgroundColor = ConsoleColor.Black;
            }

            Console.BackgroundColor = bg;
            Console.ForegroundColor = fg;
        }

        public string getHistoryString()
        {
            char tileId = '\0';
            string sz = "" + movedPiece;
            for (var i = parentState; i != null && i.movedPiece != '\0'; i = i.parentState)
            {
                if (i.movedPiece != tileId)
                {
                    tileId = i.movedPiece;
                    sz = "" + i.movedPiece + sz;
                }
            }
            return sz;
        }

        #endregion

        public char tileAt(int row, int col)
        {
            if (row < 0 || row >= context.height || col < 0 || col >= context.width)
                return '\0';

            return stateString[col + row * context.width];
        }

        private void setTileAt(int row, int col, char tileId)
        {
            var sz = stateString.ToCharArray();
            int i = col + row * context.width;
            sz[i] = tileId;
            stateString = new string(sz);

            // state has changed, canonical string is now stale, so invalidate it
            _canonicalString = null;
            _children = null;
        }

        //
        /*
		public int countReachableStates(int maxMoves)
		{
			int count = 1;
			if (maxMoves > 0)
			{
				List<KlotskiState> children = this.getChildStates();
				foreach (KlotskiState state in children)
				{
					count += state.countReachableStates(maxMoves - 1);
				}
			}
			return count;
		}

        public List<KlotskiState> findUniqueReachableStates(int maxMoves)
        {
            var states = new List<KlotskiState>();
            states.Add(this);

            int i = 0;
            while (maxMoves > 0 && i < states.Count)
            {
                // grab next item in queue
                KlotskiState state = states[i];
                ++i;

                List<KlotskiState> a2 = state.getChildStates();
                foreach (KlotskiState newstate in a2)
                {
                    if (states.IndexOf(newstate) < 0)
                        states.Add(newstate);
                }

                --maxMoves;
            }

            return states;
        }
        */
        //  Returns list of all reachable states one move away.
        //  States are unique: all returned states are non-isomorphic to each other, as well as to all historical states.
        public List<KlotskiState> getChildStates()
        {
			if (_children != null)
				return _children;

            _children = new List<KlotskiState>();

            // find all empty spaces;
            // for each empty square, try all ways to fill it

            for (int row = 0; row < context.height; ++row)
            {
                for (int col = 0; col < context.width; ++col)
                {
                    if (this.tileAt(row, col) == EMPTY)
                    {
                        addChildIfNotBacktrack(this.moveTileInto(row, col, Direction.DOWN));
                        addChildIfNotBacktrack(this.moveTileInto(row, col, Direction.RIGHT));
                        addChildIfNotBacktrack(this.moveTileInto(row, col, Direction.UP));
                        addChildIfNotBacktrack(this.moveTileInto(row, col, Direction.LEFT));
                    }
                }
            }

			return _children;
        }

        public void detach()
        {
            _children = null;
            parentState = null;
            depth = 0;
            moveCount = 0;
            movedPiece = '\0';
        }

        public void clearChildStates()
        {
            _children = null;
        }

        /**
         * adds child to this._children, unless:
         *  - child is null, or
         *  - child is exact duplicate of another element in _children, or
         *  - child is isomorphic to a direct ancestor
         **/
        private void addChildIfNotBacktrack(KlotskiState child)
        {
            if (child == null)
                return;

            if (_children.FindIndex(st => st.stateString == child.stateString) >= 0)
                return;

            // Check history--if child is isomorphic to any previous state, do not add
            for (KlotskiState p = parentState; p != null; p = p.parentState)
            {
                if (p.isIsomorphicTo(child))
                    return;
            }

            child.depth = this.depth + 1;

            child.moveCount = this.moveCount;
            if (child.movedPiece != this.movedPiece)
                ++child.moveCount;

            child.parentState = this;

            _children.Add(child);
        }

        private void replaceTile(string tileId, string newId)
        {
            stateString.Replace(tileId, newId);
        }

        public KlotskiState moveTileInto(int row, int col, Direction direction)
        {
            char tileId = '\0';
            try
            {
                switch (direction)
                {
                    case Direction.DOWN:
                        tileId = this.tileAt(row - 1, col);
                        break;
                    case Direction.RIGHT:
                        tileId = this.tileAt(row, col - 1);
                        break;
                    case Direction.UP:
                        tileId = this.tileAt(row + 1, col);
                        break;
                    case Direction.LEFT:
                        tileId = this.tileAt(row, col + 1);
                        break;
                }

                if (tileId != '\0')
                {
                    return moveTile(tileId, direction);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e);
            }

            return null;
        }

        // attempts to move a tile. robust: in the event of an invalid
        // tile id, or impossible move, returns null
        protected KlotskiState moveTile(char tileId, Direction direction)
        {
            if (!( (tileId >= '0' && tileId <= '9') 
                || (tileId >= 'A' && tileId <= 'Z') 
                || (tileId >= 'a' && tileId <= 'z')))
                return null;

            //KlotskiState newstate = this.clone();
            var newStateString = stateString.ToArray();

            int tilesToMove = 0;

            for (int idx = 0; idx < stateString.Length; ++idx)
            {
                if (stateString[idx] == tileId)
                {
                    int col = idx % context.width;
                    int row = idx / context.width;

                    switch (direction)
                    {
                        case Direction.DOWN:
                            if (row >= context.height - 1)
                                return null;   // no room to move down

                            if (stateString[idx + context.width] != EMPTY && stateString[idx + context.width] != tileId)
                                return null;   // different tile in the way

                            // move tile down
                            newStateString[idx + context.width] = tileId;

                            // replace with empty, unless part of tile extending upward
                            if (row <= 0 || stateString[idx - context.width] != tileId)
                            {
                                // top of tile: replace with empty
                                newStateString[idx] = EMPTY;
                            }
                            break;

                        case Direction.RIGHT:
                            if (col >= context.width - 1)
                                return null;   // no room to move right

                            if (stateString[idx + 1] != EMPTY && stateString[idx + 1] != tileId)
                                return null;   // different tile in the way

                            // move tile right
                            newStateString[idx + 1] = tileId;

                            // replace with empty, unless part of tile extending leftward
                            if (col <= 0 || stateString[idx - 1] != tileId)
                            {
                                // top of tile: replace with empty
                                newStateString[idx] = EMPTY;
                            }
                            break;

                        case Direction.UP:
                            if (row <= 0)
                                return null;   // no room to move up

                            if (stateString[idx - context.width] != EMPTY && stateString[idx - context.width] != tileId)
                                return null;   // different tile in the way

                            // move tile up
                            newStateString[idx - context.width] = tileId;

                            // replace with empty, unless part of tile extending downward
                            if (row >= context.height - 1 || stateString[idx + context.width] != tileId)
                            {
                                // top of tile: replace with empty
                                newStateString[idx] = EMPTY;
                            }
                            break;

                        case Direction.LEFT:
                            if (col <= 0)
                                return null;   // no room to move left

                            if (stateString[idx - 1] != EMPTY && stateString[idx - 1] != tileId)
                                return null;   // different tile in the way

                            // move tile left
                            newStateString[idx - 1] = tileId;

                            // replace with empty, unless part of tile extending rightward
                            if (col >= context.width - 1 || stateString[idx + 1] != tileId)
                            {
                                // top of tile: replace with empty
                                newStateString[idx] = EMPTY;
                            }
                            break;

                        default:
                            throw (new Exception("Invalid direction"));
                    }

                    ++tilesToMove;
                }
            }

            if (tilesToMove <= 0)
                return null;

            var newstate = new KlotskiState(context, new string(newStateString));

            newstate.movedPiece = tileId;
            newstate.movedPieceDirection = direction;
			return newstate;
        }

    }
}
