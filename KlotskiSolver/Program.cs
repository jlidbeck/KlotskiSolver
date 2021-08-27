using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KlotskiSolverApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            //KlotskiState start = new KlotskiState(4, 5, "bAAcbAAcdeefdghfi  j");
            //KlotskiState solution = new KlotskiState(4, 5, "             AA  AA ");
            KlotskiProblemDefinition pd = new KlotskiProblemDefinition(
                4, 5, 
                "bAAcbAAcdeefdghfi  j", 
                "             AA  AA ",
                "A4b2c2d2e3f2g1h1i1j1");

            int depth = 130;

            if(args.Length>=2)
                depth= int.Parse(args[1]);

            Console.WriteLine("Got problem. Start state:\n\n" + pd.startState.ToString());

            solve(pd);

            Console.WriteLine("Hit ENTER");
            Console.ReadLine();
        }

        static void solve(KlotskiProblemDefinition pd)
        {
            // randomly move pieces until no move can be made without backtracking
            Random rand = new Random();
            KlotskiState state = pd.startState;
            List<KlotskiState> history = new List<KlotskiState>();
            history.Add(pd.startState);

            //for (int i = 0; i < 99999; ++i)
            while (true)
            {
                state.write();

                if (pd.isSolution(state))
                {
                    Console.WriteLine("**********************************");
                    Console.WriteLine("Solution found: " + state.moveCount + " moves");
                    Console.WriteLine("**********************************");
                }

                // choose a move

                //Console.WriteLine("Moves: " + i + "\n" + state.ToString());
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
                Console.Write("BACK to backtrack, ENTER to search: ");
                ConsoleKeyInfo key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key == ConsoleKey.Backspace)
                {
                    if ((key.Modifiers & ConsoleModifiers.Shift)!=0)
                    {
                        // retrack
                        int n = history.IndexOf(state);
                        if (n >= 0 && n < history.Count() - 1)
                        {
                            state = history[n + 1];
                        }
                    }
                    else
                    {
                        // backtrack
                        if (state.parent != null)
                            state = state.parent;
                        else
                        {
                            int n = history.IndexOf(state);
                            if (n > 0)
                            {
                                state = history[n - 1];
                            }
                        }

                    }
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
                        int n=history.Count()-1;
                        KlotskiState p = searchResult;
                        while (p!=null && p != history[n])
                        {
                            history.Insert(n+1, p);

                            p = p.parent;
                        }
                    }
                }
                else
                {
                    for (int j = 0; j < children.Count(); ++j)
                    {
                        if (char.ToLower(key.KeyChar) == char.ToLower(children[j].movedPiece))
                        {
                            nextState = children[j];
                            break;
                        }
                    }

                    if(nextState==null)
                    {
                        int index = -1;
                        try
                        {
                            index = int.Parse(key.KeyChar.ToString());
                        }
                        catch (FormatException)
                        { }

                        if(index>=1 && index<=children.Count())
                        {
                            nextState=children[index-1];
                        }
                    }

                    if (nextState != null)
                    {
                        state = nextState;
                        history.Add(nextState);
                    }
                }
            }

        }



    }
}
