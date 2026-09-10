using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace DemoDotNetProfiling.Chapter01
{
    // 10 x double = 10 x 8 bytes = 80 bytes.
    // Too big for a register, so passing it by value means copying 80 bytes on every call.
    public struct BigStruct
    {
        public double F0;
        public double F1;
        public double F2;
        public double F3;
        public double F4;
        public double F5;
        public double F6;
        public double F7;
        public double F8;
        public double F9;

        public BigStruct(double seed)
        {
            F0 = seed + 0; F1 = seed + 1; F2 = seed + 2; F3 = seed + 3; F4 = seed + 4;
            F5 = seed + 5; F6 = seed + 6; F7 = seed + 7; F8 = seed + 8; F9 = seed + 9;
        }
    }

    public class BigStructParameterBenchmarks
    {
        // Each [Benchmark] calls its target 10 million times.
        private const int N = 10_000_000;

        private BigStruct _data = new BigStruct(1); 

        // NoInlining is mandatory here: if the JIT inlines these one-liners the copy
        // disappears and all three benchmarks measure the exact same thing.

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double ReadByValue(BigStruct s) => s.F0;   // copies 80 bytes per call

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double ReadByRef(ref BigStruct s) => s.F0; // passes an 8-byte address, callee may write

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double ReadByIn(in BigStruct s) => s.F0;   // same address, callee is read-only

        // `out` cannot be part of this comparison: the callee is forbidden to read the
        // incoming value and must assign every field before returning, so it measures an
        // 80-byte *write*, not a read. Kept here only to show the calling convention.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteByOut(out BigStruct s) => s = new BigStruct(1);

        [Benchmark(Baseline = true, Description = "by value (copy 80 bytes)")]
        public double ByValue()
        {
            double sum = 0;
            for (int i = 0; i < N; i++)
            {
                sum += ReadByValue(_data);
            }
            return sum;
        }

        [Benchmark(Description = "ref (address)")]
        public double ByRef()
        {
            double sum = 0;
            for (int i = 0; i < N; i++)
            {
                sum += ReadByRef(ref _data);
            }
            return sum;
        }

        [Benchmark(Description = "in (readonly address)")]
        public double ByIn()
        {
            double sum = 0;
            for (int i = 0; i < N; i++)
            {
                sum += ReadByIn(in _data);
            }
            return sum;
        }

        [Benchmark(Description = "out (write only, not comparable)")]
        public double ByOut()
        {
            double sum = 0;
            for (int i = 0; i < N; i++)
            {
                WriteByOut(out BigStruct s);
                sum += s.F0;
            }
            return sum;
        }
    }

    // Exercise 1.3: ref / out / in with a large struct
    class RefOutInWithBigStructDemo
    {
        public static void Run()
        {
            Console.WriteLine($"sizeof(BigStruct) = {Unsafe.SizeOf<BigStruct>()} bytes");
            Console.WriteLine("Running BenchmarkDotNet (Release build required), this takes a few minutes...");
            Console.WriteLine();

            BenchmarkRunner.Run<BigStructParameterBenchmarks>();
        }

        // Results 
        //| Method                             | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD |
        //|----------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|
        //| 'by value (copy 80 bytes)'         | 19.03 ms | 0.854 ms | 2.518 ms | 20.12 ms |  1.02 |    0.22 |
        //| 'ref (address)'                    | 15.53 ms | 0.523 ms | 1.541 ms | 15.83 ms |  0.84 |    0.17 |
        //| 'in (readonly address)'            | 13.87 ms | 0.266 ms | 0.336 ms | 13.87 ms |  0.75 |    0.13 |
        //| 'out (write only, not comparable)' | 14.20 ms | 0.138 ms | 0.122 ms | 14.20 ms |  0.76 |    0.13 |

        // Mean: Thời gian trung bình mà phương thức mất để thực hiện, được đo bằng mili giây (ms).
        // Error: Sai số chuẩn của phép đo, cho biết mức độ không chắc chắn trong kết quả.
        // StdDev: Độ lệch chuẩn của các phép đo, cho biết mức độ biến đổi trong kết quả.
        // Median: Giá trị trung vị của các phép đo, cho biết giá trị giữa của tập dữ liệu.
        // Ratio: Tỷ lệ so sánh với phương thức cơ sở (Baseline),
        // cho biết phương thức mất bao nhiêu lần so với phương thức cơ sở.
        // RatioSD: Sai số chuẩn của tỷ lệ, cho biết mức độ không chắc chắn trong tỷ lệ.
    }
}
