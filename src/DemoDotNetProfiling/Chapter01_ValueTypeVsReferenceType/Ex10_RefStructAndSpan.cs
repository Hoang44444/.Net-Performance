// Bo dau // o dong duoi de bat cac doan code CO TINH GAY LOI BIEN DICH o PHAN B.
// Muc dich cua bai la DOC LOI, nen dung de no bat mac dinh (project se khong build duoc).
//#define REF_STRUCT_COMPILE_ERRORS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace DemoDotNetProfiling.Chapter01
{
    // =========================================================================================
    // Bai 1.10 - ref struct / Span<T> VA CHON struct HAY class
    //
    //   1) Parse chuoi ngay "yyyy-MM-dd" bang ReadOnlySpan<char>.Slice + int.Parse,
    //      do allocation = 0.
    //   2) Thu dua 1 ref struct lam field cua class, lam bien trong async method
    //      -> quan sat loi bien dich, giai thich vi sao compiler cam.
    //   3) Ap 4 tieu chi cua Microsoft cho Vector3 / Customer / HttpRequestContext.
    //
    // Y chinh: Span<T> la mot ref struct - mot "cua so nhin vao bo nho co san" gom con tro +
    // do dai. No khong so huu gi ca, nen tao ra no khong ton dong nao. Doi lai, compiler phai
    // bao dam no KHONG BAO GIO leo len heap, vi con tro no giu co the tro vao khung stack cua
    // ham dang chay - khung do chet la con tro thanh rac.
    // =========================================================================================
    public class RefStructAndSpanDemo
    {
        // =====================================================================================
        // PHAN A: PARSE "yyyy-MM-dd" - KHONG CAP PHAT MOT BYTE NAO
        // =====================================================================================

        // Khong he cham toi string nao: chi truot con tro tren bo nho da co san.
        private static bool TryParseIsoDate(ReadOnlySpan<char> s, out DateOnly date)
        {
            date = default;

            if (s.Length != 10) return false;
            if (s[4] != '-' || s[7] != '-') return false;

            // Slice KHONG copy: no tra ve mot Span khac tro vao ĐÚNG bo nho do,
            // chi khac offset va length. Chi phi = tao 2 gia tri trong thanh ghi.
            if (!int.TryParse(s.Slice(0, 4), out int year)) return false;
            if (!int.TryParse(s.Slice(5, 2), out int month)) return false;
            if (!int.TryParse(s.Slice(8, 2), out int day)) return false;

            if ((uint)(month - 1) >= 12) return false;
            if (day < 1 || day > DateTime.DaysInMonth(year, month)) return false;

            date = new DateOnly(year, month, day);   // DateOnly la struct -> van khong cap phat
            return true;
        }

        // Ban "ngay tho" ma ai cung viet dau tien - va cai gia cua no.
        private static bool TryParseIsoDateWithSplit(string s, out DateOnly date)
        {
            date = default;

            string[] parts = s.Split('-');   // cap phat: 1 mang + 3 string moi
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out int year)) return false;
            if (!int.TryParse(parts[1], out int month)) return false;
            if (!int.TryParse(parts[2], out int day)) return false;

            if ((uint)(month - 1) >= 12) return false;
            if (day < 1 || day > DateTime.DaysInMonth(year, month)) return false;

            date = new DateOnly(year, month, day);
            return true;
        }

        private const int ParseCount = 100_000;
        private const string SampleDate = "2026-09-11";

        private static void PartA_ZeroAllocParse()
        {
            Section("PHẦN A", "Parse \"yyyy-MM-dd\": ReadOnlySpan.Slice vs string.Split");

            // Kiem tra dung truoc da - nhanh khong co nghia gi neu sai.
            bool okSpan = TryParseIsoDate(SampleDate.AsSpan(), out DateOnly d1);
            bool okSplit = TryParseIsoDateWithSplit(SampleDate, out DateOnly d2);
            Console.WriteLine($"  TryParseIsoDate(\"{SampleDate}\")          -> {okSpan}, {d1:yyyy-MM-dd}");
            Console.WriteLine($"  TryParseIsoDateWithSplit(\"{SampleDate}\") -> {okSplit}, {d2:yyyy-MM-dd}");
            Console.WriteLine($"  Hai bản cho cùng kết quả: {d1 == d2}");

            Console.WriteLine();
            Console.WriteLine("  Chuỗi sai định dạng đều bị chặn (không ném exception):");
            foreach (string bad in new[] { "2026-13-01", "2026-02-30", "2026/09/11", "26-09-11", "" })
                Console.WriteLine($"    {'"' + bad + '"',-14} -> {TryParseIsoDate(bad.AsSpan(), out _)}");

            // Chay nong truoc de JIT xong, khong tinh vao so do.
            _ = ParseManySpan(1000);
            _ = ParseManySplit(1000);

            var (bytesSpan, msSpan, cSpan) = Measure(() => ParseManySpan(ParseCount));
            var (bytesSplit, msSplit, cSplit) = Measure(() => ParseManySplit(ParseCount));

            Console.WriteLine();
            Console.WriteLine($"  Parse {ParseCount:N0} lần, cả hai parse thành công {cSpan:N0} / {cSplit:N0} lần:");
            Console.WriteLine();
            Console.WriteLine("  +------------------------------------+---------------+---------------+");
            Console.WriteLine("  | Cach parse                         |   Cap phat    |   Thoi gian   |");
            Console.WriteLine("  +------------------------------------+---------------+---------------+");
            Console.WriteLine($"  | ReadOnlySpan<char>.Slice           | {Bytes(bytesSpan),13} | {msSpan,10:F2} ms |");
            Console.WriteLine($"  | string.Split('-')                  | {Bytes(bytesSplit),13} | {msSplit,10:F2} ms |");
            Console.WriteLine("  +------------------------------------+---------------+---------------+");
            Result(expected: 0, actual: bytesSpan, expr: "byte cấp phát của bản Span");
            Console.WriteLine($"     (bản Split cấp phát ~{bytesSplit / Math.Max(ParseCount, 1)} byte cho MỖI lần parse)");

            Console.WriteLine("""

                VÌ SAO BẢN SPAN CẤP PHÁT ĐÚNG 0 BYTE:
                  - Span<T>/ReadOnlySpan<T> chỉ gồm 2 field: một tham chiếu quản lý (ref T) và
                    một int Length -> 16 byte, nằm gọn trong thanh ghi. Nó KHÔNG sở hữu dữ liệu,
                    chỉ là một "cửa sổ" nhìn vào bộ nhớ đã có sẵn.
                  - "2026-09-11".AsSpan() không copy ký tự nào: nó trỏ thẳng vào vùng char của
                    chính string đó.
                  - Slice(0, 4) cũng không copy: chỉ là (con trỏ + 0, độ dài 4). Cắt bao nhiêu
                    lát cũng vẫn 0 byte heap.
                  - int.TryParse có overload nhận ReadOnlySpan<char>, nên chuỗi con không cần
                    tồn tại dưới dạng string thật.
                  - Kết quả DateOnly cũng là struct -> vẫn không cấp phát.

                VÌ SAO BẢN SPLIT TỐN:
                  - Split trả về string[]: 1 mảng + 3 string mới, MỖI LẦN GỌI. Cả 4 object đó
                    chết ngay sau khi parse xong -> rác Gen0 thuần tuý.
                  - Trong một API xử lý vài nghìn request/giây, đúng dòng Split đó là thứ nuôi
                    GC. Đây là kiểu tối ưu "không đánh đổi gì": cùng độ dễ đọc, cùng độ đúng,
                    nhưng bớt được toàn bộ rác.

                LƯU Ý: chỉ dùng Span cho phần XỬ LÝ. Đừng cố giữ Span lại làm dữ liệu - muốn giữ
                một lát cắt qua thời gian thì dùng Memory<T>/ReadOnlyMemory<T> (là struct thường,
                được phép nằm trên heap). Phần B giải thích vì sao.
                """);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ParseManySpan(int count)
        {
            long ok = 0;
            ReadOnlySpan<char> span = SampleDate.AsSpan();
            for (int i = 0; i < count; i++)
                if (TryParseIsoDate(span, out _)) ok++;
            return ok;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ParseManySplit(int count)
        {
            long ok = 0;
            for (int i = 0; i < count; i++)
                if (TryParseIsoDateWithSplit(SampleDate, out _)) ok++;
            return ok;
        }

        // =====================================================================================
        // PHAN B: NHUNG CHO COMPILER CAM ref struct - VA VI SAO
        // =====================================================================================

        // Mot ref struct tu viet, de thay no khong co gi huyen bi: cung chi la con tro + do dai.
        private ref struct LineReader
        {
            private ReadOnlySpan<char> _rest;

            public LineReader(ReadOnlySpan<char> text) => _rest = text;

            public bool TryReadLine(out ReadOnlySpan<char> line)
            {
                if (_rest.IsEmpty) { line = default; return false; }

                int nl = _rest.IndexOf('\n');
                if (nl < 0) { line = _rest; _rest = default; return true; }

                line = _rest.Slice(0, nl);
                _rest = _rest.Slice(nl + 1);
                return true;
            }
        }

        // Tach dong nhung khong tao ra string nao - chi dem, de do cho sach.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long CountLines(ReadOnlySpan<char> text)
        {
            var reader = new LineReader(text);
            long count = 0;
            while (reader.TryReadLine(out _)) count++;
            return count;
        }

        private static void PartB_RefStructRestrictions()
        {
            Section("PHẦN B", "ref struct: dùng được ở đâu, và bị cấm ở đâu");

            // ---- Dung DUNG cach: bien cuc bo, khong bao gio roi khoi stack ----
            const string text = "alpha\nbeta\ngamma";

            // In ra truoc cho de nhin - phan in KHONG duoc nam trong vung do,
            // vi Console.WriteLine + chuoi noi suy tu no da cap phat roi.
            var printer = new LineReader(text.AsSpan());
            int index = 0;
            while (printer.TryReadLine(out ReadOnlySpan<char> line))
                Console.WriteLine($"    dòng {++index}: \"{line}\"   (dài {line.Length} ký tự)");

            // Gio moi do: chi tach dong, khong I/O, khong tao string nao.
            var (bytesSplitLines, _, lineCount) = Measure(() => CountLines(text.AsSpan()));
            Console.WriteLine($"  Tách {lineCount} dòng bằng LineReader (ref struct), cấp phát: {bytesSplitLines} byte");
            Console.WriteLine("     (cùng việc đó bằng text.Split('\\n') là 1 mảng + 3 string mới)");

            // Ban async HOP LE: span chet TRUOC await nen khong bi nhac len state machine.
            int asyncValue = GoodAsync().GetAwaiter().GetResult();
            Console.WriteLine($"  GoodAsync() - span dùng xong trước await -> biên dịch được, trả về {asyncValue}");

            Console.WriteLine("""

                NHỮNG CHỖ COMPILER CẤM - bỏ dấu // ở dòng #define đầu file này để tự đọc lỗi.
                Bảng dưới là mã lỗi THẬT do compiler của .NET 10 / C# 14 in ra:

                +------------------------------------------+--------+----------------------------------+
                | Viet gi                                  | Loi    | Vi sao cam                       |
                +------------------------------------------+--------+----------------------------------+
                | class C { Span<int> _s; }                | CS8345 | field cua class -> nam tren heap |
                | object o = span;                         | CS0029 | box = chep gia tri len heap      |
                | new List<Span<int>>()                    | CS9244 | T co the bi cat vao heap         |
                | Span<int>[] arr;                         | CS0611 | phan tu mang nam tren heap       |
                | Func<int> f = () => span[0];             | CS8175 | closure la mot CLASS tren heap   |
                | async: dung span sau 'await'             | CS4007 | state machine nam tren heap      |
                | iterator: dung span sau 'yield return'   | CS4007 | state machine nam tren heap      |
                +------------------------------------------+--------+----------------------------------+
                (Mã lỗi có đổi theo phiên bản compiler: trước C# 13 thì dòng generic là CS0306,
                 còn async/iterator là CS4012/CS4013 - cấm thẳng, không cần biết có qua await hay không.)

                MỘT LỜI GIẢI THÍCH CHO CẢ BẢNG:
                  Span<T> giữ một con trỏ có thể trỏ vào ba nơi: mảng trên heap, bộ nhớ
                  stackalloc trên STACK, hoặc bộ nhớ native. Với trường hợp stackalloc, con trỏ
                  đó chỉ còn hợp lệ chừng nào khung stack của hàm còn sống.

                  Nếu span được phép nằm trên heap thì nó có thể SỐNG LÂU HƠN khung stack mà nó
                  trỏ tới. Hàm return xong, khung stack bị tái sử dụng cho lời gọi khác, còn cái
                  span trên heap vẫn cầm con trỏ cũ -> nó đang chỉ vào rác, hoặc vào dữ liệu của
                  một hàm hoàn toàn khác. Đọc ra thì được số bậy, ghi vào thì hỏng bộ nhớ của
                  người khác. Đây chính là lỗi dangling pointer của C/C++ mà .NET cam kết không
                  bao giờ có.

                  Vì runtime không thể phát hiện chuyện đó lúc chạy (không có ai kiểm tra được
                  một con trỏ còn hợp lệ hay không), compiler chọn cách chặn sạch từ lúc BIÊN
                  DỊCH: đánh dấu kiểu là 'ref struct' = "chỉ được tồn tại trên stack", rồi cấm
                  mọi con đường có thể đưa nó lên heap. Cả 7 dòng trong bảng chỉ là 7 con đường
                  khác nhau dẫn tới cùng một chỗ.

                  Riêng async/iterator đáng chú ý: nhìn thì chúng là biến cục bộ như thường,
                  nhưng compiler biến cả hàm thành một STATE MACHINE và mọi local phải sống qua
                  await/yield đều bị nhấc thành FIELD của state machine - mà state machine thì
                  được cấp phát trên heap khi hàm tạm dừng. Nên bản chất vẫn là "field của
                  class" ở dòng đầu bảng. (C# 13 trở đi nới lỏng: được khai báo ref struct local
                  trong async/iterator, miễn là nó KHÔNG sống qua await/yield - đúng ranh giới
                  "có bị nhấc lên heap hay không".)

                LỐI THOÁT KHI THẬT SỰ CẦN GIỮ LẠI:
                  - Memory<T> / ReadOnlyMemory<T>: cùng ý tưởng "cửa sổ nhìn vào bộ nhớ" nhưng
                    là struct THƯỜNG, được nằm trên heap, được đi qua await. Khi cần đọc thì gọi
                    .Span để lấy Span<T> ngay tại chỗ dùng. Đây là cách mọi API async trong BCL
                    làm (Stream.ReadAsync nhận Memory<byte>, không nhận Span<byte>).
                  - Hoặc chấp nhận copy: span.ToArray() / new string(span).
                """);

#if REF_STRUCT_COMPILE_ERRORS
            // ==========================================================================
            // Cac doan duoi day CO CHU DINH khong bien dich duoc.
            // Bat #define o dau file de compiler in ra loi that.
            //
            // LUU Y KHI DOC LOI: Roslyn bien dich theo TUNG PHA - pha khai bao truoc,
            // pha than ham sau. Truong hop (1) la loi o PHA KHAI BAO, nen khi no con
            // do thi compiler chua bind than ham va bao CHI MOT loi CS8345.
            // Muon xem 6 loi con lai thi comment tam class BadHolder o duoi di.
            // ==========================================================================

            // (1) ref struct lam field cua class -> CS8345
            //     "Field or auto-implemented property cannot be of type 'Span<int>'
            //      unless it is an instance member of a ref struct."
            _ = new BadHolder();

            // (2) box mot ref struct -> CS0029
            Span<int> span = stackalloc int[4];
            object boxed = span;

            // (3) lam tham so kieu cua generic -> CS9244
            //     "The type 'Span<int>' may not be a ref struct or a type parameter allowing
            //      ref structs in order to use it as parameter 'T' ..."
            var list = new List<Span<int>>();

            // (4) lam phan tu mang -> CS0611
            Span<int>[] arrayOfSpans = new Span<int>[2];

            // (5) bi lambda bat lai (closure la class tren heap) -> CS8175
            Func<int> f = () => span[0];

            // (6) song qua await trong async method -> CS4007
            _ = BadAsync();

            // (7) song qua yield return trong iterator -> CS4007
            foreach (var _ in BadIterator()) { }
#endif
        }

#if REF_STRUCT_COMPILE_ERRORS
        // (1) CS8345
        private sealed class BadHolder
        {
            private Span<int> _span;                  // <-- LOI
        }

        // (6) async: span con duoc dung SAU await -> phai bi nhac thanh field cua state
        //     machine, ma state machine nam tren heap.
        //     CS4007: "Instance of type 'System.Span<int>' cannot be preserved across
        //              'await' or 'yield' boundary."
        private static async Task<int> BadAsync()
        {
            Span<int> span = stackalloc int[4];
            await Task.Delay(1);
            return span[0];                           // <-- LOI: span phai song qua await
        }

        // (7) iterator: y het - state machine cua yield cung nam tren heap. Cung CS4007.
        private static IEnumerable<int> BadIterator()
        {
            Span<int> span = stackalloc int[4];
            yield return 1;
            yield return span[0];                     // <-- LOI: span phai song qua yield
        }
#endif

        // Ban DUNG cua (6), va no bien dich duoc that - de o ngoai #if de chung minh.
        // Tu C# 13, ref struct local trong async KHONG con bi cam thang tay nua; ranh gioi
        // that su la "co phai song qua await hay khong", tuc "co bi nhac len heap hay khong".
        // Ở đây span chết trước await -> nó chỉ nằm trên stack -> hợp lệ.
        private static async Task<int> GoodAsync()
        {
            Span<int> span = stackalloc int[4];
            span[0] = 42;
            int value = span[0];                      // lay gia tri ra TRUOC await
            await Task.Delay(1);                      // tu day tro di khong ai cham vao span
            return value;
        }

        // =====================================================================================
        // PHAN C: 4 TIEU CHI CUA MICROSOFT - struct hay class?
        // =====================================================================================

        // Ba kieu du lieu de soi. Chi khai bao cho co that de doi chieu, khong dung vao dau.
        private readonly record struct Vector3(float X, float Y, float Z);

        private sealed class Customer
        {
            public int Id { get; init; }
            public string Name { get; set; } = "";
            public string Email { get; set; } = "";
            public DateOnly CreatedAt { get; init; }
            public decimal CreditLimit { get; set; }
        }

        private sealed class HttpRequestContext
        {
            public string Method { get; set; } = "";
            public string Path { get; set; } = "";
            public Dictionary<string, string> Headers { get; } = new();
            public Stream? Body { get; set; }
            public IServiceProvider? Services { get; set; }
        }

        private static void PartC_StructOrClass()
        {
            Section("PHẦN C", "4 tiêu chí của Microsoft áp cho Vector3 / Customer / HttpRequestContext");

            Console.WriteLine($"  Unsafe.SizeOf<Vector3>() = {Unsafe.SizeOf<Vector3>()} byte (3 × float)");

            Console.WriteLine("""

                QUY TẮC GỐC (Framework Design Guidelines - "Choosing Between Class and Struct"):
                  TRÁNH định nghĩa struct trừ khi kiểu đó thoả CẢ 4 điều sau:
                    (1) Nó biểu diễn một GIÁ TRỊ DUY NHẤT, giống các kiểu nguyên thuỷ (int, double).
                    (2) Kích thước một instance DƯỚI 16 BYTE.
                    (3) Nó BẤT BIẾN.
                    (4) Nó KHÔNG BỊ BOX thường xuyên.
                  Thiếu một điều thôi -> dùng class. Mặc định luôn là class.

                +-------------------------+------------------+------------------+---------------------+
                | Tieu chi                | Vector3          | Customer         | HttpRequestContext  |
                +-------------------------+------------------+------------------+---------------------+
                | 1. Mot gia tri duy nhat | DAT              | KHONG            | KHONG               |
                | 2. Duoi 16 byte         | DAT (12 byte)    | KHONG (~48+)     | KHONG (rat lon)     |
                | 3. Bat bien             | DAT              | KHONG            | KHONG               |
                | 4. Hiem khi bi box      | DAT              | KHONG            | KHONG               |
                +-------------------------+------------------+------------------+---------------------+
                | KET LUAN                | struct           | class            | class               |
                +-------------------------+------------------+------------------+---------------------+

                GIẢI THÍCH TỪNG TIÊU CHÍ:

                (1) "Biểu diễn một giá trị duy nhất" - có ĐỊNH DANH hay không?
                    Vector3 (2, 3, 4) không có "cái nào là cái nào": hai vector cùng toạ độ thì
                    LÀ một, hệt như hai số 5. Đó là giá trị.
                    Customer thì ngược lại: hai khách hàng cùng tên vẫn là hai người khác nhau,
                    phân biệt bằng Id. Sửa Name không tạo ra khách hàng mới - vẫn là người đó.
                    Có định danh và có vòng đời => phải là class.
                    HttpRequestContext còn xa hơn: nó là một TÚI TRẠNG THÁI (method, path, header,
                    body stream, DI container), không phải một giá trị nào cả.

                (2) "Dưới 16 byte" - vì gán/truyền struct là COPY TOÀN BỘ.
                    12 byte của Vector3 nằm gọn trong thanh ghi, copy gần như miễn phí, lại còn
                    tránh được một lần dereference con trỏ khi truy cập.
                    Customer nhiều field + vài tham chiếu string đã vượt xa 16 byte; truyền qua
                    lại là copy hàng chục byte mỗi lời gọi, trong khi class chỉ copy 8 byte địa
                    chỉ - đúng bài 1.3 (struct 80 byte truyền by-value vs in/ref).
                    Đây là ngưỡng kinh nghiệm, không phải luật: nếu kiểu lớn nhưng luôn truyền
                    bằng 'in'/'ref' và không bao giờ box thì struct vẫn có thể thắng - đo rồi
                    hãy quyết.

                (3) "Bất biến" - vì struct mutable là bẫy của bài 1.7.
                    Đọc một struct ra từ property / readonly field / biến foreach là ra BẢN SAO;
                    ghi vào bản sao thì mất thay đổi, KHÔNG lỗi, KHÔNG warning.
                    Vector3 bất biến tự nhiên: phép cộng trả về vector mới.
                    Customer thì bản chất là hay đổi (đổi email, đổi hạn mức) và người ta MONG
                    đợi mọi chỗ đang giữ nó đều thấy thay đổi - đúng ngữ nghĩa alias của
                    reference type. Làm struct là dính bẫy ngay ngày đầu.
                    HttpRequestContext bị middleware sửa liên tục suốt pipeline - y hệt.

                (4) "Không bị box thường xuyên" - vì box xoá sạch mọi lợi thế.
                    Nhét struct vào object, vào interface, vào ArrayList, hoặc gọi
                    ValueType.Equals mặc định là cấp phát trên heap + copy - tức là đã trả cái
                    giá của class mà không được lợi gì (bài 1.6).
                    Vector3 chủ yếu sống trong mảng/vòng lặp toán học, hiếm khi bị box.
                    Customer thì suốt ngày đi vào List<T>, LINQ, interface, serializer, DI.
                    HttpRequestContext còn được truyền qua các pipeline BẤT ĐỒNG BỘ - và đây là
                    chỗ nối thẳng với phần B: kể cả có muốn làm nó thành ref struct để khỏi cấp
                    phát, compiler cũng cấm, vì nó phải sống qua await.

                HAI GHI CHÚ THỰC TẾ:
                  - System.Numerics.Vector3 thật của BCL lại là struct MUTABLE với field public,
                    tức cố ý vi phạm tiêu chí (3). Lý do: nó cần khớp layout với thanh ghi SIMD
                    và bị dùng trong vòng lặp cực nóng. Đó là ngoại lệ có đo đạc, không phải
                    tấm giấy phép để bắt chước.
                  - Thứ tự quyết định trong thực tế: mặc định class; chỉ đổi sang struct khi
                    thoả cả 4 tiêu chí VÀ có số đo cho thấy nó đáng; cần "cửa sổ nhìn vào bộ
                    nhớ, sống ngắn, tuyệt đối không cấp phát" thì mới tới ref struct - và chấp
                    nhận toàn bộ giới hạn ở phần B.
                """);
        }

        // ------------------------------- helper hien thi -------------------------------------
        private static (long bytes, double ms, long count) Measure(Func<long> action)
        {
            // Stopwatch la CLASS -> new/StartNew cap phat. Phai tao TRUOC khi doc moc 'before',
            // khong thi chinh dung cu do lam hong ket qua "0 byte" o phan A.
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

        private static void Result(long expected, long actual, string expr)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = actual == expected ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"  => {expr} = {actual}   (kỳ vọng {expected})   {(actual == expected ? "ĐÚNG" : "SAI")}");
            Console.ForegroundColor = old;
        }

        public static void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;

            PartA_ZeroAllocParse();
            PartB_RefStructRestrictions();
            PartC_StructOrClass();

            Console.WriteLine();
            Console.Write("Chạy BenchmarkDotNet cho phần A? (cần Release build, vài phút) [y/N]: ");
            string? answer = Console.ReadLine();
            if (answer is not null && answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
                BenchmarkRunner.Run<DateParseBenchmarks>();
        }
    }

    // Exercise 1.10a: cot can nhin la Allocated - ban Span phai la 0 B.
    [MemoryDiagnoser]
    public class DateParseBenchmarks
    {
        private const string Input = "2026-09-11";

        [Benchmark(Baseline = true, Description = "string.Split('-') - 1 mang + 3 string")]
        public DateOnly Split()
        {
            string[] p = Input.Split('-');
            return new DateOnly(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
        }

        [Benchmark(Description = "Substring - 3 string")]
        public DateOnly Substring()
            => new DateOnly(
                int.Parse(Input.Substring(0, 4)),
                int.Parse(Input.Substring(5, 2)),
                int.Parse(Input.Substring(8, 2)));

        [Benchmark(Description = "ReadOnlySpan.Slice - 0 byte")]
        public DateOnly Span()
        {
            ReadOnlySpan<char> s = Input.AsSpan();
            return new DateOnly(
                int.Parse(s.Slice(0, 4)),
                int.Parse(s.Slice(5, 2)),
                int.Parse(s.Slice(8, 2)));
        }

        // De doi chieu voi ban co san cua BCL.
        [Benchmark(Description = "DateOnly.ParseExact - ban cua BCL")]
        public DateOnly ParseExact()
            => DateOnly.ParseExact(Input, "yyyy-MM-dd", null);
    }

    // Results
    // (dan bang ket qua sau khi chay Release)
}
