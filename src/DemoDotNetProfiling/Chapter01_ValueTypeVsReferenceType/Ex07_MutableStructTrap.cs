using System;
using System.Text;

namespace DemoDotNetProfiling.Chapter01
{
    // =========================================================================================
    // Bai 1.7 - MUTABLE STRUCT TRAP
    //
    //   1) Viet struct Counter { public int Value; public void Inc() => Value++; }
    //   2) Tai hien dung 3 bay:
    //        - field readonly trong class
    //        - property tra ve struct
    //        - foreach tren mang struct
    //   3) Sua lai toan bo bang: readonly record struct Counter(int Value)
    //      voi Inc() tra ve struct MOI.
    //
    // Y chinh: struct la VALUE TYPE. Moi lan doc no ra tu mot cho khong-phai-bien
    // (readonly field, ket qua property, bien foreach) deu sinh mot BAN SAO.
    // Method mutable ghi vao ban sao do -> ban sao chet -> mat toan bo thay doi,
    // KHONG loi, KHONG warning.
    // =========================================================================================
    public class MutableStructTrapDemo
    {
        // -------------------------------------------------------------------------------------
        // PHAN A: struct MUTABLE - nguon goc cua ca 3 bay
        // -------------------------------------------------------------------------------------
        private struct Counter
        {
            public int Value;
            public void Inc() => Value++;   // ghi truc tiep vao field cua "this"
        }

        // =====================================================================================
        // BAY 1: field readonly kieu struct mutable trong class
        // =====================================================================================
        private sealed class Container
        {
            public readonly Counter Counter;          // <-- readonly field, kieu struct MUTABLE
            public Counter Mutable;                   // <-- y het, chi bo chu readonly (de doi chung)

            public Container(Counter counter)
            {
                Counter = counter;
                Mutable = counter;
            }
        }

        private const string Trap1Code = """
            sealed class Container
            {
                public readonly Counter Counter;          // readonly field kieu struct MUTABLE
                public          Counter Mutable;          // y het, chi bo chu readonly
                ...
            }

            var container = new Container(new Counter());

            container.Counter.Inc();                      // bien dich OK, khong warning
            container.Counter.Inc();
            Console.WriteLine(container.Counter.Value);   // ky vong 2

            container.Mutable.Inc();                      // doi chung: field KHONG readonly
            container.Mutable.Inc();
            Console.WriteLine(container.Mutable.Value);   // ky vong 2
            """;

        private static void Trap1_ReadonlyFieldInClass()
        {
            Section("BẪY 1", "readonly field kiểu struct mutable trong class");
            Console.WriteLine(Trap1Code);

            var container = new Container(new Counter());

            container.Counter.Inc();
            container.Counter.Inc();
            Result(expected: 2, actual: container.Counter.Value, expr: "container.Counter.Value  (readonly)");

            container.Mutable.Inc();
            container.Mutable.Inc();
            Result(expected: 2, actual: container.Mutable.Value, expr: "container.Mutable.Value (thuong)  ");

            Console.WriteLine("""

                LÝ DO: chỉ khác đúng một chữ 'readonly' mà hai dòng ra hai kết quả khác nhau.
                  'Counter' là readonly field. Inc() là instance method KHÔNG readonly, tức là
                  nó có quyền ghi vào 'this'. Nếu compiler cho nó nhận địa chỉ thật của field
                  thì readonly bị phá. Nên compiler chèn một DEFENSIVE COPY (bản sao phòng thủ):

                      ldarg.0
                      ldfld      valuetype Counter Container::Counter   // copy field ra stack
                      stloc.0                                           // cất vào temp local
                      ldloca.s   V_0                                    // lấy địa chỉ của TEMP
                      call       instance void Counter::Inc()           // Inc() tăng TEMP

                  Inc() tăng bản sao tạm, hết câu lệnh bản sao bị vứt, field gốc vẫn 0.
                  Không lỗi biên dịch, không warning -> bug im lặng, lại còn tốn thêm một lần
                  copy struct cho mỗi lần gọi.

                  Với field 'Mutable' (không readonly), compiler được phép lấy địa chỉ thật
                  của field (ldflda) và truyền thẳng cho Inc(), nên nó chạy đúng = 2.
                  Đó chính là bằng chứng: thủ phạm là 'readonly' + struct mutable, không phải
                  bản thân Inc().
                """);
        }

        // =====================================================================================
        // BAY 2: property tra ve struct
        // =====================================================================================
        private sealed class Holder
        {
            public Counter Counter { get; set; } = new Counter();
        }

        private const string Trap2Code = """
            sealed class Holder
            {
                public Counter Counter { get; set; } = new Counter();   // property kieu struct
            }

            var holder = new Holder();

            // holder.Counter.Value = 42;   // (a) CS1612 - compiler CHAN thang
            holder.Counter.Inc();           // (b) bien dich OK... nhung mat tac dung
            holder.Counter.Inc();
            Console.WriteLine(holder.Counter.Value);   // ky vong 2
            """;

