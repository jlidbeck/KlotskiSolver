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

    }

}
