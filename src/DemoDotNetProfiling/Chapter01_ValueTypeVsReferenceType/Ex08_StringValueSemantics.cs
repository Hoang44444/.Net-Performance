using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace DemoDotNetProfiling.Chapter01
{
    // =========================================================================================
    // Bai 1.8 - STRING: REFERENCE TYPE MANG TINH CACH VALUE TYPE
    //
    //   1) Chung minh string bat bien (Replace/Substring tao string MOI, khong sua ban goc).
    //   2) Chung minh literal interning: ReferenceEquals giua 2 literal giong nhau,
    //      va giua 1 literal voi 1 string tao luc chay (string.Concat).
    //   3) Do allocation khi noi chuoi bang += trong vong lap 10.000 lan vs StringBuilder.
    //   4) Cau hoi: vi sao string == so sanh NOI DUNG nhung string van la REFERENCE TYPE?
    //
    // Y chinh: string nam tren HEAP va bien chi giu dia chi - dung nghia reference type.
    // Nhung 3 thu: bat bien + interning + operator== bi nap chong, lam no HANH XU nhu value
    // type. Ca 3 deu la lop son o tren; ban chat ben duoi van la tham chieu, va cho nao lop
    // son bong ra (gan vao object, dung ReferenceEquals) la thay ngay.
    // =========================================================================================
    public class StringValueSemanticsDemo
    {
        // =====================================================================================
        // PHAN A: BAT BIEN - moi "phep sua" that ra la mot string MOI
        // =====================================================================================
        private static void PartA_Immutability()
        {
            Section("PHẦN A", "string BẤT BIẾN: Replace/Substring trả về chuỗi MỚI");

            string goc = "Hello, World";
            Console.WriteLine($"  goc                       = \"{goc}\"   {Id(goc)}");

            // Replace/Substring/ToUpper... khong co cai nao ghi de len bo nho cua 'goc'.
            // Chung DOC 'goc' roi cap phat mot string khac tren heap.
            string thay = goc.Replace("World", "C#");
            string cat = goc.Substring(7);
            string hoa = goc.ToUpperInvariant();

            Console.WriteLine($"  goc.Replace(\"World\",\"C#\") = \"{thay}\"       {Id(thay)}");
            Console.WriteLine($"  goc.Substring(7)          = \"{cat}\"              {Id(cat)}");
            Console.WriteLine($"  goc.ToUpperInvariant()    = \"{hoa}\"   {Id(hoa)}");
            Console.WriteLine();
            Console.WriteLine($"  => sau tất cả: goc vẫn là \"{goc}\"   {Id(goc)}");
            Result(expected: "Hello, World", actual: goc, expr: "goc (sau 3 phép \"sửa\")");

            Console.WriteLine();
            Console.WriteLine("  Mỗi ô Id# ở trên là một object KHÁC nhau trên heap:");
            Console.WriteLine($"    ReferenceEquals(goc, thay) = {ReferenceEquals(goc, thay)}");
            Console.WriteLine($"    ReferenceEquals(goc, cat)  = {ReferenceEquals(goc, cat)}");

            // Chi tiet dang nho: khi ket qua chac chan y het ban goc, BCL tra ve chinh 'this'
            // (khong cap phat gi ca). Duoc phep lam vay CHINH VI string bat bien - khong ai
            // sua duoc ban tra ve, nen chia se chung mot object la an toan.
            Console.WriteLine();
            Console.WriteLine("  Tối ưu chỉ có được nhờ tính bất biến - không match thì trả về CHÍNH nó:");
            string khongMatch = goc.Replace("Java", "C#");
            string catTuDauDenCuoi = goc.Substring(0);
            Console.WriteLine($"    ReferenceEquals(goc, goc.Replace(\"Java\",\"C#\")) = {ReferenceEquals(goc, khongMatch)}");
            Console.WriteLine($"    ReferenceEquals(goc, goc.Substring(0))         = {ReferenceEquals(goc, catTuDauDenCuoi)}");

            Console.WriteLine("""

                VÌ SAO PHẢI BẤT BIẾN:
                  - string được chia sẻ tự do: làm key Dictionary, làm tham số, cache, hằng số.
                    Nếu sửa được tại chỗ thì một chỗ sửa là mọi chỗ khác vỡ - y hệt bẫy alias
                    của reference type ở bài 1.1.
                  - Bất biến => hash code không đổi => làm key an toàn.
                  - Bất biến => chia sẻ chung một object là vô hại => mới có interning (phần B).
                  - Cái giá: mọi phép "sửa" đều cấp phát. Đó là lý do phần C tồn tại.
                """);
        }

        // =====================================================================================
        // PHAN B: INTERNING - literal giong nhau thi dung chung MOT object
        // =====================================================================================
        private static void PartB_Interning()
        {
            Section("PHẦN B", "Literal interning: 2 literal giống nhau = 1 object");

            // Compiler nhet moi literal vao metadata cua assembly; luc nap, runtime dua chung
            // vao INTERN POOL (bang bam string dung chung cho ca process). Hai literal giong
            // nhau -> tra ve cung mot tham chieu.
            string a = "dotnet";
            string b = "dotnet";

            // "dot" + "net" voi ca hai deu la hang -> Roslyn gop luon thanh literal "dotnet"
            // ngay luc bien dich, nen cung duoc intern.
            const string dot = "dot";
            string c = dot + "net";

            // Con day la noi luc CHAY: Concat khong tra cuu intern pool, no cap phat moi.
            string d = string.Concat("dot", Net());

            Console.WriteLine($"  a = \"dotnet\" (literal)                {Id(a)}");
            Console.WriteLine($"  b = \"dotnet\" (literal)                {Id(b)}");
            Console.WriteLine($"  c = const \"dot\" + \"net\"               {Id(c)}   <- gộp lúc BIÊN DỊCH");
            Console.WriteLine($"  d = string.Concat(\"dot\", Net())       {Id(d)}   <- tạo lúc CHẠY");
            Console.WriteLine();
            Console.WriteLine($"    ReferenceEquals(a, b) = {ReferenceEquals(a, b),-5}  <- cùng object trong intern pool");
            Console.WriteLine($"    ReferenceEquals(a, c) = {ReferenceEquals(a, c),-5}  <- hằng số được gộp rồi intern");
            Console.WriteLine($"    ReferenceEquals(a, d) = {ReferenceEquals(a, d),-5}  <- object MỚI, không nằm trong pool");
            Console.WriteLine($"    a == d                = {a == d,-5}  <- nhưng == vẫn True vì so NỘI DUNG");

            // string.Intern: tra pool - co thi tra ve ban trong pool, chua co thi them vao.
            string e = string.Intern(d);
            Console.WriteLine();
            Console.WriteLine($"  e = string.Intern(d)                  {Id(e)}");
            Console.WriteLine($"    ReferenceEquals(a, e) = {ReferenceEquals(a, e),-5}  <- Intern kéo về đúng object trong pool");
            Console.WriteLine($"    string.IsInterned(d) != null = {string.IsInterned(d) != null}");

            Console.WriteLine("""

                LƯU Ý VỀ INTERNING:
                  - CHỈ literal (và hằng số được gộp lúc biên dịch) mới tự động vào pool.
                    Mọi string sinh lúc chạy - Concat, Substring, ToString, đọc file, parse
                    JSON, nhận từ HTTP - đều là object mới, KHÔNG được intern.
                  - Nên đừng bao giờ dùng ReferenceEquals để so string, và cũng đừng lấy
                    "hai chuỗi bằng nhau" làm bằng chứng rằng chúng là cùng một object.
                  - string.Intern có giá: pool sống đến hết đời process, không bị GC thu.
                    Intern bừa dữ liệu người dùng = tự tay tạo rò rỉ bộ nhớ.
                """);
        }

        // =====================================================================================
        // PHAN C: DO ALLOCATION - += 10.000 lan vs StringBuilder
        // =====================================================================================
        private const int LoopCount = 10_000;

        private static void PartC_ConcatAllocation()
        {
            Section("PHẦN C", $"Nối chuỗi {LoopCount:N0} lần: += vs StringBuilder");

            // Chay nong truoc de JIT xong, khong tinh vao so do.
            _ = ConcatWithPlusEquals(64);
            _ = ConcatWithStringBuilder(64);

            var (bytesPlus, msPlus, lenPlus) = Measure(() => ConcatWithPlusEquals(LoopCount));
            var (bytesSb, msSb, lenSb) = Measure(() => ConcatWithStringBuilder(LoopCount));

            Console.WriteLine($"  Cả hai cho ra chuỗi dài {lenPlus:N0} ký tự (giống nhau: {lenPlus == lenSb}).");
            Console.WriteLine();
            Console.WriteLine("  +----------------------------+---------------+---------------+");
            Console.WriteLine("  | Cach lam                   |   Cap phat    |   Thoi gian   |");
            Console.WriteLine("  +----------------------------+---------------+---------------+");
            Console.WriteLine($"  | s += \"x\" trong vong lap    | {Bytes(bytesPlus),13} | {msPlus,10:F2} ms |");
            Console.WriteLine($"  | StringBuilder.Append       | {Bytes(bytesSb),13} | {msSb,10:F2} ms |");
            Console.WriteLine("  +----------------------------+---------------+---------------+");
            Console.WriteLine($"  => += cấp phát gấp {(double)bytesPlus / Math.Max(bytesSb, 1):N0} lần"
                            + $" và chậm gấp {msPlus / Math.Max(msSb, 0.001):N0} lần.");

            Console.WriteLine($"""

                VÌ SAO += LẠI TỆ ĐẾN THẾ - độ phức tạp O(n²):
                  - string bất biến nên 's += "x"' KHÔNG ghi thêm ký tự vào s. Compiler dịch nó
                    thành 's = string.Concat(s, "x")': cấp phát một string dài hơn 1 ký tự,
                    COPY toàn bộ s sang, rồi bỏ s cũ cho GC.
                  - Vòng thứ i copy i ký tự => tổng copy = 1+2+...+n = n²/2 ký tự.
                    Với n = {LoopCount:N0}: ~{(long)LoopCount * LoopCount / 2:N0} ký tự
                    = ~{Bytes((long)LoopCount * LoopCount)} (2 byte/ký tự). Đúng bằng số đo ở trên.
                  - Và {LoopCount:N0} string rác đó đều rơi vào Gen0 => ép GC chạy liên tục.

                VÌ SAO StringBuilder RẺ:
                  - Nó giữ buffer char[] MUTABLE và ghi thẳng vào đó. Hết chỗ thì nối thêm chunk
                    mới (nhân đôi dung lượng), nên tổng chi phí là O(n) - khoảng log2(n) lần
                    cấp phát thay vì n lần.
                  - ToString() ở cuối mới cấp phát đúng 1 string kết quả.

                KHI NÀO KHÔNG CẦN StringBuilder:
                  - Số mảnh biết trước và ít: 'a + b + c + d' được compiler gộp thành MỘT lần
                    gọi string.Concat -> chỉ 1 lần cấp phát, còn nhanh hơn StringBuilder.
                  - Nối một tập hợp: string.Join / string.Concat(IEnumerable) đã tối ưu sẵn.
                  - Biết trước độ dài kết quả: string.Create ghi thẳng vào buffer của string.
                  - StringBuilder chỉ thắng khi số lần nối KHÔNG biết trước lúc biên dịch:
                    vòng lặp, đệ quy, nhánh điều kiện.
                """);
        }

        // [MethodImpl(NoInlining)] de JIT khong "nhin xuyen" vao roi toi uu mat vong lap.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ConcatWithPlusEquals(int n)
        {
            string s = "";
            for (int i = 0; i < n; i++)
                s += "x";        // = s = string.Concat(s, "x") -> cap phat + copy MOI vong
            return s;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ConcatWithStringBuilder(int n)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
                sb.Append('x');  // ghi thang vao buffer mutable, khong cap phat string trung gian
            return sb.ToString();
        }

        // =====================================================================================
        // PHAN D: TRA LOI CAU HOI CUA DE BAI
        // =====================================================================================
        private static void PartD_WhyEqualsCompareContent()
        {
            Section("PHẦN D", "Vì sao string == so sánh nội dung nhưng vẫn là reference type?");

            string x = "dotnet";
            string y = string.Concat("dot", Net());     // cung noi dung, KHAC object

            Console.WriteLine($"  string x = \"dotnet\";    {Id(x)}");
            Console.WriteLine($"  string y = Concat(...); {Id(y)}");
            Console.WriteLine();
            Console.WriteLine($"    x == y                = {x == y,-5}  <- gọi string.op_Equality -> so từng ký tự");
            Console.WriteLine($"    x.Equals(y)           = {x.Equals(y),-5}  <- string override Equals");
            Console.WriteLine($"    ReferenceEquals(x, y) = {ReferenceEquals(x, y),-5}  <- SỰ THẬT: hai object khác nhau");

            // Man chung minh quan trong nhat: doi KIEU TINH cua bien la doi luon toan tu duoc
            // chon. Viec chon overload cua == xay ra luc BIEN DICH, khong phai luc chay.
            object ox = x;
            object oy = y;
            Console.WriteLine();
            Console.WriteLine("  Gán y nguyên hai chuỗi đó vào biến kiểu object rồi so lại:");
            Console.WriteLine($"    ox == oy              = {ox == oy,-5}  <- gọi object.op_Equality -> so ĐỊA CHỈ");
            Console.WriteLine($"    ox.Equals(oy)         = {ox.Equals(oy),-5}  <- Equals là ảo, vẫn vào String.Equals");
            Result(expected: "False", actual: (ox == oy).ToString(), expr: "(object)x == (object)y ");

            Console.WriteLine("""

                TRẢ LỜI:
                  string LÀ reference type theo đúng mọi tiêu chí kỹ thuật: object nằm trên heap,
                  biến chỉ giữ 8 byte địa chỉ, gán là copy địa chỉ, null được, có object header,
                  do GC quản lý. Việc '==' so nội dung KHÔNG mâu thuẫn với điều đó, vì:

                  1) '==' KHÔNG phải phép so sánh tham chiếu cố định của ngôn ngữ - nó là một
                     TOÁN TỬ CÓ THỂ NẠP CHỒNG. Lớp String tự khai báo:
                         public static bool operator ==(string? a, string? b) => string.Equals(a, b);
                     nên khi kiểu tĩnh là string, compiler phát ra lời gọi hàm đó (so ordinal
                     từng ký tự) chứ không phát ra lệnh so địa chỉ. Việc chọn toán tử diễn ra
                     lúc BIÊN DỊCH theo kiểu tĩnh của biến - đó là lý do gán sang object thì kết
                     quả đổi ngay như demo ở trên. Nếu 'giá trị' thật sự nằm ở nội dung, thì đổi
                     kiểu khai báo đã không thể đổi được kết quả.

                  2) Sở dĩ DÁM cho == so nội dung là nhờ BẤT BIẾN. Nội dung không bao giờ đổi nên
                     "bằng nhau bây giờ" cũng là "bằng nhau mãi mãi" - đúng tính chất mà value
                     type có sẵn. Với một class mutable, so sánh kiểu này rất dễ thành bug.

                  3) Đây thuần túy là tiện lợi cho lập trình viên: string được dùng nhiều đến mức
                     nếu bắt viết .Equals() thì 99% chỗ dùng '==' sẽ thành bug ngầm.

                  GỌI CHUNG: string là REFERENCE TYPE có VALUE SEMANTICS. Ba thứ tạo nên nó:
                     bất biến  +  interning  +  ==/Equals/GetHashCode override theo nội dung.

                  CHỖ LỚP SƠN BONG RA (phải nhớ):
                    - Gán vào object / dynamic / generic không ràng buộc -> == quay về so địa chỉ.
                    - ReferenceEquals luôn nói sự thật: hai chuỗi bằng nhau vẫn là 2 object.
                    - Mỗi phép "sửa" là một lần cấp phát trên heap (phần C).
                    - string vẫn null được - value type thật thì không.
                """);
        }

        // ------------------------------- helper hien thi -------------------------------------

        // "So hieu object": identity hash cua CLR. Hai bien cung tro toi mot object thi so nay
        // giong nhau. Dung thay cho dia chi that (dia chi bi GC doi cho lien tuc).
        private static string Id(string s) => $"[Id#{RuntimeHelpers.GetHashCode(s),10}]";

        // Chan Roslyn gop hang so, de string.Concat that su chay luc runtime.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Net() => "net";

        private static (long bytes, double ms, int length) Measure(Func<string> action)
        {
            // Stopwatch la CLASS -> new/StartNew cap phat. Phai tao TRUOC khi doc moc 'before',
            // khong thi chinh dung cu do lai lam ban so do.
            var sw = new Stopwatch();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart();
            string result = action();
            sw.Stop();
            long after = GC.GetAllocatedBytesForCurrentThread();

            return (after - before, sw.Elapsed.TotalMilliseconds, result.Length);
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

        private static void Result(string expected, string actual, string expr)
        {
            var old = Console.ForegroundColor;
            bool ok = string.Equals(expected, actual, StringComparison.Ordinal);
            Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"  => {expr} = \"{actual}\"   (kỳ vọng \"{expected}\")   {(ok ? "ĐÚNG" : "SAI")}");
            Console.ForegroundColor = old;
        }

        public static void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;

            PartA_Immutability();
            PartB_Interning();
            PartC_ConcatAllocation();
            PartD_WhyEqualsCompareContent();

            Console.WriteLine();
            Console.Write("Chạy BenchmarkDotNet cho phần C? (cần Release build, vài phút) [y/N]: ");
            string? answer = Console.ReadLine();
            if (answer is not null && answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
                BenchmarkRunner.Run<StringConcatBenchmarks>();
        }
    }

    // Exercise 1.8c: ban benchmark chuan cua phan C.
    // Cot can nhin la Allocated, khong phai Mean.
    [MemoryDiagnoser]
    public class StringConcatBenchmarks
    {
        [Params(1_000, 10_000)]
        public int N;

        [Benchmark(Baseline = true, Description = "s += x  - O(n^2), cap phat n string")]
        public string PlusEquals()
        {
            string s = "";
            for (int i = 0; i < N; i++) s += "x";
            return s;
        }

        [Benchmark(Description = "StringBuilder.Append - O(n), buffer mutable")]
        public string StringBuilderAppend()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < N; i++) sb.Append('x');
            return sb.ToString();
        }

        // Biet truoc do dai -> cap phat DUNG 1 string va ghi thang vao bo nho cua no.
        // Nhanh nhat, nhung chi dung duoc khi da biet do dai cuoi cung.
        [Benchmark(Description = "string.Create - cap phat dung 1 lan")]
        public string StringCreate()
            => string.Create(N, 'x', static (span, c) => span.Fill(c));
    }

    // Results
    // (dan bang ket qua sau khi chay Release)
}
