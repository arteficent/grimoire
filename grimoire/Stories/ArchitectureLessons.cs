using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class ArchitectureLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return WhatIsAnIsa();
        yield return DecodeRiscV();
        yield return Pipeline();
        yield return X86OutOfOrder();
        yield return ArmLoadStore();
        yield return Simd();
        yield return BranchPrediction();
        yield return CpuDesigns();
        yield return GpuIsas();
        yield return GpuExecution();
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson WhatIsAnIsa() =>
        new StoryBuilder("AR1", "An ISA is the contract between software and silicon: which instructions exist, what registers there are, how memory behaves. Many different chips implement the same ISA, and the same source compiles to different ISAs.")
            .Setup(s =>
            {
                string[] layers = ["your code: C#, C++, Rust, Go", "compiler / JIT", "ISA: instructions, registers, memory model", "microarchitecture: pipelines, caches, predictors", "transistors"];
                for (var i = 0; i < layers.Length; i++)
                {
                    s.Box("layer" + i, layers[i], 300, 30 + i * 74, 400, 60, tone: i == 2 ? Info : Neutral);
                    if (i > 0)
                    {
                        s.Connect("layer" + (i - 1), "layer" + i);
                    }
                }
            })
            .NoCode()
            .Step("The ISA is a contract",
                "Software is compiled to an instruction set architecture (ISA). Hardware promises to execute that ISA, however it is built inside.",
                "Intel and AMD build completely different cores that run the same x86-64 programs. Apple, Qualcomm and AWS build different ARM cores. The ISA is the stable interface; the microarchitecture changes every generation.")
            .Step("Same line of code, three ISAs",
                "a = b + c compiles to one instruction on each, but with different encodings and register names.",
                "x86-64 'add eax, ecx' is 2 bytes (01 C8). AArch64 'add w0, w1, w2' is the 32-bit word 0x0B020020. RISC-V 'add a0, a1, a2' is 0x00C58533. All three are stored little-endian in memory.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("layer", StringComparison.Ordinal));
                    s.ClearLinks();
                    s.Box("src", "a = b + c", 400, 40, 200, 56, "source", Info);
                    (string Id, string Isa, string Asm, string Hex, int Color)[] isas =
                    [
                        ("x86", "x86-64", "add eax, ecx", "01 C8  (2 bytes)", 0),
                        ("arm", "AArch64", "add w0, w1, w2", "0x0B020020  (4 bytes)", 1),
                        ("rv", "RISC-V", "add a0, a1, a2", "0x00C58533  (4 bytes)", 2)
                    ];
                    for (var i = 0; i < isas.Length; i++)
                    {
                        s.Box(isas[i].Id, isas[i].Asm, 60 + i * 310, 200, 260, 70, isas[i].Isa);
                        s.Tint(isas[i].Id, isas[i].Color);
                        s.Shape(isas[i].Id).Mono = true;
                        s.Box(isas[i].Id + "h", isas[i].Hex, 60 + i * 310, 320, 260, 50, "machine code");
                        s.Shape(isas[i].Id + "h").Mono = true;
                        s.Connect("src", isas[i].Id).Connect(isas[i].Id, isas[i].Id + "h");
                    }
                })
            .Step("CISC vs RISC",
                "x86 (CISC) instructions are 1 to 15 bytes long and can read memory directly. ARM and RISC-V (RISC) use fixed 4-byte instructions, and only loads and stores touch memory.",
                "Fixed-length encodings let a core decode 8 or more instructions per cycle cheaply. x86 cores spend extra logic finding instruction boundaries, then translate each instruction into simple RISC-like micro-operations internally, so the two styles converge inside the chip.",
                s =>
                {
                    s.Write("x86", "add rax, [rbx+8]", "x86-64: one instruction reads memory and adds");
                    s.Write("x86h", "48 03 43 08  (4 bytes; up to 15)");
                    s.Write("arm", "ldr x3, [x1, #8]\nadd x0, x0, x3", "AArch64: load, then add");
                    s.Write("armh", "2 x 4 bytes");
                    s.Write("rv", "ld t0, 8(a1)\nadd a0, a0, t0", "RISC-V: load, then add");
                    s.Write("rvh", "2 x 4 bytes (or 2 x 2 compressed)");
                    s.Resize("x86", 260, 90).Resize("arm", 260, 90).Resize("rv", 260, 90);
                })
            .Step("Where each ISA runs",
                "x86-64 dominates PCs and most servers. ARM runs nearly every phone, Apple Macs and a growing share of cloud servers. RISC-V is an open ISA spreading through embedded chips, accelerators and now application processors.",
                "Licensing differs: x86 is controlled by Intel and AMD, ARM licenses its ISA and core designs, and RISC-V is royalty-free and governed by RISC-V International. POWER (IBM) and older MIPS and SPARC still exist in niches.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id is "src" or "x86h" or "armh" or "rvh");
                    s.ClearLinks();
                    s.Table("fam", "ISA families", 60, 330, 880,
                        "x86-64      CISC, 1-15 byte instructions   Intel, AMD             PCs, servers",
                        "AArch64     RISC, fixed 4 bytes            Apple, Qualcomm, AWS   phones, Macs, cloud",
                        "RISC-V      RISC, open, modular             SiFive, many vendors   embedded, accelerators",
                        "POWER       RISC                            IBM                    mainframes, HPC");
                    s.Write("x86", "x86-64", "CISC").Write("arm", "AArch64 (ARMv8/v9)", "RISC").Write("rv", "RISC-V", "RISC, open standard");
                    s.Resize("x86", 260, 70).Resize("arm", 260, 70).Resize("rv", 260, 70);
                })
            .Step("Virtual ISAs: portable bytecode",
                ".NET IL, JVM bytecode, WebAssembly, NVIDIA PTX and SPIR-V are 'virtual' instruction sets. A JIT or AOT compiler turns them into the real ISA of the machine they run on.",
                "This is how one .NET DLL runs on x64 and ARM64, and how a CUDA binary can run on a GPU released after it was compiled. The price is a compilation step at load time (JIT) or ahead of time (AOT, ReadyToRun).",
                s =>
                {
                    s.Remove("fam");
                    s.Box("il", ".NET IL / JVM / Wasm", 360, 330, 280, 56, "virtual ISA", Info);
                    s.Connect("il", "x86", "JIT").Connect("il", "arm", "JIT").Connect("il", "rv", "JIT");
                    s.Say("Write once, compile again for each real ISA", Good);
                })
            .Lesson("What an instruction set architecture is", "Instruction sets", "The software-hardware contract, CISC vs RISC, and the same code in x86, ARM and RISC-V.");

    // ------------------------------------------------------------------------------------------
    private static Lesson DecodeRiscV()
    {
        (string, string, double)[] fields =
        [
            ("funct7", "0000000", 7), ("rs2 = x12", "01100", 5), ("rs1 = x11", "01011", 5),
            ("funct3", "000", 3), ("rd = x10", "01010", 5), ("opcode", "0110011", 7)
        ];

        return new StoryBuilder("AR2", "RISC-V keeps register fields in the same bit positions in every format, so decoding is a few wires and a small lookup. The opcode picks the format, and funct3/funct7 pick the exact operation.")
            .Setup(s =>
            {
                s.Box("word", "0x00C58533", 360, 30, 280, 56, "32-bit instruction word", Info);
                s.Shape("word").Mono = true;
                s.Table("regs", "register file", 60, 380, 300, "x10 (a0) = ?", "x11 (a1) = 5", "x12 (a2) = 7");
                s.Box("alu", "ALU", 470, 400, 140, 80);
                s.Box("mem", "memory @ PC", 720, 380, 220, 100, "bytes 33 85 C5 00");
                s.Shape("mem").Mono = true;
            })
            .Code("""
                add a0, a1, a2        # x10 = x11 + x12

                # R-type layout (bit 31 on the left):
                # funct7 | rs2 | rs1 | funct3 | rd | opcode
                #   7       5     5      3      5      7
                """)
            .Step("Fetch the instruction",
                "The program counter (PC) points at 4 bytes in memory: 33 85 C5 00. Because RISC-V is little-endian, they form the word 0x00C58533.",
                "Every base instruction is exactly 32 bits, so the next instruction is always at PC + 4. With the C extension, common instructions also come in 16-bit forms, which you can tell apart by the low two bits.",
                [1],
                s =>
                {
                    s.Paint("mem", Info);
                    s.Move("shape:mem", "shape:word", "fetch", Info);
                })
            .Step("Split the bits into fields",
                "Written in binary, the word divides into six fields.",
                "In every format, rs1, rs2 and rd sit in the same bit positions, so the register file can be read before the decoder has even finished working out the operation. That is a big reason simple RISC decoders are fast and small.",
                [3, 4, 5],
                s =>
                {
                    s.Paint("mem", Neutral);
                    s.Fields("f", fields, 60, 140, 880, 70);
                    for (var i = 0; i < 6; i++)
                    {
                        s.Connect("word", "f" + i, arrow: false, dashed: true);
                    }
                })
            .Step("opcode + funct3 + funct7 = ADD",
                "opcode 0110011 means 'register-register ALU operation'. funct3 = 000 with funct7 = 0000000 selects ADD. With funct7 = 0100000 the same bits would mean SUB.",
                "The decoder is essentially a small lookup table, with no microcode needed. The whole base integer ISA (RV64I) has only about 50 instructions.",
                [1],
                s =>
                {
                    s.ClearLinks();
                    s.PaintAll(Warn, "f0", "f3", "f5");
                    s.Box("op", "ADD", 400, 260, 200, 50, "decoded operation", Good);
                    s.Connect("f5", "op").Connect("f3", "op").Connect("f0", "op");
                })
            .Step("Read registers, add, write back",
                "rs1 = x11 holds 5 and rs2 = x12 holds 7. The ALU adds them and writes 12 into rd = x10.",
                "In a pipelined core this is spread over stages: fetch, decode, execute, memory, write-back (see the next lesson). Register x0 is hard-wired to zero, which gives free 'mov' and 'nop' encodings.",
                [1],
                s =>
                {
                    s.ClearLinks();
                    s.PaintAll(Neutral, "f0", "f3", "f5");
                    s.PaintAll(Info, "f1", "f2");
                    s.Paint("f4", Good);
                    s.Move("shape:regs", "shape:alu", "5, 7", Info);
                    s.Move("shape:alu", "shape:regs", "12", Good);
                    s.Table("regs", "register file", 60, 380, 300, "x10 (a0) = 12", "x11 (a1) = 5", "x12 (a2) = 7").HotRow("regs", 0);
                    s.Paint("alu", Good);
                })
            .Step("A modular ISA",
                "RISC-V is a small base (RV32I/RV64I) plus optional extensions: M multiply, A atomics, F/D floating point, C compressed, V vectors, and more.",
                "'RV64GC' (G = IMAFD + Zicsr + Zifencei, plus C) is the common Linux target, and the RVA23 profile (ratified 2024) adds vectors and other features for application processors. Chips include only what they need, from tiny microcontrollers to server cores.",
                [],
                s =>
                {
                    s.Paint("alu", Neutral);
                    s.Remove("op");
                    s.Say("Base + extensions: RV64I + M A F D C V ...", Info);
                })
            .Lesson("Decoding a RISC-V instruction, bit by bit", "Instruction sets", "From four bytes in memory to 'add a0, a1, a2': fields, opcodes and register reads.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Pipeline()
    {
        string[] stages = ["IF", "ID", "EX", "MEM", "WB"];
        const double x0 = 250, cw = 70, top = 70, rh = 64;

        void Cell(SceneState s, int row, int cycle, string text, Tone tone = Neutral, int color = -1)
        {
            var id = $"p{row}_{cycle}";
            s.Box(id, text, x0 + cycle * cw, top + row * rh, cw - 4, rh - 10, tone: tone);
            s.Tint(id, color);
        }

        void Instr(SceneState s, int row, int start, params int[] stallsBefore)
        {
            var cycle = start;
            for (var k = 0; k < 5; k++)
            {
                if (stallsBefore.Contains(k))
                {
                    Cell(s, row, cycle++, "stall", Bad);
                }

                Cell(s, row, cycle++, stages[k], color: k);
            }
        }

        void Rows(SceneState s, params string[] names)
        {
            s.RemoveWhere(sh => sh.Id.StartsWith('p') || sh.Id.StartsWith("row", StringComparison.Ordinal));
            s.ClearLinks();
            for (var i = 0; i < names.Length; i++)
            {
                s.Label("row" + i, names[i], 30, top + i * rh + 16, 210, 13);
                s.Shape("row" + i).Mono = true;
            }
        }

        return new StoryBuilder("AR3", "Pipelining overlaps instructions like an assembly line: ideally one finishes every cycle. Hazards (data dependencies, loads, branches) force forwarding, stalls or flushes.")
            .Setup(s =>
            {
                for (var c = 0; c < 10; c++)
                {
                    s.Label("cyc" + c, $"c{c + 1}", x0 + c * cw + 22, 40, 40, 11);
                }
            })
            .NoCode()
            .Step("Without a pipeline",
                "Each instruction goes through fetch (IF), decode (ID), execute (EX), memory (MEM) and write-back (WB) before the next starts: 2 instructions take 10 cycles.",
                "Each stage's hardware sits idle 80% of the time. The clock must also be slow enough for all five stages' work to fit in one long cycle.",
                s =>
                {
                    Rows(s, "add x1, x2, x3", "sub x4, x5, x6");
                    Instr(s, 0, 0);
                    Instr(s, 1, 5);
                })
            .Step("Pipelined: one instruction per cycle",
                "Start a new instruction every cycle. In cycle 5 all five stages are busy with five different instructions.",
                "Throughput approaches 1 instruction per cycle, even though each instruction still takes 5 cycles of latency. Modern cores have 10-20 stages and are superscalar, issuing 4-8 instructions per cycle.",
                s =>
                {
                    Rows(s, "add x1, x2, x3", "sub x4, x5, x6", "and x7, x8, x9", "or  x10, x11, x12", "xor x13, x14, x15");
                    for (var i = 0; i < 5; i++)
                    {
                        Instr(s, i, i);
                    }
                })
            .Step("Data hazard: forwarding",
                "sub needs x1, which add only writes back in cycle 5. Instead of waiting, the ALU result is forwarded straight from add's EX stage into sub's EX stage.",
                "Bypass networks are a major part of a core's wiring. Without forwarding, sub would stall 2-3 cycles every time it used a result computed just before.",
                s =>
                {
                    Rows(s, "add x1, x2, x3", "sub x4, x1, x5");
                    Instr(s, 0, 0);
                    Instr(s, 1, 1);
                    s.Paint("p0_2", Good).Paint("p1_3", Good);
                    s.Connect("p0_2", "p1_3", "forward x1", Good);
                })
            .Step("Load-use hazard: one stall",
                "A load only has its data after the MEM stage. The next instruction needs it in EX, so even with forwarding one bubble (stall) is unavoidable.",
                "Compilers reorder independent instructions into that slot, and out-of-order cores find other work automatically. An L1 cache hit takes about 4-5 cycles on modern x86, so real load-use delays are longer.",
                s =>
                {
                    Rows(s, "ld  x1, 0(x2)", "add x3, x1, x4");
                    Instr(s, 0, 0);
                    Instr(s, 1, 1, 2);
                    s.Paint("p0_3", Good).Paint("p1_4", Good);
                    s.Connect("p0_3", "p1_4", "forward loaded x1", Good);
                })
            .Step("Branch: flush the wrong path",
                "The branch is only resolved in EX. By then two instructions after it have been fetched. If the branch is taken, they are flushed and fetching restarts at the target.",
                "This is why branch predictors exist (see the branch prediction lesson). In a 15-20 stage pipeline, a misprediction throws away about 15-20 cycles of work, potentially dozens of instructions on a wide core.",
                s =>
                {
                    Rows(s, "beq x1, x2, L", "add (wrong path)", "sub (wrong path)", "L: or x5, x6, x7");
                    Instr(s, 0, 0);
                    for (var r = 1; r <= 2; r++)
                    {
                        Cell(s, r, r, "IF", Bad);
                        Cell(s, r, r + 1, r == 1 ? "ID" : "flush", Bad);
                        s.Ghost($"p{r}_{r}").Ghost($"p{r}_{r + 1}");
                    }

                    Instr(s, 3, 3);
                    s.Paint("p0_2", Warn);
                    s.Say("Taken branch resolved in EX: 2 wasted fetch slots", Warn);
                })
            .Lesson("The instruction pipeline and its hazards", "Execution", "IF-ID-EX-MEM-WB animated: overlap, forwarding, load stalls and branch flushes.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson X86OutOfOrder() =>
        new StoryBuilder("AR4", "Modern x86 cores decode variable-length CISC instructions into simple micro-operations, rename registers, execute them out of order across many ports, and retire them in program order.")
            .Setup(s =>
            {
                s.Fields("b", [("REX.W", "48", 1), ("opcode ADD r64, r/m64", "03", 2), ("ModRM: rax, [rbx+disp8]", "43", 2.4), ("disp8", "08", 1)], 230, 30, 540, 60, Info);
                string[] front = ["fetch 32 B", "length decode", "6 decoders", "µop cache", "rename + ROB"];
                for (var i = 0; i < front.Length; i++)
                {
                    s.Box("fe" + i, front[i], 40 + i * 190, 160, 160, 54);
                    if (i > 0)
                    {
                        s.Connect("fe" + (i - 1), "fe" + i);
                    }
                }

                s.Box("sched", "scheduler (reservation stations)", 40, 270, 920, 46);
                string[] ports = ["ALU 0", "ALU 1", "ALU 2", "ALU 3", "LOAD 0", "LOAD 1", "STORE", "VEC/FP"];
                for (var i = 0; i < ports.Length; i++)
                {
                    s.Box("port" + i, ports[i], 40 + i * 116, 360, 104, 50);
                }

                s.Box("retire", "retire in program order (ROB head)", 40, 460, 920, 46);
            })
            .Code("""
                add rax, [rbx+8]     ; 48 03 43 08
                ; -> uop 1: tmp = load [rbx+8]
                ; -> uop 2: rax = rax + tmp
                imul rcx, rdx        ; independent: runs while the load waits
                """)
            .Step("Variable-length bytes",
                "add rax, [rbx+8] is 4 bytes: a REX prefix (64-bit operand), the opcode, a ModRM byte naming rax and [rbx + disp8], and the displacement 8.",
                "x86 instructions are 1-15 bytes with optional prefixes, so the front end must first find where each one starts. That length decoding is one of the hardest parts of an x86 front end.",
                [1])
            .Step("Decode into micro-ops",
                "The decoder splits the CISC instruction into two simple micro-ops (µops): a load and an add.",
                "Decoded µops are kept in a µop cache, so hot loops skip the decoders entirely, which saves power and bandwidth. Intel's Golden Cove core can decode 6 instructions per cycle.",
                [2, 3],
                s =>
                {
                    s.PaintAll(Info, "fe0", "fe1", "fe2");
                    s.Box("u1", "µop: tmp = load [rbx+8]", 300, 225, 200, 36, tone: Info);
                    s.Box("u2", "µop: rax = rax + tmp", 520, 225, 200, 36, tone: Info);
                })
            .Step("Rename and enter the reorder buffer",
                "Architectural registers (rax) are mapped onto a large pool of physical registers, and each µop gets a slot in the reorder buffer (ROB).",
                "Renaming removes false dependencies when code reuses the same register name for unrelated values. ROBs hold hundreds of µops in flight: 512 on Golden Cove, 320 on AMD Zen 4.",
                [2, 3],
                s =>
                {
                    s.PaintAll(Neutral, "fe0", "fe1", "fe2");
                    s.Paint("fe4", Info);
                    s.Write("u1", "load -> P71").Write("u2", "P72 = P12 + P71");
                    s.Place("u1", 300, 225).Place("u2", 520, 225);
                })
            .Step("Execute out of order",
                "The load goes to a load port and waits about 5 cycles for L1. Meanwhile an independent imul is issued to an ALU port, so the core does not sit idle.",
                "The scheduler issues any µop whose inputs are ready, regardless of program order. That is how a core hides cache latency, and it is also why memory ordering needs fences on weakly ordered CPUs.",
                [4],
                s =>
                {
                    s.Paint("fe4", Neutral);
                    s.Paint("port4", Warn).Paint("port1", Good);
                    s.Place("u1", 500, 412).Write("u1", "load: waiting on L1");
                    s.Box("u3", "imul rcx, rdx", 140, 412, 140, 36, tone: Good);
                    s.Place("u2", 700, 225).Write("u2", "add: waits for load");
                })
            .Step("Retire in order",
                "When the load arrives, the add executes. Both retire from the ROB in original program order, so the architectural state looks exactly as if the instructions ran one by one.",
                "In-order retirement gives precise exceptions: if the load faulted, nothing after it would have become visible. Speculative results that never retire are discarded, although their cache side effects are what Spectre exploited.",
                [1, 4],
                s =>
                {
                    s.PaintAll(Neutral, "port4", "port1");
                    s.Remove("u3");
                    s.Place("u1", 300, 416).Write("u1", "load retired").Paint("u1", Good);
                    s.Place("u2", 520, 416).Write("u2", "add retired").Paint("u2", Good);
                    s.Paint("retire", Good);
                })
            .Step("The x86 extension zoo",
                "On top of the base ISA come SSE, AVX, AVX2, AVX-512 and AMX for vectors and matrices, plus feature levels x86-64-v2/v3/v4 that compilers can target.",
                "Intel announced APX (32 general-purpose registers) and AVX10 (a converged vector ISA) in 2023. Software checks CPUID at run time and picks the best code path. .NET does this automatically for its Vector APIs.",
                [],
                s => s.Say("Same ISA name, very different feature sets per generation", Info))
            .Lesson("x86-64: CISC bytes to out-of-order micro-ops", "CPU microarchitecture", "Decoding, register renaming, execution ports and in-order retirement, animated.");

    // ------------------------------------------------------------------------------------------
    private static Lesson ArmLoadStore()
    {
        void Program(SceneState s, int hot) =>
            s.Table("prog", "AArch64 program", 40, 40, 330, "ldr x3, [x1, #8]", "add x3, x3, x2", "str x3, [x1, #8]").HotRow("prog", hot);

        return new StoryBuilder("AR5", "ARM is a load/store architecture: arithmetic works only on registers, and memory is touched only by loads and stores. Simple fixed-width instructions make very wide, power-efficient cores possible.")
            .Setup(s =>
            {
                Program(s, -1);
                s.Table("regs", "registers", 40, 220, 330, "x1 = 0x1000 (base pointer)", "x2 = 5", "x3 = ?");
                s.Box("alu", "ALU", 470, 230, 140, 80);
                s.Region("memr", "memory", 690, 40, 280, 300);
                s.Box("m1000", "...", 720, 90, 220, 50, "0x1000");
                s.Box("m1008", "40", 720, 170, 220, 50, "0x1008");
                s.Box("m1010", "...", 720, 250, 220, 50, "0x1010");
            })
            .Code("""
                // C: p->count += delta;
                ldr x3, [x1, #8]     // load  x3 <- mem[x1 + 8]
                add x3, x3, x2       // add   (registers only)
                str x3, [x1, #8]     // store mem[x1 + 8] <- x3
                // x86 does all of this in one instruction: add [rbx+8], rax
                """)
            .Step("One C statement, three instructions",
                "p->count += delta needs a load, an add and a store on ARM. x86 can express it as a single read-modify-write instruction.",
                "More instructions do not mean slower: each one is simple, exactly 4 bytes, and easy to decode in parallel. AArch64 has 31 general-purpose 64-bit registers (x0-x30), so far fewer values spill to memory.",
                [1, 5])
            .Step("ldr: memory to register",
                "The load computes the address x1 + 8 = 0x1008 and copies 40 into x3.",
                "The address is computed in the load unit (base + immediate offset), with optional pre- or post-increment modes for walking arrays.",
                [2],
                s =>
                {
                    Program(s, 0);
                    s.Paint("m1008", Info);
                    s.Move("shape:m1008", "shape:regs", "40", Info);
                    s.Table("regs", "registers", 40, 220, 330, "x1 = 0x1000 (base pointer)", "x2 = 5", "x3 = 40").HotRow("regs", 2);
                })
            .Step("add: registers only",
                "The ALU adds x3 and x2 and writes 45 back into x3. No memory access happens.",
                "Because arithmetic never touches memory, the pipeline is simpler and the only instructions that can fault on memory are loads and stores.",
                [3],
                s =>
                {
                    Program(s, 1);
                    s.Paint("m1008", Neutral).Paint("alu", Good);
                    s.Move("shape:regs", "shape:alu", "40 + 5", Info);
                    s.Table("regs", "registers", 40, 220, 330, "x1 = 0x1000 (base pointer)", "x2 = 5", "x3 = 45").HotRow("regs", 2);
                })
            .Step("str: register to memory",
                "The store writes 45 back to 0x1008.",
                "ARM's memory model is weakly ordered: another core may observe stores in a different order unless barriers (dmb) or acquire/release instructions (ldar/stlr) are used. That is exactly what C# volatile and Interlocked compile to on ARM64.",
                [4],
                s =>
                {
                    Program(s, 2);
                    s.Paint("alu", Neutral).Write("m1008", "45").Paint("m1008", Good);
                    s.Move("shape:regs", "shape:m1008", "45", Good);
                })
            .Step("Why ARM cores can be so wide",
                "Fixed 4-byte instructions mean the decoder knows where every instruction starts. Apple's big cores decode 8 instructions per cycle at low power.",
                "ARMv8.3 added pointer authentication (PAC), v8.5 added memory tagging (MTE), and ARMv9 makes SVE2 scalable vectors standard. big.LITTLE designs mix fast and efficient cores on one chip.",
                [],
                s =>
                {
                    Program(s, -1);
                    s.Paint("m1008", Neutral);
                    s.Say("Simple, fixed-width instructions: wide decode at low power", Good);
                })
            .Lesson("ARM AArch64: a load/store architecture", "Instruction sets", "Why ARM splits read-modify-write into load, add, store, and why that enables wide, efficient cores.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Simd()
    {
        float[] a = [1, 2, 3, 4, 5, 6, 7, 8];
        float[] b = [10, 20, 30, 40, 50, 60, 70, 80];

        void Rows(SceneState s, int done, bool vector)
        {
            for (var i = 0; i < 8; i++)
            {
                var active = vector ? done > 0 : i < done;
                s.Write("r" + i, active ? (a[i] + b[i]).ToString() : "").Paint("r" + i, active ? Good : Neutral);
                s.Paint("a" + i, vector && done > 0 ? Info : !vector && i == done - 1 ? Info : Neutral);
                s.Paint("b" + i, vector && done > 0 ? Info : !vector && i == done - 1 ? Info : Neutral);
            }
        }

        return new StoryBuilder("AR6", "SIMD instructions apply one operation to a whole vector of numbers at once. Each ISA has its own vector extensions (SSE/AVX/AVX-512, NEON/SVE, RISC-V V), and libraries hide the differences.")
            .Setup(s =>
            {
                s.Caption("ca", "ymm1 (8 x float)", 60, 70);
                s.Cells("a", a.Select(v => v.ToString()).ToArray(), 200, 60, 80, 46, 6, tags: false);
                s.Caption("cb", "ymm2", 60, 150);
                s.Cells("b", b.Select(v => v.ToString()).ToArray(), 200, 140, 80, 46, 6, tags: false);
                s.Caption("cr", "ymm0 = ymm1 + ymm2", 60, 240);
                s.Cells("r", Enumerable.Repeat("", 8).ToArray(), 200, 230, 80, 46, 6, tags: false);
                s.Label("count", "", 200, 310, 700, 16, Info);
            })
            .Code("""
                // scalar: 8 separate adds
                for (int i = 0; i < 8; i++) r[i] = a[i] + b[i];

                // AVX2: one instruction for all 8 lanes  (vaddps ymm0, ymm1, ymm2)
                var va = Vector256.Create(a); var vb = Vector256.Create(b);
                (va + vb).CopyTo(r);
                """)
            .Step("Scalar: one add at a time",
                "A normal add instruction handles one number. Adding two arrays of 8 floats takes 8 adds, plus loop overhead.",
                "Each scalar add uses only 32 bits of a 256-bit vector unit, so most of the silicon sits idle.",
                [2],
                s =>
                {
                    Rows(s, 3, false);
                    s.Write("count", "scalar: 3 of 8 adds done...");
                })
            .Step("SIMD: all 8 lanes in one instruction",
                "vaddps adds all eight pairs at once. Same latency as one scalar add, 8x the work.",
                "256-bit AVX2 registers hold 8 floats or 4 doubles; AVX-512 doubles that to 16 floats. Throughput-bound code (image processing, compression, JSON parsing, ML inference) often gets 4-10x speedups.",
                [5, 6],
                s =>
                {
                    Rows(s, 1, true);
                    s.Write("count", "AVX2: 1 instruction, 8 results");
                })
            .Step("Vector widths across ISAs",
                "x86: SSE 128-bit, AVX/AVX2 256-bit, AVX-512 512-bit. ARM: NEON 128-bit, SVE/SVE2 128-2048-bit (scalable). RISC-V: the V extension with an implementation-chosen vector length.",
                "Scalable vectors (SVE, RVV) let one binary use whatever width the chip has: the loop asks the hardware how many lanes it got. Fixed-width ISAs need a separate code path per width.",
                [],
                s =>
                {
                    s.LabeledBars("w", [("SSE / NEON", 128, "128"), ("AVX2", 256, "256"), ("AVX-512", 512, "512"), ("SVE (max)", 2048, "2048")], 200, 520, 140, 40, 180, log: true, logMin: 64);
                    s.Write("count", "register width in bits (log scale)");
                })
            .Step("Practical SIMD in .NET",
                "Vector128/256/512<T> map to SSE/AVX/AVX-512 on x64 and NEON on ARM64. Many BCL methods (Span search, string comparison, Sum) already use them internally.",
                "Watch for the 'tail' (lengths that are not a multiple of the lane count), data alignment, and gather/scatter (non-contiguous loads are slow). Check Vector256.IsHardwareAccelerated before choosing a path.",
                [4, 5, 6],
                s => s.Say("Same code, the JIT picks the best vector ISA for the CPU", Good))
            .Lesson("SIMD: one instruction, many numbers", "Execution", "Vector registers and instructions across x86, ARM and RISC-V, and how .NET uses them.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BranchPrediction()
    {
        string[] states = ["strongly not taken", "weakly not taken", "weakly taken", "strongly taken"];
        bool[] outcomes = [true, true, true, false, true, true, true, false];

        var b = new StoryBuilder("AR7", "Branch predictors guess the outcome of every branch so the pipeline never waits. Regular patterns are predicted almost perfectly; random data-dependent branches cost a pipeline flush about half the time.")
            .Setup(s =>
            {
                for (var i = 0; i < 4; i++)
                {
                    s.Circle("st" + i, i.ToString("b2").PadLeft(2, '0'), 140 + i * 240, 120, 44, states[i]);
                    if (i > 0)
                    {
                        s.Connect("st" + (i - 1), "st" + i, "taken", arrow: true);
                    }
                }

                s.Caption("ocap", "loop branch outcomes (T = taken, N = not taken)", 140, 250, 500);
                s.Cells("o", outcomes.Select(o => o ? "T" : "N").ToArray(), 140, 275, 70, 46, 8, tags: false);
                s.Label("stats", "", 140, 360, 700, 16, Info);
            })
            .Code("""
                for (int i = 0; i < 4; i++) { ... }   // T T T N  (repeats)

                if (data[i] >= 128) sum += data[i];   // random data: ~50% mispredicted
                sum += data[i] & -(data[i] >= 128 ? 1 : 0);   // branchless alternative
                """);

        var state = 2;
        var hits = 0;
        b.Step("A 2-bit saturating counter",
            "Each branch has a small counter. 'Taken' moves it right, 'not taken' moves it left. Predict 'taken' when it is in the right half.",
            "Two bits instead of one mean a single surprise (the loop exit) does not flip the prediction. Real predictors (TAGE, perceptron-based) combine global history from many branches and are right more than 95% of the time.",
            [1],
            s => s.Paint("st2", Info));

        for (var i = 0; i < outcomes.Length; i++)
        {
            var predicted = state >= 2;
            var correct = predicted == outcomes[i];
            if (correct)
            {
                hits++;
            }

            state = Math.Clamp(state + (outcomes[i] ? 1 : -1), 0, 3);
            if (i is 0 or 3 or 4 or 7)
            {
                var (idx, st, h) = (i, state, hits);
                b.Step($"Branch #{i + 1}: actual {(outcomes[i] ? "taken" : "not taken")}",
                    correct
                        ? $"Predicted {(predicted ? "taken" : "not taken")}: correct, so the pipeline keeps streaming."
                        : $"Predicted {(predicted ? "taken" : "not taken")}: wrong. The pipeline flushes about 15-20 cycles of speculative work.",
                    string.Empty,
                    [1],
                    s =>
                    {
                        for (var k = 0; k < 4; k++)
                        {
                            s.Paint("st" + k, k == st ? (correct ? Good : Bad) : Neutral);
                        }

                        for (var k = 0; k < outcomes.Length; k++)
                        {
                            s.Ghost("o" + k, k > idx).Paint("o" + k, k == idx ? (correct ? Good : Bad) : Neutral);
                        }

                        s.Write("stats", $"{h} of {idx + 1} predicted correctly");
                    });
            }
        }

        b.Step("Random data defeats prediction",
            "A branch on random data is taken about 50% of the time, with no pattern. Half of all predictions miss.",
            "This is the famous 'why is processing a sorted array faster' effect: sorting the data makes the branch predictable. Branchless code (conditional moves, masks, SIMD) avoids the gamble entirely.",
            [3, 4],
            s => s.Say("Unpredictable branches: about 50% mispredicted, about 10 ns each", Bad));

        b.Step("Speculation and Spectre",
            "CPUs execute down the predicted path before they know it is right. Wrong-path results are discarded, but the cache lines they touched remain.",
            "Spectre (2018) showed that attackers can train predictors and read secrets through those cache side effects. Mitigations (retpolines, IBRS, speculation barriers) cost some performance. JIT and browser engines also add their own hardening.",
            [],
            s => s.Say("Prediction keeps pipelines full, and opens side channels", Warn));

        return b.Lesson("Branch prediction and speculative execution", "CPU microarchitecture", "Saturating counters, misprediction cost, sorted-array speedups and Spectre.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson CpuDesigns() =>
        new StoryBuilder("AR8", "Modern CPUs differ in how they combine cores: hybrid big/little cores, chiplets connected by a fabric, unified memory shared with the GPU, and many-core server meshes. The ISA stays the same; performance characteristics do not.")
            .Setup(s =>
            {
                s.Region("hyb", "hybrid: big + efficient cores", 30, 30, 460, 240, Info);
                for (var i = 0; i < 4; i++)
                {
                    s.Box("pc" + i, "P-core", 50 + i * 105, 70, 95, 70, "fast, large");
                }

                for (var i = 0; i < 8; i++)
                {
                    s.Box("ec" + i, "E", 50 + i * 52, 170, 44, 44);
                }

                s.Region("chip", "chiplets: CCDs + I/O die", 510, 30, 460, 240, Warn);
                s.Box("ccd0", "CCD 0: 8 cores + L3", 530, 70, 200, 70);
                s.Box("ccd1", "CCD 1: 8 cores + L3", 750, 70, 200, 70);
                s.Box("iod", "I/O die: memory, PCIe", 620, 180, 240, 60);
                s.Connect("ccd0", "iod", "fabric", arrow: false).Connect("ccd1", "iod", "fabric", arrow: false);

                s.Region("soc", "SoC with unified memory", 30, 300, 460, 230, Good);
                s.Box("scpu", "CPU", 50, 340, 120, 60);
                s.Box("sgpu", "GPU", 190, 340, 120, 60);
                s.Box("snpu", "NPU", 330, 340, 120, 60);
                s.Box("umem", "one shared memory pool", 50, 440, 400, 60);
                s.Connect("scpu", "umem", arrow: false).Connect("sgpu", "umem", arrow: false).Connect("snpu", "umem", arrow: false);

                s.Region("srv", "many-core server", 510, 300, 460, 230);
                for (var i = 0; i < 24; i++)
                {
                    s.Box("sc" + i, null, 530 + i % 8 * 54, 340 + i / 8 * 56, 46, 46);
                }
            })
            .NoCode()
            .Step("Four ways to build a CPU",
                "Same ISAs, different organisation: hybrid cores, chiplets, systems-on-chip with shared memory, and many-core meshes.",
                "Where data lives relative to a core (same core, same chiplet, another chiplet, another socket) now matters as much as clock speed.")
            .Step("Hybrid cores",
                "Big performance cores handle latency-sensitive threads, and small efficiency cores run background work at a fraction of the power. ARM's big.LITTLE (2011) pioneered this; Intel adopted it in 2021 with Alder Lake.",
                "The OS scheduler needs hints (Intel Thread Director, ARM energy models) to place threads well. A benchmark can vary a lot depending on which core it lands on.",
                s =>
                {
                    s.PaintAll(Info, "pc0", "pc1", "pc2", "pc3");
                    s.PaintAll(Good, Enumerable.Range(0, 8).Select(i => "ec" + i).ToArray());
                })
            .Step("Chiplets",
                "AMD's Zen designs split a CPU into core chiplets (CCDs) and a separate I/O die. Smaller dies yield better and can be mixed and matched.",
                "Talking to another CCD's cache crosses the fabric, adding latency. Pinning communicating threads to the same CCD, or avoiding false sharing across CCDs, can matter. UCIe standardises chiplet links across vendors.",
                s =>
                {
                    s.PaintAll(Neutral, "pc0", "pc1", "pc2", "pc3");
                    s.PaintAll(Neutral, Enumerable.Range(0, 8).Select(i => "ec" + i).ToArray());
                    s.PaintAll(Warn, "ccd0", "ccd1");
                    s.Move("shape:ccd0", "shape:ccd1", "cross-CCD hop", Warn);
                })
            .Step("Unified memory SoCs",
                "Apple's M-series put CPU, GPU and neural engine on one chip sharing one memory pool, so no copies are needed between CPU and GPU memory.",
                "Contrast this with discrete GPUs, which need PCIe transfers (see the GPU track). Unified memory makes CPU/GPU cooperation cheap, but everything competes for the same memory bandwidth.",
                s =>
                {
                    s.PaintAll(Neutral, "ccd0", "ccd1");
                    s.PaintAll(Good, "scpu", "sgpu", "snpu", "umem");
                })
            .Step("Many-core servers",
                "Cloud CPUs (AMD EPYC, Intel Xeon, ARM Neoverse in AWS Graviton, NVIDIA Grace and Microsoft Cobalt) pack dozens to hundreds of cores connected by a mesh.",
                "At this scale, NUMA placement, memory bandwidth per core and lock contention dominate. Code that scaled on 8 cores can collapse on 128 because of a single shared counter.",
                s =>
                {
                    s.PaintAll(Neutral, "scpu", "sgpu", "snpu", "umem");
                    s.PaintAll(Info, Enumerable.Range(0, 24).Select(i => "sc" + i).ToArray());
                    s.Say("Same ISA, very different topology: measure where your threads run", Info);
                })
            .Lesson("CPU designs: hybrid cores, chiplets, SoCs, many-core", "CPU microarchitecture", "How modern CPUs are organised, and why topology now matters as much as clock speed.");

    // ------------------------------------------------------------------------------------------
    private static Lesson GpuIsas() =>
        new StoryBuilder("AR9", "GPU vendors change their native instruction sets every generation, so code ships in a stable virtual ISA (PTX, SPIR-V, DXIL, AIR) that the driver compiles to the real hardware ISA (SASS, AMDGPU, Xe).")
            .Setup(s =>
            {
                string[] nv = ["CUDA C++", "PTX (virtual ISA)", "SASS sm_90 / sm_100", "NVIDIA GPU"];
                string[] amd = ["HIP / OpenCL", "LLVM IR", "AMDGPU ISA (GCN/RDNA/CDNA)", "AMD GPU"];
                string[] port = ["HLSL / GLSL / WGSL", "SPIR-V / DXIL", "driver compiler", "any vendor's GPU"];
                for (var i = 0; i < 4; i++)
                {
                    s.Box("nv" + i, nv[i], 40, 40 + i * 120, 270, 60).Tint("nv" + i, 2);
                    s.Box("amd" + i, amd[i], 365, 40 + i * 120, 270, 60).Tint("amd" + i, 3);
                    s.Box("pt" + i, port[i], 690, 40 + i * 120, 270, 60).Tint("pt" + i, 0);
                    if (i > 0)
                    {
                        s.Connect("nv" + (i - 1), "nv" + i).Connect("amd" + (i - 1), "amd" + i).Connect("pt" + (i - 1), "pt" + i);
                    }
                }
            })
            .Code("""
                // PTX (virtual, stable across generations)
                ld.global.f32  %f1, [%rd1];
                fma.rn.f32     %f3, %f1, %f2, %f3;

                // SASS (native, changes per architecture)
                LDG.E R2, [R4.64]
                FFMA  R6, R2, R3, R6

                // AMD (vector vs scalar units)
                v_fmac_f32  v1, v2, v3
                s_load_dword s0, s[4:5], 0x0
                """)
            .Step("Three toolchains",
                "NVIDIA, AMD and the portable graphics APIs all compile through an intermediate language before reaching the GPU's real instruction set.",
                "CPU ISAs stay backward compatible for decades. GPU vendors redesign their native ISAs freely, and the intermediate layer is what keeps old binaries running on new GPUs.",
                [1, 2])
            .Step("NVIDIA: PTX to SASS",
                "nvcc compiles CUDA to PTX, a virtual ISA, then ptxas turns PTX into SASS for specific architectures (sm_80, sm_90, sm_100...). A 'fat binary' bundles several SASS versions plus PTX.",
                "If no SASS matches the GPU, the driver JIT-compiles the embedded PTX and caches the result. That is how a CUDA program built in 2020 runs on a GPU released in 2025.",
                [2, 3, 6, 7],
                s => s.PaintAll(Info, "nv1", "nv2"))
            .Step("AMD: an openly documented ISA",
                "AMD publishes its GPU ISA manuals. Instructions are split into scalar (s_) operations that run once per wavefront and vector (v_) operations that run per lane.",
                "The scalar unit handles values that are the same for every lane (loop counters, addresses), which saves vector registers. CDNA (data centre) uses 64-wide wavefronts; RDNA (gaming, 2019+) uses wave32.",
                [10, 11],
                s =>
                {
                    s.PaintAll(Neutral, "nv1", "nv2");
                    s.PaintAll(Info, "amd2");
                })
            .Step("Portable IRs for graphics and compute",
                "Vulkan and OpenCL use SPIR-V, DirectX 12 uses DXIL, Apple Metal uses AIR, and WebGPU's WGSL is translated into one of those. Each driver then compiles to its own hardware ISA.",
                "That final compile is why games show 'compiling shaders' and keep shader caches. Stutter from on-the-fly shader compilation is a classic PC gaming problem.",
                [],
                s =>
                {
                    s.Paint("amd2", Neutral);
                    s.PaintAll(Info, "pt1", "pt2");
                    s.Say("Stable virtual ISA on top, ever-changing hardware ISA underneath", Good);
                })
            .Lesson("GPU instruction sets: PTX, SASS, AMDGPU, SPIR-V", "GPU execution", "Why GPUs ship virtual ISAs, and how drivers turn them into native machine code.");

    // ------------------------------------------------------------------------------------------
    private static Lesson GpuExecution()
    {
        void Lanes(SceneState s, Tone tone, Func<int, bool>? ghost = null)
        {
            for (var i = 0; i < 32; i++)
            {
                var id = "ln" + i;
                if (!s.Has(id))
                {
                    s.Box(id, null, 380 + i % 16 * 36, 300 + i / 16 * 44, 32, 38);
                }

                s.Paint(id, tone).Ghost(id, ghost?.Invoke(i) ?? false);
            }
        }

        return new StoryBuilder("AR10", "A GPU SM issues one instruction for a whole warp each cycle, choosing among many resident warps so that latency is hidden. Tensor cores execute a whole small matrix multiply as a single warp-wide instruction.")
            .Setup(s =>
            {
                s.Caption("wcap", "resident warps", 40, 30);
                for (var w = 0; w < 8; w++)
                {
                    s.Box("w" + w, $"warp {w}", 40, 55 + w * 52, 130, 44, w % 3 == 0 ? "waiting on memory" : "ready");
                    s.Paint("w" + w, w % 3 == 0 ? Warn : Neutral);
                }

                s.Box("sched", "warp scheduler", 220, 140, 130, 70);
                s.Box("rf", "register file", 380, 140, 280, 70, "64K x 32-bit");
                s.Box("tc", "tensor core", 720, 140, 240, 70, "D = A x B + C");
                s.Caption("lcap", "32 FP32 lanes", 380, 276);
                Lanes(s, Neutral);
                s.Label("info", "", 220, 420, 740, 15, Info);
            })
            .Code("""
                FFMA R6, R2, R3, R6      // R6 = R2 * R3 + R6, for all 32 threads
                HMMA.16816.F32 ...       // tensor core: warp-wide 16x8x16 matrix multiply-accumulate
                """)
            .Step("Pick a ready warp",
                "Each cycle, the scheduler picks a warp whose operands are ready (tracked by a scoreboard). Warps waiting on memory are simply skipped.",
                "There is no context-switch cost: every resident warp keeps its own registers. This is the latency-hiding mechanism from the occupancy lesson.",
                [1],
                s =>
                {
                    s.Paint("w1", Info).Paint("sched", Info);
                    s.Move("shape:w1", "shape:sched", "issue", Info);
                })
            .Step("Collect operands",
                "The register file reads R2, R3 and R6 for all 32 threads at once: 3 x 32 x 4 bytes = 384 bytes in one go.",
                "Register files are banked to deliver that bandwidth. Bank conflicts between operands, or register spills to local memory, slow this stage down.",
                [1],
                s =>
                {
                    s.Paint("sched", Neutral).Paint("rf", Info);
                    s.Move("shape:rf", "shape:ln8", "R2, R3, R6 x 32", Info);
                })
            .Step("Execute across 32 lanes",
                "All 32 lanes perform the fused multiply-add (FFMA) on their own data. The result is ready about 4 cycles later.",
                "NVIDIA's guide gives roughly 4 cycles of latency for most arithmetic on Volta-class GPUs. While warp 1 waits for its result, the scheduler issues instructions from warps 2, 4, 5, and so on.",
                [1],
                s =>
                {
                    s.Paint("rf", Neutral);
                    Lanes(s, Good);
                    s.Paint("w1", Neutral).Paint("w2", Info);
                    s.Write("info", "warp 1 in flight; the scheduler already issues warp 2's next instruction");
                })
            .Step("Masked lanes on divergence",
                "If only some threads take a branch, the other lanes are masked off for that instruction. They still occupy their slots.",
                "This is the warp divergence lesson at the instruction level: a per-lane active mask decides which lanes write results.",
                [1],
                s =>
                {
                    Lanes(s, Good, i => i % 4 == 3);
                    s.Write("info", "active mask: 24 of 32 lanes write results");
                })
            .Step("Tensor cores: a matrix in one instruction",
                "A single warp-level MMA instruction multiplies small matrix tiles (for example 16x8x16 in FP16 or BF16) and accumulates into FP32.",
                "Tensor cores (since Volta, 2017) deliver many times the FP32 throughput for matrix maths. Hopper added FP8 and Blackwell added FP4, which is why AI training and inference run on them.",
                [2],
                s =>
                {
                    Lanes(s, Neutral);
                    s.Paint("tc", Good).Paint("w2", Neutral).Paint("w5", Info);
                    s.Move("shape:w5", "shape:tc", "HMMA", Good);
                    s.Write("info", "one instruction = thousands of multiply-adds");
                    s.Say("SIMT lanes for general code, tensor cores for matrix maths", Good);
                })
            .Lesson("How a GPU executes an instruction", "GPU execution", "Warp scheduling, operand collection, 32-lane execution, masks and tensor cores.");
    }
}
