using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class DataRepresentationLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return BitsBytesHex();
        yield return HexAddresses();
        yield return Integers();
        yield return Endianness();
        yield return FloatingPoint();
        yield return TextEncoding();
        yield return BoolsEnumsFlags();
        yield return StructLayout();
        yield return ReferencesAndObjects();
    }

    private static SceneState Bits(this SceneState s, string prefix, string bits, double x, double y, double w = 64, Tone tone = Tone.Neutral, bool weights = true)
    {
        for (var i = 0; i < bits.Length; i++)
        {
            var id = prefix + i;
            s.Box(id, bits[i].ToString(), x + i * (w + 6), y, w, 52, tone: bits[i] == '1' ? Info : tone);
            s.Shape(id).Mono = true;
            s.TagText(id, weights ? (1 << (bits.Length - 1 - i)).ToString() : null);
        }

        return s;
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BitsBytesHex() =>
        new StoryBuilder("DR1", "Everything in memory is bits. Hexadecimal is just a compact way to write them: each hex digit is exactly 4 bits, so two hex digits are exactly one byte.")
            .Setup(s =>
            {
                s.Bits("bit", "10110111", 180, 80);
                s.Label("dec", "", 180, 160, 700, 18, Info);
            })
            .NoCode()
            .Step("A byte is 8 bits",
                "Each bit is 0 or 1, and each position has a weight: 128, 64, 32, 16, 8, 4, 2, 1. Add the weights of the 1 bits.",
                "8 bits give 2^8 = 256 combinations (0-255). A byte is the smallest unit that has its own memory address.",
                s => s.Write("dec", "128 + 32 + 16 + 4 + 2 + 1 = 183"))
            .Step("Split into two nibbles",
                "Group the bits in fours: 1011 and 0111. Each group of 4 bits (a nibble) has 16 possible values: 0-9 then A-F.",
                "1011 = 8 + 2 + 1 = 11 = B, and 0111 = 7. So the byte is 0xB7. Decimal digits do not line up with bits, but hex digits line up perfectly, which is why programmers use hex.",
                s =>
                {
                    s.Region("n0", "nibble = B", 172, 30, 286, 132, Info);
                    s.Region("n1", "nibble = 7", 456, 30, 286, 132, Good);
                    s.Write("dec", "1011 0111  =  0xB7  =  183");
                })
            .Step("The hex digits",
                "Sixteen digits, each standing for exactly 4 bits.",
                "Memorise a few anchors: 0x0 = 0000, 0x8 = 1000, 0xA = 1010, 0xF = 1111. Then any hex number converts to binary digit by digit, with no arithmetic.",
                s =>
                {
                    s.Remove("n0", "n1");
                    for (var d = 0; d < 16; d++)
                    {
                        s.Box("hx" + d, d.ToString("X"), 60 + d % 8 * 112, 240 + d / 8 * 80, 100, 64, Convert.ToString(d, 2).PadLeft(4, '0'));
                        s.Shape("hx" + d).Mono = true;
                    }

                    s.PaintAll(Info, "hx11", "hx7");
                })
            .Step("Bigger values: 4 bytes = 8 hex digits",
                "A 32-bit value like 0xDEADBEEF is 4 bytes: DE, AD, BE, EF. Every pair of hex digits is one byte.",
                "That is why debuggers and memory dumps show hex: you can see byte boundaries at a glance. 0xDEADBEEF and 0xCAFEBABE are famous 'magic' values chosen to be easy to spot.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("hx", StringComparison.Ordinal));
                    s.Fields("bytes", [("byte 3", "DE", 1), ("byte 2", "AD", 1), ("byte 1", "BE", 1), ("byte 0", "EF", 1)], 180, 260, 600, 70, Info);
                    s.Write("dec", "0xDEADBEEF = 3,735,928,559 = 32 bits = 4 bytes");
                })
            .Step("Hex in everyday tools",
                "C# and C write hex as 0x... and binary as 0b.... Colours are hex bytes (#FF8800 = red FF, green 88, blue 00). Sizes too: 4 KiB = 4096 = 0x1000.",
                "KB (1000) and KiB (1024 = 0x400) differ, and the gap grows with size: 1 TB is about 0.91 TiB. Use 'X2' formatting in .NET to print a byte as two hex digits.",
                s =>
                {
                    s.Remove("bytes0", "bytes1", "bytes2", "bytes3");
                    s.Box("c1", "#FF8800", 180, 260, 200, 70, "colour: R=FF G=88 B=00", Warn);
                    s.Box("c2", "0x1000", 420, 260, 200, 70, "4 KiB page size", Info);
                    s.Box("c3", "0b1011_0111", 660, 260, 200, 70, "binary literal (C#)", Good);
                    s.Write("dec", "0xB7 == 183 == 0b1011_0111");
                })
            .Lesson("Bits, bytes and hexadecimal", "Numbers and memory", "Why every byte is two hex digits, and how to convert between binary, hex and decimal in your head.");

    // ------------------------------------------------------------------------------------------
    private static Lesson HexAddresses()
    {
        string[] text = ["48", "65", "6C", "6C", "6F", "2C", "20", "77", "6F", "72", "6C", "64", "21", "00", "00", "00"];
        string[] text2 = ["2A", "00", "00", "00", "FF", "FF", "FF", "FF", "40", "1A", "FE", "7F", "00", "00", "00", "00"];

        return new StoryBuilder("DR2", "A memory address is the index of a byte in a giant array, written in hex. The low hex digits tell you the position inside a row, a cache line or a page; the high digits tell you which region of memory you are in.")
            .Setup(s =>
            {
                s.Label("hdr", "address", 40, 30, 150, 12);
                for (var c = 0; c < 16; c++)
                {
                    s.Label("col" + c, $"+{c:X}", 190 + c * 48 + 12, 30, 40, 12);
                    s.Shape("col" + c).Mono = true;
                }
                s.Label("a0", "0x7FFE1A20", 40, 66, 150, 13);
                s.Label("a1", "0x7FFE1A30", 40, 126, 150, 13);
                s.Shape("a0").Mono = true;
                s.Shape("a1").Mono = true;
                s.Cells("r0", text, 190, 56, 44, 40, 4, tags: false);
                s.Cells("r1", text2, 190, 116, 44, 40, 4, tags: false);
                s.Label("ascii", "\"Hello, world!\"", 40, 180, 400, 14, Info);
            })
            .Code("""
                byte* p = (byte*)0x7FFE1A20;
                byte c = p[3];                 // reads address 0x7FFE1A23 -> 0x6C ('l')
                int*  q = (int*)p;
                int*  next = q + 1;            // 0x7FFE1A24: pointer arithmetic adds sizeof(int)
                """)
            .Step("Memory is a byte array",
                "A hex dump shows memory 16 bytes per row. The address column is the address of the first byte in that row, and each row is 0x10 (16) bytes after the previous one.",
                "Every byte has its own address (byte-addressable memory). The bytes 48 65 6C 6C 6F... are ASCII for \"Hello, world!\", ending with a 00 terminator, as a C string would.",
                [1])
            .Step("Read address 0x7FFE1A23",
                "The last hex digit is the column: row 0x7FFE1A20, column +3. That byte is 0x6C, the letter 'l'.",
                "This is why hex is used for addresses: with 16 bytes per row, the last digit is literally the column. Decimal addresses would need division to find it.",
                [2],
                s => s.Paint("r03", Good).Paint("a0", Info))
            .Step("A 64-bit address, digit by digit",
                "On x64, a pointer is 8 bytes = 16 hex digits, for example 0x00007FFE_1A2B3C40. Today's CPUs use only the low 48 (or 57) bits; the top bits must copy bit 47.",
                "So user-space addresses start with 0x0000 7F..., and kernel addresses with 0xFFFF 8.... An address that breaks this 'canonical' rule faults immediately. Each run of a program gets different addresses because of ASLR (address space layout randomisation).",
                [],
                s =>
                {
                    s.Paint("r03", Neutral).Paint("a0", Neutral);
                    s.Fields("va", [("unused: copies bit 47", "0000", 4), ("virtual page number (36 bits)", "7FFE_1A2B3", 9), ("page offset (12 bits)", "C40", 3)], 40, 260, 920, 70);
                    s.PaintAll(Neutral, "va0");
                    s.Paint("va1", Info).Paint("va2", Good);
                })
            .Step("The last three hex digits: inside the page",
                "4 KiB = 0x1000, so the last 3 hex digits (12 bits) are the offset inside a 4 KiB page. Everything above them is the page number the TLB translates.",
                "Alignment can also be read straight off the hex. An address ending in 0 is 16-byte aligned; one ending in 00, 40, 80 or C0 is 64-byte (cache line) aligned; one ending in 000 is page aligned.",
                [],
                s =>
                {
                    s.Paint("va2", Good);
                    s.Box("align", "...C40 ends in 40: 64-byte (cache line) aligned", 240, 380, 520, 50, tone: Good);
                })
            .Step("Pointer arithmetic",
                "Adding 1 to an int* moves it 4 bytes (0x7FFE1A20 -> 0x7FFE1A24), because the compiler scales by the size of the pointed-to type.",
                "Address 0x0 (null) is deliberately left unmapped, so dereferencing null faults instead of silently reading garbage. In C# you see raw addresses only in unsafe code, debuggers and crash dumps.",
                [3, 4],
                s =>
                {
                    s.Remove("align");
                    s.PaintAll(Info, "r00", "r01", "r02", "r03");
                    s.PaintAll(Good, "r04", "r05", "r06", "r07");
                    s.Box("ptr", "q = 0x7FFE1A20, q + 1 = 0x7FFE1A24", 240, 380, 520, 50, tone: Info);
                })
            .Lesson("What a hexadecimal memory address means", "Numbers and memory", "Reading hex dumps, 64-bit addresses, page offsets, alignment and pointer arithmetic.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Integers() =>
        new StoryBuilder("DR3", "Integers are fixed-width bit patterns. Signed integers use two's complement, so the same adder works for both. Overflow silently wraps around unless you ask for checking.")
            .Setup(s =>
            {
                s.Bits("u", "00000101", 180, 70);
                s.Label("val", "unsigned byte 0000 0101 = 5", 180, 150, 700, 18, Info);
            })
            .Code("""
                sbyte x = -5;              // 0xFB = 1111 1011
                int max = int.MaxValue;     // 0x7FFFFFFF
                int wrapped = max + 1;      // unchecked: -2147483648
                checked { max + 1; }        // throws OverflowException
                long widened = x;           // sign-extended: 0xFFFFFFFFFFFFFFFB
                """)
            .Step("Unsigned: plain binary",
                "A byte holds 0 to 255. 5 is 0000 0101.",
                "Unsigned types (byte, ushort, uint, ulong) use every bit for magnitude.",
                [])
            .Step("Signed: two's complement",
                "To store -5: invert the bits of 5 (1111 1010) and add 1, giving 1111 1011 = 0xFB. In other words, the top bit is worth -128.",
                "-128 + 64 + 32 + 16 + 8 + 2 + 1 = -5. The same adder circuit adds signed and unsigned numbers; only the interpretation differs. An sbyte ranges from -128 to 127.",
                [1],
                s =>
                {
                    s.Bits("u", "11111011", 180, 70);
                    s.TagText("u0", "-128").Paint("u0", Warn);
                    s.Write("val", "sbyte 1111 1011 = 0xFB = -5   (but as a byte it is 251)");
                })
            .Step("Overflow wraps around",
                "127 + 1 in an sbyte gives 0111 1111 + 1 = 1000 0000 = -128. int.MaxValue + 1 is int.MinValue.",
                "C# is unchecked by default, for speed. Use 'checked' or <CheckForOverflowUnderflow> where overflow is a bug. YouTube's view counter overflowed a 32-bit signed int in 2014 (Gangnam Style), and Ariane 5 was lost in 1996 after converting a 64-bit float into a 16-bit integer overflowed.",
                [2, 3, 4],
                s =>
                {
                    s.Bits("u", "10000000", 180, 70);
                    s.TagText("u0", "-128").Paint("u0", Bad);
                    s.Write("val", "0111 1111 (127) + 1 = 1000 0000 = -128").Paint("val", Bad);
                })
            .Step("Every integer type",
                "Width decides the range: 8, 16, 32, 64 or 128 bits, signed or unsigned.",
                "nint/nuint are pointer-sized (64-bit on x64/ARM64). char is a 16-bit unsigned UTF-16 code unit, not a 'character'. Int128 and UInt128 arrived in .NET 7. BigInteger has arbitrary size but is heap-allocated.",
                [],
                s =>
                {
                    s.Paint("val", Info);
                    s.Table("types", "integer types", 180, 220, 640,
                        "sbyte / byte     8 bit   -128..127        / 0..255",
                        "short / ushort  16 bit   -32,768..32,767  / 0..65,535",
                        "int / uint      32 bit   about +/-2.1 billion / 0..4.29 billion",
                        "long / ulong    64 bit   about +/-9.2 x 10^18 / 0..1.8 x 10^19",
                        "Int128/UInt128 128 bit   about +/-1.7 x 10^38",
                        "nint / nuint    pointer-sized (64 bit on x64/ARM64)");
                })
            .Step("Widening: sign extension",
                "Converting sbyte -5 (0xFB) to long copies the sign bit into all new bits: 0xFFFFFFFFFFFFFFFB. Converting byte 0xFB zero-extends to 251.",
                "Mixing signed and unsigned values is a classic bug source. For example, (uint)-1 is 4,294,967,295, so a check like 'if (index < count)' with a negative index cast to uint can unexpectedly pass or fail.",
                [5],
                s =>
                {
                    s.Remove("types");
                    s.Bits("u", "11111011", 180, 70);
                    s.TagText("u0", "-128").Paint("u0", Warn);
                    s.Fields("ext", [("sign-extended bits", "FFFFFFFFFFFFFF", 7), ("original", "FB", 1)], 180, 230, 640, 60, Info);
                    s.Write("val", "sbyte -5 -> long -5 = 0xFFFFFFFFFFFFFFFB");
                })
            .Lesson("Integers: unsigned, two's complement and overflow", "Numbers and memory", "How signed numbers are stored, why overflow wraps, and every integer type side by side.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Endianness() =>
        new StoryBuilder("DR4", "A multi-byte value occupies several consecutive addresses, and the CPU decides which end goes first. x86, ARM and RISC-V are little-endian; network protocols and many file formats are big-endian.")
            .Setup(s =>
            {
                s.Box("val", "0x12345678", 380, 30, 240, 56, "int value", Info);
                s.Shape("val").Mono = true;
                s.Caption("lcap", "little-endian (x86, ARM, RISC-V)", 60, 140);
                s.Caption("bcap", "big-endian (network byte order)", 60, 300);
                for (var i = 0; i < 4; i++)
                {
                    s.Box("le" + i, "", 60 + i * 120, 170, 110, 60, $"0x{0x1000 + i:X4}");
                    s.Box("be" + i, "", 60 + i * 120, 330, 110, 60, $"0x{0x1000 + i:X4}");
                    s.Shape("le" + i).Mono = true;
                    s.Shape("be" + i).Mono = true;
                }
            })
            .Code("""
                int v = 0x12345678;
                byte[] bytes = BitConverter.GetBytes(v);                  // 78 56 34 12 on x64
                int net = BinaryPrimitives.ReadInt32BigEndian(packet);     // network order
                bool little = BitConverter.IsLittleEndian;                 // true on x64 / ARM64
                """)
            .Step("Four bytes, four addresses",
                "Storing the int 0x12345678 at address 0x1000 uses addresses 0x1000-0x1003. Which byte goes first?",
                "Single bytes have no order problem. The question only appears when a value spans more than one address.",
                [1])
            .Step("Little-endian: least significant byte first",
                "x86, ARM (in its normal mode) and RISC-V store 78 at 0x1000, then 56, 34, 12.",
                "A handy property: the low byte is at the same address whatever the width, so reading the first 1, 2 or 4 bytes of a value gives the narrower value directly.",
                [2],
                s =>
                {
                    string[] le = ["78", "56", "34", "12"];
                    for (var i = 0; i < 4; i++)
                    {
                        s.Write("le" + i, le[i]).Paint("le" + i, i == 0 ? Good : Info);
                    }
                })
            .Step("Big-endian: most significant byte first",
                "Network protocols (IP, TCP headers) and formats such as PNG and Java class files store 12 first, which reads naturally left to right.",
                "That is why sockets code converts with htons/htonl or BinaryPrimitives: the value on the wire is big-endian, while the CPU is little-endian.",
                [3],
                s =>
                {
                    string[] be = ["12", "34", "56", "78"];
                    for (var i = 0; i < 4; i++)
                    {
                        s.Write("be" + i, be[i]).Paint("be" + i, i == 0 ? Warn : Info);
                    }
                })
            .Step("Read with the wrong order: garbage",
                "Interpreting the big-endian bytes as little-endian yields 0x78563412, which is 2,018,915,346 instead of 305,419,896.",
                "Bugs like this appear when parsing binary files or network packets with BitConverter or pointer casts. Always state the byte order explicitly (BinaryPrimitives.ReadInt32BigEndian/LittleEndian).",
                [2, 3, 4],
                s =>
                {
                    s.Box("wrong", "0x78563412 (wrong order)", 560, 330, 300, 60, tone: Bad);
                    s.Say("Byte order is part of every binary format", Warn);
                })
            .Lesson("Endianness: byte order in memory", "Numbers and memory", "Little- vs big-endian, network byte order, and the bug you get when they are mixed up.");

    // ------------------------------------------------------------------------------------------
    private static Lesson FloatingPoint() =>
        new StoryBuilder("DR5", "Floats store sign, exponent and mantissa, like scientific notation in binary. Most decimal fractions (0.1!) cannot be represented exactly. Pick the format by range, precision and hardware support.")
            .Setup(s =>
            {
                s.Fields("f", [("sign (1)", "0", 3), ("exponent (8)", "10000001", 8), ("mantissa (23)", "10010000000000000000000", 23)], 60, 60, 880, 70, Info);
                s.Label("eq", "", 60, 160, 900, 17, Info);
            })
            .Code("""
                float f = 6.25f;                     // 0x40C80000
                double d = 0.1 + 0.2;                // 0.30000000000000004
                bool eq = d == 0.3;                  // false: compare with a tolerance
                decimal money = 0.1m + 0.2m;         // exactly 0.3 (base-10)
                Half h; BFloat16? ...                // 16-bit formats for graphics and ML
                """)
            .Step("Three fields",
                "A 32-bit float has 1 sign bit, an 8-bit exponent and a 23-bit mantissa (fraction). Value = (-1)^sign x 1.mantissa x 2^(exponent - 127).",
                "The leading '1.' is implicit, so it is not stored, which gives 24 bits of precision for free. The exponent is stored with a bias of 127 so that it can be negative without a sign bit.",
                [1])
            .Step("Encode 6.25",
                "6.25 in binary is 110.01 = 1.1001 x 2^2. Sign 0, exponent 2 + 127 = 129 = 10000001, mantissa 1001 followed by zeros. Together: 0x40C80000.",
                "Numbers that are sums of powers of two (6.25 = 4 + 2 + 0.25) are stored exactly.",
                [1],
                s => s.Write("eq", "6.25 = +1.1001b x 2^(129-127)  ->  0x40C80000"))
            .Step("0.1 cannot be stored exactly",
                "In binary, 0.1 is 0.000110011001100... repeating forever, like 1/3 in decimal. A float keeps the nearest value: 0.100000001490116...",
                "In double, 0.1 + 0.2 = 0.30000000000000004, so == comparisons fail. Compare with a tolerance, and use decimal (base-10, 128-bit) for money.",
                [2, 3, 4],
                s =>
                {
                    s.Fields("f", [("sign", "0", 3), ("exponent", "01111011", 8), ("mantissa", "10011001100110011001101", 23)], 60, 60, 880, 70, Warn);
                    s.Write("eq", "0.1f is stored as 0x3DCCCCCD = 0.100000001490116...").Paint("eq", Warn);
                })
            .Step("Special values",
                "All exponent bits set: Infinity (mantissa 0) or NaN (mantissa non-zero). All exponent bits clear: zero and tiny 'subnormal' numbers. There is a +0 and a -0.",
                "NaN != NaN, so use double.IsNaN. Subnormals can be 10-100x slower on some CPUs. GPUs and ML code often flush them to zero.",
                [],
                s =>
                {
                    s.Fields("f", [("sign", "0", 3), ("exponent", "11111111", 8), ("mantissa", "00000000000000000000000", 23)], 60, 60, 880, 70, Bad);
                    s.Write("eq", "exponent all 1s, mantissa 0 = +Infinity (non-zero mantissa = NaN)").Paint("eq", Bad);
                })
            .Step("A family of formats",
                "Wider formats trade memory for range and precision. Narrow ones (FP16, BF16, FP8, FP4) power ML hardware, because halving the bits doubles the throughput.",
                "BF16 keeps float's 8-bit exponent (same range) with only 7 mantissa bits. FP8 (E4M3/E5M2) arrived with NVIDIA Hopper, and FP4 with Blackwell. .NET has Half, float, double and decimal built in.",
                [5],
                s =>
                {
                    s.Paint("eq", Info);
                    s.Fields("f", [("sign (1)", "0", 3), ("exponent (8)", "10000001", 8), ("mantissa (23)", "10010000000000000000000", 23)], 60, 60, 880, 70, Info);
                    s.Table("fmts", "format: sign / exponent / mantissa bits", 60, 220, 880,
                        "FP64 double     1 / 11 / 52   ~15-16 decimal digits",
                        "FP32 float      1 /  8 / 23   ~7 digits",
                        "FP16 Half       1 /  5 / 10   ~3 digits, graphics",
                        "BF16            1 /  8 /  7   float's range, ML training",
                        "FP8 E4M3/E5M2   1 / 4 / 3  or  1 / 5 / 2   AI inference/training",
                        "decimal        128-bit base-10: 96-bit integer + scale, money");
                    s.Write("eq", "fewer bits = more numbers per second, less precision");
                })
            .Lesson("Floating point (IEEE 754)", "Numbers and memory", "Sign, exponent, mantissa; why 0.1 + 0.2 != 0.3; NaN and Infinity; FP64 to FP4.");

    // ------------------------------------------------------------------------------------------
    private static Lesson TextEncoding() =>
        new StoryBuilder("DR6", "Text is numbers too: characters map to Unicode code points, and an encoding (UTF-8, UTF-16) turns code points into bytes. Never assume one character = one byte, or even one char.")
            .Setup(s =>
            {
                s.Box("H", "H", 140, 40, 120, 70, "U+0048");
                s.Box("e", "é", 300, 40, 120, 70, "U+00E9");
                s.Box("smile", "😀", 460, 40, 120, 70, "U+1F600");
                s.Label("info", "", 60, 470, 900, 15, Info);
            })
            .Code("""
                string s = "Hé😀";
                int chars = s.Length;                              // 4 (UTF-16 code units!)
                int bytes = Encoding.UTF8.GetByteCount(s);         // 7
                int runes = s.EnumerateRunes().Count();            // 3 code points
                """)
            .Step("Code points",
                "Unicode gives every character a number (a code point), written U+XXXX in hex: H is U+0048, é is U+00E9, 😀 is U+1F600.",
                "There are 1,114,112 possible code points (U+0000 to U+10FFFF). ASCII is the first 128 of them: 'A' = 0x41, '0' = 0x30, newline = 0x0A.",
                [1])
            .Step("UTF-8: 1 to 4 bytes per code point",
                "ASCII characters take 1 byte, é takes 2 (C3 A9), and the emoji takes 4 (F0 9F 98 80). 'Hé😀' is 7 bytes.",
                "The leading bits of each byte say how long the sequence is (0xxxxxxx, 110xxxxx, 1110xxxx, 11110xxx), and continuation bytes start with 10. UTF-8 is ASCII-compatible and is used by about 98% of websites.",
                [3],
                s =>
                {
                    s.Cells("u8", ["48", "C3", "A9", "F0", "9F", "98", "80"], 140, 180, 70, 50, 6, tags: false, tone: Info);
                    s.Caption("u8cap", "UTF-8 bytes", 60, 160);
                    s.PaintAll(Good, "u81", "u82");
                    s.PaintAll(Warn, "u83", "u84", "u85", "u86");
                })
            .Step("UTF-16: what a C# char is",
                "C# strings, Java and Windows APIs use UTF-16: 2-byte code units. H and é take one unit each, but the emoji needs a surrogate pair (D83D DE00).",
                "So \"Hé😀\".Length is 4, not 3. Substring can cut a surrogate pair in half and produce invalid text. Use Rune, EnumerateRunes, or StringInfo for grapheme clusters (👍🏽 is two code points but one visible character).",
                [2, 4],
                s =>
                {
                    s.Cells("u16", ["0048", "00E9", "D83D", "DE00"], 140, 300, 110, 50, 6, tags: false, tone: Info);
                    s.Caption("u16cap", "UTF-16 code units (C# chars)", 60, 280);
                    s.PaintAll(Warn, "u162", "u163");
                    s.Write("info", "\"Hé😀\".Length == 4   UTF-8 bytes == 7   code points == 3");
                })
            .Step("Strings in memory",
                "A .NET string is a heap object: header, method-table pointer, a 4-byte length, then UTF-16 chars and a terminating 0. A C string is just bytes ending in 0x00.",
                "Strings in .NET are immutable, so concatenating in a loop allocates a new copy each time (use StringBuilder). C's null-terminated strings make strlen O(n) and are the root of countless buffer overflows.",
                [],
                s =>
                {
                    s.Fields("str", [("8 B", "header", 2), ("8 B", "method table", 2), ("4 B", "length 3", 1.4), ("chars (UTF-16)", "48 00 E9 00 3D D8 00 DE", 5), ("\\0", "00 00", 1)], 60, 380, 880, 60, Neutral);
                    s.Write("info", ".NET string object layout (64-bit)");
                })
            .Lesson("Characters and strings: ASCII, Unicode, UTF-8, UTF-16", "Text", "Code points, variable-length encodings, surrogate pairs and how strings sit in memory.");

    // ------------------------------------------------------------------------------------------
    private static Lesson BoolsEnumsFlags() =>
        new StoryBuilder("DR7", "Booleans and enums are small integers in disguise. Flags pack many yes/no values into the bits of one integer, which you test and combine with bitwise operators.")
            .Setup(s =>
            {
                string[] names = ["-", "-", "-", "-", "-", "Execute", "Write", "Read"];
                for (var i = 0; i < 8; i++)
                {
                    s.Box("bit" + i, "0", 120 + i * 96, 140, 88, 60, names[i]);
                    s.Shape("bit" + i).Mono = true;
                    s.TagText("bit" + i, (1 << (7 - i)).ToString());
                }

                s.Label("info", "", 120, 260, 800, 16, Info);
            })
            .Code("""
                [Flags] enum Access : byte { None = 0, Read = 1, Write = 2, Execute = 4 }

                var a = Access.Read | Access.Execute;      // 0b0000_0101 = 5
                bool canWrite = (a & Access.Write) != 0;   // false
                a |= Access.Write;                         // set a bit
                a &= ~Access.Execute;                      // clear a bit
                """)
            .Step("bool: one byte for one bit",
                "A .NET bool occupies 1 byte (0 or 1), even though it needs only 1 bit. When marshalled to Win32 it becomes a 4-byte BOOL.",
                "Bytes are the smallest addressable unit, so a lone bool cannot be smaller. Packing many bools into bits (BitArray, bitsets) uses 8x less memory.",
                [1])
            .Step("Combine flags with OR",
                "Read = 1 (bit 0), Write = 2 (bit 1), Execute = 4 (bit 2). Read | Execute sets bits 0 and 2: 0b101 = 5.",
                "An enum is just a named integer (int by default; ': byte' makes it 1 byte). [Flags] only changes how ToString prints combined values.",
                [3],
                s =>
                {
                    s.Write("bit7", "1").Paint("bit7", Good).Write("bit5", "1").Paint("bit5", Good);
                    s.Write("info", "Read | Execute = 0000 0101 = 5");
                })
            .Step("Test with AND",
                "a & Write keeps only bit 1, which is 0, so writing is not allowed.",
                "Bit tests are single instructions (and, test, bt). Unix file permissions are the same idea: rwxr-x--- is 111 101 000 = 0750 in octal.",
                [4],
                s =>
                {
                    s.Paint("bit6", Bad);
                    s.Write("info", "0000 0101 & 0000 0010 = 0  ->  no Write permission");
                })
            .Step("Set and clear bits",
                "|= Write sets bit 1. &= ~Execute clears bit 2. The value is now Read | Write = 3.",
                "Hardware registers, network header flags (TCP SYN/ACK/FIN), GPU pipeline state and permission masks are all bit fields underneath.",
                [5, 6],
                s =>
                {
                    s.Write("bit6", "1").Paint("bit6", Good);
                    s.Write("bit5", "0").Paint("bit5", Neutral);
                    s.Write("info", "after |= Write and &= ~Execute: 0000 0011 = Read | Write");
                })
            .Lesson("Booleans, enums, flags and bit fields", "Numbers and memory", "How yes/no values and options are packed into bits and tested with &, | and ~.");

    // ------------------------------------------------------------------------------------------
    private static Lesson StructLayout()
    {
        void Grid(SceneState s, int count)
        {
            for (var i = 0; i < 16; i++)
            {
                var id = "g" + i;
                if (!s.Has(id))
                {
                    s.Box(id, null, 100 + i * 52, 120, 48, 50);
                    s.TagText(id, $"+{i:X}");
                }

                s.Ghost(id, i >= count);
            }
        }

        void Field(SceneState s, string id, string name, int offset, int size, int color, bool padding = false)
        {
            s.Box(id, name, 100 + offset * 52, 190, size * 52 - 4, 50, padding ? "padding" : $"{size} B");
            if (padding)
            {
                s.Ghost(id).Paint(id, Warn).Tint(id, -1);
            }
            else
            {
                s.Ghost(id, false).Tint(id, color);
            }
        }

        return new StoryBuilder("DR8", "Fields are aligned to their size, so the compiler inserts padding. Ordering fields from largest to smallest removes most of it, and smaller structs mean more data per cache line.")
            .Setup(s =>
            {
                Grid(s, 12);
                s.Label("size", "", 100, 280, 800, 17, Info);
            })
            .Code("""
                struct Bad  { byte A; int B; byte C; }     // 12 bytes
                struct Good { int B; byte A; byte C; }     // 8 bytes

                [StructLayout(LayoutKind.Sequential, Pack = 1)]
                struct Packed { byte A; int B; byte C; }   // 6 bytes, misaligned B
                """)
            .Step("Natural alignment",
                "An N-byte field is placed at an address that is a multiple of N: ints at offsets 0, 4, 8..., longs at multiples of 8.",
                "Aligned loads never straddle two cache lines and are atomic for their size. Misaligned accesses can be slower, can fault on some ARM instructions, and break the atomicity guarantees used by Interlocked.",
                [1])
            .Step("struct Bad: 12 bytes",
                "A (1 byte) is at offset 0. B (int) must start at 4, so 3 padding bytes follow A. C goes at 8, then 3 more padding bytes round the size up to a multiple of 4.",
                "One third of this struct is padding. An array of a million Bad structs wastes 6 MB.",
                [1],
                s =>
                {
                    Field(s, "fa", "A", 0, 1, 0);
                    Field(s, "p1", "", 1, 3, 0, padding: true);
                    Field(s, "fb", "B (int)", 4, 4, 1);
                    Field(s, "fc", "C", 8, 1, 2);
                    Field(s, "p2", "", 9, 3, 0, padding: true);
                    s.Write("size", "sizeof(Bad) = 12 (6 useful bytes + 6 padding)");
                })
            .Step("struct Good: 8 bytes",
                "Put the int first: B at 0, A at 4, C at 5, then 2 padding bytes. Same data in 8 bytes.",
                "Rule of thumb: order fields from largest to smallest. The CLR does this automatically for classes (auto layout), but structs default to sequential layout because they are often passed to native code.",
                [2],
                s =>
                {
                    Grid(s, 8);
                    s.Remove("p1", "p2");
                    Field(s, "fb", "B (int)", 0, 4, 1);
                    Field(s, "fa", "A", 4, 1, 0);
                    Field(s, "fc", "C", 5, 1, 2);
                    Field(s, "p3", "", 6, 2, 0, padding: true);
                    s.Write("size", "sizeof(Good) = 8 (6 useful bytes + 2 padding)");
                })
            .Step("Pack = 1: no padding, misaligned fields",
                "Pack = 1 removes all padding (6 bytes), as file formats and network headers require. But B now starts at offset 1, which is misaligned.",
                "Use packed layouts only at boundaries (parsing and serialising), then copy into well-aligned structs for computation. LayoutKind.Explicit with FieldOffset gives full control, including overlapping fields (C-style unions).",
                [4, 5],
                s =>
                {
                    Grid(s, 6);
                    s.Remove("p3");
                    Field(s, "fa", "A", 0, 1, 0);
                    Field(s, "fb", "B (misaligned)", 1, 4, 1);
                    Field(s, "fc", "C", 5, 1, 2);
                    s.Paint("fb", Warn).Tint("fb", -1);
                    s.Write("size", "sizeof(Packed) = 6, but B straddles an alignment boundary");
                })
            .Lesson("Structs, alignment and padding", "Layout", "Why field order changes struct size, and when to pack.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson ReferencesAndObjects() =>
        new StoryBuilder("DR9", "A reference is just a 64-bit address. Every .NET object starts with a header and a method-table pointer before its fields, which is why small objects cost at least 24 bytes and why value types can be far denser.")
            .Setup(s =>
            {
                s.Region("stack", "stack", 30, 30, 300, 480, Info);
                s.Region("heap", "heap", 360, 30, 610, 480, Warn);
                s.Box("ref", "p = 0x0000020A5C3F1040", 50, 80, 260, 56, "8-byte reference");
                s.Shape("ref").Mono = true;
            })
            .Code("""
                class PointC { public int X, Y; }       // reference type
                struct PointS { public int X, Y; }      // value type

                var p = new PointC();                   // 24 bytes on the heap + 8-byte reference
                var arr = new PointS[1000];             // 8,000 bytes of contiguous X,Y pairs
                object boxed = 42;                      // boxing: a new 24-byte heap object
                """)
            .Step("A reference is an address",
                "The variable p holds the address of an object on the heap, shown here in hex. Dereferencing p means 'go to that address'.",
                "References in managed code cannot be offset like C pointers, and the GC may move the object (updating every reference to it). null is address 0.",
                [4])
            .Step("Object layout",
                "At the address: an 8-byte object header (lock and hash info), an 8-byte method-table pointer (the object's type), then the fields X and Y.",
                "The reference actually points at the method-table pointer; the header sits just before it. 8 + 8 + 8 (two ints) = 24 bytes, which is the minimum object size on 64-bit .NET.",
                [1, 4],
                s =>
                {
                    s.Fields("obj", [("8 B", "header", 2), ("8 B", "method table ptr", 2), ("int", "X", 1), ("int", "Y", 1)], 400, 80, 540, 56, Info);
                    s.Connect("ref", "obj1", "points here");
                })
            .Step("Arrays of classes vs structs",
                "An array of PointC stores references, so each element points at a separate heap object. An array of PointS stores the X,Y pairs inline and contiguously.",
                "Struct arrays are 3x smaller here and cache-friendly. Class arrays mean a pointer chase per element (see the arrays-vs-lists lesson).",
                [5],
                s =>
                {
                    s.Cells("ra", ["ref", "ref", "ref", "ref"], 400, 200, 120, 44, 6, tags: false);
                    s.Caption("racap", "PointC[]: references", 400, 180);
                    for (var i = 0; i < 4; i++)
                    {
                        s.Box("o" + i, "obj 24 B", 400 + i * 140, 280 + i % 2 * 40, 110, 36, tone: Warn);
                        s.Connect("ra" + i, "o" + i);
                    }

                    s.Cells("sa", ["X,Y", "X,Y", "X,Y", "X,Y", "X,Y", "X,Y"], 400, 420, 80, 44, 6, tags: false, tone: Good);
                    s.Caption("sacap", "PointS[]: values inline", 400, 400);
                })
            .Step("Boxing",
                "Assigning an int to an object variable 'boxes' it: a new 24-byte heap object holding the 4-byte int.",
                "Boxing in hot loops (non-generic collections, string.Format with value types, interface calls on structs) creates GC pressure. Generics, Span<T> and overloads avoid it.",
                [6],
                s =>
                {
                    s.ClearLinks();
                    s.RemoveWhere(sh => sh.Id.StartsWith("ra", StringComparison.Ordinal) || (sh.Id.StartsWith('o') && sh.Id.Length == 2));
                    s.Remove("racap");
                    s.Box("ival", "int 42 (4 B)", 50, 220, 260, 50, "stack value");
                    s.Fields("box", [("8 B", "header", 2), ("8 B", "MT: Int32", 3), ("4 B + pad", "42", 2)], 400, 200, 540, 56, Warn);
                    s.Connect("ival", "box1", "box: allocate + copy", Warn);
                    s.Say("Value types live inline; boxing moves them to the heap", Info);
                })
            .Lesson("References, pointers and .NET object layout", "Layout", "What a reference holds, how objects are laid out, value vs reference types, and boxing.");
}
