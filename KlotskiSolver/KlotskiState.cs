using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace KlotskiSolverApplication
{
    //  Represents a board state of a specific KlotskiProblemDefinition.
    //  States make up a tree data structure, with each state linked to its parent state
    //  and potentially multiple child states.

    [DebuggerDisplay("{stateString} {tileMove} Move={moveCount} Depth={depth}")]
    class KlotskiState
    {
        public const char EMPTY = ' ';

        // serialized state descriptor
        public string stateString { get; private set; } = null;

        // serialized state descriptor with tile IDs replaced with type IDs
        string _canonicalString = null;

		// data structure
        public KlotskiProblemDefinition context { get; private set; } = null;
		public KlotskiState parentState { get; set; } = null;
		List<KlotskiState> _children = null;

		// number of steps from startState
        public int depth { get; private set; } = 0;

        // piece move count. this can be less than {depth} because consecutive moves of the same tile are only counted once
        public int moveCount { get; set; } = 0;

        // data describing the move transforming parent state to this state
        //public char movedTile => tileMove.tile;
        public enum Direction { DOWN=1, RIGHT=2, UP=3, LEFT=4 };
        //public Direction movedTileDirection => tileMove.direction;

        [DebuggerDisplay("[{tile.ToString()} {direction}]")]
        public struct TileMove
        {
            public char tile;
            public Direction direction;

            public override string ToString()
            {
                if (tile == '\0')
                    return "[]";
                return $"[{tile.ToString()} {direction}]";
            }

            public static TileMove Empty = new TileMove { tile = '\0' };
        }

        public TileMove tileMove { get; private set; } = new TileMove { tile = '\0' };

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

        //  DistanceHeuristicComparer
        //  This heuristic comparison function estimates the distance from the goal state using the sum of the
        //  distance squared for each tile cell specified in the goal state. It is not guaranteed to find an
        //  optimal solution, but it will probably find a solution much faster.
        //  Note that for best performance a larger depth cutoff should be used, well more than the minimum.
        //  This comparer does especially well when the goal state specifies multiple tiles, such as the 15-style
        //  "boss" slider puzzles.
        //  The move count is given some weight, to help avoid getting stuck in local minima.
        //  A higher moveCountWeight searches tend to take longer but find shorter solutions.
        public class DistanceHeuristicComparer : IComparer<KlotskiState>
        {
            public int distanceWeight { get; private set; } = 7;
            public int moveCountWeight { get; private set; } = 2;

            public DistanceHeuristicComparer(int distanceWeight, int moveCountWeight)
            {
                this.distanceWeight = distanceWeight;
                this.moveCountWeight = moveCountWeight;
            }

            public override string ToString()
            {
                return $"DistanceSquaredHeuristic {distanceWeight}/{moveCountWeight}";
            }

            public int Compare(KlotskiState x, KlotskiState y)
            {
                return distanceWeight    * ( x.getDistanceEstimate() - y.getDistanceEstimate() )
                     + moveCountWeight * ( x.moveCount             - y.moveCount             );
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

        //  Estimate distance from this state to a goalState state.
        //  This metric uses the total distance squared from the goalState squares to the nearest part of the goalState tile.
        public int getDistanceEstimate()
        {
            int distance = 0;
            var goalState = context.goalState.stateString;

            // for each non-blank in the goal state..
            for (int i = 0; i < goalState.Length; ++i)
            {
                char c = goalState[i];
                if (c == ' ') continue;

                // find the nearest matching square (by Taxicab distance)

                int ix = i % context.width;
                int iy = i / context.width;

                int minDist = int.MaxValue;
                for (int j = 0; j < stateString.Length; ++j)
                {
                    if (stateString[j] == c)
                    {
                        int jx = j % context.width;
                        int jy = j / context.width;
                        int dist = Math.Abs(ix - jx) + Math.Abs(iy - jy);
                        if (dist < minDist)
                            minDist = dist;
                    }
                }

                distance += minDist;

                // if the solution square is occupied by a different tile, increment the distance
                if (stateString[i] != ' ' && this.stateString[i] != goalState[i])
                    distance++;
            }

            return distance;
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
        public void write(bool big)
        {
            var fg = Console.ForegroundColor;
            var bg = Console.BackgroundColor;

            // tiles are drawn using spaces with background color = tile color.
            // text is black

            Console.ForegroundColor = ConsoleColor.Black;

            // used to ensure that each tile's ID is printed only once
            var tileIdsPrinted = new HashSet<char>();

            if (big)
            {
                for (int row = 0; row < context.height; ++row)
                {
                    for (int i = 0; i < 2; ++i)
                    {
                        Console.BackgroundColor = ConsoleColor.Black;
                        Console.Write("     ");

                        // note that we're iterating one column past the end, to draw the side shading
                        for (int col = 0; col <= context.width; ++col)
                        {
                            char tileId = this.tileAt(row, col);

                            // render out-of-bounds as an empty square
                            if (tileId == '\0')
                                tileId = ' ';
                            
                            Console.BackgroundColor = context.tileColorMap[tileId];
                            if (tileId == ' ' && this.tileAt(row, col - 1) > 0 && this.tileAt(row, col - 1) != ' ')
                            {
                                // render a tile's rightmost edge differently
                                Console.BackgroundColor = context.tileColorMap[this.tileAt(row, col - 1)];
                                Console.Write("▓");
                                Console.BackgroundColor = context.tileColorMap[tileId];
                                Console.Write("   ");
                            }
                            else if (tileIdsPrinted.Contains(tileId))
                            {
                                // render a tile or empty square
                                Console.Write("    ");
                            }
                            else
                            {
                                // render the top-left corner of a tile
                                tileIdsPrinted.Add(tileId);
                                Console.Write(tileId);
                                Console.Write("   ");
                            }
                        }

                        Console.BackgroundColor = ConsoleColor.Black;
                        Console.WriteLine();
                    }
                }
            }
            else
            {
                for (int row = 0; row < context.height; ++row)
                {
                    Console.BackgroundColor = ConsoleColor.Black;
                    Console.Write("     ");
                    for (int col = 0; col < context.width; ++col)
                    {
                        char tileId = this.tileAt(row, col);

                        Console.BackgroundColor = context.tileColorMap[tileId];
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
            }

            Console.BackgroundColor = bg;
            Console.ForegroundColor = fg;
        }

        // Renders the state to the console output
        public void writeToConsole(int cx, int cy)
        {
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

                    Console.BackgroundColor = context.tileColorMap[tileId];
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
            string sz = "" + tileMove.tile;
            for (var i = parentState; i != null && i.tileMove.tile != '\0'; i = i.parentState)
            {
                if (i.tileMove.tile != tileId)
                {
                    tileId = i.tileMove.tile;
                    sz = "" + i.tileMove.tile + sz;
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

        // detach this state from its history
        public void detach()
        {
            _children = null;
            parentState = null;
            depth = 0;
            moveCount = 0;
            tileMove = TileMove.Empty;
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
            if (child.tileMove.tile != this.tileMove.tile)
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

        // Attempts to move a tile.
        // Returns a state object representing the move if it is valid--valid tile,
        // no collisions--otherwise returns null.
        // Does not modify {this}.
        public KlotskiState moveTile(char tileId, Direction direction)
        {
            if (!( (tileId >= '0' && tileId <= '9') 
                || (tileId >= 'A' && tileId <= 'Z') 
                || (tileId >= 'a' && tileId <= 'z')))
                return null;

            KlotskiState newstate = this.clone();

            int tilesToMove = 0;

            for (int row = 0; row < context.height; ++row)
            {
                for (int col = 0; col < context.width; ++col)
                {
                    if (this.tileAt(row, col) == tileId)
                    {
                        switch(direction)
                        {
                            case Direction.DOWN:
                                if (row >= context.height - 1)
                                    return null;   // no room to move down

                                if (this.tileAt(row + 1, col) != EMPTY && this.tileAt(row + 1, col) != tileId)
                                    return null;   // different tile in the way

                                // move tile down
                                newstate.setTileAt(row + 1, col, tileId);

                                // replace with empty, unless part of tile extending upward
                                if (row <= 0 || this.tileAt(row - 1, col) != tileId)
                                {
                                    // top of tile: replace with empty
                                    newstate.setTileAt(row, col, EMPTY);
                                }
                                break;

                            case Direction.RIGHT:
                                if (col >= context.width - 1)
                                    return null;   // no room to move right

                                if (this.tileAt(row, col+1) != EMPTY && this.tileAt(row, col+1) != tileId)
                                    return null;   // different tile in the way

                                // move tile right
                                newstate.setTileAt(row, col+1, tileId);

                                // replace with empty, unless part of tile extending leftward
                                if (col <= 0 || this.tileAt(row, col-1) != tileId)
                                {
                                    // top of tile: replace with empty
                                    newstate.setTileAt(row, col, EMPTY);
                                }
                                break;

                            case Direction.UP:
                                if (row <=0)
                                    return null;   // no room to move up

                                if (this.tileAt(row - 1, col) != EMPTY && this.tileAt(row - 1, col) != tileId)
                                    return null;   // different tile in the way

                                // move tile up
                                newstate.setTileAt(row - 1, col, tileId);

                                // replace with empty, unless part of tile extending downward
                                if (row >=context.height-1 || this.tileAt(row + 1, col) != tileId)
                                {
                                    // top of tile: replace with empty
                                    newstate.setTileAt(row, col, EMPTY);
                                }
                                break;

                            case Direction.LEFT:
                                if (col <=0)
                                    return null;   // no room to move left

                                if (this.tileAt(row, col - 1) != EMPTY && this.tileAt(row, col - 1) != tileId)
                                    return null;   // different tile in the way

                                // move tile left
                                newstate.setTileAt(row, col - 1, tileId);

                                // replace with empty, unless part of tile extending rightward
                                if (col >=context.width-1 || this.tileAt(row, col + 1) != tileId)
                                {
                                    // top of tile: replace with empty
                                    newstate.setTileAt(row, col, EMPTY);
                                }
                                break;

                            default:
                                throw(new Exception("Invalid direction"));
                        }

                        ++tilesToMove;
                    }
                }
            }

            if (tilesToMove <= 0)
                return null;

            //if (tileMove.tile == '1' && (direction == LEFT || direction == RIGHT))
            //	Console.WriteLine("moved tile 1: "+direction);

            newstate.tileMove = new TileMove { tile = tileId, direction = direction };
            newstate.parentState = this;

			return newstate;
        }

    }
}
