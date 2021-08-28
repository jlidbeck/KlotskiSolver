using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KlotskiSolverApplication
{
    class Program
    {
        static readonly Dictionary<string, KlotskiProblemDefinition> puzzlePresets = new Dictionary<string, KlotskiProblemDefinition>();


        static void Main(string[] args)
        {
            puzzlePresets["Forget-me-not"] = new KlotskiProblemDefinition(
                4, 5, 
                "bAAcbAAcdeefdghfi  j", 
                "             AA  AA ",
                "A4b2c2d2e3f2g1h1i1j1",
                81);

            puzzlePresets["2.1"] = new KlotskiProblemDefinition(
                4, 5,
                "AbbcAAbcddeefg  hi  ",
                "              A   AA",
                "A1B2c3d4e4f5g5h5i5",
                12);

            puzzlePresets["2.11 Breakthrough"] = new KlotskiProblemDefinition(
                5, 6,
                " AAA   A   def BBICCBjIkCBBmCC",
                "                     AAA   A  ",
                "A1B2C3I4d5e5f5j5k5m5",
                28);

            puzzlePresets["2.13"] = new KlotskiProblemDefinition(
                5, 6,
                "bAAAfjjAgfhkkgchllmmd   inn ei",
                "                     AAA   A  ",
                "A1b2c2d2e2f3g3h3i3j4k4l4m4",
                30);

            puzzlePresets["Trivial"] = new KlotskiProblemDefinition(
                5, 6,
                "AA   AA                       ",
                "                       AA   AA",
                "",
                1);

            int depth = 130;

            if (args.Length >= 2)
                depth = int.Parse(args[1]);

            int selectedIndex = 1;

            while (true)
            {
                if (selectedIndex < 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Select a puzzle:");
                    for (int i = 0; i < puzzlePresets.Count(); ++i)
                    {
                        var preset = puzzlePresets.ElementAt(i);
                        Console.WriteLine($"    {i}: {preset.Key.PadRight(25, ' ')} Goal: {preset.Value.goalMoves}");
                    }
                    Console.Write(">");

                    string input = Console.ReadLine();
                    if (!int.TryParse(input, out selectedIndex) || selectedIndex >= puzzlePresets.Count())
                    {
                        return;
                    }
                }

                var pd = puzzlePresets.ElementAt(selectedIndex);

                Console.WriteLine();
                Console.WriteLine(pd.Key);
                Console.WriteLine();
                Console.WriteLine("    Start         Goal");
                Console.WriteLine();
                var startStateStrings = pd.Value.startState.ToStrings();
                var goalStateStrings = pd.Value.goalState.ToStrings();
                for (int i = 0; i < startStateStrings.Count; ++i)
                {
                    Console.WriteLine("    " + startStateStrings[i] + "   ==>   " + goalStateStrings[i]);
                }
                Console.WriteLine();

                solve(pd.Value);

                // how did we get back here? prompt to start a new puzzle
                selectedIndex = -1;
            }   // while(true)
        }

        static void solve(KlotskiProblemDefinition pd)
        {
            // randomly move pieces until no move can be made without backtracking
            Random rand = new Random();
            KlotskiState state = pd.startState;
            List<KlotskiState> history = new List<KlotskiState>();
            history.Add(pd.startState);

            while (true)
            {
                state.write();

                if (pd.matchesGoalState(state))
                {
                    Console.WriteLine("***********  Goal  ***********");
                }

                // choose a move

                List<KlotskiState> children = state.getChildStates();

                // manual/interactive
                KlotskiState nextState = null;

                string prompt = "Available moves: ";
                if (children.Count() <= 0)
                {
                    prompt += "NONE";
                }
                else
                {
                    for (int j = 0; j < children.Count(); ++j)
                    {
                        prompt += "\n  " + (j + 1) + ": " + children[j].movedPiece + " " + children[j].movedPieceDirection;
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
                    Console.WriteLine("    HOME:     Rewind to start state");
                    Console.WriteLine("    END:      Forward to last state");
                    Console.WriteLine("    ENTER:    Search from current state");
                    Console.WriteLine("    ESC:      Exit");
                    Console.WriteLine();
                }
                else if(key.Key == ConsoleKey.O && (key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    return;
                }
                else if (key.Key == ConsoleKey.Home)
                {
                    state = history[0];
                }
                else if (key.Key == ConsoleKey.End)
                {
                    state = history.Last();
                }
                else if ((key.Key == ConsoleKey.Backspace && (key.Modifiers & ConsoleModifiers.Shift  ) == 0) ||
                         (key.Key == ConsoleKey.Z         && (key.Modifiers & ConsoleModifiers.Control) != 0))
                {
                    // Ctrl+Z or backspace: backtrack
                    if (state.parentState != null)
                    {
                        state = state.parentState;
                    }
                    else
                    {
                        int n = history.IndexOf(state);
                        if (n > 0)
                        {
                            state = history[n - 1];
                        }
                    }
                }
                else if (key.Key == ConsoleKey.Backspace && (key.Modifiers & ConsoleModifiers.Shift) != 0 ||
                         key.Key == ConsoleKey.Y && (key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    // Ctrl+Y or shift+Back: retrack
                    int n = history.IndexOf(state);
                    if (n >= 0 && n < history.Count() - 1)
                    {
                        state = history[n + 1];
                    }
                }
                else if (key.Key == ConsoleKey.F2)
                {
                    // print a concise history of moved tiles
                    char tileId = '\0';
                    Console.Write("History: ");
                    for (int i = 0; i < history.Count; ++i)
                    {
                        if (history[i].movedPiece != tileId)
                        {
                            tileId = history[i].movedPiece;
                            Console.Write(tileId);
                        }
                    }
                    Console.WriteLine();
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    //autoSearch = true;
                    Console.WriteLine("\nBeginning auto search from move " + history.Count);

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    KlotskiState searchResult = pd.search(history, 130);
                    stopwatch.Stop();

                    if (searchResult != null)
                    {
                        Console.WriteLine("Search time: " + stopwatch.Elapsed);

                        state = searchResult;

                        // add search history to UI history
                        int n = history.Count() - 1;
                        KlotskiState p = searchResult;
                        while (p != null && p != history[n])
                        {
                            history.Insert(n + 1, p);

                            p = p.parentState;
                        }
                    }
                }
                else
                {
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
                        state = nextState;
                        history.Add(nextState);
                    }
                }
            }   // while(true)

        }



    }
}
