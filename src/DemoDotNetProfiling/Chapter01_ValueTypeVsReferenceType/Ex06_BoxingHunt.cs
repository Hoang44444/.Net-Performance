using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DemoDotNetProfiling.Chapter01
{
    // Bài 1.6 - Săn Boxing bằng IL
    //
    // Boxing là gì (nói kiểu nhập môn):
    //   - Value type (int, struct, enum...) bình thường nằm trên STACK: rẻ, tự dọn.
    //   - Khi cần đưa nó vào một chỗ chỉ chứa được "địa chỉ" (object, interface,
    //     ArrayList, object[]...) thì .NET phải xin một ô nhớ trên HEAP, chép giá trị
    //     vào đó, rồi dùng địa chỉ ô nhớ này. Việc đóng gói đó gọi là BOXING.
    //   - Mỗi lần boxing = 1 lần cấp phát Heap = thêm rác cho GC phải dọn.
    //   - Trong IL, boxing hiện ra bằng lệnh "box".
    //
    // Cách đọc output khi chạy:
    //   - Đoạn code MÀU ĐỎ  = code gây boxing.
    //   - Đoạn code MÀU XANH = code đã sửa, không còn boxing.
    //   - Số "bytes/op" trong phần giải thích là số đo thật trên .NET 10 (Release) bằng
    //     GC.GetAllocatedBytesForCurrentThread(). Một vỏ hộp của int/struct nhỏ = 24 bytes.
    public class BoxingHuntDemo
    {
        // Đổi thành true nếu muốn tô NỀN đỏ/xanh thay vì chỉ đổi màu chữ.
        private static readonly bool HighlightBackground = false;

        // ------------------------------------------------------------------
        //  1. Gán value type vào object
        // ------------------------------------------------------------------
        private static object AssignValueTypeToObject()
        {
            int valueType = 42;
            object boxedValue = valueType;
            PrintCase(reason1, boxing1, fix1);
            return boxedValue;
        }

        private const string reason1 = """
            ===== BOXING #1: GÁN THẲNG VALUE TYPE VÀO object (boxing kinh điển) =====
            Nguyên nhân:
              - object là reference type, biến object chỉ giữ được ĐỊA CHỈ, không giữ được giá trị.
              - Khi gán int (đang nằm trên stack) vào object, CLR phải xin 1 ô nhớ trên heap,
                copy giá trị 42 vào ô đó, rồi cho biến object trỏ tới ô đó.
              - IL sinh ra lệnh: box System.Int32 -> 1 lần cấp phát heap + rác cho GC.
              - Đo thật: 24 bytes/op.
            Cách sửa:
              - Dùng Generic <T> để giữ nguyên kiểu, JIT sinh code riêng cho int nên không cần box.
              - Nếu biết trước kiểu cụ thể thì khai báo/trả về đúng kiểu đó, đừng hạ xuống object.
            """;

        private const string boxing1 = """
            int valueType = 42;
            object boxedValue = valueType;      // box System.Int32  -> 24 bytes
            """;

        private const string fix1 = """
            private static T AssignValueTypeGeneric<T>(T valueType)
            {
                T genericsValue = valueType;    // 0 lần box, T được JIT thay bằng int
                return genericsValue;
            }
            """;


        // ------------------------------------------------------------------
        //  2. Gán value type vào biến kiểu interface
        // ------------------------------------------------------------------
        struct MyStruct : IComparable
        {
            public int CompareTo(object? obj)
            {
                return 0;
            }
        }

        private static int CallInterfaceMethodOnStruct()
        {
            IComparable c = 42;

            int result = c.CompareTo(42);

            PrintCase(reason2, boxing2, fix2);
            return result;
        }

        private const string reason2 = """
            ===== BOXING #2: GÁN VALUE TYPE VÀO BIẾN KIỂU INTERFACE (interface dispatch) =====
            Nguyên nhân (ở đây box tới 2 lần):
              - Lần 1: "IComparable c = 42;" - interface cũng chỉ giữ được địa chỉ như object,
                nên 42 phải được box lên heap rồi c mới trỏ vào được.
              - Lần 2: IComparable (bản không generic) khai báo CompareTo(object? obj),
                nên đối số 42 truyền vào cũng bị box thêm một lần nữa.
              - Gọi method qua interface trên value type luôn phải đi qua "vỏ hộp" trên heap.
            Cách sửa:
              - Dùng bản GENERIC của interface: IComparable<int> có sẵn CompareTo(int) -> không box.
              - Hoặc gọi thẳng trên kiểu cụ thể, đừng gán qua biến interface.
              - Trong method generic thì ràng buộc where T : IComparable<T>, JIT sinh code riêng
                cho int và gọi trực tiếp (constrained call), không tạo vỏ hộp nào.
            """;

        private const string boxing2 = """
            IComparable c = 42;                 // box lần 1 (biến interface)
            int result = c.CompareTo(42);       // box lần 2 (tham số là object)
            """;

        private const string fix2 = """
            int a = 42;
            int r1 = a.CompareTo(42);           // int.CompareTo(int) - 0 lần box

            private static int CompareGeneric<T>(T a, T b) where T : IComparable<T>
            {
                return a.CompareTo(b);          // constrained call - 0 lần box
            }
            """;


        // ------------------------------------------------------------------
        //  3. Collection không generic (ArrayList)
        // ------------------------------------------------------------------
        private static ArrayList NonGenericCollection()
        {
            ArrayList list = new ArrayList();
            list.Add(42);
            PrintCase(reason3, boxing3, fix3);
            return list;
        }

        private const string reason3 = """
            ===== BOXING #3: COLLECTION KHÔNG GENERIC (ArrayList, Hashtable, Queue đời cũ) =====
            Nguyên nhân:
              - ArrayList sinh ra từ thời chưa có generic, bên trong nó chỉ là một object[].
              - Chữ ký là Add(object value), nên mọi value type bỏ vào đều bị box.
              - Thêm 1000 số int = 1000 lần cấp phát heap; lúc lấy ra còn phải unbox + ép kiểu,
                sai kiểu là ném InvalidCastException lúc chạy chứ compiler không bắt giúp được.
            Cách sửa:
              - Dùng collection generic: List<int>, Dictionary<K,V>, Queue<int>, HashSet<int>...
                List<int> lưu thẳng int[] bên trong -> 0 lần box và an toàn kiểu ngay lúc biên dịch.
            """;

        private const string boxing3 = """
            ArrayList list = new ArrayList();
            list.Add(42);                       // Add(object) -> box mỗi phần tử
            int x = (int)list[0]!;              // còn phải unbox + ép kiểu khi đọc ra
            """;

        private const string fix3 = """
            List<int> list = new List<int>();
            list.Add(42);                       // int[] bên trong - 0 lần box
            int x = list[0];                    // đọc thẳng, không ép kiểu
            """;


        // ------------------------------------------------------------------
        //  4. Nối chuỗi / chuỗi nội suy / string.Format
        // ------------------------------------------------------------------
        private static string StringConcatenation()
        {
            string str = "The answer is: " + 42;
            PrintCase(reason4, boxing4, fix4);
            return str;
        }

        private const string reason4 = """
            ===== BOXING #4: NỐI CHUỖI - Ở ĐÂY KHÔNG BOX, NHƯNG string.Format THÌ CÓ =====
            Kết luận thực tế: dòng "The answer is: " + 42 KHÔNG box.
              - Compiler tự đổi thành 42.ToString() rồi String.Concat(string, string).
              - int đã override ToString() nên gọi thẳng trên giá trị, không cần vỏ hộp.
            Nhưng vẫn có bẫy box thật sự khi làm việc với chuỗi:
              - string.Format nhận tham số object -> value type truyền vào bị box.
              - Đo thật cùng một kết quả "txt 42": nối chuỗi 40 bytes/op, nội suy 40 bytes/op,
                string.Format 64 bytes/op. Chênh đúng 24 bytes = 1 vỏ hộp.
              - Nội suy $"..." từ .NET 6 dùng DefaultInterpolatedStringHandler với
                AppendFormatted<T> (generic) nên KHÔNG box - đây là cách nên dùng.
            Cách sửa:
              - Ưu tiên nội suy $"..." hoặc + với ToString(), tránh string.Format cho value type.
              - Chỗ cực nóng thì format thẳng vào buffer: int.TryFormat(Span<char>, out int written).
            """;

        private const string boxing4 = """
            string s = string.Format("txt {0}", 42);   // box 42  -> 64 bytes/op
            string t = String.Concat("txt ", (object)42);
            """;

        private const string fix4 = """
            string s = $"txt {42}";             // 40 bytes/op - chỉ tốn chuỗi kết quả
            string t = "txt " + 42;             // 40 bytes/op - compiler gọi 42.ToString()
            """;


        // ------------------------------------------------------------------
        //  5. params object[]
        // ------------------------------------------------------------------
        private static void Log(params object[] args)
        {

        }

        private static void ParamsObjectArray()
        {
            Log("a", 1, 2);
            PrintCase(reason5, boxing5, fix5);
        }

        private const string reason5 = """
            ===== BOXING #5: params object[] (bẫy phổ biến nhất ở các hàm log) =====
            Nguyên nhân:
              - params object[] bắt compiler tạo ngầm new object[3] trên heap ở mỗi lần gọi.
              - Mỗi phần tử là value type phải box để nhét vừa ô object: số 1 box, số 2 box.
              - "a" đã là string (reference type) nên không bị box.
              - Tổng chi phí 1 lần gọi Log("a", 1, 2) = 1 mảng + 2 vỏ hộp = 3 lần cấp phát heap.
              - Nguy hiểm ở chỗ hàm log thường bị gọi rất nhiều lần trong vòng lặp.
            Cách sửa:
              - Viết overload generic cho các số lượng tham số hay dùng -> 0 box, 0 mảng.
              - Hoặc dùng interpolated string handler ([InterpolatedStringHandlerArgument]) /
                LoggerMessage.Define của Microsoft.Extensions.Logging: chúng chỉ dựng chuỗi khi
                log level thật sự được bật.
            """;

        private const string boxing5 = """
            private static void Log(params object[] args) { }

            Log("a", 1, 2);                     // new object[3] + box 1 + box 2
            """;

        private const string fix5 = """
            private static void Log<T1, T2>(string msg, T1 a, T2 b) { }

            Log("a", 1, 2);                     // 0 mảng, 0 lần box
            """;


        // ------------------------------------------------------------------
        //  6. Gọi method của object mà struct chưa override
        // ------------------------------------------------------------------
        private struct Point
        {
            public int x, y;
        }

        private static string UnoverriddenObjectMethod()
        {
            Point p = new Point { x = 1, y = 2 };
            string? str = p.ToString();
            if (str == null)
            {
                str = "String is null";
            }
            PrintCase(reason6, boxing6, fix6);
            return str;
        }

        private const string reason6 = """
            ===== BOXING #6: BOX "TÀNG HÌNH" - GỌI METHOD CỦA object MÀ STRUCT CHƯA OVERRIDE =====
            Nguyên nhân:
              - ToString/Equals/GetHashCode là method ảo thừa kế từ object, muốn gọi phải có
                bảng method của một đối tượng trên heap.
              - Point không override ToString() nên phải chạy bản gốc ValueType.ToString(),
                JIT buộc phải box p rồi mới gọi được. Bản gốc này còn dùng reflection -> rất chậm.
              - Vì sao gọi là tàng hình: TÌM CHỮ "box" TRONG IL SẼ KHÔNG THẤY GÌ, IL chỉ hiện
                "constrained. Point" + "callvirt object::ToString()", box do JIT sinh lúc chạy.
              - Đo thật: 24 bytes/op (chuỗi tên kiểu trả về là chuỗi có sẵn, nên 24 bytes đó
                đúng bằng 1 vỏ hộp).
            Cách sửa:
              - Override ToString() (và Equals/GetHashCode) ngay trong struct. Khi struct đã có
                bản của riêng nó, lệnh constrained. gọi thẳng trên địa chỉ struct -> 0 lần box.
              - Hoặc khai báo bằng record struct, compiler tự sinh sẵn ToString/Equals/GetHashCode.
            """;

        private const string boxing6 = """
            private struct Point { public int x, y; }       // chưa override ToString

            Point p = new Point { x = 1, y = 2 };
            string s = p.ToString();            // JIT box p  -> 24 bytes/op
            """;

        private const string fix6 = """
            private struct Point
            {
                public int x, y;
                public override string ToString() => $"({x}, {y})";
            }

            string s = p.ToString();            // constrained. gọi thẳng - 0 lần box
            """;


        // ------------------------------------------------------------------
        //  7. Enum làm key Dictionary (.NET cũ)
        // ------------------------------------------------------------------
        private enum Color
        {
            Red, Green, Blue
        }

        private static void EnumAsDictionaryKey()
        {
            Dictionary<Color, int> dict = new Dictionary<Color, int>();
            dict[Color.Red] = 1;
            PrintCase(reason7, boxing7, fix7);
        }

        private const string reason7 = """
            ===== BOXING #7: ENUM LÀM KEY DICTIONARY (giờ chỉ còn là vấn đề của .NET Framework cũ) =====
            Nguyên nhân (trên .NET Framework cũ):
              - Dictionary tra key bằng EqualityComparer<TKey>.Default.
              - Với enum, runtime cũ không có comparer chuyên dụng nên rơi về ObjectEqualityComparer<T>,
                tức là gọi Equals(object) -> box key MỖI LẦN thêm/tra/xoá phần tử.
              - Box này nằm trong code của BCL nên soi IL method của mình sẽ không thấy chữ "box".
            Trên .NET Core / .NET 5+ / .NET 10 (project này):
              - EqualityComparer<TEnum>.Default đã được tối ưu, so sánh thẳng theo underlying type.
              - Đo thật: 0 bytes/op -> dòng code ở trên hiện tại là an toàn.
            Cách sửa (nếu buộc phải chạy trên Framework cũ):
              - Tự cài IEqualityComparer<Color> rồi truyền vào constructor của Dictionary.
              - Hoặc đổi key sang kiểu số: Dictionary<int, int> với key là (int)Color.Red.
            """;

        private const string boxing7 = """
            // Trên .NET Framework cũ: box key ở MỖI lần thêm / tra / xoá
            Dictionary<Color, int> dict = new Dictionary<Color, int>();
            dict[Color.Red] = 1;
            """;

        private const string fix7 = """
            sealed class ColorComparer : IEqualityComparer<Color>
            {
                public bool Equals(Color a, Color b) => a == b;     // 0 lần box
                public int GetHashCode(Color c) => (int)c;
            }

            var dict = new Dictionary<Color, int>(new ColorComparer());
            """;


        // ------------------------------------------------------------------
        //  8. LINQ / foreach trên struct qua interface
        // ------------------------------------------------------------------
        private static void LinqOnStructViaInterface()
        {
            List<int> list = new List<int> { 1, 2, 3 };
            IEnumerable<int> enumerable = list;
            foreach (var item in enumerable)
            {

            }
            PrintCase(reason8, boxing8, fix8);
        }

        private const string reason8 = """
            ===== BOXING #8: BOX ENUMERATOR KHI foreach/LINQ ĐI QUA INTERFACE =====
            Nguyên nhân:
              - Dòng "IEnumerable<int> enumerable = list;" KHÔNG box: List<int> vốn đã là class.
              - Box xảy ra ở dòng foreach: gọi GetEnumerator() qua interface IEnumerable<int>,
                giá trị trả về là List<int>.Enumerator - vốn là một STRUCT - phải box lên heap
                để nhét vừa biến kiểu IEnumerator<int>.
              - Box này cũng nằm trong BCL nên IL của method mình không hiện chữ "box".
              - LINQ (Where/Select...) cũng luôn làm việc qua IEnumerable<T> nên dính y hệt.
              - Đo thật: qua interface 40 bytes/vòng lặp, duyệt trực tiếp 0 bytes.
            Cách sửa:
              - foreach thẳng trên kiểu cụ thể List<int>. Compiler dùng cơ chế pattern-based:
                lấy struct enumerator và gọi trực tiếp -> 0 box, 0 cấp phát.
              - Vòng lặp cực nóng thì duyệt qua Span để bỏ luôn enumerator.
              - Đừng hạ kiểu xuống IEnumerable<T> nếu không thật sự cần trừu tượng hoá.
            """;

        private const string boxing8 = """
            List<int> list = new List<int> { 1, 2, 3 };
            IEnumerable<int> e = list;          // dòng này KHÔNG box
            foreach (int x in e) { }            // box struct enumerator -> 40 bytes
            """;

        private const string fix8 = """
            List<int> list = new List<int> { 1, 2, 3 };
            foreach (int x in list) { }         // dùng struct enumerator - 0 bytes

            foreach (int x in CollectionsMarshal.AsSpan(list)) { }   // bỏ luôn enumerator
            """;


        // ------------------------------------------------------------------
        //  9. Nullable<T> gán vào object
        // ------------------------------------------------------------------
        private static void NullableValueTypeToObject()
        {
            int? nullableValue = 42;
            object? boxedNullable = nullableValue;
            PrintCase(reason9, boxing9, fix9);
        }

        private const string reason9 = """
            ===== BOXING #9: BOX Nullable<T> (int?) VÀO object =====
            Nguyên nhân:
              - int? thực chất là struct Nullable<int> gồm 2 field: bool hasValue + int value.
              - Nó vẫn là value type nên gán vào object phải box, IL sinh: box Nullable<int>.
              - Runtime xử lý đặc biệt cho Nullable: nếu HasValue = false thì kết quả là null,
                KHÔNG cấp phát gì; nếu có giá trị thì chỉ box phần int bên trong.
                Vì vậy boxedNullable.GetType() trả về System.Int32 chứ không phải Nullable<int>.
              - Đo thật: int? có giá trị -> 24 bytes/op; int? = null -> 0 bytes/op.
            Cách sửa:
              - Đừng nhét int? vào object; giữ nguyên int? hoặc dùng generic <T>.
              - Nếu bắt buộc phải đưa đi chỗ khác thì lấy giá trị ra trước bằng
                HasValue / GetValueOrDefault() để không kéo theo vỏ hộp thừa.
            """;

        private const string boxing9 = """
            int? nullableValue = 42;
            object boxed = nullableValue;       // box Nullable<int> -> 24 bytes
            """;

        private const string fix9 = """
            int? nullableValue = 42;
            int plain = nullableValue.GetValueOrDefault();   // 0 lần box
            """;


        // ------------------------------------------------------------------
        //  10. Ném (throw) một số nguyên - chỉ minh hoạ khái niệm
        // ------------------------------------------------------------------
        private static void Source10_ThrowBoxedPrimitive()
        {
            // C# bắt buộc throw phải là Exception, đây là ví dụ minh hoạ, không compile được thật:
            // throw 42; // (chỉ hợp lệ ở vài ngôn ngữ .NET khác như C++/CLI) -> nếu được thì 42 sẽ bị box
            PrintCase(reason10, boxing10, fix10);
        }

        private const string reason10 = """
            ===== BOXING #10: THROW MỘT SỐ NGUYÊN (chỉ minh hoạ khái niệm, C# không cho phép) =====
            Nguyên nhân (về mặt lý thuyết):
              - Cơ chế exception của CLR chỉ làm việc với reference: chỗ ném và chỗ bắt trao nhau
                một địa chỉ đối tượng trên heap.
              - Nên nếu ném một value type (như "throw 42;" ở C++/CLI) thì 42 bắt buộc phải box
                lên heap mới ném đi được.
              - C# chặn ngay từ lúc biên dịch: chỉ được throw thứ kế thừa từ Exception.
            Cách sửa:
              - Luôn ném một Exception cụ thể và nhét dữ liệu vào property/message của nó.
            """;

        private const string boxing10 = """
            throw 42;                           // C# không cho phép; nếu được thì 42 sẽ bị box
            """;

        private const string fix10 = """
            throw new ArgumentOutOfRangeException(nameof(value), 42, "Giá trị không hợp lệ");
            """;


        // ------------------------------------------------------------------
        //  11. So sánh struct qua ValueType.Equals
        // ------------------------------------------------------------------
        private static void CompareStructViaValueTypeEquals()
        {
            Point p1 = new Point { x = 1, y = 2 };
            Point p2 = new Point { x = 1, y = 2 };
            bool areEqual = p1.Equals(p2);
            PrintCase(reason11, boxing11, fix11);
        }

        private const string reason11 = """
            ===== BOXING #11: SO SÁNH STRUCT BẰNG ValueType.Equals (box 2 lần + reflection) =====
            Nguyên nhân:
              - Point không override Equals nên p1.Equals(p2) rơi về bản gốc ValueType.Equals(object).
              - Lần 1: p2 bị box vì tham số của method đó là object.
              - Lần 2: p1 bị box để làm "this" cho lời gọi ảo (constrained. + callvirt).
              - Tệ hơn nữa: ValueType.Equals có thể phải so sánh từng field bằng reflection,
                chậm hơn code so sánh viết tay hàng chục lần.
              - Đo thật: chưa override 48 bytes/op (đúng 2 vỏ hộp); cài IEquatable<T> 0 bytes/op.
            Cách sửa:
              - Cài IEquatable<Point> (có Equals(Point)) + override Equals(object) + GetHashCode
                + toán tử == / !=. Lúc đó compiler chọn overload Equals(Point) -> 0 lần box.
              - Hoặc khai báo record struct, compiler tự sinh toàn bộ những thứ trên.
            """;

        private const string boxing11 = """
            private struct Point { public int x, y; }       // chưa override Equals

            bool eq = p1.Equals(p2);            // box p1 + box p2 -> 48 bytes/op
            """;

        private const string fix11 = """
            private struct Point : IEquatable<Point>
            {
                public int x, y;
                public bool Equals(Point other) => x == other.x && y == other.y;
                public override bool Equals(object? obj) => obj is Point p && Equals(p);
                public override int GetHashCode() => HashCode.Combine(x, y);
            }

            bool eq = p1.Equals(p2);            // gọi Equals(Point) - 0 bytes/op
            """;


        // ------------------------------------------------------------------
        //  12. Lambda bắt (capture) biến value type
        // ------------------------------------------------------------------
        private static void LambdaCapturingValueType()
        {
            int valueType = 42;
            Func<int> func = () => valueType;

            PrintCase(reason12, boxing12, fix12);
        }

        private const string reason12 = """
            ===== BOXING #12: LAMBDA BẮT BIẾN LOCAL - KHÔNG BOX NHƯNG VẪN TỐN HEAP =====
            Nguyên nhân (phân biệt cho rõ, đây KHÔNG phải boxing):
              - IL không có lệnh box nào ở đây.
              - Nhưng biến local valueType phải sống lâu hơn method, nên compiler sinh ra một
                class ẩn (display class / closure) để chứa nó -> 1 lần cấp phát heap.
              - Cộng thêm 1 object delegate cho Func<int> -> tổng 2 lần cấp phát heap mỗi lần gọi.
              - Bài học: cấp phát heap không đồng nghĩa với boxing, nhưng với GC thì đều là rác.
            Cách sửa:
              - Lambda KHÔNG bắt biến nào thì compiler cache sẵn delegate vào field static -> 0 alloc.
              - Viết "static" trước lambda để compiler báo lỗi nếu mình lỡ tay capture, rồi truyền
                dữ liệu qua tham số (nhiều API có overload nhận TState đúng để tránh closure).
            """;

        private const string boxing12 = """
            int valueType = 42;
            Func<int> func = () => valueType;   // 1 closure + 1 delegate = 2 lần cấp phát heap
            """;

        private const string fix12 = """
            Func<int> func = static () => 42;               // delegate được cache - 0 alloc
            dict.GetOrAdd(key, static k => k.Length);       // truyền state qua tham số
            """;


        // ------------------------------------------------------------------
        //  13. Struct làm key Dictionary/HashSet mà không cài IEquatable<T>
        // ------------------------------------------------------------------
        private static void StructKeyWithoutIEquatable()
        {
            Dictionary<Point, int> dict = new Dictionary<Point, int>();
            Point key = new Point { x = 1, y = 2 };
            dict[key] = 1;
            int value = dict[key];
            PrintCase(reason13, boxing13, fix13);
        }

        private const string reason13 = """
            ===== BOXING #13: STRUCT LÀM KEY Dictionary/HashSet MÀ KHÔNG CÀI IEquatable<T> =====
            Nguyên nhân (đây là trường hợp tốn kém nhất trong cả bài):
              - Dictionary dùng EqualityComparer<TKey>.Default. Nếu struct KHÔNG cài IEquatable<T>
                thì rơi về ObjectEqualityComparer<T>, chỉ biết gọi GetHashCode() và Equals(object).
              - Mỗi lần tra 1 key mất tới 3 vỏ hộp: 1 cho GetHashCode(), 1 cho "this" và 1 cho
                đối số của Equals(object).
              - Đo thật: KHÔNG cài IEquatable 72 bytes/op; CÓ cài IEquatable 0 bytes/op.
              - Đây là boxing nằm trong BCL: IL method của mình sạch trơn, nhưng vòng lặp tra
                Dictionary 1 triệu lần là 72 MB rác.
            Cách sửa:
              - Bất kỳ struct nào định làm key của Dictionary/HashSet, hoặc bỏ vào List rồi
                Contains/IndexOf, đều PHẢI cài IEquatable<T> và override GetHashCode().
              - Hoặc dùng record struct (compiler tự sinh IEquatable<T> + GetHashCode).
              - Lưu ý: EqualityComparer<T>.Default.Equals(a, b) gọi thẳng cũng box 48 bytes/op
                nếu struct chưa cài IEquatable<T>, nên đó KHÔNG phải cách né.
            """;

        private const string boxing13 = """
            private struct Point { public int x, y; }       // không cài IEquatable<Point>

            var dict = new Dictionary<Point, int>();
            dict[key] = 1;
            int v = dict[key];                  // 3 lần box mỗi lượt tra -> 72 bytes/op
            """;

        private const string fix13 = """
            private readonly record struct Point(int x, int y);     // tự có IEquatable + GetHashCode

            var dict = new Dictionary<Point, int>();
            int v = dict[key];                  // 0 bytes/op
            """;


        // ------------------------------------------------------------------
        //  14. enum.ToString() và nội suy chuỗi với enum
        // ------------------------------------------------------------------
        private static void EnumToStringBoxing()
        {
            Color color = Color.Red;
            string name = color.ToString();
            string interpolated = $"{color}";
            PrintCase(reason14, boxing14, fix14);
        }

        private const string reason14 = """
            ===== BOXING #14: enum.ToString() VÀ NỘI SUY $"{enumValue}" =====
            Nguyên nhân:
              - Kiểu enum của mình (Color) KHÔNG tự override ToString(); bản override nằm ở
                lớp cha System.Enum (là một class).
              - Vì method thật nằm ở lớp cha nên lệnh "constrained." không giải quyết được
                tại chỗ, JIT buộc phải box giá trị enum rồi mới gọi Enum.ToString().
              - Đo thật: enum.ToString() 24 bytes/op, $"{enumValue}" 32 bytes/op.
                Để đối chứng: 42.ToString() = 0 bytes/op vì int có override ToString() thật sự.
              - Rất dễ dính vì enum hay được ghi ra log, ra response, ra tên file.
            Cách sửa:
              - Enum.GetName<TEnum>(value) - bản generic từ .NET 5. Đo thật: 0 bytes/op.
              - Hoặc tự viết switch expression trả về chuỗi hằng. Đo thật: 0 bytes/op.
              - Cách này còn nhanh hơn nhiều vì không phải tra bảng tên của Enum.
            """;

        private const string boxing14 = """
            Color color = Color.Red;
            string a = color.ToString();        // box enum -> 24 bytes/op
            string b = $"{color}";              //           -> 32 bytes/op
            """;

        private const string fix14 = """
            string a = Enum.GetName<Color>(color);          // 0 bytes/op (.NET 5+)

            string b = color switch                         // 0 bytes/op, nhanh nhất
            {
                Color.Red   => "Red",
                Color.Green => "Green",
                _           => "Blue",
            };
            """;


        // ------------------------------------------------------------------
        //  15. Enum.HasFlag
        // ------------------------------------------------------------------
        [Flags]
        private enum Permission
        {
            None = 0, Read = 1, Write = 2, Execute = 4
        }

        private static void EnumHasFlagBoxing()
        {
            Permission permission = Permission.Read | Permission.Write;
            bool canRead = permission.HasFlag(Permission.Read);
            PrintCase(reason15, boxing15, fix15);
        }

        private const string reason15 = """
            ===== BOXING #15: Enum.HasFlag (bẫy kinh điển của .NET Framework) =====
            Nguyên nhân:
              - Chữ ký là bool HasFlag(Enum flag) - tham số kiểu Enum, mà Enum là CLASS.
              - Nên cả giá trị đang xét lẫn cờ truyền vào đều bị box: 2 vỏ hộp mỗi lần gọi,
                cộng thêm việc kiểm tra kiểu bên trong -> chậm hơn phép AND cả chục lần.
            Trên .NET Core 2.1+ / .NET 10 (project này):
              - JIT đã có tối ưu riêng cho HasFlag, xoá luôn 2 lần box khi code được tối ưu.
              - Đo thật: 0,24 bytes/op - phần dư siêu nhỏ đó là do những vòng chạy đầu tiên
                còn ở code tier-0 (chưa tối ưu) vẫn box thật; sau khi lên tier-1 thì về 0.
              - Nghĩa là: build Debug hoặc code chạy vài lần rồi bỏ vẫn tốn box.
            Cách sửa:
              - Dùng phép AND bit trực tiếp, luôn đúng trên mọi phiên bản .NET và nhanh nhất.
              - Đo thật: 0 bytes/op ngay cả ở tier-0.
            """;

        private const string boxing15 = """
            Permission p = Permission.Read | Permission.Write;
            bool canRead = p.HasFlag(Permission.Read);      // box 2 lần (Framework cũ / tier-0)
            """;

        private const string fix15 = """
            Permission p = Permission.Read | Permission.Write;
            bool canRead = (p & Permission.Read) != 0;      // 0 bytes/op ở mọi phiên bản
            """;


        // ------------------------------------------------------------------
        //  16. Túi dữ liệu kiểu object và các API BCL nhận object
        // ------------------------------------------------------------------
        private static void ObjectBagAndLegacyApis()
        {
            Dictionary<string, object> bag = new Dictionary<string, object>();
            bag["id"] = 42;
            bag["price"] = 19.99m;
            PrintCase(reason16, boxing16, fix16);
        }

        private const string reason16 = """
            ===== BOXING #16: TÚI DỮ LIỆU object (Dictionary<string,object>) VÀ API NHẬN object =====
            Nguyên nhân:
              - Rất nhiều chỗ trong code thật dùng object làm "chỗ chứa gì cũng được":
                Dictionary<string, object> cho config / metadata / JSON tạm,
                property kiểu object trong DTO, cache Dictionary<string, object>.
              - Mọi value type bỏ vào đều box. Đo thật: bag["k"] = 42 -> 24 bytes/op.
              - Các API đời cũ trong BCL và thư viện cũng nhận object nên dính y hệt:
                cmd.Parameters.AddWithValue("@id", 42), DataRow["col"] = 42,
                Convert.ToInt32(object), HttpContext.Items["key"] = 42.
              - Trong web API bị gọi vài nghìn lần/giây thì đây là nguồn rác Gen0 lớn nhất.
            Cách sửa:
              - Định nghĩa class/record có property đúng kiểu thay vì túi object.
              - Nếu buộc phải có túi động thì gom giá trị vào 1 object duy nhất (1 lần box cho
                cả cụm) thay vì box từng field lẻ.
              - Với ADO.NET thì tạo DbParameter và gán DbType + Value đúng kiểu.
            """;

        private const string boxing16 = """
            var bag = new Dictionary<string, object>();
            bag["id"] = 42;                     // box int     -> 24 bytes/op
            bag["price"] = 19.99m;              // box decimal -> mỗi lần gán là 1 lần box

            cmd.Parameters.AddWithValue("@id", 42);         // tham số object -> box
            """;

        private const string fix16 = """
            private sealed record Order(int Id, decimal Price);     // đúng kiểu, 0 lần box

            var order = new Order(42, 19.99m);  // 1 lần cấp phát cho cả cụm, không box từng field
            """;


        // ------------------------------------------------------------------
        //  17. Sắp xếp struct chỉ cài IComparable (bản không generic)
        // ------------------------------------------------------------------
        private struct LegacyItem : IComparable
        {
            public int value;
            public int CompareTo(object? obj) => value.CompareTo(((LegacyItem)obj!).value);
        }

        private static void SortWithNonGenericIComparable()
        {
            LegacyItem[] items = new LegacyItem[8];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = new LegacyItem { value = items.Length - i };
            }
            Array.Sort(items);
            PrintCase(reason17, boxing17, fix17);
        }

        private const string reason17 = """
            ===== BOXING #17: SẮP XẾP STRUCT CHỈ CÀI IComparable (BẢN KHÔNG GENERIC) =====
            Nguyên nhân:
              - Array.Sort / List<T>.Sort dùng Comparer<T>.Default.
              - Nếu struct chỉ cài IComparable (nhận object) chứ không cài IComparable<T>,
                Comparer<T>.Default phải box CẢ HAI vế ở MỖI phép so sánh.
              - Sort n phần tử là khoảng n*log(n) phép so sánh -> số vỏ hộp tăng rất nhanh.
              - Đo thật với mảng chỉ 8 phần tử: chỉ có IComparable 1408 bytes/lần sort;
                có IComparable<T> 0 bytes/lần sort. Mảng 1000 phần tử thì chênh lệch khủng khiếp.
            Cách sửa:
              - Cài IComparable<T> (bản generic). Comparer<T>.Default nhận ra ngay và gọi
                trực tiếp CompareTo(T) -> 0 lần box.
              - Giữ luôn cả bản IComparable cũ nếu cần tương thích code cũ, nhưng bản generic
                mới là bản được dùng.
            """;

        private const string boxing17 = """
            private struct LegacyItem : IComparable
            {
                public int value;
                public int CompareTo(object? obj) => value.CompareTo(((LegacyItem)obj!).value);
            }

            Array.Sort(items);                  // box 2 vế mỗi phép so sánh -> 1408 bytes/sort
            """;

        private const string fix17 = """
            private struct FastItem : IComparable<FastItem>
            {
                public int value;
                public int CompareTo(FastItem other) => value.CompareTo(other.value);
            }

            Array.Sort(items);                  // gọi thẳng CompareTo(FastItem) -> 0 bytes/sort
            """;


        // ------------------------------------------------------------------
        //  18. object.Equals(a, b) tĩnh và các API so sánh nhận object
        // ------------------------------------------------------------------
        private static void StaticObjectEquals()
        {
            int a = 42;
            int b = 42;
            bool areEqual = object.Equals(a, b);
            PrintCase(reason18, boxing18, fix18);
        }

        private const string reason18 = """
            ===== BOXING #18: object.Equals(a, b) TĨNH VÀ CÁC API SO SÁNH NHẬN object =====
            Nguyên nhân:
              - Bản tĩnh có chữ ký object.Equals(object objA, object objB), hai tham số đều là
                object nên truyền int/struct vào là box cả hai.
              - Đo thật: object.Equals(42, 42) -> 48 bytes/op, đúng 2 vỏ hộp.
              - Cùng họ này còn có: object.ReferenceEquals(a, b) - vừa box vừa luôn trả về false
                với value type (vì hai vỏ hộp là hai đối tượng khác nhau!), Comparer.Default
                (bản không generic), ArrayList.Contains, Hashtable.ContainsKey.
              - Dễ lọt lưới vì nhiều người quen dùng object.Equals cho "an toàn với null",
                nhưng value type thì không bao giờ null nên hoàn toàn không cần.
            Cách sửa:
              - Với value type thì so sánh trực tiếp bằng == hoặc gọi Equals(T) đúng kiểu.
              - Trong code generic thì ràng buộc where T : IEquatable<T> rồi gọi a.Equals(b).
              - TUYỆT ĐỐI không dùng ReferenceEquals cho value type.
            """;

        private const string boxing18 = """
            int a = 42, b = 42;
            bool eq = object.Equals(a, b);      // box cả a và b -> 48 bytes/op
            bool r  = object.ReferenceEquals(a, b);  // box 2 lần VÀ luôn trả về false
            """;

        private const string fix18 = """
            int a = 42, b = 42;
            bool eq = a == b;                   // 0 bytes/op

            private static bool AreEqual<T>(T a, T b) where T : IEquatable<T>
                => a.Equals(b);                 // constrained call - 0 bytes/op
            """;


        // ==================================================================
        //  Phần in ra màn hình: code gây boxing tô ĐỎ, code đã sửa tô XANH
        // ==================================================================
        private static void PrintCase(string reason, string boxingCode, string fixedCode)
        {
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(reason);
            Console.WriteLine();
            Console.WriteLine("  >> CODE GÂY BOXING:");
            WriteCodeBlock(boxingCode, ConsoleColor.Red);
            Console.WriteLine("  >> CODE ĐÃ SỬA:");
            WriteCodeBlock(fixedCode, ConsoleColor.Green);
        }

        private static void WriteCodeBlock(string code, ConsoleColor color)
        {
            string[] lines = code.Replace("\r\n", "\n").Split('\n');

            int width = 0;
            foreach (string line in lines)
            {
                if (line.Length > width)
                {
                    width = line.Length;
                }
            }

            foreach (string line in lines)
            {
                if (HighlightBackground)
                {
                    Console.BackgroundColor = color;
                    Console.ForegroundColor = ConsoleColor.Black;
                }
                else
                {
                    Console.ForegroundColor = color;
                }

                Console.Write("      " + line.PadRight(width + 2));
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        public static void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("18 trường hợp boxing trong C# (đỏ = gây boxing, xanh = đã sửa)");

            AssignValueTypeToObject();
            CallInterfaceMethodOnStruct();
            NonGenericCollection();
            StringConcatenation();
            ParamsObjectArray();
            UnoverriddenObjectMethod();
            EnumAsDictionaryKey();
            LinqOnStructViaInterface();
            NullableValueTypeToObject();
            Source10_ThrowBoxedPrimitive();
            CompareStructViaValueTypeEquals();
            LambdaCapturingValueType();
            StructKeyWithoutIEquatable();
            EnumToStringBoxing();
            EnumHasFlagBoxing();
            ObjectBagAndLegacyApis();
            SortWithNonGenericIComparable();
            StaticObjectEquals();

            Console.ResetColor();
        }

        // ==================================================================================
        //  KẾT QUẢ SĂN BOXING
        //  (đối chiếu IL thật trong bin/Debug/net10.0/DemoDotNetProfiling.dll, và đo allocation
        //   bằng GC.GetAllocatedBytesForCurrentThread() trên .NET 10 build Release)
        // ==================================================================================
        //
        //  Bảng tổng kết 18 tình huống
        //
        //  #  | Tình huống                        | Kết luận                       | Đo được
        //  ---+-----------------------------------+--------------------------------+-----------
        //  1  | Gán int vào object                | CÓ, 1 lần - box System.Int32   | 24 B/op
        //  2  | Gán int vào biến IComparable      | CÓ, 2 lần - box c + box đối số |
        //  3  | ArrayList.Add(42)                 | CÓ, 1 lần - Add nhận object    |
        //  4  | Nối chuỗi "..." + 42              | KHÔNG - compiler gọi ToString  | 40 B/op
        //     | (nhưng string.Format thì CÓ)      | rồi String.Concat(str, str)    | 64 B/op
        //  5  | params object[]                   | CÓ, 2 box + 1 mảng object[]    |
        //  6  | struct.ToString() chưa override   | CÓ - IL chỉ hiện "constrained",| 24 B/op
        //     |                                   | JIT sinh box lúc chạy          |
        //  7  | Enum làm key Dictionary           | KHÔNG trên .NET Core/.NET 10;  | 0 B/op
        //     |                                   | Framework cũ box trong BCL     |
        //  8  | List<int> qua IEnumerable<int>    | Dòng gán KHÔNG box; foreach    | 40 B/op
        //     |                                   | box struct enumerator          | (0 nếu duyệt
        //     |                                   |                                | trực tiếp)
        //  9  | int? gán vào object               | CÓ nếu HasValue; null thì KHÔNG| 24 / 0 B/op
        //  10 | throw số nguyên                   | C# không cho phép, minh hoạ    |
        //  11 | p1.Equals(p2) với struct          | CÓ, 2 lần - box p1 và p2       | 48 B/op
        //  12 | Lambda bắt biến local             | KHÔNG box, nhưng 2 lần cấp phát|
        //     |                                   | heap: closure + delegate       |
        //  13 | Struct key Dictionary không       | CÓ, 3 lần MỖI LƯỢT TRA         | 72 B/op
        //     | IEquatable<T>                     | (GetHashCode + 2 vế Equals)    | (0 nếu có)
        //  14 | enum.ToString() / $"{enumValue}"  | CÓ - override nằm ở System.Enum| 24 / 32 B/op
        //  15 | Enum.HasFlag                      | KHÔNG ở code đã tối ưu (.NET   | ~0 B/op
        //     |                                   | Core 2.1+); tier-0/Framework CÓ|
        //  16 | Dictionary<string,object>,        | CÓ, 1 lần mỗi giá trị value    | 24 B/op
        //     | AddWithValue, DataRow             | type bỏ vào                    |
        //  17 | Array.Sort struct chỉ có          | CÓ, 2 lần MỖI PHÉP SO SÁNH     | 1408 B/sort
        //     | IComparable non-generic           |                                | (mảng 8 ptu)
        //  18 | object.Equals(a, b) tĩnh          | CÓ, 2 lần; ReferenceEquals còn | 48 B/op
        //     |                                   | luôn trả về false với value    |
        //
        //  Bài học rút ra: tìm chữ "box" trong IL là cách săn nhanh nhất, nhưng chưa đủ.
        //  Ba kiểu box "tàng hình" cần nhớ thêm:
        //    - "constrained." + callvirt trên struct chưa override (số 6, 11, 14)
        //    - box xảy ra bên trong thư viện BCL chứ không phải trong code của mình (số 8, 13, 17)
        //    - box chỉ còn ở code chưa tối ưu tier-0 / .NET Framework cũ (số 7, 15)
        //  Và ngược lại: không phải cứ cấp phát Heap là boxing (số 12 là closure).
        //
        //  Ba trường hợp đắt nhất trong code thật, đáng sửa trước:
        //    #13 struct làm key Dictionary mà thiếu IEquatable<T>  -> 72 bytes MỖI lượt tra
        //    #17 sort struct thiếu IComparable<T>                  -> box theo n*log(n)
        //    #16 túi Dictionary<string, object> trong web API      -> box theo số request
    }
}
