# Đề bài: RPG Battle Simulator — console lab luyện Value Type vs Reference Type

## Domain

Console app mô phỏng chiến đấu theo lượt (turn-based). Model:

```csharp
struct Vector2   { float X, Y; }                                   // vị trí/di chuyển
struct StatBlock  { int Hp, Atk, Def, Spd; }                        // chỉ số nhân vật
struct Item       { string Name; ItemType Type; int Value; }        // vũ khí/giáp/vật phẩm
record struct BattleLogEntry { int Turn; string Attacker; string Defender; int Damage; bool Critical; }

class Character   { Guid Id; string Name; StatBlock Stats; Vector2 Position; List<Item> Inventory; }
class Monster : Character { ... }
class Party       { List<Character> Members; }
```

Chạy: `dotnet run -- <module>`. Mỗi module dưới đây = 1 yêu cầu code cụ thể + tiêu chí cần đạt sau khi code xong (không cần dựng code sai để rồi sửa — code đúng ngay từ đầu, chứng minh bằng số liệu/kết quả chạy).

---

## Module 1 — `valueref` (mục 1.1–1.2)

**Yêu cầu:** Định nghĩa đủ 3 struct (`Vector2`, `StatBlock`, `Item`) và 2 class (`Character`, `Party`) như trên. Viết hàm demo: copy 1 `StatBlock`, sửa bản copy, in cả bản gốc lẫn bản copy; làm tương tự với `Character`. Kèm bảng liệt kê toàn bộ kiểu dùng trong domain, phân loại value/reference kèm lý do chọn cho từng kiểu.

**Đạt được:** In ra được bằng chứng: sửa bản copy `StatBlock` không ảnh hưởng bản gốc; sửa qua biến copy `Character` thì bản gốc cũng đổi theo. Bảng phân loại đầy đủ, lý do hợp lý (không chỉ liệt kê suông).

## Module 2 — `params` (mục 1.3)

**Yêu cầu:** Viết 4 hàm nghiệp vụ dùng đủ 4 cách truyền tham số:
- `int CalculateDamage(StatBlock attacker, StatBlock defender)` — truyền value
- `void ApplyDamage(ref StatBlock target, int amount)` — trừ HP trực tiếp bằng `ref`
- `bool TryLevelUp(Character c, out StatBlock newStats)` — `out`
- `void LogStats(in StatBlock stats)` — chỉ đọc, `in`

Chạy 1 trận đấu demo vài lượt, dùng đủ cả 4 hàm.

**Đạt được:** In trạng thái HP trước/sau mỗi lượt, thấy rõ hàm nào mutate được state gốc (ref), hàm nào không (value), và `in` không cho phép sửa nhầm (thử sửa trong `LogStats` phải không biên dịch được — chứng minh bằng comment giải thích, không cần build lỗi thật).

## Module 3 — `memory` (mục 1.4)

**Yêu cầu:** Đo `Marshal.SizeOf`/`sizeof()` cho `Vector2`, `StatBlock`, `Item`. Thử 2 thứ tự khai báo field khác nhau cho `StatBlock`, so sánh kích thước. Tạo 100,000 `Character` (class) và 100,000 `StatBlock` trần (struct), đo `GC.GetTotalMemory` trước/sau mỗi loại.

**Đạt được:** Bảng số liệu byte thật cho từng kiểu; kết luận bằng con số cụ thể (không ước lượng) overhead heap của class so với struct; chỉ ra thứ tự field nào tiết kiệm hơn và vì sao (alignment).

## Module 4 — `equality` (mục 1.5)

**Yêu cầu:** Override `Equals`+`GetHashCode` cho `StatBlock`/`Item` (structural equality — 2 item cùng Name+Type+Value là bằng nhau). `Character` so sánh theo `Id` (identity). Benchmark so sánh 1 triệu cặp `StatBlock` bằng `Equals` mặc định (chưa override) vs bản đã override.

**Đạt được:** Số liệu chênh lệch thời gian cụ thể (ms) giữa 2 cách; xác nhận `GC.CollectionCount(0)` không tăng khi dùng bản đã override (không boxing xảy ra).

## Module 5 — `boxing` (mục 1.6)

