namespace DemoDotNetProfiling.Chapter01
{
    // Exercise 1.1: Copy (value type) vs Alias (reference type)
    class CopyVsAliasDemo
    {
        private struct PointS
        {
            public int X;
            public int Y;
            public PointS(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        private class PointC
        {
            public int X;
            public int Y;
            public PointC(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        public static void Run()
        {
            PointS InstanceStructA = new PointS(1, 2);
            PointC InstanceClassA = new PointC(1, 2);


            PointS InstanceStructB = InstanceStructA;
            PointC InstanceClassB = InstanceClassA;

            InstanceStructB.X = 10;
            InstanceClassB.Y = 100;

            Console.WriteLine($"InstanceStructA ({InstanceStructA.X}, {InstanceStructA.Y})" +
                $" and InstanceStructB ({InstanceStructB.X}, {InstanceStructB.Y})" +
                $" is Copy");

            Console.WriteLine("The 8 bytes of the struct are copied to the new variable.");

            Console.WriteLine();

            Console.WriteLine($"InstanceClassA ({InstanceClassA.X}, {InstanceClassA.Y})" +
                $" and InstanceClassB ({InstanceClassB.X}, {InstanceClassB.Y})" +
                $" is Alias");

            Console.WriteLine("The reference to the object is copied to the new variable.");
        }
    }
}
