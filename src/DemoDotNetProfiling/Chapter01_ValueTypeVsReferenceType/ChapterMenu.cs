namespace DemoDotNetProfiling.Chapter01
{
    // Root: only dispatches to each exercise.
    class ChapterMenu
    {
        public static void Run()
        {
            while (true)
            {
                int exerciseNumber = 0;
                Console.Write("Enter exercise number or 0 to exit: ");
                string? input = Console.ReadLine();
                if (input is null) return;              // stdin het (vd: chay qua pipe) -> thoat
                if (int.TryParse(input, out exerciseNumber))
                {
                    switch (exerciseNumber)
                    {
                        case 0:
                            return;
                        case 1:
                            Console.WriteLine("Exercise 1.1");
                            CopyVsAliasDemo.Run();
                            break;
                        case 2:
                            Console.WriteLine("Exercise 1.2");
                            ReassignVsMutateDemo.Run();
                            break;
                        case 3:
                            Console.WriteLine("Exercise 1.3");
                            RefOutInWithBigStructDemo.Run();
                            break;
                        case 4:
                            Console.WriteLine("Exercise 1.4");
                            AlignmentAndPaddingDemo.Run();
                            break;
                        case 5:
                            Console.WriteLine("Exercise 1.5");
                            EqualityTrapDemo.Run();
                            break;
                        case 6:
                            Console.WriteLine("Exercise 1.6");
                            BoxingHuntDemo.Run();
                            break;
                        case 7:
                            Console.WriteLine("Exercise 1.7");
                            MutableStructTrapDemo.Run();
                            break;
                        case 8:
                            Console.WriteLine("Exercise 1.8");
                            StringValueSemanticsDemo.Run();
                            break;
                        case 9:
                            Console.WriteLine("Exercise 1.9");
                            NullableAndTuplesDemo.Run();
                            break;
                        case 10:
                            Console.WriteLine("Exercise 1.10");
                            RefStructAndSpanDemo.Run();
                            break;
                    }
                }
            }
        }
    }
}