        private static void Trap2_PropertyReturningStruct()
        {
            Section("BẪY 2", "property trả về struct");
            Console.WriteLine(Trap2Code);

            var holder = new Holder();
            // holder.Counter.Value = 42;
            // error CS1612: Cannot modify the return value of 'Holder.Counter'
            //               because it is not a variable
            holder.Counter.Inc();
            holder.Counter.Inc();

            Result(expected: 2, actual: holder.Counter.Value, expr: "holder.Counter.Value");

            Console.WriteLine("""

                LÝ DO:
                  'holder.Counter' KHÔNG phải một chỗ chứa (storage location), nó là lời gọi
                  get_Counter(). Method trả về một GIÁ TRỊ nằm tạm trên evaluation stack -
                  với struct thì giá trị đó là bản sao đầy đủ của backing field.

                  (a) holder.Counter.Value = 42;
                      Ghi vào member của một giá trị tạm là vô nghĩa, nên compiler chặn hẳn
                      bằng CS1612 "Cannot modify the return value ... because it is not a
                      variable". Cần đủ CẢ HAI điều kiện mới ra lỗi này:
                        - phải là property/method (nếu là field thì có địa chỉ để ghi),
                        - phải là struct   (nếu là class thì bản sao là reference, vẫn trỏ
                                            về đúng object gốc nên ghi được).

                  (b) holder.Counter.Inc();
                      Trường hợp này compiler KHÔNG chặn: nó cất giá trị trả về vào một temp
                      local rồi gọi Inc() trên temp đó (ldloca temp / call Inc). Câu lệnh chạy
                      trơn tru, không lỗi, không warning, và không thay đổi gì hết.
                      Đây mới là biến thể nguy hiểm - CS1612 ít ra còn đỏ lên trong IDE.
                """);
        }

        // =====================================================================================
        // BAY 3: foreach tren mang struct
        // =====================================================================================
        private const string Trap3Code = """
            var counters = new Counter[3];
            foreach (var c in counters)
            {
                c.Inc();                            // bien dich OK, khong warning
            }
            Console.WriteLine(counters[0].Value);   // ky vong 1
            """;

        private static void Trap3_ForeachOnStructArray()
        {
            Section("BẪY 3", "foreach trên mảng struct");
            Console.WriteLine(Trap3Code);

            var counters = new Counter[3];
            foreach (var c in counters)
            {
                c.Inc();
            }

            Result(expected: 1, actual: counters[0].Value, expr: "counters[0].Value");

            Console.WriteLine("""

                LÝ DO: hai lần sao chép chồng lên nhau.
                  1) Mỗi vòng lặp, foreach thực hiện 'c = counters[i]' -> copy cả struct từ
                     ô nhớ trong mảng (trên heap) ra biến local c (trên stack).
                  2) Biến lặp của foreach là READ-ONLY theo đặc tả C#. Gọi Inc() (method
                     không readonly) trên một biến read-only lại sinh tiếp một defensive copy
                     y hệt bẫy 1.
                  Kết quả: Inc() tăng bản sao của bản sao. counters[i] không bao giờ đổi.

                  Lưu ý: 'foreach (var c in ...) c = new Counter();' thì compiler BÁO LỖI
                  (CS1656 - không gán được vào biến lặp), nhưng gọi method mutate thì lại
                  cho qua. Đúng chỗ nguy hiểm nhất thì compiler im lặng.

                  Nếu thật sự cần sửa tại chỗ trên MẢNG struct mutable, phải lấy REF:
                      foreach (ref var c in counters.AsSpan()) c.Inc();   // ref -> khong copy
                      for (int i = 0; i < counters.Length; i++) counters[i].Inc();
                  (counters[i] la array element access -> tra ve dia chi that nen ghi duoc)
                """);
        }

        // =====================================================================================
        // PHAN B: BAN SUA - readonly record struct Counter(int Value), Inc() tra ve struct MOI
        // =====================================================================================
        private static class Fixed
        {
            // readonly  -> moi field la readonly, moi instance method duoc coi la readonly
            //              => compiler KHONG can defensive copy nua.
            // record    -> value equality + ToString + bieu thuc 'with' co san.
            // Inc()     -> KHONG mutate, tra ve mot Counter MOI.
            public readonly record struct Counter(int Value)
            {
                public Counter Inc() => this with { Value = Value + 1 };
            }

            // Sua bay 1: struct da bat bien nen readonly field an toan tuyet doi.
            // Muon "tang" thi thay ca object chua no -> y dinh hien ro trong code.
            public sealed class Container
            {
                public readonly Counter Counter;
                public Container(Counter counter) => Counter = counter;
                public Container Inc() => new Container(Counter.Inc());
            }

