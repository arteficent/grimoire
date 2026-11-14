using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class ResourceLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return ConnectionPool();
        yield return TokenBucket();
        yield return RetryStorm();
        yield return CacheStampede();
        yield return CircuitBreaker();
        yield return HandleLeaks();
        yield return Queueing();
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson ConnectionPool() =>
        new StoryBuilder("RM1", "Pools make expensive resources cheap to reuse, but only if every borrower returns what it took. One leaked connection per request drains the pool, and then every request times out.")
            .Setup(s =>
            {
                s.Region("pool", "connection pool (max 4)", 300, 60, 400, 380, Info);
                for (var i = 0; i < 4; i++)
                {
                    s.Box("c" + i, $"conn {i + 1}", 340, 110 + i * 78, 320, 56, "idle");
                }

                s.Box("db", "database", 780, 200, 180, 100, "TCP + TLS + auth");
                s.Caption("reqcap", "incoming requests", 40, 60);
            })
            .Code("""
                await using var conn = new SqlConnection(connectionString);   // Dispose returns it
                await conn.OpenAsync(ct);                                      // borrows from the pool
                await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 5 };
                // Max Pool Size = 100 by default; a missing Dispose leaks one per call
                """)
            .Step("Why pool connections?",
                "Opening a database connection means a TCP handshake, TLS, and authentication: several milliseconds. A pool keeps a few open connections and lends them out.",
                "SqlClient, Npgsql and HttpClient (SocketsHttpHandler) all pool connections by connection string or endpoint. Borrowing from the pool takes microseconds.",
                [1, 2])
            .Step("Borrow and return",
                "Two requests borrow connections, run their queries, and Dispose returns the connections to the pool.",
                "'await using' guarantees the return even if the query throws. The physical connection stays open; only its ownership changes.",
                [1, 2],
                s =>
                {
                    s.Paint("c0", Info).SubText("c0", "in use by request 1").Paint("c1", Info).SubText("c1", "in use by request 2");
                    s.Move("shape:c0", "shape:db", "query", Info);
                })
            .Step("A code path forgets to dispose",
                "An error path skips Dispose. That connection is never returned and stays checked out until the GC finalises the object, if it ever does.",
                "The pool cannot tell a leaked connection from a slow one. Each leak permanently lowers the effective pool size.",
                [1],
                s =>
                {
                    s.Paint("c0", Neutral).SubText("c0", "idle").Paint("c1", Neutral).SubText("c1", "idle");
                    s.Paint("c2", Bad).SubText("c2", "leaked (never disposed)");
                })
            .Step("The pool runs dry",
                "More leaks and a few slow queries take every connection. New requests queue up waiting for one to be returned.",
                "After the connect timeout (15 s by default in SqlClient) callers get 'Timeout expired... max pool size was reached'. The database itself may be idle: the bottleneck is the client-side pool.",
                [2],
                s =>
                {
                    s.Paint("c0", Bad).SubText("c0", "leaked").Paint("c3", Bad).SubText("c3", "leaked");
                    s.Paint("c1", Warn).SubText("c1", "slow query, 30 s");
                    for (var i = 0; i < 5; i++)
                    {
                        s.Box("r" + i, $"request {i + 1}", 40, 100 + i * 66, 200, 50, "waiting...", Warn);
                        s.Connect("r" + i, "pool", dashed: true, tone: Warn, animate: true);
                    }

                    s.Say("Pool exhausted: requests time out while the database sits idle", Bad);
                })
            .Step("Fixes",
                "Always 'await using' connections, put timeouts on commands, and keep transactions short. Cap concurrency upstream so the pool is never the queue.",
                "Watch the pool metrics (active versus idle connections, wait time). Size the pool to what the database can actually serve; a bigger pool in front of a saturated database just moves the queue.",
                [1, 3],
                s =>
                {
                    s.ClearLinks();
                    s.RemoveWhere(sh => sh.Id.StartsWith('r'));
                    for (var i = 0; i < 4; i++)
                    {
                        s.Paint("c" + i, i < 2 ? Info : Neutral).SubText("c" + i, i < 2 ? "in use" : "idle");
                    }

                    s.Say("Every borrow returns: the pool stays healthy", Good);
                })
            .Lesson("Connection pool exhaustion", "Pools and limits", "How a single missing Dispose drains a pool and times out a healthy service.");

    // ------------------------------------------------------------------------------------------
    private static Lesson TokenBucket()
    {
        int[] arrivals = [3, 2, 0, 1, 4, 1, 0, 2];
        const int capacity = 4;
        var tokens = capacity;

        var b = new StoryBuilder("RM2", "A token bucket allows bursts up to its capacity while enforcing a long-run average rate equal to its refill rate. Requests without a token are rejected (HTTP 429) or queued.")
            .Setup(s =>
            {
                s.Region("bucket", "bucket (capacity 4, +1 token per tick)", 30, 60, 260, 200, Info);
                for (var i = 0; i < capacity; i++)
                {
                    s.Circle("tk" + i, "token", 90 + i % 2 * 110, 130 + i / 2 * 80, 30, tone: Good);
                }

                for (var t = 0; t < arrivals.Length; t++)
                {
                    s.Label("tick" + t, $"tick {t}", 330 + t * 80, 495, 70, 11);
                }

                s.Label("stats", "allowed 0   rejected 0", 30, 300, 300, 15);
            })
            .Code("""
                var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 4,                              // burst size
                    TokensPerPeriod = 1,                         // sustained rate
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    QueueLimit = 0                               // reject instead of queueing
                });
                using var lease = await limiter.AcquireAsync(1);
                if (!lease.IsAcquired) return Results.StatusCode(429);
                """);

        b.Step("A full bucket", "The bucket starts full with 4 tokens. Every request must take one token.",
            "This shape is used by API gateways, cloud provider quotas and network shapers. System.Threading.RateLimiting and the ASP.NET Core rate-limiting middleware implement it.",
            [1, 2, 3, 4]);

        int allowed = 0, rejected = 0;
        for (var t = 0; t < arrivals.Length; t++)
        {
            if (t > 0)
            {
                tokens = Math.Min(capacity, tokens + 1);
            }

            var results = new List<bool>();
            for (var k = 0; k < arrivals[t]; k++)
            {
                if (tokens > 0)
                {
                    tokens--;
                    allowed++;
                    results.Add(true);
                }
                else
                {
                    rejected++;
                    results.Add(false);
                }
            }

            var (tick, left, res, al, rj) = (t, tokens, results.ToArray(), allowed, rejected);
            var ok = res.Count(r => r);
            b.Step($"Tick {t}: {arrivals[t]} request{(arrivals[t] == 1 ? string.Empty : "s")}",
                arrivals[t] == 0
                    ? $"No traffic. The bucket refills to {left} tokens."
                    : $"{(t > 0 ? "One token is added. " : string.Empty)}{ok} allowed, {res.Length - ok} rejected. {left} token{(left == 1 ? string.Empty : "s")} left.",
                t == 4 ? "A burst of 4 arrives, but only 2 tokens have built up since the last burst, so 2 are rejected. Bursts are only allowed after quiet periods." : string.Empty,
                [7, 8],
                s =>
                {
                    for (var i = 0; i < capacity; i++)
                    {
                        s.Ghost("tk" + i, i >= left).Paint("tk" + i, i < left ? Good : Neutral);
                    }

                    for (var k = 0; k < res.Length; k++)
                    {
                        s.Box($"q{tick}_{k}", res[k] ? "200" : "429", 330 + tick * 80, 440 - k * 46, 66, 40, tone: res[k] ? Good : Bad);
                    }

                    s.Write("stats", $"allowed {al}   rejected {rj}");
                });
        }

        b.Step("Burst plus average rate",
            $"Over 8 ticks the bucket let through {allowed} requests: the initial 4-token burst plus about one per tick afterwards.",
            "In a distributed system, keep the bucket in a shared store (for example, Redis with an atomic script), or give each node a share of the limit. Always tell clients when to retry (the Retry-After header).",
            [],
            s => s.Say($"Allowed {allowed}, rejected {rejected}: the long-run rate is about 1 per tick", Good));

        return b.Lesson("Rate limiting with a token bucket", "Pools and limits", "Bursts versus sustained rate, simulated tick by tick.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson RetryStorm()
    {
        const double baseY = 460, unit = 1.4;

        void Load(SceneState s, int[] values, string caption, int? outage = null)
        {
            for (var i = 0; i < values.Length; i++)
            {
                var h = Math.Max(4, values[i] * unit);
                var id = "s" + i;
                if (!s.Has(id))
                {
                    s.Box(id, string.Empty, 80 + i * 74, baseY - h, 60, h);
                }

                var over = values[i] > 200;
                s.Place(id, 80 + i * 74, baseY - h).Resize(id, 60, h).Write(id, values[i].ToString())
                 .Paint(id, outage is { } o && i >= o && i < o + 2 ? Bad : over ? Warn : Info);
                s.Label("t" + i, $"{i}s", 100 + i * 74, baseY + 6, 40, 10.5);
            }

            s.Write("cap", caption);
        }

        return new StoryBuilder("RM3", "Retries multiply load exactly when a service is weakest. Use exponential backoff with full jitter, cap the number of retries, and use retry budgets or circuit breakers.")
            .Setup(s =>
            {
                s.Box("capline", null, 60, baseY - 200 * unit, 900, 3, tone: Warn);
                s.Label("caplbl", "server capacity: 200 req/s", 760, baseY - 200 * unit - 24, 220, 11, Warn);
                s.Label("cap", "", 60, 30, 900, 17, Info);
            })
            .Code("""
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try { return await CallAsync(ct); }
                    catch (TransientException)
                    {
                        var cap = Math.Min(30_000, 100 * (1 << attempt));       // exponential
                        await Task.Delay(Random.Shared.Next(0, cap), ct);      // full jitter
                    }
                }
                """)
            .Step("Normal traffic",
                "Clients send about 120 requests per second, comfortably below the server's capacity of 200.",
                "Retries are a good thing for rare, transient failures. The trouble starts when failures are correlated, so everyone retries at the same moment.",
                [3],
                s => Load(s, [120, 118, 122, 121, 119, 120, 122, 118, 121, 120, 119, 121], "steady state"))
            .Step("A 2-second blip, with immediate retries",
                "The server fails for 2 seconds. Every client retries straight away and keeps retrying every second, so load stacks up: new requests plus all the retries.",
                "Even after the blip, the extra retry traffic keeps the server over capacity, so it keeps failing. This is a metastable failure: the trigger is gone, but the system cannot recover by itself.",
                [3, 4],
                s => Load(s, [120, 120, 240, 360, 360, 360, 360, 360, 360, 360, 360, 360], "naive retry every 1 s: overload never ends", 2))
            .Step("Exponential backoff",
                "Wait 1, 2, 4, 8 seconds between attempts. Total retry load drops, but clients that failed together still retry together, causing synchronised spikes.",
                "Doubling the delay gives the server room to recover. Without randomness, though, the retries arrive as a thundering herd at each power-of-two mark.",
                [6],
                s => Load(s, [120, 120, 240, 240, 120, 240, 120, 120, 240, 120, 120, 120], "exponential backoff: spikes at 1, 2, 4, 8 s", 2))
            .Step("Full jitter",
                "Pick a random delay between 0 and the exponential cap. Retries spread out evenly and load stays below capacity, so the server recovers.",
                "AWS's analysis of backoff strategies found that 'full jitter' gives the lowest total work and the fastest completion. Also cap the attempts, retry only idempotent operations (or use idempotency keys), and keep a retry budget (for example, retries no more than 10% of traffic).",
                [6, 7],
                s => Load(s, [120, 120, 150, 160, 155, 150, 140, 135, 130, 125, 122, 120], "exponential backoff + full jitter: smooth recovery", 2))
            .Lesson("Retry storms, backoff and jitter", "Resilience", "How well-meant retries can keep a service down, and the backoff strategy that prevents it.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson CacheStampede() =>
        new StoryBuilder("RM4", "When a hot cache entry expires, every concurrent request can miss at once and hit the database together. Let exactly one caller recompute (single-flight), and serve stale data or refresh early.")
            .Setup(s =>
            {
                for (var i = 0; i < 6; i++)
                {
                    s.Box("u" + i, $"request {i + 1}", 40, 50 + i * 76, 160, 54);
                }

                s.Box("cache", "cache", 400, 210, 180, 90, "key: product:42");
                s.Box("db", "database", 780, 210, 180, 90, "query: 200 ms");
            })
            .Code("""
                // Stampede-prone: every miss runs the factory
                var value = await cache.GetOrCreateAsync(key, _ => LoadFromDbAsync(key));

                // Single-flight: HybridCache coalesces concurrent misses for the same key
                var value = await hybridCache.GetOrCreateAsync(key, ct => LoadFromDbAsync(key, ct));
                """)
            .Step("A hot key served from cache",
                "Thousands of requests per second read the same product, and all of them are served from the cache.",
                "The database sees almost no traffic for this key. Its capacity is sized for that, which is exactly the problem when the cache misses.",
                [2],
                s =>
                {
                    s.Paint("cache", Good);
                    for (var i = 0; i < 6; i++)
                    {
                        s.Move("shape:u" + i, "shape:cache", "hit", Good);
                    }
                })
            .Step("The entry expires",
                "The TTL runs out. All concurrent requests miss at the same instant, and each one starts its own expensive database query.",
                "Six duplicate queries here; in production it can be thousands. The database slows down, queries take longer, more requests pile up, and the stampede feeds itself.",
                [2],
                s =>
                {
                    s.Paint("cache", Warn).SubText("cache", "expired");
                    s.Paint("db", Bad).FillTo("db", 1);
                    for (var i = 0; i < 6; i++)
                    {
                        s.Paint("u" + i, Warn);
                        s.Move("shape:u" + i, "shape:db", "miss: query", Bad);
                    }

                    s.Say("Stampede: every request recomputes the same value", Bad);
                })
            .Step("Fix: single-flight",
                "The first miss takes a per-key lock (or stores a shared Task). The others wait for that one result instead of querying themselves.",
                "IMemoryCache.GetOrCreate does not prevent duplicate factory calls. HybridCache (.NET 9) coalesces concurrent misses for the same key. Lazy<Task<T>> in a ConcurrentDictionary is the manual version (see lesson C3).",
                [5],
                s =>
                {
                    s.FillTo("db", 0.2).Paint("db", Info);
                    s.Paint("u0", Info);
                    for (var i = 1; i < 6; i++)
                    {
                        s.Paint("u" + i, Neutral).SubText("u" + i, "waits for request 1");
                        s.Connect("u" + i, "cache", dashed: true, animate: true, tone: Info);
                    }

                    s.Move("shape:u0", "shape:db", "1 query", Info);
                })
            .Step("Also: refresh before expiry",
                "Serve slightly stale data while one background refresh runs (stale-while-revalidate), and add random jitter to TTLs so keys do not all expire together.",
                "Probabilistic early expiration (the 'XFetch' approach) refreshes hot keys slightly before their TTL, with a probability that grows as expiry approaches.",
                [],
                s =>
                {
                    s.ClearLinks();
                    s.Paint("cache", Good).SubText("cache", "stale-while-revalidate");
                    s.Paint("db", Good);
                    for (var i = 0; i < 6; i++)
                    {
                        s.Paint("u" + i, Good).SubText("u" + i, null);
                    }

                    s.Say("One query per expiry, no user ever waits on the database", Good);
                })
            .Lesson("Cache stampede (thundering herd)", "Resilience", "What happens when a hot cache key expires under load, and single-flight fixes.");

    // ------------------------------------------------------------------------------------------
    private static Lesson CircuitBreaker() =>
        new StoryBuilder("RM5", "A circuit breaker stops calling a failing dependency for a while, so callers fail fast instead of tying up threads on timeouts, and the dependency gets room to recover.")
            .Setup(s =>
            {
                s.Circle("closed", "Closed", 200, 260, 70, "calls flow normally", Good);
                s.Circle("open", "Open", 500, 110, 70, "fail fast", Neutral);
                s.Circle("half", "Half-open", 800, 260, 70, "one trial call", Neutral);
                s.Connect("closed", "open", "failures >= 5");
                s.Connect("open", "half", "after 30 s");
                s.Connect("half", "closed", "trial succeeds");
                s.Connect("half", "open", "trial fails", dashed: true);
                s.Label("count", "failures: 0 / 5", 120, 440, 400, 16);
            })
            .Code("""
                builder.Services.AddHttpClient<PricingClient>()
                    .AddResilienceHandler("pricing", p => p.AddCircuitBreaker(new()
                    {
                        FailureRatio = 0.5,
                        MinimumThroughput = 10,
                        BreakDuration = TimeSpan.FromSeconds(30)
                    }));
                """)
            .Step("Closed: everything flows",
                "Calls go through to the dependency. The breaker counts failures in a sliding window.",
                "Without a breaker, every call to a dead dependency waits for its full timeout (say 30 s), holding a thread, a socket and memory the whole time.",
                [3])
            .Step("Failures cross the threshold: Open",
                "Too many failures in a short time, so the breaker opens. Calls now fail immediately without touching the dependency.",
                "Failing fast frees resources at once and lets callers fall back (cached data, a default, or a degraded feature). It also stops hammering a service that is trying to recover.",
                [4, 5, 6],
                s =>
                {
                    s.Paint("closed", Neutral).Paint("open", Bad);
                    s.RetoneLinks(Bad, l => l.From == "closed" && l.To == "open");
                    s.Write("count", "failures: 5 / 5 -> breaker OPEN");
                })
            .Step("After the break: Half-open",
                "After the break duration, the breaker lets a single trial call through to test the waters.",
                "Only one (or a few) calls are allowed. If every caller retried at once, the recovering dependency could be knocked over again.",
                [7],
                s =>
                {
                    s.RetoneLinks(Neutral);
                    s.Paint("open", Neutral).Paint("half", Warn);
                    s.RetoneLinks(Warn, l => l.From == "open" && l.To == "half");
                    s.Write("count", "trial call in flight");
                })
            .Step("Trial succeeds: Closed again",
                "The dependency answered, so the breaker closes and normal traffic resumes. If the trial had failed, it would open again for another break.",
                "Combine with timeouts (always), retries with jitter (inside the breaker), and bulkheads (separate pools per dependency) so one slow dependency cannot exhaust shared resources.",
                [],
                s =>
                {
                    s.RetoneLinks(Neutral);
                    s.Paint("half", Neutral).Paint("closed", Good);
                    s.RetoneLinks(Good, l => l.From == "half" && l.To == "closed");
                    s.Write("count", "failures: 0 / 5");
                    s.Say("Recovered: fail fast while broken, probe gently, resume", Good);
                })
            .Lesson("Circuit breaker", "Resilience", "Closed, open, half-open: failing fast to protect both caller and dependency.");

    // ------------------------------------------------------------------------------------------
    private static Lesson HandleLeaks() =>
        new StoryBuilder("RM6", "Objects that wrap OS resources (files, sockets, handles) must be disposed deterministically. The GC only notices memory pressure, not handle or socket exhaustion.")
            .Setup(s =>
            {
                s.Region("proc", "your process", 30, 40, 450, 480, Info);
                s.Region("os", "operating system", 520, 40, 450, 480, Warn);
                s.Table("ht", "handle / socket table", 560, 80, 380, "(empty)");
            })
            .Code("""
                var text = new StreamReader(File.OpenRead(path)).ReadToEnd();   // handle leaked until GC

                using var reader = new StreamReader(File.OpenRead(path));        // closed at end of scope

                var http = new HttpClient();                                      // per request: socket churn
                static readonly HttpClient Shared = new(new SocketsHttpHandler
                    { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });      // reuse + DNS refresh
                """)
            .Step("A FileStream wraps a kernel handle",
                "File.OpenRead asks the OS to open the file and returns a FileStream that holds the OS handle.",
                "The managed object is tiny. The real cost lives in the kernel: an open file object, a file lock (on Windows, by default), and a slot in a limited table.",
                [1],
                s =>
                {
                    s.Box("fs1", "FileStream", 70, 100, 180, 50, tone: Info);
                    s.Table("ht", "handle / socket table", 560, 80, 380, "0x1A4  file  report.csv").HotRow("ht", 0);
                    s.Connect("fs1", "ht");
                })
            .Step("Forget Dispose: the handle stays open",
                "Nothing closes the handle when you stop using the stream. It stays open until a GC happens to run the finaliser, which could be seconds or hours later.",
                "Meanwhile the file stays locked ('being used by another process'), and with plenty of free memory the GC may not run for a long time. Memory pressure triggers GCs; handle pressure does not.",
                [1],
                s =>
                {
                    s.Paint("fs1", Warn).SubText("fs1", "unreferenced, not finalised");
                    for (var i = 2; i <= 5; i++)
                    {
                        s.Box("fs" + i, "FileStream", 70, 40 + i * 70, 180, 50, "leaked", Warn);
                    }

                    s.Table("ht", "handle / socket table", 560, 80, 380,
                        "0x1A4  file  report.csv", "0x1A8  file  report.csv", "0x1AC  file  report.csv", "0x1B0  file  report.csv", "0x1B4  file  report.csv");
                })
            .Step("Same story with sockets: new HttpClient per request",
                "Creating and disposing an HttpClient per request opens a new connection each time. Closed sockets linger in TIME_WAIT for up to minutes.",
                "Under load, thousands of sockets in TIME_WAIT exhaust the ephemeral port range (about 16k-28k ports) and requests fail with SocketException. Linux processes also hit 'Too many open files' (often a 1024 soft limit).",
                [4],
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("fs", StringComparison.Ordinal));
                    s.ClearLinks();
                    s.Table("ht", "handle / socket table", 560, 80, 380,
                        "socket :50112  TIME_WAIT", "socket :50113  TIME_WAIT", "socket :50114  TIME_WAIT", "socket :50115  TIME_WAIT", "socket :50116  TIME_WAIT", "... 28,000 more", "connect: address already in use")
                     .HotRow("ht", 6).Paint("ht", Bad);
                    s.Box("hc", "new HttpClient() x 28,000", 70, 120, 360, 60, tone: Bad);
                    s.Say("Port and handle exhaustion while memory looks fine", Bad);
                })
            .Step("Fix: dispose deterministically and reuse",
                "Wrap disposable resources in 'using' so they close at the end of the scope. Share one long-lived HttpClient (or use IHttpClientFactory) so connections are pooled.",
                "Set PooledConnectionLifetime so pooled connections are recycled and DNS changes are picked up. Code analysers CA2000 (dispose objects before losing scope) and CA2213 catch most leaks at build time.",
                [3, 5, 6],
                s =>
                {
                    s.Remove("hc");
                    s.Box("shared", "static HttpClient (pooled)", 70, 120, 360, 60, tone: Good);
                    s.Table("ht", "handle / socket table", 560, 80, 380, "socket :50112  ESTABLISHED (reused)", "socket :50113  ESTABLISHED (reused)").Paint("ht", Good);
                    s.Connect("shared", "ht", tone: Good);
                    s.Say("A few long-lived connections serve every request", Good);
                })
            .Lesson("Leaking handles and sockets", "Pools and limits", "IDisposable, finalisers, TIME_WAIT and the classic HttpClient mistake.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Queueing()
    {
        (int Util, double Wait)[] points = [(50, 2), (70, 3.3), (80, 5), (90, 10), (95, 20), (99, 100)];

        void Bars(SceneState s, int upTo)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var (u, w) = points[i];
                var h = Math.Min(380, w * 12);
                var id = "w" + i;
                if (!s.Has(id))
                {
                    s.Box(id, string.Empty, 110 + i * 140, 470, 100, 1);
                }

                var visible = i < upTo;
                s.Place(id, 110 + i * 140, 470 - (visible ? h : 1)).Resize(id, 100, visible ? h : 1)
                 .Write(id, visible ? $"{w * 10:0} ms" : string.Empty)
                 .Paint(id, w >= 10 ? Bad : w >= 5 ? Warn : Good).Ghost(id, !visible);
                s.Label("x" + i, $"{u}% busy", 120 + i * 140, 480, 100, 12);
            }
        }

        return new StoryBuilder("RM7", "Waiting time grows as 1 / (1 - utilisation). Past about 80% busy, latency explodes. Keep headroom, bound your queues, and shed load early.")
            .Setup(s => s.Label("title", "average time in system, service time = 10 ms (M/M/1 queue)", 60, 40, 900, 16, Info))
            .NoCode()
            .Step("One server, random arrivals",
                "Requests arrive at random and each takes 10 ms on average to serve. At 50% utilisation, a request spends 20 ms in the system on average.",
                "For an M/M/1 queue, time in system W = S / (1 - p), where S is the service time and p is the utilisation. Randomness alone creates queues, even when average capacity is plenty.",
                s => Bars(s, 1))
            .Step("Busier: 70% and 80%",
                "At 80% busy, latency is already 5x the service time.",
                "Each extra percent of load costs more latency than the one before. Tail latency (p99) grows even faster than the average.",
                s => Bars(s, 3))
            .Step("The hockey stick: 90-99%",
                "At 90% it is 100 ms; at 99% it is a full second. Same server, same code: only the load changed.",
                "This is why services fall off a cliff without warning, and why autoscaling on 'CPU > 90%' reacts too late.",
                s =>
                {
                    Bars(s, 6);
                    s.Say("Latency explodes as utilisation approaches 100%", Bad);
                })
            .Step("Little's law and what to do",
                "Little's law: items in the system = arrival rate x time in system. Longer waits mean more concurrent requests holding memory, threads and connections.",
                "Plan for 60-75% utilisation on latency-critical paths. Use bounded queues with load shedding (reject early with 429/503) rather than unbounded queues that turn overload into ever-growing latency.",
                s => s.Say("Keep headroom, bound queues, shed load early", Good))
            .Lesson("Queueing: why 90% utilisation hurts", "Pools and limits", "The 1 / (1 - p) curve behind sudden latency cliffs, and Little's law.");
    }
}
