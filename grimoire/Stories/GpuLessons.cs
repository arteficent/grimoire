using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class GpuLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return CpuVsGpu();
        yield return WarpDivergence();
        yield return Coalescing();
        yield return TransfersAndStreams();
        yield return BankConflicts();
        yield return Occupancy();
        yield return VramFragmentation();
        yield return AsyncLaunch();
    }

    private static SceneState Lanes(this SceneState s, string prefix, double y, Func<int, Tone> tone, Func<int, bool>? ghost = null)
    {
        for (var i = 0; i < 32; i++)
        {
            var id = prefix + i;
            if (!s.Has(id))
            {
                s.Box(id, i.ToString(), 52 + i * 28, y, 26, 34);
                s.Shape(id).FontSize = 9;
            }

            s.Paint(id, tone(i)).Ghost(id, ghost?.Invoke(i) ?? false);
        }

        return s;
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson CpuVsGpu() =>
        new StoryBuilder("GP1", "CPUs minimise latency for a few threads with big caches and clever control logic. GPUs maximise throughput by running tens of thousands of lightweight threads and switching between them for free.")
            .Setup(s =>
            {
                s.Region("cpu", "CPU: a few big cores", 30, 40, 360, 470, Info);
                for (var i = 0; i < 8; i++)
                {
                    s.Box("cc" + i, "core", 55 + i % 2 * 165, 85 + i / 2 * 100, 145, 80, "OoO, branch predictor");
                }

                s.Region("gpu", "GPU: many streaming multiprocessors (SMs)", 420, 40, 550, 470, Good);
                for (var i = 0; i < 40; i++)
                {
                    s.Box("sm" + i, "SM", 440 + i % 8 * 65, 85 + i / 8 * 80, 58, 64);
                    s.Shape("sm" + i).FontSize = 10;
                }
            })
            .NoCode()
            .Step("Two different design goals",
                "A CPU spends its transistors making one thread fast. A GPU spends them on many simple cores grouped into SMs.",
                "A server CPU might run 64 hardware threads. A data-centre GPU with 80 SMs can keep about 160,000 threads resident (2,048 per SM), according to NVIDIA's CUDA Best Practices Guide.")
            .Step("CPU cores: latency machines",
                "Each CPU core runs out of order, predicts branches, and hides memory latency with large caches.",
                "Switching between OS threads on a CPU means saving and restoring registers (microseconds). The CPU is built to finish one task as soon as possible.",
                s => s.PaintAll(Info, Enumerable.Range(0, 8).Select(i => "cc" + i).ToArray()))
            .Step("GPU SMs: throughput machines",
                "Each SM runs threads in groups of 32 (warps) that execute the same instruction together. When one warp waits for memory, the SM issues instructions from another warp.",
                "Every resident thread keeps its own registers allocated, so switching warps costs nothing. Latency is hidden by having plenty of other work ready, not by caches.",
                s =>
                {
                    s.PaintAll(Neutral, Enumerable.Range(0, 8).Select(i => "cc" + i).ToArray());
                    s.PaintAll(Good, Enumerable.Range(0, 40).Select(i => "sm" + i).ToArray());
                })
            .Step("When is a GPU worth it?",
                "When the same operation is applied to huge amounts of data, and there is enough arithmetic per byte to pay for moving the data over PCIe.",
                "Adding two NxN matrices does N^2 operations on 3N^2 elements moved: not worth the transfer. Multiplying them does N^3 operations on the same data, so the benefit grows with N. Keep data on the GPU between kernels.",
                s => s.Say("Data-parallel + compute-heavy + data stays on the device", Good))
            .Lesson("CPU vs GPU: latency vs throughput", "Architecture", "Why GPUs run thousands of threads, and when moving work to one pays off.");

    // ------------------------------------------------------------------------------------------
    private static Lesson WarpDivergence() =>
        new StoryBuilder("GP2", "All 32 threads of a warp share one instruction stream. If they branch differently, the warp runs each path in turn with some lanes switched off. Make branch conditions uniform per warp.")
            .Setup(s =>
            {
                s.Caption("wcap", "one warp = 32 threads executing in lockstep (SIMT)", 52, 60, 700);
                s.Lanes("l", 90, _ => Neutral);
                s.Label("pass", "", 52, 160, 900, 18, Info);
                s.Box("time", "", 52, 260, 0, 50, tone: Info);
            })
            .Code("""
                __global__ void kernel(float* data)
                {
                    int tid = blockIdx.x * blockDim.x + threadIdx.x;
                    if (threadIdx.x % 2 == 0) data[tid] = expensiveA(data[tid]);   // even lanes
                    else                      data[tid] = expensiveB(data[tid]);   // odd lanes
                }
                // uniform alternative: branch on (threadIdx.x / 32) so whole warps agree
                """)
            .Step("One warp, one instruction stream",
                "The 32 threads of a warp fetch and execute the same instruction at the same time, each on its own data.",
                "This is SIMT (single instruction, multiple threads). It is why GPUs are so efficient: one instruction fetch and decode drives 32 lanes.",
                [1])
            .Step("Branch: even lanes take path A",
                "The condition differs inside the warp. First the warp executes A with the odd lanes masked off: half the hardware is idle.",
                "Masked lanes still occupy their slots and burn time. The warp cannot run both paths at once.",
                [4],
                s =>
                {
                    s.Lanes("l", 90, i => i % 2 == 0 ? Info : Neutral, i => i % 2 == 1);
                    s.Write("pass", "pass 1: path A (odd lanes idle)");
                    s.Resize("time", 420, 50).Write("time", "A");
                })
            .Step("Then odd lanes take path B",
                "Next the warp runs B with the even lanes masked off. The total time is A + B, at 50% efficiency.",
                "With nested branches or loops with different trip counts, efficiency can drop to 1/32. Profilers report this as 'branch efficiency' or 'warp execution efficiency'.",
                [5],
                s =>
                {
                    s.Lanes("l", 90, i => i % 2 == 1 ? Warn : Neutral, i => i % 2 == 0);
                    s.Write("pass", "pass 2: path B (even lanes idle)");
                    s.Resize("time", 840, 50).Write("time", "A, then B (serialised)").Paint("time", Bad);
                    s.Say("Divergence: the warp pays for both branches", Bad);
                })
            .Step("Fix: make the branch uniform per warp",
                "Branch on something that is the same for all 32 lanes, such as threadIdx.x / 32, or sort the data so similar items are processed by the same warp.",
                "Then each warp takes exactly one path and runs at full width. Short branches are often compiled to predicated instructions with no real divergence. Since Volta, __syncwarp() reconverges explicitly.",
                [7],
                s =>
                {
                    s.Lanes("l", 90, _ => Good);
                    s.Write("pass", "warp 0 runs A on all 32 lanes; warp 1 runs B on all 32 lanes");
                    s.Resize("time", 420, 50).Write("time", "A or B, full width").Paint("time", Good);
                    s.Say("No divergence: every lane does useful work", Good);
                })
            .Lesson("SIMT warps and branch divergence", "Execution", "Why an if/else inside a warp can halve your throughput.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Coalescing()
    {
        void Access(SceneState s, int[] words, string stats, Tone tone)
        {
            s.Lanes("l", 70, _ => Info);
            var touched = words.Select(w => w / 8).Distinct().ToHashSet();
            for (var seg = 0; seg < 8; seg++)
            {
                s.Region("seg" + seg, $"32 B #{seg}", 52 + seg * 112, 220, 108, 90, touched.Contains(seg) ? tone : Neutral);
            }

            for (var w = 0; w < 64; w++)
            {
                s.Paint("w" + w, words.Contains(w) ? tone : Neutral);
            }

            s.Write("stats", stats);
        }

        return new StoryBuilder("GP3", "When the 32 threads of a warp access neighbouring addresses, the hardware merges them into a few 32-byte transactions. Strided or scattered access wastes bandwidth.")
            .Setup(s =>
            {
                s.Caption("lcap", "warp lanes 0-31", 52, 46);
                s.Caption("mcap", "global memory: 4-byte words grouped into 32-byte segments", 52, 196, 700);
                for (var w = 0; w < 64; w++)
                {
                    s.Box("w" + w, null, 56 + w / 8 * 112 + w % 8 * 13, 250, 11, 40);
                }

                s.Label("stats", "", 52, 360, 900, 17, Info);
            })
            .Code("""
                out[i] = in[i];              // coalesced: lane k reads word k
                out[i] = in[i + 1];          // misaligned: one extra segment
                out[i] = in[i * 2];          // stride 2: twice the segments, 50% useful
                out[i] = in[index[i]];       // gather: up to 32 separate segments
                """)
            .Step("Contiguous access",
                "Lane k reads word k. 32 lanes x 4 bytes = 128 bytes, exactly four 32-byte segments.",
                "Four transactions deliver 128 useful bytes: 100% efficiency. This is the pattern the hardware is designed for.",
                [1],
                s => Access(s, Enumerable.Range(0, 32).ToArray(), "4 transactions, 100% of fetched bytes used", Good))
            .Step("Misaligned by one word",
                "Shift every access by one element. The same 128 bytes now straddle five segments.",
                "One extra transaction costs about 20% of bandwidth. cudaMalloc returns memory aligned to at least 256 bytes, and block sizes that are multiples of 32 keep warps aligned.",
                [2],
                s => Access(s, Enumerable.Range(1, 32).ToArray(), "5 transactions, 80% efficiency", Warn))
            .Step("Stride 2",
                "Lane k reads word 2k. The warp touches eight segments but uses only half of each.",
                "NVIDIA measures strided copies on a V100 dropping steadily as the stride grows, until each lane needs its own transaction. This is typical when reading one field from an array of structs.",
                [3],
                s => Access(s, Enumerable.Range(0, 32).Select(i => i * 2).ToArray(), "8 transactions, 50% efficiency", Bad))
            .Step("Fixes: change the layout",
                "Use structure-of-arrays (separate arrays per field) so neighbouring threads read neighbouring values. For transposes, stage tiles through shared memory and write them out coalesced.",
                "The CUDA guide's matrix-transpose example goes from 12.8 GB/s to 140 GB/s just by coalescing global loads through a shared-memory tile.",
                [1],
                s =>
                {
                    Access(s, Enumerable.Range(0, 32).ToArray(), "structure-of-arrays: back to 4 transactions", Good);
                    s.Say("Neighbouring threads should read neighbouring addresses", Good);
                })
            .Lesson("Memory coalescing", "Memory", "How warp access patterns turn into memory transactions, and why layout decides bandwidth.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson TransfersAndStreams()
    {
        const double x0 = 170, unit = 52;

        return new StoryBuilder("GP4", "Host-device transfers over PCIe are about 50x slower than on-GPU memory. Minimise them, use pinned memory, and overlap copies with kernels using streams.")
            .Setup(s =>
            {
                s.Box("host", "CPU + host RAM", 40, 40, 220, 80);
                s.Box("pcie", "PCIe Gen3 x16 ~16 GB/s", 330, 55, 300, 50, tone: Warn);
                s.Box("dev", "GPU + HBM ~900 GB/s", 700, 40, 260, 80, tone: Good);
                s.Connect("host", "pcie", arrow: false).Connect("pcie", "dev", arrow: false);
                s.Label("r0", "copy in (H->D)", 30, 220, 140, 12);
                s.Label("r1", "kernel", 30, 290, 140, 12);
                s.Label("r2", "copy out (D->H)", 30, 360, 140, 12);
            })
            .Code("""
                // sequential
                cudaMemcpy(d, h, bytes, cudaMemcpyHostToDevice);
                kernel<<<grid, block>>>(d);
                cudaMemcpy(h, d, bytes, cudaMemcpyDeviceToHost);

                // staged across 4 streams (h must be pinned: cudaHostAlloc)
                for (int i = 0; i < 4; i++) {
                    cudaMemcpyAsync(d + off[i], h + off[i], chunk, H2D, stream[i]);
                    kernel<<<g, b, 0, stream[i]>>>(d + off[i]);
                    cudaMemcpyAsync(h + off[i], d + off[i], chunk, D2H, stream[i]);
                }
                """)
            .Step("Two memories, one narrow pipe",
                "The CPU and GPU have separate memories connected by PCIe. Moving data across is far slower than the GPU reading its own memory.",
                "The CUDA Best Practices Guide quotes 898 GB/s for V100 HBM2 versus 16 GB/s for PCIe Gen3 x16. Keep intermediate data on the device, and batch many small transfers into one.",
                [1])
            .Step("Pageable vs pinned host memory",
                "Ordinary (pageable) memory is first copied into a hidden pinned staging buffer, then DMA'd. Pinned memory is DMA'd directly and is needed for async copies.",
                "Pinned transfers reach about 12 GB/s on PCIe Gen3 x16. But pinned memory cannot be paged out, so overusing it hurts the rest of the system.",
                [6],
                s =>
                {
                    s.Paint("host", Info).SubText("host", "pinned buffer: DMA directly");
                    s.Move("shape:host", "shape:dev", "DMA", Info);
                })
            .Step("Sequential: copy, compute, copy",
                "Each phase waits for the previous one. The GPU sits idle during both copies.",
                "Total time = copy in + kernel + copy out = 12 units here. cudaMemcpy also blocks the CPU thread until it finishes.",
                [2, 3, 4],
                s =>
                {
                    s.Paint("host", Neutral);
                    s.Box("seq0", "copy all", x0, 210, 4 * unit - 3, 40).Tint("seq0", 0);
                    s.Box("seq1", "kernel all", x0 + 4 * unit, 280, 4 * unit - 3, 40).Tint("seq1", 2);
                    s.Box("seq2", "copy all", x0 + 8 * unit, 350, 4 * unit - 3, 40).Tint("seq2", 1);
                    s.Label("total", "total: 12 units", x0, 420, 300, 15, Warn);
                })
            .Step("Streams: overlap copies with compute",
                "Split the data into 4 chunks, each in its own stream. While chunk 2 is copying in, chunk 1 is computing and chunk 0 is copying out.",
                "GPUs have separate copy engines, so H->D, kernel and D->H work can run at the same time. The guide's estimate is tE + tT / nStreams instead of tE + tT. Here the total drops to about 6 units.",
                [6, 7, 8, 9, 10, 11],
                s =>
                {
                    s.Remove("seq0", "seq1", "seq2");
                    for (var i = 0; i < 4; i++)
                    {
                        s.Box($"in{i}", $"in {i}", x0 + i * unit, 210, unit - 3, 40).Tint($"in{i}", 0);
                        s.Box($"k{i}", $"k {i}", x0 + (i + 1) * unit, 280, unit - 3, 40).Tint($"k{i}", 2);
                        s.Box($"out{i}", $"out {i}", x0 + (i + 2) * unit, 350, unit - 3, 40).Tint($"out{i}", 1);
                    }

                    s.Write("total", "total: 6 units (half)").Paint("total", Good);
                    s.Say("Overlapped transfers hide most of the PCIe cost", Good);
                })
            .Lesson("Host-device transfers, pinned memory and streams", "Memory", "The PCIe bottleneck and how streams overlap copies with kernels.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson BankConflicts()
    {
        void Banks(SceneState s, Func<int, int> bankOf, string stats, Tone tone)
        {
            var hits = new int[32];
            for (var lane = 0; lane < 32; lane++)
            {
                hits[bankOf(lane)]++;
            }

            for (var b = 0; b < 32; b++)
            {
                var id = "b" + b;
                var h = Math.Max(30, hits[b] * 10);
                if (!s.Has(id))
                {
                    s.Box(id, string.Empty, 52 + b * 28, 460 - h, 26, h);
                    s.Shape(id).FontSize = 9;
                }

                s.Place(id, 52 + b * 28, 460 - h).Resize(id, 26, h).Write(id, hits[b] == 0 ? string.Empty : hits[b].ToString())
                 .Paint(id, hits[b] == 0 ? Neutral : hits[b] == 1 ? Good : Bad);
            }

            s.Lanes("l", 70, _ => tone);
            s.Write("stats", stats);
        }

        return new StoryBuilder("GP5", "Shared memory has 32 banks; successive 4-byte words go to successive banks. If several lanes of a warp hit the same bank (different addresses), the accesses are serialised. Padding fixes it.")
            .Setup(s =>
            {
                s.Caption("lcap", "warp lanes", 52, 46);
                s.Caption("bcap", "requests per shared-memory bank (32 banks)", 52, 140, 600);
                s.Label("stats", "", 52, 170, 900, 17, Info);
            })
            .Code("""
                __shared__ float tile[32][32];
                float a = tile[threadIdx.y][threadIdx.x];   // row: lane k -> bank k
                float b = tile[threadIdx.x][threadIdx.y];   // column: every lane -> same bank!

                __shared__ float tile[32][33];              // pad one column: stride 33
                """)
            .Step("Row access: one lane per bank",
                "Reading along a row, lane k reads word k, which lives in bank k. All 32 banks serve the warp in a single cycle.",
                "Shared memory is on-chip and about as fast as L1, but only when the banks are not contended.",
                [1, 2],
                s => Banks(s, lane => lane, "conflict-free: 1 cycle", Good))
            .Step("Column access: a 32-way conflict",
                "Reading down a column of a 32 x 32 tile, the lanes are 32 words apart, so every lane maps to the same bank.",
                "The hardware splits the request into 32 serial accesses: 32x slower for that load. This happens in transposes and many stencil codes.",
                [3],
                s =>
                {
                    Banks(s, _ => 0, "32-way bank conflict: 32 cycles", Bad);
                    s.Say("Every lane hits bank 0: fully serialised", Bad);
                })
            .Step("Fix: pad the tile to 33 columns",
                "With one extra column, consecutive rows start one bank later. Column reads hit banks k * 33 % 32 = k, so they are conflict-free again.",
                "The CUDA guide's C = A x A^T example goes from 140 GB/s to 199 GB/s just from this padding. The exception: when many lanes read the same address, it is a broadcast and costs nothing extra.",
                [5],
                s =>
                {
                    Banks(s, lane => lane * 33 % 32, "padded: conflict-free again", Good);
                    s.Say("[32][33] spreads columns across all banks", Good);
                })
            .Lesson("Shared memory bank conflicts", "Memory", "Why a column read can be 32x slower, and the one-column padding trick.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Occupancy()
    {
        void Slots(SceneState s, int active, Tone tone)
        {
            for (var i = 0; i < 64; i++)
            {
                var id = "slot" + i;
                if (!s.Has(id))
                {
                    s.Box(id, null, 60 + i % 16 * 32, 80 + i / 16 * 36, 28, 30);
                }

                s.Paint(id, i < active ? tone : Neutral).Ghost(id, i >= active);
            }
        }

        return new StoryBuilder("GP6", "Occupancy is active warps divided by the maximum warps per SM. More resident warps give the scheduler more chances to hide memory latency, but registers and shared memory limit how many fit.")
            .Setup(s =>
            {
                s.Caption("scap", "SM warp slots (max 64 warps = 2,048 threads)", 60, 56, 520);
                s.Box("regs", "register file: 65,536 x 32-bit", 620, 80, 330, 60);
                s.Label("stats", "", 60, 240, 900, 16, Info);
            })
            .Code("""
                __global__ void __launch_bounds__(256, 4) kernel(...)   // hint: 256 threads, 4 blocks/SM
                // nvcc -Xptxas -v  -> reports registers per thread
                // occupancy = active warps / 64
                """)
            .Step("32 registers per thread: full occupancy",
                "64 warps x 32 threads x 32 registers = 65,536 registers, exactly the register file. All 64 warp slots can be resident.",
                "Each thread's registers stay allocated while it is resident. That is what makes warp switching free, and also why register usage directly limits occupancy.",
                [3],
                s =>
                {
                    Slots(s, 64, Good);
                    s.FillTo("regs", 1).Paint("regs", Good);
                    s.Write("stats", "occupancy 100%: 64 / 64 warps");
                })
            .Step("64 registers per thread: half the warps",
                "Double the registers per thread and only 32 warps fit: 50% occupancy.",
                "Heavy kernels (unrolled loops, big local arrays) push register counts up. The compiler may spill to 'local memory', which is actually off-chip and slow.",
                [2],
                s =>
                {
                    Slots(s, 32, Warn);
                    s.Write("stats", "occupancy 50%: 32 / 64 warps (register-limited)");
                })
            .Step("Why it matters: hiding memory latency",
                "A global memory load takes hundreds of cycles. With many warps, the scheduler always finds one that is ready to issue while the others wait.",
                "Think of it as the GPU's version of the CPU's out-of-order execution. Instead of reordering instructions within a thread, it interleaves many threads.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("slot", StringComparison.Ordinal));
                    for (var w = 0; w < 4; w++)
                    {
                        s.Label("wl" + w, $"warp {w}", 30, 110 + w * 60, 80, 12);
                        s.Box($"c{w}", "compute", 110 + w * 60, 100 + w * 60, 60, 40).Tint($"c{w}", w);
                        s.Box($"m{w}", "waiting on memory", 172 + w * 60, 100 + w * 60, 240, 40, tone: Neutral).Ghost($"m{w}");
                        s.Box($"d{w}", "compute", 414 + w * 60, 100 + w * 60, 60, 40).Tint($"d{w}", w);
                    }

                    s.Write("stats", "4 warps take turns: the SM issues work almost every cycle").Place("stats", 60, 360);
                })
            .Step("Too few warps: idle cycles",
                "With only one warp, nothing else can run while it waits on memory, so the SM sits idle.",
                "Higher occupancy is not always faster: past a point, more instruction-level parallelism or better cache reuse wins. Measure with Nsight Compute and use the occupancy calculator.",
                s =>
                {
                    for (var w = 1; w < 4; w++)
                    {
                        s.Remove($"c{w}", $"m{w}", $"d{w}", "wl" + w);
                    }

                    s.Paint("m0", Bad).Ghost("m0", false).Write("m0", "SM idle: nothing else to run");
                    s.Write("stats", "occupancy too low: memory latency is fully exposed").Paint("stats", Bad);
                })
            .Lesson("Occupancy and latency hiding", "Execution", "How registers per thread limit resident warps, and why that hides or exposes memory latency.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson VramFragmentation()
    {
        const double x0 = 50, unit = 37.5;

        void Seg(SceneState s, string id, double start, double size, string text, int color, bool free = false)
        {
            s.Box(id, text, x0 + start * unit, 200, size * unit - 3, 110, $"{size} GB");
            if (free)
            {
                s.Ghost(id).Tint(id, -1);
            }
            else
            {
                s.Ghost(id, false).Tint(id, color);
            }
        }

        return new StoryBuilder("GP7", "GPU memory is small and slow to allocate, so frameworks cache freed blocks. Variable-sized tensors then fragment that cache, and 'out of memory' appears while plenty of VRAM is nominally free.")
            .Setup(s =>
            {
                s.Caption("cap", "24 GB of VRAM", 50, 170);
                s.Label("stats", "", 50, 340, 900, 16, Info);
            })
            .Code("""
                // cudaMalloc / cudaFree are expensive and synchronising; prefer pools:
                cudaMallocAsync(&ptr, bytes, stream);   // stream-ordered pool allocator
                cudaFreeAsync(ptr, stream);

                // PyTorch: torch.cuda.memory_summary(); PYTORCH_CUDA_ALLOC_CONF=expandable_segments:True
                """)
            .Step("Model weights and activations",
                "8 GB of weights stay resident. Each training step allocates activations and temporary workspaces on top.",
                "The CUDA guide warns that cudaMalloc and cudaFree are expensive, and cudaFree implicitly synchronises the device. Allocating per step would stall the GPU.",
                [1],
                s =>
                {
                    Seg(s, "w", 0, 8, "weights", 0);
                    Seg(s, "a1", 8, 6, "activations", 1);
                    Seg(s, "t1", 14, 3, "temp", 2);
                    Seg(s, "a2", 17, 5, "activations", 3);
                    Seg(s, "f0", 22, 2, "free", 0, free: true);
                    s.Write("stats", "allocated 22 GB, free 2 GB");
                })
            .Step("A caching allocator keeps freed blocks",
                "When a step ends, its tensors are freed, but the framework keeps the blocks reserved for reuse instead of returning them to the driver.",
                "Frameworks such as PyTorch do this, and so does CUDA's own stream-ordered pool (cudaMallocAsync). Reuse is fast, but the free space is now a set of differently sized holes.",
                [2, 3],
                s =>
                {
                    Seg(s, "a1", 8, 6, "free (cached)", 0, free: true);
                    Seg(s, "a2", 17, 5, "free (cached)", 0, free: true);
                    s.Write("stats", "free total: 13 GB in three holes (6 + 5 + 2)");
                })
            .Step("A larger batch: out of memory",
                "The next step asks for 7 GB of contiguous activations. 13 GB is free in total, but no single hole is 7 GB, so allocation fails.",
                "This is the classic 'CUDA out of memory. Tried to allocate 7.00 GiB ... reserved but unallocated' message: external fragmentation inside the cache.",
                s =>
                {
                    s.Box("req", "needs 7 GB contiguous", 380, 410, 260, 50, tone: Bad);
                    s.Say("OOM with 13 GB free: fragmentation, not lack of memory", Bad);
                })
            .Step("Fixes",
                "Use smaller or fixed batch shapes, mixed precision (fp16/bf16 halve activations), activation checkpointing, or allocator settings that let segments grow (expandable segments).",
                "Pre-allocating fixed-size buffers once (as inference servers do with static shapes or CUDA graphs) avoids fragmentation entirely. Monitor reserved versus allocated memory, not just 'used'.",
                [5],
                s =>
                {
                    s.Remove("req");
                    Seg(s, "a1", 8, 4, "act (fp16)", 1);
                    Seg(s, "t1", 12, 3, "temp", 2);
                    Seg(s, "a2", 15, 4, "act (fp16)", 3);
                    Seg(s, "f0", 19, 5, "free", 0, free: true);
                    s.Write("stats", "half-precision activations: fits with 5 GB headroom");
                    s.Say("Smaller, regular allocations avoid fragmentation", Good);
                })
            .Lesson("VRAM out-of-memory and fragmentation", "Memory", "Why 'CUDA out of memory' can happen with plenty of free memory.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson AsyncLaunch()
    {
        const double x0 = 150, unit = 40;

        return new StoryBuilder("GP8", "Kernel launches are asynchronous: the CPU only queues work. Time with GPU events, synchronise only where results are needed, and check errors explicitly, because they surface later.")
            .Setup(s =>
            {
                s.Label("cpu", "CPU thread", 30, 160, 120, 13);
                s.Label("gpu", "GPU stream", 30, 260, 120, 13);
                s.Label("stats", "", 30, 360, 940, 15, Info);
            })
            .Code("""
                auto t0 = now();
                kernel<<<grid, block>>>(d);          // returns immediately: only queued
                auto t1 = now();                     // measures ~5 us of launch, not the kernel!

                cudaEventRecord(start, stream);
                kernel<<<grid, block, 0, stream>>>(d);
                cudaEventRecord(stop, stream);
                cudaEventSynchronize(stop);          // wait only for what you need
                cudaEventElapsedTime(&ms, start, stop);
                """)
            .Step("Launch returns immediately",
                "kernel<<<...>>> puts the work into a stream queue and returns at once. The CPU keeps running while the GPU executes later.",
                "This asynchrony is good: the CPU can prepare the next batch while the GPU computes. Kernels in the same stream run in order.",
                [2],
                s =>
                {
                    s.Box("launch", "launch", x0, 150, unit - 3, 40).Tint("launch", 0);
                    s.Box("cpuwork", "CPU continues: prepare next batch", x0 + unit, 150, 9 * unit, 40, tone: Good);
                    s.Box("k", "kernel (10 ms)", x0 + unit, 250, 10 * unit, 40).Tint("k", 2);
                })
            .Step("Pitfall: timing with a CPU clock",
                "Measuring around the launch captures only the queueing time (microseconds), not the 10 ms the kernel actually takes.",
                "The CUDA guide says to synchronise before reading CPU timers, or better, to use cudaEvent timestamps recorded in the stream itself.",
                [1, 2, 3],
                s =>
                {
                    s.Box("timer", "CPU timer: 0.005 ms ?!", x0, 100, 4 * unit, 36, tone: Bad);
                    s.Write("stats", "the measured time ends before the kernel even starts");
                })
            .Step("Pitfall: hidden synchronisation",
                "A plain cudaMemcpy afterwards blocks the CPU until all earlier work in the stream is done, so the CPU sits idle.",
                "Implicit syncs also come from cudaFree, device-wide calls and the default stream's serialising behaviour. They silently destroy the CPU/GPU overlap.",
                s =>
                {
                    s.Remove("timer");
                    s.Resize("cpuwork", 3 * unit, 40).Write("cpuwork", "prep");
                    s.Box("block", "blocked in cudaMemcpy", x0 + 4 * unit, 150, 7 * unit, 40, tone: Bad);
                    s.Box("copy", "copy", x0 + 11 * unit, 250, 2 * unit, 40).Tint("copy", 1);
                    s.Write("stats", "CPU idle for 7 units: a synchronous copy waits for the kernel");
                })
            .Step("Pitfall: errors arrive late",
                "If the kernel faults, the launch itself still reports success. The error shows up at the next synchronising call, far from its cause.",
                "Check cudaGetLastError() after launches. In debug builds, set CUDA_LAUNCH_BLOCKING=1 (or synchronise after each kernel) so errors point at the right line.",
                s =>
                {
                    s.Paint("k", Bad).Write("k", "kernel: illegal address");
                    s.Write("stats", "the error is reported by the later cudaMemcpy, not by the launch");
                })
            .Step("Do it right",
                "Time with events, copy with cudaMemcpyAsync on pinned memory, and synchronise only on the event you actually need.",
                "The CPU stays busy, the GPU stays fed, and timings are correct. The same principles apply to every async API, from .NET Tasks to Vulkan command buffers.",
                [5, 6, 7, 8, 9],
                s =>
                {
                    s.Remove("block", "copy");
                    s.Tint("k", 2).Write("k", "kernel (10 ms)");
                    s.Resize("cpuwork", 9 * unit, 40).Write("cpuwork", "CPU keeps working").Paint("cpuwork", Good);
                    s.Box("ev", "event sync", x0 + 11 * unit, 150, 2 * unit, 40, tone: Info);
                    s.Write("stats", "measured with events: 10.0 ms, CPU and GPU overlapped");
                    s.Say("Asynchronous by default, synchronise deliberately", Good);
                })
            .Lesson("Async kernel launches and synchronisation", "Execution", "Why GPU timings lie, where hidden syncs come from, and why errors appear late.");
    }
}
