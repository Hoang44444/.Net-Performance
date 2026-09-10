namespace DemoDotNetProfiling.Chapter01
{
    // Exercise 1.2: Reassign a parameter vs Mutate the object it points to
    class ReassignVsMutateDemo
    {
        private class Account
        {
            public decimal Balance { get; set; }
            public Account(decimal balance)
            {
                Balance = balance;
            }
        }

        private static void Reassign(Account a) => a = new Account(200);
        private static void Mutate(Account a) => a.Balance = 200;

        public static void Run()
        {
            Account account = new Account(500);
            Reassign(account);
            Console.WriteLine($"After Reassign, account.Balance = {account.Balance}"); // Output: 500
            // The Reassign method creates a new Account object and assigns it to the local variable a,
            // but it does not change the original account object that was passed in.
            // Therefore, the original account object still has a balance of 500.


            Mutate(account);
            Console.WriteLine($"After Mutate, account.Balance = {account.Balance}"); // Output: 200
            // The Mutate method modifies the Balance property of the original account object that was passed in.
        }
    }
}
