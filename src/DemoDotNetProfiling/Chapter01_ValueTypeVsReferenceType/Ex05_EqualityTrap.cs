using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace DemoDotNetProfiling.Chapter01
{
    public class EqualityTrapDemo
    {
        // Struct KHONG override Equals/GetHashCode -> day chinh la "cai bay".
        // Khai bao public (thay vi private) vi BenchmarkDotNet sinh code o assembly khac,
        // moi kieu ma benchmark cham toi deu phai truy cap duoc.
        public struct Money
        {
            public decimal Amount { get; }
            public string Currency { get; }
            public Money(decimal amount, string currency)
            {
                Amount = amount;
                Currency = currency;
            }
        }

        private static void CompareMoneyNotOverrideEqual()
        {
            Money money1 = new Money(100, "USD");
            Money money2 = new Money(100, "USD");
            bool isEqual = money1.Equals(money2);
            Console.WriteLine($"money1.Equals(money2): {isEqual}");
        }

        public static void Run()
        {
            CompareMoneyNotOverrideEqual();

            Console.WriteLine();
            Console.WriteLine("Running BenchmarkDotNet (Release build required), this takes a few minutes...");
            Console.WriteLine();

            BenchmarkRunner.Run<MoneyEqualityBenchmarks>();
            BenchmarkRunner.Run<MoneyCollectionBenchmarks>();
        }
    }

    #region Cac bien the Money dung de doi chung

    // Co override + implement IEquatable<T>: cach lam dung.
    public readonly struct MoneyEquatable : IEquatable<MoneyEquatable>
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public MoneyEquatable(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        // Overload nhan dung kieu -> khong boxing, khong reflection.
        public bool Equals(MoneyEquatable other)
            => Amount == other.Amount
               && string.Equals(Currency, other.Currency, StringComparison.Ordinal);

        // Van phai override Equals(object) de nhat quan khi bi goi qua object.
        public override bool Equals(object? obj) => obj is MoneyEquatable other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Amount, Currency);

        public static bool operator ==(MoneyEquatable left, MoneyEquatable right) => left.Equals(right);
        public static bool operator !=(MoneyEquatable left, MoneyEquatable right) => !left.Equals(right);
    }

    // Khong override, nhung KHONG chua field kieu tham chieu.
    // Khi do ValueType.Equals(object) co the dung duong tat so sanh bit (memcmp)
    // thay vi duyet tung field bang reflection -> van boxing nhung nhanh hon han.
    public struct MoneyBlittable
    {
        public long AmountMinorUnits;   // 100.00 USD -> 10000
        public int CurrencyCode;        // ISO 4217 numeric, USD -> 840

        public MoneyBlittable(long amountMinorUnits, int currencyCode)
        {
            AmountMinorUnits = amountMinorUnits;
            CurrencyCode = currencyCode;
        }
    }

    #endregion

    // Exercise 1.5a: do truc tiep mot phep so sanh bang.
    [MemoryDiagnoser]
    public class MoneyEqualityBenchmarks
    {
        // Moi [Benchmark] thuc hien N phep so sanh.
        private const int N = 200_000;

        // De o field (khong phai const/local) de JIT khong gap hang so
        // va khong loai bo luon phep so sanh.
        private EqualityTrapDemo.Money _a, _b;
        private MoneyEquatable _ea, _eb;
        private MoneyBlittable _ba, _bb;

        [GlobalSetup]
        public void Setup()
        {
            _a = new EqualityTrapDemo.Money(100m, "USD");
            _b = new EqualityTrapDemo.Money(100m, "USD");

            _ea = new MoneyEquatable(100m, "USD");
            _eb = new MoneyEquatable(100m, "USD");

            _ba = new MoneyBlittable(10000, 840);
            _bb = new MoneyBlittable(10000, 840);
        }

        // Chinh la thu ma CompareMoneyNotOverrideEqual() dang lam.
        // Money khong override Equals -> compiler bind vao ValueType.Equals(object):
        //   1. _b bi boxing (cap phat tren heap moi lan goi),
        //   2. vi Money co field kieu tham chieu (string) nen runtime khong dung duoc
        //      duong tat so sanh bit, phai duyet tung field bang reflection
        //      va boxing tiep gia tri cua tung field.
        [Benchmark(Baseline = true, Description = "no override: Equals(object) - box + reflection")]
        public int NoOverride_ValueTypeEquals()
        {
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                if (_a.Equals(_b)) count++;
            }
            return count;
        }

        // Van khong override, nhung struct chi toan field blittable
        // -> van boxing nhung runtime so sanh bit, bo duoc phan reflection.
        [Benchmark(Description = "no override, blittable: Equals(object) - box + memcmp")]
        public int Blittable_ValueTypeEquals()
        {
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                if (_ba.Equals(_bb)) count++;
            }
            return count;
        }

        // Co override Equals(object) nhung goi qua object -> tach rieng chi phi boxing
        // ra khoi chi phi reflection: phan chenh voi benchmark duoi chinh la boxing.
        [Benchmark(Description = "override: Equals((object)b) - chi con boxing")]
        public int Equatable_EqualsObject()
        {
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                if (_ea.Equals((object)_eb)) count++;
            }
            return count;
        }

        // Cach lam dung: overload nhan dung kieu, khong boxing, khong reflection.
        [Benchmark(Description = "IEquatable<T>.Equals(Money) - khong box")]
        public int Equatable_TypedEquals()
        {
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                if (_ea.Equals(_eb)) count++;
            }
            return count;
        }

        // operator == chi la duong dan toi Equals(MoneyEquatable), do de xac nhan
        // khong co chi phi phat sinh them.
        [Benchmark(Description = "operator ==")]
        public int Equatable_OperatorEquals()
        {
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                if (_ea == _eb) count++;
            }
            return count;
        }
    }

    // Exercise 1.5b: cho cai bay thuc su can trong code that.
    // List<T>.Contains / Dictionary / HashSet deu di qua EqualityComparer<T>.Default:
    //   - T implement IEquatable<T> -> GenericEqualityComparer<T>, goi thang Equals(T), JIT devirtualize duoc.
    //   - T khong implement         -> ObjectEqualityComparer<T>, goi Equals(object) -> boxing moi phan tu.
    [MemoryDiagnoser]
    public class MoneyCollectionBenchmarks
    {
        private const int Size = 1_000;

        private List<EqualityTrapDemo.Money> _listNoOverride = null!;
        private List<MoneyEquatable> _listEquatable = null!;

        private EqualityTrapDemo.Money _missing;
        private MoneyEquatable _missingEquatable;

        [GlobalSetup]
        public void Setup()
        {
            _listNoOverride = new List<EqualityTrapDemo.Money>(Size);
            _listEquatable = new List<MoneyEquatable>(Size);
            for (int i = 0; i < Size; i++)
            {
                _listNoOverride.Add(new EqualityTrapDemo.Money(i, "USD"));
                _listEquatable.Add(new MoneyEquatable(i, "USD"));
            }

            // Phan tu khong ton tai -> ep Contains quet het Size phan tu (worst case, on dinh).
            _missing = new EqualityTrapDemo.Money(-1, "USD");
            _missingEquatable = new MoneyEquatable(-1, "USD");
        }

        [Benchmark(Baseline = true, Description = "List<Money>.Contains - ObjectEqualityComparer")]
        public bool Contains_NoOverride() => _listNoOverride.Contains(_missing);

        [Benchmark(Description = "List<MoneyEquatable>.Contains - GenericEqualityComparer")]
        public bool Contains_Equatable() => _listEquatable.Contains(_missingEquatable);
    }

    // Results
    // (dan bang ket qua sau khi chay Release)

    //| Method                                                    | Mean        | Error     | StdDev    | Ratio | Gen0    | Allocated | Alloc Ratio |
    //|---------------------------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
    //| 'List<Money>.Contains - ObjectEqualityComparer'           | 51,948.2 ns | 471.02 ns | 367.74 ns |  1.00 | 19.5313 |  184000 B |        1.00 |
    //| 'List<MoneyEquatable>.Contains - GenericEqualityComparer' |    928.8 ns |   4.16 ns |   3.69 ns |  0.02 |       - |         - |        0.00 |

    // Lý do mà List<Money>.Contains chậm hơn là vì nó mất thời gian để cấp phát bộ nhớ trên heap 
    //      cho mỗi lần gọi phương thức Contains, do Money không override Equals và GetHashCode, dẫn đến boxing.

    // Còn việc List<MoneyEquatable>.Contains nhanh hơn là vì nó không cần phải thực hiện boxing
    //      (không cần cấp phát bộ nhớ trên heap),
    //      do MoneyEquatable đã override Equals và GetHashCode, và implement IEquatable<T>.

    // Mean: Thoi gian trung binh ma phuong thuc mat de thuc hien.
    // Error: Sai so chuan cua phep do, cho biet muc do khong chac chan trong ket qua.
    // StdDev: Do lech chuan cua cac phep do, cho biet muc do bien doi trong ket qua.
    // Ratio: Ty le so sanh voi phuong thuc co so (Baseline).
    // Allocated: So byte cap phat tren heap cho moi lan goi benchmark
    //            -> cot quan trong nhat o bai nay, no lo ra boxing.
}
