using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace DemoDotNetProfiling.Chapter01
{
    // =========================================================================================
    // Bai 1.9 - Nullable<T> VA ValueTuple vs Tuple
    //
    //   1) In kich thuoc int? bang Unsafe.SizeOf, giai thich vi sao KHONG phai 4 byte.
    //   2) object o = (int?)5; roi unbox lai - thu unbox thanh Nullable<int> va thanh int,
    //      xem cai nao chay duoc.
    //   3) Benchmark tao 1 trieu (int, string) ValueTuple vs 1 trieu Tuple.Create(int, string),
    //      so allocation.
    //
    // Y chinh: ca hai phan deu la mot cau hoi - "gia tri nay nam O DAU". Nullable<T> la STRUCT
    // boc them mot co bool (nen ton them byte + padding, va bien mat khi bi box). Tuple<...>
    // la CLASS (moi cai la mot object tren heap), con ValueTuple<...> la STRUCT (nam ngay tai
    // cho, khong cap phat).
    // =========================================================================================
    public class NullableAndTuplesDemo
    {
        // =====================================================================================
        // PHAN A: KICH THUOC CUA Nullable<T>
        // =====================================================================================
        private static void PartA_NullableSize()
        {
            Section("PHẦN A", "Unsafe.SizeOf<int?>() - vì sao không phải 4 byte?");

            Console.WriteLine("  +---------------+------+-------------+-------+");
            Console.WriteLine("  | Kieu          |    T | Nullable<T> | Chenh |");
            Console.WriteLine("  +---------------+------+-------------+-------+");
            PrintSize<bool>("bool");
            PrintSize<byte>("byte");
            PrintSize<short>("short");
            PrintSize<int>("int");
            PrintSize<long>("long");
            PrintSize<double>("double");
            PrintSize<Guid>("Guid");
            PrintSize<decimal>("decimal");
            Console.WriteLine("  +---------------+------+-------------+-------+");

            int sizeInt = Unsafe.SizeOf<int>();
            int sizeNullableInt = Unsafe.SizeOf<int?>();
            Result(expected: 8, actual: sizeNullableInt, expr: "Unsafe.SizeOf<int?>()");

            Console.WriteLine($$"""

                GIẢI THÍCH - vì sao int? là {{sizeNullableInt}} byte chứ không phải {{sizeInt}}:

                  1) int? CHỈ là đường tắt cú pháp của System.Nullable<int>, mà Nullable<T> là
                     một STRUCT bình thường, khai báo đại khái:

                         public struct Nullable<T> where T : struct
                         {
                             private readonly bool hasValue;   // 1 byte
                             internal readonly T value;        // sizeof(T)
                         }

                     Nó KHÔNG phải một con trỏ có thể null, cũng không phải kiểu tham chiếu.

                  2) int chiếm trọn 4 byte cho dữ liệu: mọi tổ hợp bit đều là một số hợp lệ, kể
                     cả 0 và -1. Không còn tổ hợp bit nào dư ra để nói "tôi rỗng". Cho nên phải
                     có một field RIÊNG mang cờ hasValue -> 4 + 1 = 5 byte.
                     (Khác hẳn reference type: ở đó địa chỉ 0 vốn không bao giờ hợp lệ nên được
                     mượn làm null miễn phí, string? vẫn đúng 8 byte như string.)

                  3) Rồi ALIGNMENT (đúng bài 1.4) làm tròn 5 lên bội số của 4 - độ căn lề của
                     field int - thành {{sizeNullableInt}} byte. Layout thật:

                         offset 0 : hasValue (1 byte)
                         offset 1 : PADDING  (3 byte bỏ không)
                         offset 4 : value    (4 byte)

                     Đúng 3 byte lãng phí. Với long? thì padding còn là 7 byte (1 + 7 + 8 = 16).

                  HỆ QUẢ THỰC TẾ:
                    - Một struct/lớp có nhiều field nullable phình rất nhanh: 4 field int? = 32
                      byte, trong khi 4 int + 4 cờ gói chung chỉ khoảng 20 byte. Nếu bảng dữ
                      liệu hàng triệu dòng, cân nhắc gom cờ vào một bitmask.
                    - Nullable<T> KHÔNG cấp phát heap - nó vẫn là value type nằm tại chỗ. "null"
                      ở đây chỉ là hasValue == false, không liên quan gì tới con trỏ null.
                    - Không lồng được: int?? là lỗi biên dịch, vì Nullable<T> ràng buộc
                      T : struct mà bản thân Nullable<T> lại không thoả 'non-nullable struct'.
                """);
        }

        private static void PrintSize<T>(string name) where T : struct
        {
            int sizeT = Unsafe.SizeOf<T>();
            int sizeNullable = Unsafe.SizeOf<Nullable<T>>();
            Console.WriteLine($"  | {name,-13} | {sizeT,4} | {sizeNullable,11} | {sizeNullable - sizeT,5} |");
        }

        // =====================================================================================
        // PHAN B: BOXING / UNBOXING Nullable<T>
        // =====================================================================================
        private static void PartB_BoxUnbox()
        {
            Section("PHẦN B", "object o = (int?)5;  rồi unbox thành int? và thành int");

            // ---------------- Truong hop 1: co gia tri ----------------
            int? five = 5;
            object o = five;          // IL: box Nullable<int>  -> runtime xu ly DAC BIET

            Console.WriteLine("  --- Trường hợp HasValue == true ---");
            Console.WriteLine($"    int? five = 5;  object o = five;");
            Console.WriteLine($"    o.GetType()        = {o.GetType()}      <- KHÔNG phải Nullable`1 !");
            Console.WriteLine($"    typeof(int?)       = {typeof(int?)}");
            Console.WriteLine($"    o is null          = {o is null}");
            Console.WriteLine($"    o is int           = {o is int}");

            // Ca hai cach unbox deu CHAY DUOC khi co gia tri.
            // Chu y: '(int)o' bi canh bao CS8605 "Unboxing a possibly null value" - chinh la
            // canh bao ve dung cai se no o truong hop 2 ben duoi. Tat o day de build sach,
            // nhung trong code that thi DUNG tat: no dang bao dung.
#pragma warning disable CS8605
            int unboxToInt = (int)o;
#pragma warning restore CS8605
            int? unboxToNullable = (int?)o;   // khong canh bao: unbox sang int? luon an toan
            Console.WriteLine($"    (int)o             = {unboxToInt}         <- CHẠY ĐƯỢC");
            Console.WriteLine($"    (int?)o            = {unboxToNullable}         <- CŨNG CHẠY ĐƯỢC");

            // ---------------- Truong hop 2: rong ----------------
            int? none = null;
            object? oNull = none;     // box mot Nullable rong -> ra dung tham chieu null

            Console.WriteLine();
            Console.WriteLine("  --- Trường hợp HasValue == false ---");
            Console.WriteLine($"    int? none = null;  object? oNull = none;");
            Console.WriteLine($"    oNull is null      = {oNull is null}      <- KHÔNG có box nào xảy ra cả");

            try
            {
                int boom = (int)oNull!;   // unbox.any int tren tham chieu null
                Console.WriteLine($"    (int)oNull         = {boom}");
            }
            catch (NullReferenceException ex)
            {
                var old = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    (int)oNull         => NÉM {ex.GetType().Name}  <- KHÔNG chạy được");
                Console.ForegroundColor = old;
            }

            int? backToNullable = (int?)oNull;   // unbox.any Nullable<int> tren null -> rong
            Console.WriteLine($"    (int?)oNull        = {(backToNullable.HasValue ? backToNullable.Value.ToString() : "null")}"
                            + $"      <- CHẠY ĐƯỢC, HasValue = {backToNullable.HasValue}");

            Console.WriteLine("""

                CÁI NÀO CHẠY ĐƯỢC - kết luận:
                  - HasValue == true : unbox thành int VÀ thành int? đều chạy.
                  - HasValue == false: chỉ unbox thành int? chạy; unbox thành int ném
                    NullReferenceException (không phải InvalidCastException - vì cái nó gặp là
                    một tham chiếu null chứ không phải object sai kiểu).
                  => int? là kiểu unbox AN TOÀN hơn hẳn. Nếu không chắc, dùng 'o as int?' hoặc
                     'o is int v' để không phải bắt exception.

                CƠ CHẾ - runtime xử lý riêng cho Nullable<T>, không box nó như struct thường:
                    box Nullable<T>   : HasValue ? box(value) : null
                    unbox.any Nullable<T> : null ? default(Nullable<T>) : new Nullable<T>((T)obj)

                  Nói cách khác trên heap KHÔNG bao giờ tồn tại một object kiểu Nullable<int>.
                  Chỉ có hoặc một Int32 đã box, hoặc null. Đó là lý do o.GetType() trả về
                  System.Int32 - và cũng là lý do không có cách nào phân biệt được "int? = 5 đã
                  box" với "int = 5 đã box": chúng là cùng một thứ trên heap.

                HAI HỆ QUẢ HAY GÂY BẤT NGỜ:
                  - Boxing một int? rỗng KHÔNG cấp phát gì cả (ra null), còn boxing một int?
                    có giá trị thì cấp phát y như boxing int - đây là một trong 12 nguồn
                    boxing ở bài 1.6, và nó rất hay lọt vào code qua tham số object/interface.
                  - '(int?)o' KHÔNG phải là ép kiểu an toàn kiểu 'as': nếu o đang giữ một
                    string đã box thì nó vẫn ném InvalidCastException như thường.
                """);
        }

        // =====================================================================================
        // PHAN C: ValueTuple (struct) vs Tuple (class)
        // =====================================================================================
        private const int N = 1_000_000;

        private static void PartC_TupleAllocation()
        {
            Section("PHẦN C", $"{N:N0} × (int, string): ValueTuple (struct) vs Tuple (class)");

            Console.WriteLine($"  Unsafe.SizeOf<ValueTuple<int, string>>() = {Unsafe.SizeOf<ValueTuple<int, string>>()} byte"
                            + "  (int 4 + padding 4 + tham chiếu string 8)");
            Console.WriteLine("  Tuple<int, string>                       = 32 byte MỖI object trên heap"
                            + "  (header 8 + method table 8 + int 4 + pad 4 + ref 8)");
            Console.WriteLine();

            // Chay nong truoc de JIT xong, khong tinh vao so do.
            _ = SumValueTuples(1000);
            _ = SumTuples(1000);

            var (bytesValue, msValue, sumValue) = Measure(() => SumValueTuples(N));
            var (bytesRef, msRef, sumRef) = Measure(() => SumTuples(N));

            Console.WriteLine($"  Hai vòng lặp cho cùng kết quả: {sumValue == sumRef}");
            Console.WriteLine();
            Console.WriteLine("  +--------------------------------------+---------------+---------------+");
            Console.WriteLine("  | Cach tao                             |   Cap phat    |   Thoi gian   |");
            Console.WriteLine("  +--------------------------------------+---------------+---------------+");
            Console.WriteLine($"  | (i, s)           - ValueTuple/struct | {Bytes(bytesValue),13} | {msValue,10:F2} ms |");
            Console.WriteLine($"  | Tuple.Create(i,s) - Tuple/class      | {Bytes(bytesRef),13} | {msRef,10:F2} ms |");
            Console.WriteLine("  +--------------------------------------+---------------+---------------+");
            Console.WriteLine($"  => Tuple cấp phát {Bytes(bytesRef / Math.Max(N, 1))} cho MỖI phần tử,"
                            + $" ValueTuple cấp phát {bytesValue} byte cho CẢ {N:N0} vòng lặp.");

            Console.WriteLine("""

                VÌ SAO ValueTuple KHÔNG CẤP PHÁT GÌ:
                  - ValueTuple<T1,T2> là STRUCT. Biến cục bộ kiểu struct nằm ngay trong khung
                    stack của hàm; hết hàm là biến mất cùng khung. Không có object nào ra đời,
                    GC không có việc gì để làm.
                  - Thực tế JIT còn tiến thêm một bước: nó "xẻ" struct ra (scalar replacement),
                    giữ Item1/Item2 thẳng trong thanh ghi. Tức là không có cả bước ghi ra stack.

                VÌ SAO Tuple TỐN NHIỀU ĐẾN THẾ:
                  - Tuple<T1,T2> là CLASS, nên mỗi Tuple.Create là một lần cấp phát trên heap:
                    16 byte overhead (object header + method table pointer) cho 12 byte dữ liệu
                    thật -> hơn nửa bộ nhớ là phí.
                  - Cấp phát Gen0 rẻ (chỉ là dịch con trỏ), nhưng 1 triệu object vẫn kéo theo
                    nhiều lần GC Gen0, và mỗi lần GC là một lần mọi thread bị dừng.
                  - Truy cập t.Item1 phải đi qua một lần dereference con trỏ -> dễ trượt cache.

                NHƯNG ĐỪNG SUY RA "ValueTuple LUÔN THẮNG":
                  - ValueTuple là struct nên GÁN LÀ COPY. Tuple 8 phần tử toàn double = 64 byte
                    copy mỗi lần truyền -> lúc đó Tuple (chỉ copy 8 byte địa chỉ) lại rẻ hơn.
                  - Nhét ValueTuple vào object/interface/ArrayList là box lại, mất sạch lợi thế.
                  - ValueTuple là struct MUTABLE với field public - vi phạm đúng bài 1.7. BCL cố
                    ý làm vậy vì nó chỉ để làm "túi đựng tạm", đừng bắt chước trong domain model.

                CHI TIẾT VUI: tên field của ValueTuple như (int Id, string Name) chỉ tồn tại lúc
                biên dịch. Runtime chỉ thấy Item1/Item2; tên được cất trong attribute
                [TupleElementNames] để IntelliSense đọc. Nên reflection sẽ không thấy 'Id'.
                """);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long SumValueTuples(int count)
        {
            long sum = 0;
            for (int i = 0; i < count; i++)
            {
                (int, string) t = (i, "abc");      // struct: nam tren stack / trong thanh ghi
                sum += t.Item1 + t.Item2.Length;
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long SumTuples(int count)
        {
            long sum = 0;
            for (int i = 0; i < count; i++)
            {
                Tuple<int, string> t = Tuple.Create(i, "abc");   // class: 1 object/vong tren heap
                sum += t.Item1 + t.Item2.Length;
            }
            return sum;
        }

        // ------------------------------- helper hien thi -------------------------------------
        private static (long bytes, double ms, long sum) Measure(Func<long> action)
        {
            // Stopwatch la CLASS -> new/StartNew cap phat. Phai tao TRUOC khi doc moc 'before',
            // khong thi chinh dung cu do lai lam ban so do (40 byte "tu nhien" hien ra o cot
            // cap phat cua ValueTuple).
            var sw = new Stopwatch();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart();
            long result = action();
            sw.Stop();
            long after = GC.GetAllocatedBytesForCurrentThread();

            return (after - before, sw.Elapsed.TotalMilliseconds, result);
        }

        private static string Bytes(long bytes)
            => bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:N2} MB" : $"{bytes:N0} B";

        private static void Section(string tag, string title)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 100));
            Console.WriteLine($"  {tag}: {title}");
            Console.WriteLine(new string('=', 100));
        }

        private static void Result(int expected, int actual, string expr)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = actual == expected ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"  => {expr} = {actual}   (kỳ vọng {expected})   {(actual == expected ? "ĐÚNG" : "SAI")}");
            Console.ForegroundColor = old;
        }

        public static void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;

            PartA_NullableSize();
            PartB_BoxUnbox();
            PartC_TupleAllocation();

            Console.WriteLine();
            Console.Write("Chạy BenchmarkDotNet cho phần C? (cần Release build, vài phút) [y/N]: ");
            string? answer = Console.ReadLine();
            if (answer is not null && answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                BenchmarkRunner.Run<TupleAllocationBenchmarks>();
                BenchmarkRunner.Run<NullableBoxingBenchmarks>();
            }
        }
    }

    // Exercise 1.9c: cot can nhin la Allocated.
    [MemoryDiagnoser]
    public class TupleAllocationBenchmarks
    {
        private const int N = 1_000_000;
        private const string S = "abc";

        // ------- tao roi dung ngay, khong giu lai -------

        [Benchmark(Baseline = true, Description = "1tr Tuple.Create(int,string) - class")]
        public long Tuple_Class()
        {
            long sum = 0;
            for (int i = 0; i < N; i++)
            {
                var t = Tuple.Create(i, S);
                sum += t.Item1 + t.Item2.Length;
            }
            return sum;
        }

        [Benchmark(Description = "1tr (int,string) - ValueTuple/struct")]
        public long ValueTuple_Struct()
        {
            long sum = 0;
            for (int i = 0; i < N; i++)
            {
                var t = (i, S);
                sum += t.Item1 + t.Item2.Length;
            }
            return sum;
        }

        // ------- giu lai trong mang: lo ro khac biet ve BO CUC bo nho -------
        // Tuple[]      : 8 byte con tro/phan tu + 32 byte object/phan tu = 40 MB, rai rac heap.
        // ValueTuple[] : 16 byte du lieu/phan tu nam LIEN TIEP = 16 MB, chay cache rat tot.

        [Benchmark(Description = "mang 1tr Tuple<int,string> - con tro + object")]
        public Tuple<int, string>[] Tuple_Array()
        {
            var arr = new Tuple<int, string>[N];
            for (int i = 0; i < N; i++) arr[i] = Tuple.Create(i, S);
            return arr;
        }

        [Benchmark(Description = "mang 1tr (int,string) - du lieu lien tiep")]
        public (int, string)[] ValueTuple_Array()
        {
            var arr = new (int, string)[N];
            for (int i = 0; i < N; i++) arr[i] = (i, S);
            return arr;
        }
    }

    // Exercise 1.9b: do gia cua viec box Nullable<T>.
    [MemoryDiagnoser]
    public class NullableBoxingBenchmarks
    {
        private const int N = 100_000;

        private int? _hasValue;
        private int? _empty;

        [GlobalSetup]
        public void Setup()
        {
            _hasValue = 5;
            _empty = null;
        }

        [Benchmark(Baseline = true, Description = "box int? co gia tri -> cap phat nhu box int")]
        public int Box_HasValue()
        {
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                object? o = _hasValue;     // box: cap phat 24 byte tren heap
                if (o is not null) count++;
            }
            return count;
        }

        [Benchmark(Description = "box int? rong -> ra null, KHONG cap phat")]
        public int Box_Empty()
        {
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                object? o = _empty;        // ra null, khong co object nao ra doi
                if (o is not null) count++;
            }
            return count;
        }

        [Benchmark(Description = "khong box: doc HasValue truc tiep")]
        public int NoBox()
        {
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                if (_hasValue.HasValue) count++;
            }
            return count;
        }
    }

    // Results
    // (dan bang ket qua sau khi chay Release)
}