            // Sua bay 2: property van dung duoc, nhung bat buoc phai GAN NGUOC lai.
            public sealed class Holder
            {
                public Counter Counter { get; set; }
            }
        }

        private const string FixCode = """
            readonly record struct Counter(int Value)
            {
                public Counter Inc() => this with { Value = Value + 1 };   // tra ve struct MOI
            }

            // (1) readonly field  -> thay ca object chua no
            var container = new Container(new Counter());
            container = container.Inc().Inc();

            // (2) property        -> gan nguoc lai, khong the "quen" ma van tuong da chay
            var holder = new Holder();
            holder.Counter = holder.Counter.Inc();
            holder.Counter = holder.Counter.Inc();

            // (3) mang            -> ghi thang vao o nho cua mang
            var counters = new Counter[3];
            for (int i = 0; i < counters.Length; i++)
                counters[i] = counters[i].Inc();
            """;

        private static void FixedVersion()
        {
            Section("BẢN SỬA", "readonly record struct Counter(int Value)");
            Console.WriteLine(FixCode);
            Console.WriteLine();

            // (1) readonly field
            var container = new Fixed.Container(new Fixed.Counter());
            container = container.Inc().Inc();
            Result(expected: 2, actual: container.Counter.Value, expr: "container.Counter.Value");

            // (2) property
            var holder = new Fixed.Holder();
            holder.Counter = holder.Counter.Inc();
            holder.Counter = holder.Counter.Inc();
            Result(expected: 2, actual: holder.Counter.Value, expr: "holder.Counter.Value   ");

            // (3) mang
            var counters = new Fixed.Counter[3];
            for (int i = 0; i < counters.Length; i++)
                counters[i] = counters[i].Inc();
            Result(expected: 1, actual: counters[0].Value, expr: "counters[0].Value      ");

            // Qua tang kem cua 'record': ToString + value equality tu sinh.
            Console.WriteLine();
            Console.WriteLine($"  record => ToString():     {new Fixed.Counter(7)}");
            Console.WriteLine($"  record => equality:       new Counter(7) == new Counter(7) là {new Fixed.Counter(7) == new Fixed.Counter(7)}");

            Console.WriteLine("""

                TẠI SAO HẾT BẪY:
                  - 'readonly' trên struct: mọi field bất biến, mọi instance method được coi là
                    readonly. Compiler không cần defensive copy nữa -> vừa hết bug vừa bớt copy.
                    Đọc struct ra từ readonly field / property / biến foreach giờ đều vô hại,
                    vì không có gì mutate được bản sao đó.
                  - Inc() trả về giá trị mới nên việc "quên gán" hiện ngay tại chỗ gọi:
                    'c.Inc();' đứng một mình đọc là thấy vô nghĩa (còn với struct mutable thì
                    câu đó trông y như đang chạy đúng). Có thể bật analyzer cảnh báo bỏ qua
                    giá trị trả về để compiler bắt luôn hộ.
                  - Muốn đổi trạng thái thì bắt buộc phải viết một phép GÁN vào đúng chỗ chứa
                    thật (biến, field, phần tử mảng) -> không còn chỗ nào cho bản sao tạm.

                QUY TẮC RÚT RA: struct thì để bất biến (readonly struct / readonly record struct).
                Nếu buộc phải có struct mutable, chỉ được đụng vào nó qua biến thật hoặc qua ref
                (ref local, ref return, arr[i], foreach (ref var x in span)).
                """);
        }

        // =====================================================================================
        private static void Summary()
        {
            Section("TỔNG KẾT", "3 bẫy cùng một nguyên nhân: đọc struct ra là ra BẢN SAO");
            Console.WriteLine("""
                +-------------------------------+-----------------+-------------------+-----------------------------+
                | Ngu canh                      | Compiler noi gi | Chuyen gi xay ra  | Cach sua                    |
                +-------------------------------+-----------------+-------------------+-----------------------------+
                | readonlyField.Inc()           | im lang         | defensive copy    | readonly record struct      |
                | prop.Value = 42               | CS1612          | chan tu bien dich | + Inc() tra ve struct moi   |
                | prop.Inc()                    | im lang         | copy tu get_      | holder.X = holder.X.Inc()   |
                | foreach (var c ...) c.Inc()   | im lang         | copy + defensive  | for + arr[i] = arr[i].Inc() |
                | foreach (var c ...) c = ...   | CS1656          | chan tu bien dich | (nhu tren)                  |
                +-------------------------------+-----------------+-------------------+-----------------------------+
                """);
        }

        // ------------------------------- helper hien thi -------------------------------------
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

            Trap1_ReadonlyFieldInClass();
            Trap2_PropertyReturningStruct();
            Trap3_ForeachOnStructArray();
            FixedVersion();
            Summary();
        }
    }
}
