using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace KlotskiSolverApplication
{
    class KlotskiState
    {
        public const char EMPTY = ' ';

        public String stateString { get; private set; } = null;
        String _canonicalString = null;

		// data structure
        public KlotskiProblemDefinition context { get; private set; } = null;
		public KlotskiState parentState { get; private set; } = null;
		List<KlotskiState> _children = null;

        // piece move count. this can be less than {depth} because consecutive moves of the same tile are only counted once
        public int moveCount { get; private set; } = 0;
        
		public char movedPiece { get; private set; } = '\0';
        public enum Direction { DOWN=1, RIGHT=2, UP=3, LEFT=4 };
        public Direction movedPieceDirection { get; private set; }

        public KlotskiState(KlotskiProblemDefinition context, String sz)
        {
            Trace.Assert(sz.Length == context.width * context.height,
                        "Invalid state string: string length must be equal to width x height");
            this.context = context;
            stateString = sz;
        }

        //  Comparison override: true if states are identical (not just isomorphic)
        //  Allows functionality such as Array.IndexOf, etc.
        public override bool Equals(object obj)
        {
            if (!(obj is KlotskiState))
                throw (new Exception("Objects not same type"));

            KlotskiState state = obj as KlotskiState;

            return (state.stateString.Equals(this.stateString));
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

		public override int GetHashCode()
		{
			return stateString.GetHashCode();
		}

        public override string ToString()
        {
            StringBuilder sz = new StringBuilder();
            for (int row = 0; row < context.height; ++row)
            {
                sz.AppendLine(stateString.Substring(row * context.width, context.width));
            }
            return sz.ToString();
        }

        public void write()
        {
            var colors = new ConsoleColor[15] {
                ConsoleColor.White,
                ConsoleColor.Yellow,
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
                ConsoleColor.DarkBlue
                };

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
                    ConsoleColor color = ConsoleColor.Black;
                    if (tileId >= 'a' && tileId <= 'z')
                        color = colors[(tileId - 'a') % 15];
                    else if (tileId >= 'A' && tileId <= 'Z')
                        color = colors[(tileId - 'A') % 15];
                    else if (tileId >= '0' && tileId <= '9')
                        color = colors[(tileId - '0') % 15];
                    Console.BackgroundColor = color;
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

            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Depth: " + depth + " Moves: " + moveCount);
        }

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

        protected KlotskiState clone()
        {
            return new KlotskiState(context, stateString);
        }

        //

		public int countPossibleConfigurations(int maxMoves)
		{
			int count = 1;
			if (maxMoves > 0)
			{
				List<KlotskiState> children = this.getChildStates();
				foreach (KlotskiState state in children)
				{
					count += state.countPossibleConfigurations(maxMoves - 1);
				}
			}
			return count;
		}

        public List<KlotskiState> getPossibleConfigurations(int maxMoves)
        {
            List<KlotskiState> a = new List<KlotskiState>();
            a.Add(this);

            int i=0;
            while(maxMoves>0 && i<a.Count)
            {
                // grab next item in queue
                KlotskiState state=a[i];
                ++i;

                List<KlotskiState> a2=state.getChildStates();
                foreach(KlotskiState newstate in a2)
                {
                    if(a.IndexOf(newstate)<0)
                        a.Add(newstate);
                }

                --maxMoves;
            }

            return a;
        }

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

        /**
         * adds child to _children, unless
         *  - child is null
         *  - child is duplicate of direct ancestor
         *  - child is duplicate of another element in _children
         *  where isomorphic states are considered duplicates.
         **/
        private void addChildIfNotBacktrack(KlotskiState child)
        {
            if (child == null)
                return;

            if (_children.IndexOf(child) >= 0)
                return;

            KlotskiState p = parentState;
            while (p != null)
            {
                //if (p.Equals(child))
                if (p.isIsomorphicTo(child))
                    return;
                p = p.parentState;
            }

            child.moveCount = this.moveCount;
            if (child.movedPiece != this.movedPiece)
                ++child.moveCount;

            child.parentState = this;
            _children.Add(child);
        }

		public int depth
		{
			get
			{
				int d = 0;
				KlotskiState p = this.parentState;
				while (p != null)
				{
					++d;
					p = p.parentState;
				}
				return d;
			}
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

			//if (tileId == '1' && (direction == LEFT || direction == RIGHT))
			//	Console.WriteLine("moved tile 1: "+direction);

			newstate.movedPiece = tileId;
            newstate.movedPieceDirection = direction;
			return newstate;
        }

    }
}
