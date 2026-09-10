using System;
using System.Collections.Generic;
using System.Text;

namespace DemoDotNetProfiling.Chapter01
{



    public class AlignmentAndPaddingDemo
    {
        private struct Bad
        {
            byte b;
            long l;
            byte b2;
        }
        private struct Good
        {
            long l;
            byte b;
            byte b2;
        }

        private struct MyBadStruct
        {
            bool b1;
            byte b2;
            int b3;
            long b4;
        }

        private struct MyGoodStruct
        {
            long g1;
            int g2;
            byte g3;
            bool g4;
        }

        public static void Run()
        {
            Console.WriteLine($"Size of Bad = {System.Runtime.InteropServices.Marshal.SizeOf<Bad>()} bytes"); // Output: 24 bytes
            // Taị vì struct Bad có 3 trường dữ liệu: một byte, một long và một byte nữa.
            // Tuy nhiên, do alignment và padding, struct này sẽ được căn chỉnh để đảm bảo rằng các trường dữ liệu được đặt ở các địa chỉ bộ nhớ phù hợp với kích thước của chúng.
            // Cụ thể, long cần được căn chỉnh ở địa chỉ chia hết cho 8, vì vậy compiler sẽ thêm 7 byte padding sau trường byte đầu tiên để đảm bảo rằng trường long bắt đầu ở địa chỉ chia hết cho 8.
            // Sau đó, trường byte thứ hai sẽ được đặt ngay sau trường long, nhưng do struct phải có kích thước là bội số của 8 (vì long là trường lớn nhất), compiler sẽ thêm 7 byte padding nữa sau trường byte thứ hai.
            // Do đó, tổng kích thước của struct Bad là 24 bytes.

            // Hình minh họa: 
            // [byte][padding][long][byte][padding]
            //  1     7      8    1     7
            // Total: 24 bytes

            Console.WriteLine($"Size of Good = {System.Runtime.InteropServices.Marshal.SizeOf<Good>()} bytes");
            // Struct Good được sắp xếp lại để tránh padding không cần thiết.
            // Trường long được đặt đầu tiên, tiếp theo là hai trường byte.

            // Hình minh họa:
            // [long][byte][byte][padding]
            //  8     1     1      6

            Console.WriteLine($"Size of MyBadStruct = {System.Runtime.InteropServices.Marshal.SizeOf<MyBadStruct>()} bytes");
            Console.WriteLine($"Size of MyBadStruct (with [StructLayout(LayoutKind.Sequential, Pack = 1)]) = {System.Runtime.InteropServices.Marshal.SizeOf<MyBadStruct>()} bytes");

            Console.WriteLine($"Size of MyGoodStruct = {System.Runtime.InteropServices.Marshal.SizeOf<MyGoodStruct>()} bytes");
            Console.WriteLine($"Size of MyGoodStruct (with [StructLayout(LayoutKind.Sequential, Pack = 1)]) = {System.Runtime.InteropServices.Marshal.SizeOf<MyGoodStruct>()} bytes");

            // Hai struct MyBadStruct và MyGoodStruct có cùng số lượng trường dữ liệu,
            // vì tổng kích thước thực tế phải làm tròn lên con số chia hết cho trường có kích thước lớn nhất (long = 8 bytes).
        }
    }
}
