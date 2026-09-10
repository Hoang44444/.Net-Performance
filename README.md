# .NET Performance

Lab thực hành theo tài liệu **"Value Type vs Reference Type · Stack vs Heap · Object Lifetime"** — 30 bài tập chia 3 chương. Mỗi bài là một file C# chạy được, in ra bằng chứng ngay trên console (kèm số đo cấp phát/thời gian), phần nào cần đo chính xác thì có sẵn benchmark BenchmarkDotNet.

Target framework: **net10.0**.

## Cấu trúc

```
.
├── DotNetPerformance.slnx
├── docs/
│   ├── dotnet-performance-roadmap.html     # lộ trình tổng
│   └── rpg-battle-simulator-de-bai.md      # đề lab console RPG (luyện chương 1)
└── src/
    └── DemoDotNetProfiling/
        ├── Program.cs
        └── Chapter01_ValueTypeVsReferenceType/
            ├── ChapterMenu.cs              # menu điều phối, nhập số bài để chạy
            └── Ex01..Ex10_*.cs             # mỗi file một bài
```

Namespace dùng chung cho cả chương: `DemoDotNetProfiling.Chapter01`.

> Hai file PDF tài liệu gốc **không** nằm trong repo (đã cho vào `.gitignore`) — chúng ở cùng thư mục trên máy local.

## Chạy

```bash
dotnet run --project src/DemoDotNetProfiling
```

Nhập số bài (1–10), `0` để thoát. Bài nào có benchmark sẽ hỏi `[y/N]` ở cuối — trả lời `y` để chạy BenchmarkDotNet.

Benchmark **bắt buộc build Release**, nếu không BenchmarkDotNet sẽ từ chối chạy:

```bash
dotnet run --project src/DemoDotNetProfiling -c Release
```

## Chương 1 — Value Type vs Reference Type

| Bài | File | Nội dung | Số đo thu được |
|----|------|----------|----------------|
| 1.1 | `Ex01_CopyVsAlias.cs` | Gán struct là copy 8 byte dữ liệu, gán class là copy 8 byte địa chỉ | |
| 1.2 | `Ex02_ReassignVsMutate.cs` | Gán lại tham số vs sửa object mà nó trỏ tới | |
| 1.3 | `Ex03_RefOutInWithBigStruct.cs` | Struct 80 byte truyền by-value vs `ref` / `in` | benchmark |
| 1.4 | `Ex04_AlignmentAndPadding.cs` | Alignment & padding: `Bad` 24 byte vs `Good` 16 byte | |
| 1.5 | `Ex05_EqualityTrap.cs` | `ValueType.Equals` reflection-based vs `IEquatable<T>` | `List.Contains`: 184 KB → 0 B, nhanh gấp 56× |
| 1.6 | `Ex06_BoxingHunt.cs` | Săn lệnh IL `box` từ 5 nguồn boxing khác nhau | |
| 1.7 | `Ex07_MutableStructTrap.cs` | 3 bẫy struct mutable, fix bằng `readonly record struct` | |
| 1.8 | `Ex08_StringValueSemantics.cs` | Bất biến, literal interning, `+=` vs `StringBuilder` | `+=` 10.000 lần: **95,62 MB / 11,2 ms** vs **53 KB / 0,23 ms** |
| 1.9 | `Ex09_NullableAndTuples.cs` | `Unsafe.SizeOf<int?>()`, box/unbox `Nullable<T>`, ValueTuple vs Tuple | 1 triệu tuple: **0 B** vs **30,52 MB** |
| 1.10 | `Ex10_RefStructAndSpan.cs` | Parse ngày bằng `ReadOnlySpan.Slice`, giới hạn của `ref struct`, 4 tiêu chí struct/class | parse 100.000 lần: **0 B** vs **13,73 MB** |

### Ghi chú riêng cho bài 1.10

Phần B của bài chứng minh những chỗ compiler **cấm** `ref struct`. Các đoạn đó cố tình không biên dịch được nên bị bọc trong `#if`. Muốn tự đọc lỗi thì bỏ dấu `//` ở dòng đầu `Ex10_RefStructAndSpan.cs`:

```csharp
#define REF_STRUCT_COMPILE_ERRORS
```

Mã lỗi trên .NET 10 / C# 14 (đã build thử để xác nhận):

| Viết gì | Lỗi |
|---------|-----|
| `class C { Span<int> _s; }` | CS8345 |
| `object o = span;` | CS0029 |
| `new List<Span<int>>()` | CS9244 |
| `Span<int>[] arr;` | CS0611 |
| `Func<int> f = () => span[0];` | CS8175 |
| dùng span sau `await` / `yield return` | CS4007 |

Roslyn báo lỗi theo pha: CS8345 là lỗi **pha khai báo** nên khi nó còn đó thì 6 lỗi kia chưa hiện. Comment tạm `class BadHolder` để xem tiếp.

## Kết quả benchmark

Mỗi file bài tập có khối `// Results` ở cuối để dán bảng BenchmarkDotNet sau khi chạy Release. Thư mục `BenchmarkDotNet.Artifacts/` do BDN tự sinh và không được theo dõi trong git.

## Còn lại

- [ ] Chương 2 — Stack vs Heap (2.1–2.10)
- [ ] Chương 3 — Object Lifetime (3.1–3.10)