**Yêu cầu:** Viết đủ 12 đoạn code ngắn, mỗi đoạn minh hoạ 1 nguồn boxing khác nhau trong domain RPG (vd: log `StatBlock` qua `Console.WriteLine` trước khi có `ToString()` overload, lưu `Item` vào `ArrayList`, so `Vector2` qua `IComparable` non-generic, dùng `StatBlock` làm key `Hashtable`...). Benchmark 1 tác vụ xử lý bằng generic collection (`List<T>`, `Dictionary<K,V>`) vs non-generic tương đương trên 1 triệu phần tử.

**Đạt được:** 12 đoạn code chạy được, mỗi đoạn có comment chỉ rõ chỗ nào boxing xảy ra và vì sao. Số liệu benchmark cho thấy generic nhanh hơn rõ rệt (ghi số lần cụ thể) + `GC.CollectionCount(0)` delta thấp hơn hẳn.

## Module 6 — `mutablestruct` (mục 1.7)

**Yêu cầu:** Cài đặt vòng lặp turn-based cập nhật HP toàn bộ `Party.Members` khi nhận damage hàng loạt (`List<StatBlock>` hoặc field `Stats` trong `List<Character>`). Dùng đúng cách hợp lệ để mutate struct trong list tại chỗ: reassignment qua index (`list[i] = updated`) hoặc `CollectionsMarshal.AsSpan(list)` để lấy `Span` mutable. Viết hàm `ApplyDamageToParty(Party party, int[] damagePerMember)`.

**Đạt được:** Sau N turn, tổng HP thực tế của party khớp đúng với tổng damage đã áp — assert bằng số cụ thể, không chỉ nhìn mắt. Code có comment giải thích vì sao không thể mutate trực tiếp qua biến lặp `foreach` (hiểu lý thuyết, không cần chạy thử code sai).

## Module 7 — `specialtypes` (mục 1.8)

**Yêu cầu**, tách theo từng kiểu:
- **string**: dùng `Name` của `Character`/`Item` làm key tra cứu; chứng minh interning bằng so sánh string literal vs string dựng lúc chạy (đọc từ file).
- **Nullable\<T\>**: `int? TemporaryBuff` (buff tạm thời, có thể null); demo boxing khi có giá trị vs khi null.
- **ValueTuple vs Tuple**: `(int damage, bool isCritical) ResolveAttack(StatBlock attacker, StatBlock defender)`; so allocation với `Tuple<int,bool>` tương đương.
- **ref struct/Span\<T\>**: parse file text danh sách quái vật (`name,hp,atk,def` mỗi dòng) bằng `ReadOnlySpan<char>`, không dùng `Substring`; đo `GC.GetTotalMemory` khi parse 1 triệu dòng, so với cách dùng `string.Split`.
- **record class vs record struct**: `BattleLogEntry` là `record struct` (value equality, immutable log); `Character` vẫn là class thường (identity, mutable theo thời gian).

**Đạt được:** Mỗi kiểu đặc biệt có 1 đoạn code chạy được + 1 con số/kết quả đo cụ thể (byte, ms, hoặc kết quả so sánh) xác nhận đúng lý thuyết tương ứng.

## Module 8 — `advisor` (mục 1.9)

**Yêu cầu:** Console wizard hỏi-đáp áp cho từng model đã tạo (`Vector2`, `StatBlock`, `Item`, `Character`, `BattleLogEntry`): hỏi kích thước <16 byte?, cần bất biến/snapshot?, bị copy nhiều lần/giây?, cần identity xuyên suốt vòng đời?, cần kế thừa/đa hình? → in khuyến nghị struct/class kèm lý do.

**Đạt được:** Khuyến nghị wizard đưa ra cho từng model khớp đúng với lựa chọn đã dùng thật ở Module 1 — dùng để tự đối chiếu xem hiểu lý thuyết có nhất quán với thực hành không.

---

## Yêu cầu kỹ thuật chung

- `dotnet run` thuần, không cần Visual Studio; dùng `dotnet-counters`/`dotnet-trace` khi chạy module `boxing`/`memory` để soi GC thật.
- Đo nhanh bằng `Stopwatch`; muốn chuẩn hơn tách project `*.Benchmarks` dùng BenchmarkDotNet.
- Input số liệu lớn (100k–1M) nên có generator random, không gõ tay.

## Tiêu chí hoàn thành tổng

9 module chạy được, mỗi module ra kết quả/con số cụ thể đúng như phần "Đạt được" — không phải chỉ code minh hoạ mà không có gì để kiểm chứng.
