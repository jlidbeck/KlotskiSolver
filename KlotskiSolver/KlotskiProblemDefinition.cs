using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace KlotskiSolverApplication
{
    class KlotskiProblemDefinition
    {
        int _width;
        int _height;
        KlotskiState _solution;
        KlotskiState _startState;
        string _isoMap;

        public KlotskiProblemDefinition(int width, int height, string szStart, string szSolution, string isoMap)
        {
            _width = width;
            _height = height;
            _isoMap = isoMap;
            _solution = new KlotskiState(this, szSolution);
            _startState = new KlotskiState(this, szStart);
        }

        public bool isSolution(KlotskiState state)
        {
			if (state.tileAt(4, 1) == 'A' && state.tileAt(4,2)=='A')
				return true;

            for (int row = 0; row < _height; ++row)
            {
                for (int col = 0; col < _width; ++col)
                {
                    if (_solution.tileAt(row, col) != ' ')
                    {
                        if (state.tileAt(row, col) != _solution.tileAt(row, col))
                            return false;
                    }
                }
            }
            return true;
        }

        public int width
        {
            get
            {
                return _width;
            }
        }

        public int height
        {
            get { return _height; }
        }

        public KlotskiState startState
        {
            get { return _startState; }
        }

        public KlotskiState solution
        {
            get { return _solution; }
        }

        public string isoMap(string state)
        {
            char[] sz = new char[state.Length];
            for(int i=0; i<state.Length; ++i)
            {
                int j=_isoMap.IndexOf(state[i]);
                if (j >= 0)
                    sz[i] = _isoMap[j + 1];
                else
                    sz[i] = ' ';
            }

            return new string(sz);
        }


        //
        // search
        //

        // search context

        // search queue for width-first-search
        Queue<KlotskiState> _wfsQueue;

        // list of unique isomaps for all states in wfsQueue
        HashSet<string> _isoStateLookup;

        /**
         * history is provided so the search can avoid backtracking
         */
        public KlotskiState search(List<KlotskiState> history, int depth)
        {
            // init search
            _isoStateLookup = new HashSet<string>();
            _wfsQueue = new Queue<KlotskiState>();

            foreach (KlotskiState st in history)
            {
                _isoStateLookup.Add(st.isoString);
            }

            // starting state for search
            _wfsQueue.Enqueue(history[history.Count() - 1]);

            Random rand = new Random();
            int maxDepth = history.Count() + depth;

            int lastReportedDepth = -1;

            while (_wfsQueue.Count()>0)
            {
                // take one off the queue
                KlotskiState state = _wfsQueue.Dequeue();

                // output
                if(state.depth>lastReportedDepth)
                {
                    lastReportedDepth = state.depth;
                    Console.WriteLine("Depth:"+state.depth+" Queue:" + _wfsQueue.Count() + " Unique:" + _isoStateLookup.Count());
                }


                if (this.isSolution(state))
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
                            // skipping states isomorphic to states already added to the queue

                            // first priority: find children that move the same piece
                            // just moved
                            for (int k = 0; k < children.Count(); ++k)
                            {
                                if (children[k].movedPiece == state.movedPiece)
                                {
                                    string isoString = children[k].isoString;
                                    if (!_isoStateLookup.Contains(isoString))
                                    {
                                        _wfsQueue.Enqueue(children[k]);
                                        _isoStateLookup.Add(isoString);
                                    }
                                }
                            }

                            // select a child state at random
                            // todo: use heuristic
                            int j = rand.Next() % children.Count();
                            for (int m = 0; m < children.Count(); ++m)
                            {
                                int k = (j + m) % children.Count();

                                string isoString = children[k].isoString;
                                if (!_isoStateLookup.Contains(isoString))
                                {
                                    _wfsQueue.Enqueue(children[k]);
                                    _isoStateLookup.Add(isoString);
                                }
                                
                            }
                        }

                    }
                }
            }   // while(queue not empty)

            return null;
        }   // void search()
    }
}
