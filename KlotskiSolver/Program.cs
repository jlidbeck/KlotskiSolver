using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KlotskiSolverApplication
{
    class Program
    {
        static readonly List<KlotskiProblemDefinition> puzzlePresets = new List<KlotskiProblemDefinition>();

        static int animationDelay = 200;


        static void Main(string[] args)
        {
            puzzlePresets.Add(new KlotskiProblemDefinition(
                "Forget-me-not", 81, 4, 5,
                "bAAcbAAcdeefdghfi  j",
                "             AA  AA ",
                "bcdf ghij" ));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "1.50", 120, 4, 5,
                "gAAh" +
                "eAAf" +
                "ebbf" +
                "iccj" +
                " dd ",
                "gAAh" +
                " AAf" +
                " bbf" +
                "eccj" +
                "eidd",
                "bcd ef ghij"));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "2.1", 12, 4, 5,
                "AbbcAAbcddeefg  hi  ",
                "              A   AA",
                "de fghi" ));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "2.2 Ells", 16, 5, 5,
                "defAAghBAA CBBi CCjk   lm",
                "               A    AA   ",
                "defjkm" ));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "2.11 Linebreak", 28, 5, 6,
                " AAA " +
                "  A  " +
                " def " +
                "BBICC" +
                "BjIkC" +
                "BBmCC",
                "     " +
                "     " +
                "     " +
                "     " +
                " AAA " +
                "  A  ",
                "defjkm" ));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "2.13", 30, 5, 6,
                "bAAAf" +
                "jjAgf" +
                "hkkgc" +
                "hllmm" +
                "d   i" +
                "nn ei",
                "     " +
                "     " +
                "     " +
                "     " +
                " AAA " +
                "  A  ",
                "A bcde fghi jklm" ));

            puzzlePresets.Add(KlotskiProblemDefinition.createFifteenPuzzle("3x3 Boss Puzzle", 3, 3, 31));
            puzzlePresets.Add(KlotskiProblemDefinition.createFifteenPuzzle("4x4 Boss Puzzle", 4, 4, 200));
            puzzlePresets.Add(KlotskiProblemDefinition.createFifteenPuzzle("5x5 Boss Puzzle", 5, 5, 200));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "Burr", 35, 7, 7,
                "AA   ii" +
                "AAbbbjj" +
                "ccbfbdd" +
                "cggfhhd" +
                "ccefedd" +
                "kkeee  " +
                "ll     ",
                "       " +
                "       " +
                "       " +
                "       " +
                "       " +
                "     AA" +
                "     AA",
                "ghijkl"));

            // minimal example that gets messed up by pruning A's double move b/c it finds Ar, Br, Ar first
            puzzlePresets.Add(new KlotskiProblemDefinition(
                "Trivial 1", 2, 4, 1,
                "A B ",
                "  AB",
                ""));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "Trivial 2", 1, 5, 6,
                "AA   " +
                "AA   " +
                "     " +
                "     " +
                "     " +
                "     ",
                "     " +
                "     " +
                "     " +
                "     " +
                "   AA" +
                "   AA",
                "" ));


            int selectedIndex = -1;
            if (args.Length >= 1)
            {
                int idx;
                if (int.TryParse(args[0], out idx))
                    selectedIndex = idx;
            }


            //  Main program loop: topmost menu with list of presets
            while (true)
            {
                // if a puzzle is already selected, skip ahead to solve menu
                // otherwise, show puzzle menu

                if (selectedIndex < 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Select a puzzle:");
                    for (int i = 0; i < puzzlePresets.Count(); ++i)
                    {
                        var preset = puzzlePresets.ElementAt(i);
                        Console.WriteLine($"{i,6}: {preset.name,-25} Goal: {preset.goalMoves,3}");
                    }
                    Console.Write(">");

                    string input = Console.ReadLine();
                    if (!int.TryParse(input, out selectedIndex) || selectedIndex >= puzzlePresets.Count())
                    {
                        return;
                    }
                }

                var pd = puzzlePresets.ElementAt(selectedIndex);

                // display quick and simple ASCII summary of puzzle
                Console.WriteLine();
                Console.WriteLine(pd.name);
                Console.WriteLine();
                Console.WriteLine("     Start         Goal");
                Console.WriteLine();
                var startStateStrings = pd.startState.ToStrings();
                var goalStateStrings = pd.goalState.ToStrings();
                for (int i = 0; i < startStateStrings.Count; ++i)
                {
                    Console.WriteLine($"     {startStateStrings[i]}   ==>   {goalStateStrings[i]}");
                }
                Console.WriteLine();

                // enter solver menu loop
                solve(pd);

                // solver menu loop exited: return to main menu, prompt to start a new puzzle
                selectedIndex = -1;
            }   // while(true)
        }

        static void solve(KlotskiProblemDefinition pd)
        {
            Random rand = new Random();

            var state = pd.startState;                  // current state displayed in the UI
            var endState = state;

            bool restartNeeded = true;

            while (true)
            {
                if (restartNeeded)
                {
                    // clear history back to start state
                    state = pd.startState;
                    endState = state;

                    restartNeeded = false;
                }

                state.write();

                if (state.matchesGoalState())
                {
                    Console.WriteLine("***********  Goal  ***********");
                }

                Console.WriteLine($"Move: {state.moveCount}  Depth: {state.depth}  Distance: {state.getDistanceEstimate()} {(state.parentState == null ? ' ' : '<')}{(state==endState ? ' ' : '>')} ");

                // choose a move

                List<KlotskiState> children = state.getChildStates();

                // sort the menu options to put at the top of the list any moves that are the same tile just moved
                children.Sort(new KlotskiState.MoveCountComparer());

                string prompt = "Available moves: ";
                if (children.Count() <= 0)
                {
                    prompt += "NONE";
                }
                else
                {
                    for (int j = 0; j < children.Count(); ++j)
                    {
                        var st = children[j];
                        prompt += $"\n   {j+1}: {st.movedTile} {st.movedTileDirection,5}   {st.moveCount} / {st.depth}";
                    }
                }

                Console.WriteLine(prompt);
                Console.Write("Home, End, Ctrl+Z/, Ctrl+Y/. to navigate, ENTER to search: ");
                ConsoleKeyInfo key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key == ConsoleKey.Escape)
                {
                    System.Environment.Exit(0);
                }
                else if (key.KeyChar == '?')
                {
                    Console.WriteLine();
                    Console.WriteLine("    1-9:      Apply move. Only unique moves are listed--those that do not repeat a previous state.");
                    Console.WriteLine("    a-z, A-Z: Move specified tile if there is a unique move. If multiple moves are listed, first move is made.");
                    Console.WriteLine("    , Ctrl+Z: Step back");
                    Console.WriteLine("    . Ctrl+Y: Step forward");
                    Console.WriteLine("    HOME:     Rewind to start state");
                    Console.WriteLine("    END:      Forward to last state");
                    Console.WriteLine("    ENTER:    Search");
                    Console.WriteLine("    F2:       Display history");
                    Console.WriteLine("    F3:       Animate history");
                    Console.WriteLine("    F5:       Restart this puzzle");
                    Console.WriteLine("    F7:       Drop history");
                    Console.WriteLine("    F8:       Shuffle");
                    Console.WriteLine("    Ctrl+O:   Open puzzle menu");
                    Console.WriteLine("    ESC:      Exit");
                    Console.WriteLine();
                }
                else if (key.Key == ConsoleKey.O && (key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    return;
                }
                else if (key.Key == ConsoleKey.Home)
                {
                    while (state.parentState != null)
                    {
                        state = state.parentState;
                    }
                }
                else if (key.Key == ConsoleKey.End)
                {
                    state = endState;
                }
                else if (key.KeyChar == ',' ||
                        (key.Key == ConsoleKey.Backspace && (key.Modifiers & ConsoleModifiers.Shift) == 0) ||
                        (key.Key == ConsoleKey.Z && (key.Modifiers & ConsoleModifiers.Control) != 0))
                {
                    // Ctrl+Z or backspace: backtrack
                    if (state.parentState != null)
                    {
                        state = state.parentState;
                    }
                }
                else if (key.KeyChar == '.' ||
                         key.Key == ConsoleKey.Backspace && (key.Modifiers & ConsoleModifiers.Shift) != 0 ||
                         key.Key == ConsoleKey.Y && (key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    // Ctrl+Y or shift+Back: retrack
                    // since the linked list is directional, we have to search backward to find the current state
                    for (var p = endState; p != null && p.parentState != null; p = p.parentState)
                    {
                        if (p.parentState == state)
                        {
                            state = p;
                            break;
                        }
                    }
                }
                else if (key.Key == ConsoleKey.F2)
                {
                    // Display history: print a concise history of moved tiles
                    Console.WriteLine("History: " + endState.getHistoryString());
                }
                else if (key.Key == ConsoleKey.F3)
                {
                    // Animate: replay states from the beginning to {endState}

                    var states = new List<KlotskiState>();

                    // place states in list in forward order, {endState} at end
                    for (var p = endState; p != null; p = p.parentState)
                    {
                        states.Insert(0, p);
                    }

                    // Shift+F3 to animate in reverse
                    if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
                    {
                        states.Reverse();
                    }

                    Console.WriteLine();
                    var cx = Console.CursorLeft;
                    var cy = Console.CursorTop;
                    foreach (var p in states)
                    {
                        Console.CursorLeft = cx;
                        Console.CursorTop = cy;

                        Console.WriteLine();
                        p.write();
                        Console.WriteLine();
                        Console.WriteLine($"Moves: {p.moveCount}  Depth: {p.depth}  DistSq: {p.getDistanceSquareScore()} ");
                        System.Threading.Thread.Sleep(animationDelay);
                        if (Console.KeyAvailable)
                        {
                            state = p;
                            break;
                        }
                    }
                }
                else if (key.Key == ConsoleKey.F5)
                {
                    // reset
                    restartNeeded = true;
                }
                else if (key.Key == ConsoleKey.F7)
                {
                    // drop history
                    state = endState = state.clone();
                }
                else if (key.Key == ConsoleKey.F8)
                {
                    // shuffle current state, with no history
                    var shuffleState = pd.randomWalk(state.clone(), 1000);
                    if (shuffleState != null)
                    {
                        // this drops any existing history
                        state = endState = shuffleState.clone();
                    }
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine("Search:");
                    Console.WriteLine("    [1] DistSqHeuristic (fast, finds 1 solution)");
                    Console.WriteLine("     2  MoveCount BFS (slow, finds 1 solution with min moves)");
                    Console.WriteLine("     3  Complete BFS search (finds all solutions)");
                    Console.WriteLine("     4  Deepest state search (finds all states furthest from any goal state)");
                    var c = Console.ReadKey();
                    IComparer<KlotskiState> searchComparer;
                    bool deepStateSearch = false;
                    bool stopAtFirst = true;
                    switch (c.KeyChar)
                    {
                        default:
                        case '1':
                            searchComparer = new KlotskiState.DistanceSquaredHeuristicComparer(7, 2);
                            break;

                        case '2':
                            searchComparer = new KlotskiState.MoveCountComparer();
                            break;

                        case '3':
                            searchComparer = new KlotskiState.MoveCountComparer();
                            stopAtFirst = false;
                            break;

                        case '4':
                            searchComparer = new KlotskiState.MoveCountComparer();
                            stopAtFirst = false;
                            deepStateSearch = true;
                            break;
                    }
                    int searchDepth = 200;
                    Console.Write($"\nSearch depth [{searchDepth}]: ");
                    var str = Console.ReadLine();
                    if (!int.TryParse(str, out searchDepth))
                        searchDepth = 200;

                    Console.WriteLine($"\nStarting search: {searchComparer} Depth={searchDepth} from state {state.depth} Deep={deepStateSearch}");

                    state.detach();   // force new search

                    KlotskiProblemDefinition.SearchContext searchResults = null;

                    try
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        if(deepStateSearch)
                            searchResults = pd.findDeepestStates(searchDepth);
                        else
                            searchResults = pd.search(state, searchComparer, searchDepth, stopAtFirst);
                        stopwatch.Stop();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Search time: {stopwatch.Elapsed}");
                        Console.ResetColor();
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine($"Error: {err.Message}");
                    }


                    if (searchResults == null)
                    {
                        Console.WriteLine("Search was interrupted.");
                        continue;
                    }


                    // logging: don't include in release
                    if (searchResults?.allStatesVisited == true)
                    {
                        Console.WriteLine("Depth  Reachable States");
                        for (int i = 0; i < searchResults.visitedStatesByMoveCount.Count(); ++i)
                        {
                            Console.WriteLine($"{i,6} {searchResults.visitedStatesByMoveCount[i],10}");
                        }
                    }
                    Console.WriteLine($"Pruned: {searchResults.prunedAfterPop}, {searchResults.prunedBeforePush}");
                    Console.WriteLine(
                        $"Solutions found: {searchResults.solutionStates.Count()}   " +
                        $"States visited: {searchResults.visitedStates}   " +
                        $"Max depth reached: {searchResults.maxMovesReached} / {searchResults.maxDepthReached}   " +
                        $"All states visited: {(searchResults.allStatesVisited ? "YES" : "NO")}");


                    if (deepStateSearch)
                    {
                        if (searchResults.deepestStates.Count() > 0)
                        {
                            Console.WriteLine("Select a deepest state:");
                            endState = state = selectState(searchResults.deepestStates);
                        }
                        else
                        {
                            // this shouldn't happen--indicates no states were searched at all
                            Console.WriteLine("No deepest states found. This probably indicates a parameter error.");
                        }
                    }
                    else
                    {
                        if (searchResults.solutionStates.Count() == 1)
                        {
                            // add search history to UI history
                            endState = state = searchResults.solutionStates.First();
                        }
                        else if (searchResults.solutionStates.Count() > 1)
                        {
                            Console.WriteLine($"{searchResults.solutionStates.Count()} solutions found. " +
                                $"MoveCount={searchResults.solutionStates.Min(st => st.moveCount)}..{searchResults.solutionStates.Max(st => st.moveCount)} " +
                                $"Depth={searchResults.solutionStates.Min(st => st.depth)}..{searchResults.solutionStates.Max(st => st.depth)}");
                            searchResults.solutionStates.Sort(new KlotskiState.MoveCountComparer());
                            endState = state = selectState(searchResults.solutionStates);
                        }
                        else
                        {
                            Console.WriteLine("No results found.");
                        }
                    }
                }
                else
                {
                    // manual/interactive
                    KlotskiState nextState = null;

                    // If the char typed matches one of the piece IDs which can be moved,
                    // make the first move available for that piece.
                    for (int j = 0; j < children.Count(); ++j)
                    {
                        if (char.ToLower(key.KeyChar) == char.ToLower(children[j].movedTile))
                        {
                            nextState = children[j];
                            break;
                        }
                    }

                    if (nextState == null)
                    {
                        // typed char didn't match a piece ID.
                        // check whether user entered a digit
                        int index = -1;
                        try
                        {
                            index = int.Parse(key.KeyChar.ToString());
                        }
                        catch (FormatException)
                        { }

                        if (index >= 1 && index <= children.Count())
                        {
                            nextState = children[index - 1];
                        }
                    }

                    if (nextState != null)
                    {
                        state = endState = nextState;
                    }
                }
            }   // while(true)

        }

        //  Displays all states, then prompts user to select one
        static KlotskiState selectState(IEnumerable<KlotskiState> solutionStates)
        {
            if (solutionStates.Count() == 0)
                return null;

            writeStatesToConsole(solutionStates);

            Console.Write($"Select solution (0-{solutionStates.Count() - 1}) [0]: ");

            var line = Console.ReadLine();
            int idx;
            if (int.TryParse(line, out idx))
                return solutionStates.ElementAt(idx);
            return solutionStates.First();
        }

        static void writeStatesToConsole(IEnumerable<KlotskiState> results)
        {
            if (results.Count() == 0) 
                return;

            int cx = 1;
            var cy = Console.CursorTop;
            int x = cx, y = cy;
            for (int i = 0; i < results.Count(); ++i)
            {
                var state = results.ElementAt(i);
                var pd = state.context;

                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.White;
                Console.CursorLeft = x;
                Console.CursorTop = y;
                Console.Write($"{i} {state.moveCount}");

                state.writeToConsole(x, y + 1);

                if ((x += pd.width * 2 + 1) > Console.WindowWidth - pd.width * 2)
                {
                    // end of line

                    x = cx;
                    y += (pd.height + 1);

                    // wait for key input, ESC to terminate
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                        break;
                }
            }
            /*                    Console.CursorLeft = cx;
                                Console.CursorTop = cy;
            */
            Console.WriteLine();
            Console.WriteLine();
        }

    }
}
