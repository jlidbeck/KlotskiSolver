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
                " AAA   A   def BBICCBjIkCBBmCC",
                "                     AAA   A  ",
                "defjkm" ));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "2.13", 30, 5, 6,
                "bAAAfjjAgfhkkgchllmmd   inn ei",
                "                     AAA   A  ",
                "A bcde fghi jklm" ));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "15 Puzzle", 52, 4, 4,
                "OABCDEFGHIJKLMN ",
                "ABCDEFGHIJKLMNO ",
                ""));

            puzzlePresets.Add(new KlotskiProblemDefinition(
                "Trivial", 1, 5, 6,
                "AA   AA                       ",
                "                       AA   AA",
                "" ));

            int depth = 130;

            if (args.Length >= 2)
                depth = int.Parse(args[1]);

            int selectedIndex = 3;

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
                        Console.WriteLine($"    {i}: {preset.name.PadRight(25, ' ')} Goal: {preset.goalMoves}");
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

                Console.WriteLine($"Depth: {state.moveCount} / {state.depth} DistSq: {state.getDistanceSquareScore()} {(state.parentState == null ? ' ' : '<')}{(state==endState ? ' ' : '>')} ");

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
                        prompt += $"\n   {j + 1}: {children[j].movedPiece} {children[j].movedPieceDirection}";
                    }
                }

                Console.WriteLine(prompt);
                Console.Write("Ctrl+Z to backtrack, Ctrl+Y to retrack, ENTER to search: ");
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
                    Console.WriteLine("    Ctrl+Z:   Backtrack");
                    Console.WriteLine("    Ctrl+Y:   Retrack");
                    Console.WriteLine("    HOME:     Rewind to start state, preserving history");
                    Console.WriteLine("    END:      Forward to last state");
                    Console.WriteLine("    ENTER:    Search from current state");
                    Console.WriteLine("    F2:       Display history");
                    Console.WriteLine("    F3:       Animate history");
                    Console.WriteLine("    F5:       Restart this puzzle");
                    Console.WriteLine("    F6:       Go to goal state and drop history");
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
                    // print a concise history of moved tiles
                    Console.WriteLine("History: " + endState.getHistoryString());
                }
                else if (key.Key == ConsoleKey.F3)
                {
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
                        Console.WriteLine($"Depth: {p.moveCount} / {p.depth} DistSq: {p.getDistanceSquareScore()} ");
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
                    restartNeeded = true;
                }
                else if (key.Key == ConsoleKey.F6)
                {
                    // create a goal state with no history
                    state = endState = state.context.goalState.clone();
                }
                else if (key.Key == ConsoleKey.F7)
                {
                    // drop history
                    state = endState = state.clone();
                }
                else if (key.Key == ConsoleKey.F8)
                {
                    // shuffle current state, with no history
                    var shuffleState = pd.shuffle(state.clone(), 1000);
                    if (shuffleState != null)
                    {
                        // this drops any existing history
                        state = endState = shuffleState.clone();
                    }
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine("Search:\n    [1] DistSqHeuristic (fast)\n     2  MoveCount BFS (slow, finds best solution)\n");
                    var c = Console.ReadKey();
                    IComparer<KlotskiState> searchComparer;
                    switch (c.KeyChar)
                    {
                        case '2':
                            searchComparer = new KlotskiState.MoveCountComparer();
                            break;
                        default:
                            searchComparer = new KlotskiState.DistanceSquaredHeuristicComparer(7, 2);
                            break;
                    }
                    int searchDepth = 100;
                    Console.Write($"\nSearch depth [{searchDepth}]: ");
                    var str = Console.ReadLine();
                    if (!int.TryParse(str, out searchDepth))
                        searchDepth = 100;

                    Console.WriteLine($"\nStarting search: {searchComparer} depth={searchDepth} from state {state.depth}");

                    state.clearChildStates();   // force new search

                    try
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        KlotskiState searchResult = pd.search(state, searchComparer, searchDepth);
                        stopwatch.Stop();

                        Console.WriteLine($"Search time: {stopwatch.Elapsed}");

                        if (searchResult == null)
                        {
                            Console.WriteLine("No solution found.");
                        }
                        else
                        {
                            // add search history to UI history
                            endState = state = searchResult;
                        }
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine($"Error: {err.Message}");
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
                        if (char.ToLower(key.KeyChar) == char.ToLower(children[j].movedPiece))
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



    }
}
