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

        String _state;
        String _isoState=null;

		// data structure
        KlotskiProblemDefinition _context = null;
		KlotskiState _parent = null;
		List<KlotskiState> _children = null;
        int _moveCount = 0;
		char _movedPiece = '\0';
        public enum Direction { DOWN=1, RIGHT=2, UP=3, LEFT=4 };
        Direction _movedPieceDirection;

        public KlotskiState(KlotskiProblemDefinition context, String sz)
        {
            Debug.Assert(sz.Length == context.width * context.height);
            _context = context;
            _state = sz;
        }

        public KlotskiProblemDefinition context
        {
            get { return _context; }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is KlotskiState))
                throw (new Exception("Objects not same type"));

            KlotskiState state = obj as KlotskiState;

            return (state._state.Equals(this._state));
        }

        public bool isIsomorphicTo(KlotskiState state)
        {
            string sz1 = this.isoString;
            string sz2 = state.isoString;
            return (sz1.Equals(sz2));

        }

        public string isoString
        {
            get
            {
                if(_isoState==null)
                    _isoState = _context.isoMap(_state);
                return _isoState;
            }
        }

		public override int GetHashCode()
		{
			return _state.GetHashCode();
		}

        public override string ToString()
        {
            StringBuilder sz = new StringBuilder();
            for (int row = 0; row < context.height; ++row)
            {
                sz.AppendLine(_state.Substring(row * context.width, context.width));
            }
            return sz.ToString();
        }

        public void write()
        {
            Console.ForegroundColor = ConsoleColor.White;
            for (int row = 0; row < context.height; ++row)
            {
                Console.BackgroundColor = ConsoleColor.Black;
                Console.Write("     ");
                for (int col = 0; col < context.width; ++col)
                {
                    char tileId = this.tileAt(row, col);
                    ConsoleColor color = ConsoleColor.Black;
                    if(tileId>='a' && tileId<='z')
                        color = (ConsoleColor)(int)((tileId - 'a') % 8);
                    else if (tileId >= 'A' && tileId <= 'Z')
                        color = (ConsoleColor)(int)((tileId - 'A') % 8);
                    else if (tileId >= '0' && tileId <= '9')
                        color = (ConsoleColor)(int)((tileId - '0') % 8);
                    Console.BackgroundColor = color;
                    Console.Write(tileId);
                }
                Console.BackgroundColor = ConsoleColor.Black;
                Console.WriteLine();
            }
            Console.BackgroundColor = ConsoleColor.Black;
        }

        public char tileAt(int row, int col)
        {
            return _state[col+row*context.width];
        }

        public void setTileAt(int row, int col, char tileId)
        {
            int i =col+row*context.width;
            _state=_state.Substring(0,i)+tileId+_state.Substring(i+1);
            _isoState = null;
        }

        protected KlotskiState clone()
        {
            return new KlotskiState(_context, _state);
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

        // returns list of all possible states after one move
        public List<KlotskiState> getChildStates()
        {
			if (_children!=null)
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
         **/
		private void addChildIfNotBacktrack(KlotskiState child)
		{
			if (child==null)
				return;

            if (_children.IndexOf(child) >= 0)
                return;

			KlotskiState p = _parent;
			while (p!=null)
			{
				//if (p.Equals(child))
                if(p.isIsomorphicTo(child))
					return;
				p = p._parent;
			}

            child._moveCount = this._moveCount;
            if (child.movedPiece != this.movedPiece)
                ++child._moveCount;

			child._parent = this;
			_children.Add(child);
		}

        public KlotskiState parent
        {
            get { return _parent; }
        }

		public int depth
		{
			get
			{
				int d = 0;
				KlotskiState p = this._parent;
				while (p != null)
				{
					++d;
					p = p._parent;
				}
				return d;
			}
		}

        public int moveCount
        {
            get
            {
                return _moveCount;
            }
        }

        public char movedPiece
        {
            get { return _movedPiece; }
        }

        public Direction movedPieceDirection
        {
            get { return _movedPieceDirection; }
        }

        private void replaceTile(string tileId, string newId)
        {
            _state.Replace(tileId, newId);
        }

        public KlotskiState moveTileInto(int row, int col, Direction direction)
        {
            char tileId='\0';
            try
            {
                switch (direction)
                {
                    case Direction.DOWN:
                        tileId = this.tileAt(row-1, col);
                        break;
                    case Direction.RIGHT:
                        tileId = this.tileAt(row, col-1);
                        break;
                    case Direction.UP:
                        tileId = this.tileAt(row+1, col);
                        break;
                    case Direction.LEFT:
                        tileId = this.tileAt(row, col+1);
                        break;
                }
            }
            catch(Exception e)
            {
                //Console.WriteLine("Error: "+e);
                return null;
            }

            return moveTile(tileId, direction);
        }

        // attempts to move a tile. robust: in the event of an invalid
        // tile id, or impossible move, returns null
        protected KlotskiState moveTile(char tileId, Direction direction)
        {
            if (!((tileId >= '0' && tileId <= '9') 
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

			newstate._movedPiece = tileId;
            newstate._movedPieceDirection = direction;
			return newstate;
        }

    }
}
