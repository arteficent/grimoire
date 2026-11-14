using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class SystemDesignLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return Rest();
        yield return ApiStyles();
        yield return Acid();
        yield return Isolation();
        yield return CapQuorum();
        yield return Replication();
        yield return Sharding();
        yield return LoadBalancing();
        yield return Caching();
        yield return Queues();
        yield return DistributedTransactions();
        yield return Raft();
        yield return Observability();
        yield return UrlShortener();
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Rest() =>
        new StoryBuilder("SY1", "REST models an API as resources addressed by URLs and changed with a few standard HTTP verbs. Status codes, statelessness and idempotency make it cacheable, easy to scale out, and safe to retry.")
            .Setup(s =>
            {
                s.Lifelines("client", "Orders API", 300);
                s.Table("verbs", "resources and verbs", 60, 330, 880,
                    "GET     /orders/42                 read one order               safe, idempotent",
                    "GET     /orders?status=open&cursor=abc   list, filtered, paged",
                    "POST    /orders                    create (server picks the id)  not idempotent",
                    "PUT     /orders/42                 replace the whole order      idempotent",
                    "PATCH   /orders/42                 change some fields",
                    "DELETE  /orders/42                 remove                       idempotent");
            })
            .Code("""
                app.MapGet("/orders/{id:int}", async (int id, IOrderStore store) =>
                    await store.Find(id) is { } order ? Results.Ok(order) : Results.NotFound());

                app.MapPost("/orders", async (CreateOrder cmd, IOrderStore store) =>
                {
                    var order = await store.Create(cmd);
                    return Results.Created($"/orders/{order.Id}", order);          // 201 + Location header
                });
                """)
            .Step("Resources, not actions",
                "URLs name things (orders, order 42); HTTP verbs say what to do with them. Reading order 42 is GET /orders/42, answered with 200 OK and a JSON representation.",
                "Because the meaning of GET is standard, every proxy, CDN and browser knows it is safe to cache and retry. RPC-style URLs like /getOrder?id=42 or /deleteOrder lose all of that.",
                [1, 2],
                s =>
                {
                    s.Msg("msg1", true, 110, "GET /orders/42").Msg("msg2", false, 190, "200 OK  { id: 42, status: \"paid\" }", Good);
                    s.HotRow("verbs", 0);
                })
            .Step("Create with POST",
                "POST to the collection creates a new order. The server answers 201 Created with a Location header pointing at the new resource.",
                "POST is the one verb that is neither safe nor idempotent: sending it twice creates two orders. That matters as soon as networks drop responses (two steps from now).",
                [4, 5, 6, 7, 8],
                s =>
                {
                    s.ClearMessages();
                    s.Msg("msg1", true, 110, "POST /orders  { items: [...] }").Msg("msg2", false, 190, "201 Created  Location: /orders/43", Good);
                    s.HotRow("verbs", 2);
                })
            .Step("Status codes tell the story",
                "2xx means success, 3xx redirect, 4xx 'you sent something wrong', 5xx 'we failed'. Clients and monitoring rely on these classes.",
                "Returning 200 with { error: ... } in the body breaks retries, caching, metrics and alerting. Use 404 for missing, 409 for conflicts, 422 for validation, 429 for rate limits, 503 when overloaded.",
                [2],
                s =>
                {
                    s.ClearMessages();
                    s.Msg("msg1", true, 100, "GET /orders/999").Msg("msg2", false, 150, "404 Not Found", Warn)
                        .Msg("msg3", true, 200, "POST /orders  { qty: -1 }").Msg("msg4", false, 250, "422 Unprocessable Content", Warn);
                    s.Table("verbs", "status code classes", 60, 330, 880,
                        "2xx  success        200 OK, 201 Created, 204 No Content",
                        "3xx  redirection    301 Moved Permanently, 304 Not Modified",
                        "4xx  client error   400, 401, 403, 404, 409 Conflict, 422, 429 Too Many Requests",
                        "5xx  server error   500, 502 Bad Gateway, 503 Unavailable, 504 Gateway Timeout");
                })
            .Step("Stateless requests",
                "Each request carries everything needed to handle it: the token, ids, the page cursor. The server keeps no conversation state between requests.",
                "Statelessness is what lets a load balancer send each request to any instance, lets instances be added or killed freely, and lets responses be cached by URL.",
                [],
                s =>
                {
                    s.ClearMessages();
                    s.Write("ls", "Orders API (any of 3 instances)");
                    s.Msg("msg1", true, 110, "GET /orders?cursor=abc  Authorization: Bearer eyJ...").Msg("msg2", false, 190, "200 OK  + next cursor", Good);
                    s.Table("verbs", "stateless", 60, 330, 880,
                        "every request carries what it needs: token, ids, cursor",
                        "no server session to lose when an instance restarts",
                        "any instance can answer: scale out behind a load balancer",
                        "responses can be cached by URL (Cache-Control, ETag)");
                })
            .Step("Idempotency keys make retries safe",
                "The client POSTs a payment with an Idempotency-Key. The response is lost, so it retries with the same key. The server recognises the key and returns the stored result instead of charging twice.",
                "Networks lose responses all the time, and clients cannot tell 'not processed' from 'processed but the answer was lost'. Idempotency keys turn at-least-once retries into exactly-once effects. Payment APIs such as Stripe use this pattern.",
                [],
                s =>
                {
                    s.ClearMessages();
                    s.Write("ls", "Payments API");
                    s.Msg("msg1", true, 100, "POST /payments  Idempotency-Key: k-81")
                        .Msg("msg2", false, 150, "201 Created", lost: true)
                        .Msg("msg3", true, 200, "retry: POST, same key k-81", Warn)
                        .Msg("msg4", false, 250, "201: same payment, no double charge", Good);
                    s.Table("verbs", "idempotency key on the server", 60, 330, 880,
                        "first request: process, store (key -> response) for 24 h",
                        "same key again: return the stored response, do nothing else",
                        "same key with a different body: reject with 422");
                })
            .Step("API hygiene checklist",
                "Good APIs are boring and predictable: versioned, paginated, filterable, with consistent errors and an explicit contract.",
                "ETag + If-Match gives optimistic concurrency (412 Precondition Failed when someone else changed the resource). Problem Details (RFC 9457) standardises error bodies. An OpenAPI document lets clients be generated and changes be diffed in CI.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("hygiene", "API design checklist", 60, 60, 880,
                        "versioning          /v2/orders or a version header; never break v1 silently",
                        "pagination          cursor-based for large or changing lists",
                        "filtering, sorting  ?status=open&sort=-createdAt",
                        "concurrency         ETag + If-Match -> 412 if changed meanwhile",
                        "rate limits         429 + Retry-After and RateLimit headers",
                        "errors              Problem Details (RFC 9457), stable error codes",
                        "contract            OpenAPI document, checked in CI for breaking changes");
                })
            .Lesson("REST and HTTP API design", "APIs", "Resources and verbs, status codes, statelessness, idempotency keys and API hygiene.");

    // ------------------------------------------------------------------------------------------
    private static Lesson ApiStyles() =>
        new StoryBuilder("SY2", "REST, GraphQL, gRPC, WebSockets, server-sent events, webhooks and messaging each fit a different shape of communication. Choose by who drives the data shape, how much traffic there is, and whether the server must push.")
            .Setup(s =>
            {
                s.Box("app", "mobile app: profile screen", 40, 230, 240, 70, "needs name + last 3 order totals");
                s.Box("api", "REST API", 720, 230, 240, 70);
            })
            .Code("""
                # GraphQL: the client chooses the shape
                query { user(id: 7) { name orders(last: 3) { total } } }

                // gRPC: a typed contract, binary on the wire, streaming built in
                service Orders {
                  rpc GetOrder (OrderId) returns (Order);
                  rpc Watch (OrderId) returns (stream OrderEvent);
                }
                """)
            .Step("REST: over-fetching and round trips",
                "The screen needs a name and three totals. REST returns whole resources, so the app makes three requests and downloads dozens of unused fields.",
                "On mobile networks every round trip can cost 100 ms or more. Backend-for-frontend (BFF) endpoints, sparse fieldsets (?fields=) or embedding related data are the REST answers.",
                [],
                s =>
                {
                    s.Move("shape:app", "shape:api", "GET /users/7", Info).Move("shape:api", "shape:app", "user: 40 fields", Warn);
                    s.Table("trips", "3 round trips", 40, 380, 920, "GET /users/7                 -> 40 fields, 1 needed", "GET /users/7/orders?limit=3  -> 3 orders with every field", "GET /orders/{id}/totals ...  -> more requests");
                    s.Say("3 round trips; most fields unused", Warn);
                })
            .Step("GraphQL: the client asks for exactly what it needs",
                "One query describes the exact shape: the user's name and the totals of the last three orders. One round trip, no unused fields.",
                "Costs: resolvers can trigger N+1 database queries (batch them with a DataLoader), HTTP caching is harder because queries are POSTs, and expensive queries need depth and cost limits.",
                [1, 2],
                s =>
                {
                    s.Remove("trips");
                    s.Write("api", "GraphQL endpoint");
                    s.Box("q", "{ user(id: 7) { name\n  orders(last: 3) { total } } }", 320, 60, 360, 90, "one query", Info);
                    s.Shape("q").Mono = true;
                    s.Move("shape:app", "shape:api", "POST /graphql", Info).Move("shape:api", "shape:app", "exactly those fields", Good);
                })
            .Step("gRPC: typed, binary, streaming",
                "Services share a .proto contract; code is generated for both sides. Messages are binary Protocol Buffers over HTTP/2, with streaming in either direction.",
                "The order { id: 42, status: PAID, total: 1999 } is 38 bytes as JSON and 7 bytes as protobuf (field tags + varints). gRPC fits internal service-to-service calls; browsers need gRPC-Web or a gateway.",
                [4, 5, 6, 7, 8],
                s =>
                {
                    s.Wipe();
                    s.Table("proto", "orders.proto", 40, 60, 460, "service Orders {", "  rpc GetOrder (OrderId) returns (Order);", "  rpc Watch (OrderId) returns (stream OrderEvent);", "}");
                    s.LabeledBars("size", [("JSON", 38, "38 bytes"), ("protobuf", 7, "7 bytes")], 600, 420, 140, 60, 300);
                    s.Paint("size1", Good);
                })
            .Step("Pushing data to clients",
                "Polling asks repeatedly; long polling waits for news; server-sent events stream one way over HTTP; WebSockets are full duplex; webhooks call another server back.",
                "Use SSE for live feeds and notifications (simple, works with HTTP infrastructure), WebSockets for chat, games and collaboration, and webhooks for server-to-server events, signed and retried.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("push", "server push options", 40, 60, 920,
                        "polling            client asks every N seconds       simple, wasteful, delayed",
                        "long polling       request held open until news      works everywhere, one event per request",
                        "SSE                one-way stream over HTTP          live feeds, LLM token streaming",
                        "WebSockets         full-duplex connection            chat, games, collaborative editing",
                        "webhooks           server calls your URL             'payment succeeded', signed + retried",
                        "message broker     publish/subscribe between services  decoupled, durable (see Queues)");
                })
            .Step("Choosing",
                "No style wins everywhere. Many systems use REST at the edge, gRPC inside, events between services and SSE or WebSockets for live UI.",
                "Whatever the style, the same disciplines apply: explicit contracts, versioning, timeouts, idempotency and observability.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("pick", "rule of thumb", 40, 60, 920,
                        "public API for many clients        REST + JSON + OpenAPI",
                        "client-driven UIs, many screens    GraphQL (or a BFF per client)",
                        "internal high-volume calls         gRPC",
                        "live updates to browsers           SSE or WebSockets",
                        "notify other companies' systems    webhooks",
                        "decouple services in time          message queue / event stream");
                })
            .Lesson("API styles: REST, GraphQL, gRPC, WebSockets, webhooks", "APIs", "Over-fetching, typed binary contracts, streaming and server push, and when to use each.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Acid()
    {
        void Accounts(SceneState s, string a, string b, Tone tone = Neutral)
        {
            s.Table("acc", "accounts", 40, 60, 300, a, b);
            s.Paint("acc", tone);
        }

        void Wal(SceneState s, params (string Text, Tone Tone)[] entries)
        {
            for (var i = 0; i < 5; i++)
            {
                var (text, tone) = i < entries.Length ? entries[i] : (string.Empty, Neutral);
                s.Write("wal" + i, text).Paint("wal" + i, tone).Ghost("wal" + i, i >= entries.Length);
            }
        }

        return new StoryBuilder("SY3", "A transaction makes several changes all-or-nothing (atomicity), keeps rules intact (consistency), hides half-done work from others (isolation) and survives crashes once committed (durability). A write-ahead log is how databases deliver A and D.")
            .Setup(s =>
            {
                Accounts(s, "A   500", "B   100");
                s.Table("tx", "transaction T1", 400, 60, 560, "BEGIN", "UPDATE accounts SET balance = balance - 100 WHERE id = 'A'", "UPDATE accounts SET balance = balance + 100 WHERE id = 'B'", "COMMIT");
                s.Caption("walcap", "write-ahead log (append-only, flushed on commit)", 40, 300, 500);
                s.Cells("wal", Enumerable.Repeat(string.Empty, 5).ToArray(), 40, 330, 172, 56, 10, tags: false);
                Wal(s);
            })
            .Code("""
                await using var tx = await db.Database.BeginTransactionAsync();
                from.Balance -= 100;                      // both changes...
                to.Balance   += 100;
                await db.SaveChangesAsync();
                await tx.CommitAsync();                   // ...become visible together, or not at all
                """)
            .Step("Two writes that belong together",
                "Moving 100 from A to B is two updates. Either both must happen or neither; money must never appear or vanish.",
                "Any operation that touches several rows, tables or documents and must stay consistent needs a transaction (or a careful single-row design).",
                [1, 2, 3],
                s => s.HotRow("tx", 1, 2))
            .Step("Crash halfway without a transaction",
                "A is debited, then the server loses power before B is credited. 100 has vanished.",
                "Without atomicity, every multi-step change needs hand-written repair logic, and partial failures are guaranteed at scale.",
                [],
                s =>
                {
                    Accounts(s, "A   400   (debited)", "B   100   (never credited)", Bad);
                    s.Box("crash", "power loss", 400, 230, 240, 50, tone: Bad);
                    s.Say("100 vanished", Bad);
                })
            .Step("The write-ahead log",
                "Before changing data pages, the database appends each change to a log. COMMIT returns only after the log is flushed to disk.",
                "Sequential log appends are fast even on slow disks; data pages are written back later, in the background. The log is also what replication ships to replicas.",
                [4, 5],
                s =>
                {
                    s.Remove("crash");
                    Accounts(s, "A   400", "B   200", Good);
                    Wal(s, ("T1 BEGIN", Info), ("A: 500 -> 400", Info), ("B: 100 -> 200", Info), ("T1 COMMIT", Good));
                    s.HotRow("tx", 3);
                    s.Say("COMMIT returns after the log is safely on disk: durability", Good);
                })
            .Step("Recovery replays the log",
                "T2 starts and debits A, then the server crashes before COMMIT. On restart, recovery redoes committed T1 and undoes uncommitted T2.",
                "This is ARIES-style redo/undo recovery, used in some form by SQL Server, PostgreSQL, MySQL InnoDB and others. Atomicity and durability come from the same log.",
                [],
                s =>
                {
                    Wal(s, ("T1 BEGIN", Info), ("A: 500 -> 400", Info), ("B: 100 -> 200", Info), ("T1 COMMIT", Good), ("T2: A 400 -> 300, no COMMIT", Warn));
                    s.Box("crash", "crash, then recovery", 400, 230, 260, 50, tone: Warn);
                    Accounts(s, "A   400   (T2 undone)", "B   200   (T1 kept)", Good);
                })
            .Step("All four letters",
                "Atomicity and durability come from the log. Consistency comes from constraints. Isolation controls what concurrent transactions see of each other.",
                "Isolation is the subtle one: databases offer several levels, trading anomalies for throughput (next lesson). 'Consistency' here is unrelated to the C in CAP.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("acid", "ACID", 60, 60, 880,
                        "Atomicity    all or nothing                 log + undo on failure",
                        "Consistency  rules always hold              CHECK (balance >= 0), foreign keys, unique",
                        "Isolation    concurrent work does not clash  isolation levels, locks, MVCC",
                        "Durability   committed = survives crashes    log flushed to disk, replicas");
                })
            .Lesson("ACID transactions and the write-ahead log", "Data and consistency", "All-or-nothing transfers, crash recovery from the log, and what each ACID letter means.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Isolation()
    {
        void Timeline(SceneState s, string[] a, string[] b, string rowText, Tone rowTone)
        {
            for (var i = 0; i < 6; i++)
            {
                s.Write("a" + i, a[i]).Ghost("a" + i, a[i].Length == 0).Paint("a" + i, a[i].Length == 0 ? Neutral : Info);
                s.Write("b" + i, b[i]).Ghost("b" + i, b[i].Length == 0).Paint("b" + i, b[i].Length == 0 ? Neutral : Warn);
            }

            s.Write("row", rowText).Paint("row", rowTone);
        }

        return new StoryBuilder("SY4", "Concurrent transactions can see each other's partial work in surprising ways. Isolation levels choose which anomalies are allowed; MVCC lets readers see a consistent snapshot without blocking writers.")
            .Setup(s =>
            {
                s.Caption("ta", "T1", 40, 92, 60);
                s.Caption("tb", "T2", 40, 182, 60);
                for (var i = 0; i < 6; i++)
                {
                    s.Label("t" + i, $"t{i + 1}", 140 + i * 136 + 54, 40, 40, 11);
                }

                s.Cells("a", Enumerable.Repeat(string.Empty, 6).ToArray(), 140, 70, 130, 60, 6, tags: false);
                s.Cells("b", Enumerable.Repeat(string.Empty, 6).ToArray(), 140, 160, 130, 60, 6, tags: false);
                s.Box("row", string.Empty, 340, 300, 320, 60, "the row in the database");
            })
            .Code("""
                -- lost update: both read 10, both write 9
                UPDATE products SET stock = stock - 1 WHERE id = 7;      -- fix 1: atomic update
                SELECT stock FROM products WHERE id = 7 FOR UPDATE;      -- fix 2: lock the row
                UPDATE products SET stock = 9, version = 4
                WHERE id = 7 AND version = 3;                            -- fix 3: optimistic check (0 rows = conflict)

                SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
                """)
            .Step("Lost update",
                "Two buyers read stock 10 at the same time. Each writes 10 - 1 = 9. Two items sold, but the stock only dropped by one.",
                "This happens under read committed, the default in PostgreSQL, SQL Server and Oracle. Fixes: an atomic UPDATE, SELECT ... FOR UPDATE, or optimistic concurrency with a version column (EF Core concurrency tokens).",
                [1, 2, 3, 4, 5],
                s => Timeline(s, ["read 10", "", "write 9", "commit", "", ""], ["", "read 10", "", "", "write 9", "commit"], "stock = 9   (should be 8)", Bad))
            .Step("Dirty read",
                "T1 sets a price to 0 by mistake and then rolls back. Meanwhile T2 read the uncommitted 0 and charged a customer nothing.",
                "Only read uncommitted (NOLOCK in SQL Server) allows dirty reads. Every other level only shows committed data.",
                [],
                s => Timeline(s, ["price = 0", "", "rollback", "", "", ""], ["", "read 0", "charge 0", "commit", "", ""], "price = 25 again, order charged 0", Bad))
            .Step("Non-repeatable read and phantoms",
                "T1 counts open orders twice in one transaction and gets 5, then 6, because T2 inserted and committed an order in between.",
                "Reports and checks that read the same data twice can see different worlds. Repeatable read prevents changed rows; serializable also prevents new matching rows (phantoms).",
                [],
                s => Timeline(s, ["count = 5", "", "", "count = 6 ?!", "commit", ""], ["", "insert order", "commit", "", "", ""], "open orders: 6", Warn))
            .Step("MVCC: snapshots instead of locks",
                "With multi-version concurrency control, an update writes a new row version. T1's snapshot keeps seeing the version that was committed when it started, while T2 creates the next one.",
                "Readers never block writers and writers never block readers. Old versions are cleaned up later (VACUUM in PostgreSQL, the version store in SQL Server's snapshot isolation).",
                [],
                s =>
                {
                    Timeline(s, ["snapshot @100", "read 10", "", "read 10", "commit", ""], ["", "", "write 9 (v2)", "commit", "", ""], "versions: v1 stock=10 | v2 stock=9", Info);
                    s.Box("v1", "v1: stock = 10   (visible to snapshots < 105)", 100, 420, 380, 50, tone: Info);
                    s.Box("v2", "v2: stock = 9    (committed by txn 105)", 600, 420, 360, 50, tone: Good);
                    s.Connect("v2", "v1", "previous version", dashed: true);
                })
            .Step("Isolation levels",
                "Each level forbids more anomalies and costs more in locking, aborts or retries. Serializable behaves as if transactions ran one at a time.",
                "Write skew (two on-call doctors each see the other still on call and both leave) is only prevented by serializable. Under serializable, be ready to retry transactions that fail with serialization errors. MySQL InnoDB defaults to repeatable read.",
                [7],
                s =>
                {
                    s.Wipe();
                    s.Table("levels", "isolation levels vs anomalies", 40, 60, 920,
                        "level               dirty read   non-repeatable   phantom    write skew",
                        "read uncommitted    possible     possible         possible   possible",
                        "read committed      -            possible         possible   possible",
                        "repeatable read     -            -                possible*  possible",
                        "snapshot            -            -                -          possible",
                        "serializable        -            -                -          -");
                    s.Label("note", "* PostgreSQL's repeatable read is snapshot isolation, which also prevents phantoms", 40, 280, 920, 12);
                })
            .Lesson("Isolation levels, anomalies and MVCC", "Data and consistency", "Lost updates, dirty and non-repeatable reads, snapshots, and choosing an isolation level.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson CapQuorum()
    {
        (string Id, double X, double Y)[] nodes = [("A", 250, 170), ("B", 500, 390), ("C", 750, 170)];

        void Values(SceneState s, string a, string b, string c)
        {
            s.SubText("A", a).SubText("B", b).SubText("C", c);
        }

        return new StoryBuilder("SY5", "Replicated data forces trade-offs. During a network partition a system must choose consistency (refuse some requests) or availability (answer with possibly stale data). Quorums with R + W > N give consistent reads without a single leader.")
            .Setup(s =>
            {
                foreach (var n in nodes)
                {
                    s.Circle(n.Id, n.Id, n.X, n.Y, 56, "x = 0");
                }

                s.Connect("A", "B", arrow: false).Connect("B", "C", arrow: false).Connect("A", "C", arrow: false);
                s.Box("c1", "client 1", 40, 40, 160, 50, tone: Info);
                s.Box("c2", "client 2", 800, 460, 160, 50, tone: Info);
            })
            .NoCode()
            .Step("Three replicas",
                "Client 1 writes x = 1 to A, which replicates it to B and C. All three now agree.",
                "Replicas give fault tolerance and read capacity. The hard part is keeping them in agreement when messages are delayed or lost.",
                s =>
                {
                    s.Move("shape:c1", "shape:A", "x = 1", Info).Move("shape:A", "shape:B", "replicate", Info).Move("shape:A", "shape:C", "replicate", Info);
                    Values(s, "x = 1", "x = 1", "x = 1");
                })
            .Step("A network partition",
                "The link between A and the others fails. A can still talk to client 1, and B and C can still talk to client 2, but the two sides cannot talk to each other.",
                "Partitions are not hypothetical: switch failures, misconfigured firewalls, GC pauses and cloud zone outages all look like partitions. A distributed system must decide in advance what to do.",
                s =>
                {
                    Values(s, "x = 1", "x = 1", "x = 1");
                    s.Box("cut", null, 372, 30, 6, 500, tone: Bad);
                    s.Disconnect("A", "B").Disconnect("A", "C");
                    s.Connect("A", "B", "partitioned", Bad, dashed: true, arrow: false).Connect("A", "C", tone: Bad, dashed: true, arrow: false);
                })
            .Step("Choose consistency (CP)",
                "Client 1 writes x = 2 to A. A cannot reach a majority, so it refuses the write. Client 2 reads x = 1 from B, which is still the latest committed value.",
                "CP systems (etcd, ZooKeeper, Spanner, most SQL primaries with synchronous failover) stay correct but become unavailable on the minority side until the partition heals.",
                s =>
                {
                    Values(s, "x = 1", "x = 1", "x = 1");
                    s.Paint("A", Warn);
                    s.Move("shape:c1", "shape:A", "x = 2", Info).Move("shape:A", "shape:c1", "rejected: no majority", Warn);
                    s.Move("shape:c2", "shape:B", "read x", Info).Move("shape:B", "shape:c2", "1", Good);
                    s.Say("CP: the minority side stops accepting writes", Info);
                })
            .Step("Choose availability (AP)",
                "A accepts x = 2 anyway. Client 2 still reads x = 1 from B: stale. When the partition heals, the replicas must reconcile conflicting histories.",
                "AP systems (Dynamo-style stores, Cassandra, DNS, shopping carts) always answer, then converge later via last-writer-wins, version vectors or CRDTs. Fine for likes and carts, not for bank balances.",
                s =>
                {
                    Values(s, "x = 2", "x = 1", "x = 1");
                    s.Paint("A", Warn).Paint("B", Bad);
                    s.Move("shape:c1", "shape:A", "x = 2", Info).Move("shape:A", "shape:c1", "ok", Good);
                    s.Move("shape:c2", "shape:B", "read x", Info).Move("shape:B", "shape:c2", "1 (stale)", Bad);
                    s.Say("AP: always answers, sometimes stale or conflicting", Warn);
                })
            .Step("Quorums: R + W > N",
                "The partition heals. With N = 3 replicas, a write waits for W = 2 acks and a read asks R = 2 replicas. Every read set overlaps every write set, so a read always sees the newest version.",
                "Here x = 3 reached A and B; C still has an old value. Reading B and C returns versions 3 and 1, and the client keeps the newer. Tuning W and R trades write latency against read latency.",
                s =>
                {
                    s.Remove("cut");
                    s.ClearLinks();
                    s.Connect("A", "B", arrow: false).Connect("B", "C", arrow: false).Connect("A", "C", arrow: false);
                    Values(s, "x = 3 (v3)", "x = 3 (v3)", "x = 1 (v1)");
                    s.PaintAll(Good, "A", "B").Paint("C", Warn);
                    s.Move("shape:c2", "shape:B", "read", Info).Move("shape:c2", "shape:C", "read", Info).Move("shape:B", "shape:c2", "v3 wins", Good);
                    s.Say("W = 2, R = 2, N = 3: every read overlaps the last write", Good);
                })
            .Step("Consistency models and PACELC",
                "Consistency is a spectrum. Stronger models are easier to program against; weaker ones are faster and more available.",
                "PACELC extends CAP: if Partitioned choose A or C, Else choose Latency or Consistency. Even with no failures, synchronous cross-region replication costs a round trip on every write.",
                s =>
                {
                    s.Wipe();
                    s.Table("models", "consistency models (strongest first)", 40, 60, 920,
                        "linearizable       acts like a single copy; reads see the latest write   locks, leader election",
                        "sequential         everyone sees the same order of operations",
                        "causal             effects are never seen before their causes            comments after posts",
                        "read-your-writes   you always see your own updates                       profile edits",
                        "eventual           replicas converge once writes stop                    likes, DNS, carts");
                })
            .Lesson("CAP, quorums and consistency models", "Data and consistency", "What replicas do during a partition, quorum reads and writes, and the consistency spectrum.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Replication() =>
        new StoryBuilder("SY6", "Leader-follower replication copies every write from one leader to followers. It scales reads and survives failures, but asynchronous lag breaks read-your-writes, and failover can lose writes or create two leaders unless it is fenced.")
            .Setup(s =>
            {
                s.Box("L", "leader", 380, 60, 240, 70, "accepts writes", Info);
                s.Box("F1", "follower 1", 120, 330, 240, 70, "read-only");
                s.Box("F2", "follower 2", 640, 330, 240, 70, "read-only");
                s.Box("user", "user", 40, 180, 160, 56);
                s.Connect("L", "F1", "replication log").Connect("L", "F2", "replication log");
            })
            .NoCode()
            .Step("Leader and followers",
                "All writes go to the leader, which streams its log to the followers. Reads can be spread across followers to scale.",
                "This is how PostgreSQL streaming replication, MySQL replicas, SQL Server availability groups, MongoDB replica sets and Redis replicas work.",
                s =>
                {
                    s.Move("shape:user", "shape:L", "UPDATE", Info).Move("shape:L", "shape:F1", "log", Info).Move("shape:L", "shape:F2", "log", Info);
                })
            .Step("Replication lag breaks read-your-writes",
                "The user renames their profile, then immediately reloads. The read goes to follower 1, which is 2 seconds behind and still has the old name.",
                "Fixes: read your own recent writes from the leader, pass a 'last written position' token and wait for a follower to catch up, or pin each user to one follower for monotonic reads.",
                s =>
                {
                    s.Move("shape:user", "shape:L", "name = \"Ana\"", Info).Move("shape:F1", "shape:user", "name = \"Anna\" (old)", Bad);
                    s.SubText("F1", "lag 2 s").Paint("F1", Bad);
                    s.Say("The user does not see their own change", Bad);
                })
            .Step("Synchronous vs asynchronous",
                "Synchronous: the leader waits for a follower before confirming, so no committed write is lost, but every write pays the extra round trip. Asynchronous: fast, but recent writes can be lost on failover.",
                "Common middle ground: semi-synchronous, with one synchronous follower and the rest asynchronous. Cloud databases often replicate synchronously across zones and asynchronously across regions.",
                s =>
                {
                    s.Paint("F1", Good).SubText("F1", "synchronous: confirmed before commit");
                    s.Paint("F2", Warn).SubText("F2", "asynchronous: may lag");
                    s.Disconnect("L", "F1").Connect("L", "F1", "sync ack", Good);
                })
            .Step("Failover",
                "The leader crashes. Follower 1 is promoted. Clients are redirected to the new leader. Any writes the old leader had not replicated are lost.",
                "Split brain: if the old leader was only paused and comes back still believing it leads, two nodes accept writes. Epoch numbers and fencing tokens make storage reject writes from a stale leader.",
                s =>
                {
                    s.Ghost("L").Paint("L", Bad).SubText("L", "crashed");
                    s.Write("F1", "new leader").SubText("F1", "promoted, epoch 2").Paint("F1", Good);
                    s.ClearLinks();
                    s.Connect("F1", "F2", "replication log", Good);
                    s.Move("shape:user", "shape:F1", "writes now go here", Good);
                })
            .Step("Multi-leader and leaderless conflicts",
                "Two regions each accept writes. Both increment a like counter from 10 at the same moment. Last-writer-wins keeps only one: 11. A CRDT counter merges both: 12.",
                "Multi-leader and leaderless designs survive regional outages and keep writes local, but every concurrent write is a potential conflict. CRDTs (counters, sets, text) merge automatically and correctly.",
                s =>
                {
                    s.Wipe();
                    s.Box("eu", "Region EU", 80, 60, 260, 70, "likes: 10 -> 11", Info);
                    s.Box("us", "Region US", 660, 60, 260, 70, "likes: 10 -> 11", Info);
                    s.Box("lww", "last-writer-wins: likes = 11", 80, 330, 360, 64, "one increment lost", Bad);
                    s.Box("crdt", "CRDT G-counter: likes = 12", 560, 330, 360, 64, "EU +1 and US +1 both kept", Good);
                    s.Connect("eu", "lww").Connect("us", "lww").Connect("eu", "crdt").Connect("us", "crdt");
                })
            .Lesson("Replication, lag and failover", "Distributed data", "Leader-follower replication, read-your-writes, sync vs async, failover, split brain and CRDTs.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Sharding()
    {
        int[] hashes = [17, 58, 83, 109, 146, 161, 193, 206, 251, 283, 331, 352];
        const double Cx = 500, Cy = 290, R = 190;

        (double X, double Y) OnRing(double degrees, double radius)
        {
            var rad = degrees * Math.PI / 180;
            return (Cx + radius * Math.Sin(rad), Cy - radius * Math.Cos(rad));
        }

        int Owner(int hash, int[] ring)
        {
            var sorted = ring.Order().ToArray();
            var next = sorted.FirstOrDefault(n => n >= hash, sorted[0]);
            return Array.IndexOf(ring, next);
        }

        void ModLayout(SceneState s, int shards)
        {
            var stack = new int[shards];
            for (var i = 0; i < hashes.Length; i++)
            {
                var shard = hashes[i] % shards;
                var moved = shards == 5 && hashes[i] % 4 != shard;
                var k = stack[shard]++;
                s.Box("k" + i, hashes[i].ToString(), 50 + shard * 190 + k % 2 * 80, 330 - k / 2 * 44, 70, 36, tone: moved ? Warn : Neutral);
                s.Tint("k" + i, -1);
            }
        }

        void RingLayout(SceneState s, int[] ring)
        {
            for (var i = 0; i < hashes.Length; i++)
            {
                var (x, y) = OnRing(hashes[i], R + 62);
                s.Box("k" + i, hashes[i].ToString(), x - 28, y - 16, 56, 32);
                s.Tint("k" + i, Owner(hashes[i], ring));
            }
        }

        var movedByMod = hashes.Count(h => h % 4 != h % 5);
        int[] four = [40, 130, 220, 310];
        int[] five = [40, 130, 220, 310, 175];

        return new StoryBuilder("SY7", "Sharding splits data across machines by key. Simple hash-mod-N placement reshuffles almost everything when N changes; consistent hashing moves only the keys next to the new node, and virtual nodes keep the load even.")
            .Setup(s =>
            {
                for (var i = 0; i < 4; i++)
                {
                    s.Box("sh" + i, $"shard {i}", 40 + i * 190, 400, 170, 60, "hash % 4 = " + i, Info);
                }

                ModLayout(s, 4);
            })
            .Code("""
                int ShardFor(int hash, int shardCount) => hash % shardCount;      // simple, but N is baked in

                // consistent hashing: walk clockwise to the first node at or after the key's position
                Node OwnerOf(uint hash) => ring.GetViewBetween(hash, uint.MaxValue).FirstOrDefault() ?? ring.Min!;
                """)
            .Step("Hash mod N",
                "Each key's hash picks a shard: hash % 4. Twelve keys spread across four shards, and any server can compute where a key lives.",
                "Sharding (partitioning) is how databases grow past one machine's disk, memory and write throughput. The shard key choice is the most important decision: it decides balance and which queries stay on one shard.",
                [1])
            .Step("Add a fifth shard: almost everything moves",
                $"Change to hash % 5 and {movedByMod} of {hashes.Length} keys land on a different shard. All of that data must be copied while the system is live.",
                "With mod-N, adding one node out of N moves about N/(N+1) of all keys. That makes elastic scaling slow, expensive and risky.",
                [1],
                s =>
                {
                    s.Box("sh4", "shard 4", 800, 400, 170, 60, "new", Good);
                    for (var i = 0; i < 5; i++)
                    {
                        s.Write("sh" + i, $"shard {i}", "hash % 5 = " + i);
                    }

                    ModLayout(s, 5);
                    s.Say($"{movedByMod} of {hashes.Length} keys moved", Bad);
                })
            .Step("Consistent hashing: a ring",
                "Place nodes and keys on a ring of hash values. Each key belongs to the first node clockwise from it. Keys are coloured by their owner.",
                "Used by Dynamo, Cassandra, Riak, many caches and CDNs. Each node owns the arc between its predecessor and itself.",
                [3, 4],
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("sh", StringComparison.Ordinal));
                    s.Circle("ring", null, Cx, Cy, R);
                    s.Ghost("ring");
                    for (var n = 0; n < four.Length; n++)
                    {
                        var (x, y) = OnRing(four[n], R);
                        s.Circle("n" + n, "N" + n, x, y, 26);
                        s.Tint("n" + n, n);
                    }

                    RingLayout(s, four);
                })
            .Step("Add a node: only neighbours move",
                "Node N4 joins between N1 and N2. Only the keys on that arc, 2 of 12, move from N2 to N4. Everything else stays put.",
                "Adding or removing one node moves about 1/N of the keys, and only to or from its neighbours. That is what makes elastic clusters practical.",
                [3, 4],
                s =>
                {
                    var (x, y) = OnRing(175, R);
                    s.Circle("n4", "N4", x, y, 26);
                    s.Tint("n4", 4);
                    RingLayout(s, five);
                    s.Say("2 of 12 keys moved", Good);
                })
            .Step("Virtual nodes and hot keys",
                "With few nodes the arcs are uneven. Giving each physical node many positions (virtual nodes) on the ring evens out the load and spreads a failed node's keys across all others.",
                "A single hot key (a celebrity account) still overloads one shard: split it with key suffixes, cache it, or replicate it. Range partitioning (by key order) supports range scans but creates hotspots on sequential keys such as timestamps.",
                [],
                s =>
                {
                    double[] vAngles = [10, 70, 100, 160, 200, 240, 265, 295, 325, 345];
                    for (var i = 0; i < vAngles.Length; i++)
                    {
                        var (x, y) = OnRing(vAngles[i], R);
                        s.Dot("v" + i, x, y, i % 5, 9);
                    }

                    s.Say("Many virtual positions per node even out the arcs", Info);
                })
            .Lesson("Sharding and consistent hashing", "Distributed data", "Hash-mod-N vs a hash ring, how many keys move when nodes join, virtual nodes and hot keys.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson LoadBalancing()
    {
        void Server(SceneState s, int i, string sub, Tone tone = Neutral) =>
            s.Box("s" + i, $"server {i + 1}", 720, 40 + i * 125, 240, 64, sub, tone);

        return new StoryBuilder("SY8", "Horizontal scaling adds identical stateless servers behind a load balancer. The balancer spreads requests, routes around slow or failed instances using health checks, and lets an autoscaler add capacity under load.")
            .Setup(s =>
            {
                for (var i = 0; i < 3; i++)
                {
                    s.Box("c" + i, $"client {i + 1}", 40, 80 + i * 150, 160, 56);
                    Server(s, i, "0 active");
                    s.Connect("c" + i, "lb", tone: Neutral);
                }

                s.Box("lb", "load balancer", 330, 230, 220, 70, "round robin", Info);
                for (var i = 0; i < 3; i++)
                {
                    s.Connect("lb", "s" + i);
                }
            })
            .NoCode()
            .Step("Round robin",
                "Requests go to server 1, 2, 3, 1, 2, 3... Each server gets an equal share.",
                "Round robin is simple and works when requests cost about the same and servers are identical. Weighted round robin handles servers of different sizes.",
                s =>
                {
                    for (var i = 0; i < 3; i++)
                    {
                        s.Move("shape:c" + i, "shape:lb", $"req {i + 1}", Info).Move("shape:lb", "shape:s" + i, $"req {i + 1}", Info);
                        Server(s, i, "1 active");
                    }
                })
            .Step("Least connections",
                "Server 2 is stuck in a long garbage collection and has 12 requests piling up. Least-connections sends new work to servers 1 and 3 instead.",
                "Good algorithms adapt to real load: least connections, least response time, or 'power of two choices' (pick two servers at random and use the less loaded one), which avoids herding.",
                s =>
                {
                    s.Write("lb", "load balancer", "least connections");
                    Server(s, 0, "2 active", Good);
                    Server(s, 1, "12 active, GC pause", Warn);
                    Server(s, 2, "2 active", Good);
                    s.Move("shape:lb", "shape:s0", "new", Good).Move("shape:lb", "shape:s2", "new", Good);
                })
            .Step("Health checks remove failed servers",
                "Server 3's health endpoint starts returning 503. The balancer takes it out of rotation; users never reach it.",
                "Separate liveness (restart me) from readiness (do not send me traffic yet). A readiness check that calls the database can take the whole fleet out if the database blips, so keep checks shallow and add outlier detection.",
                s =>
                {
                    Server(s, 1, "4 active", Good);
                    Server(s, 2, "GET /health: 503", Bad);
                    s.Ghost("s2");
                    s.Disconnect("lb", "s2").Connect("lb", "s2", "removed", Bad, dashed: true);
                })
            .Step("Autoscaling adds capacity",
                "CPU and queue length rise during a sale. The autoscaler starts server 4, which passes its readiness check and joins the pool.",
                "Scale on a signal that leads the pain (requests in flight, queue age, p95 latency), keep a warm minimum, and scale in slowly. New instances need warm-up time: JIT, caches and connection pools.",
                s =>
                {
                    Server(s, 3, "new: warming up", Info);
                    s.Connect("lb", "s3", "joined", Good);
                })
            .Step("Stateless servers, shared state elsewhere",
                "Any server can handle any request only if sessions, caches and uploads live in shared stores, not in one server's memory or disk.",
                "Sticky sessions are a workaround that breaks on failover and unbalances load. L4 balancers route TCP/UDP connections fast; L7 balancers understand HTTP and can route by path, header or cookie, terminate TLS and retry.",
                s =>
                {
                    s.Box("redis", "shared session + cache store", 300, 450, 300, 56, "e.g. Redis", Good);
                    for (var i = 0; i < 4; i++)
                    {
                        s.Connect("s" + i, "redis", dashed: true, arrow: false);
                    }

                    s.Say("Keep servers stateless so any of them can serve any request", Good);
                })
            .Lesson("Load balancing and horizontal scaling", "Scaling", "Round robin vs least connections, health checks, autoscaling and why servers must be stateless.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Caching() =>
        new StoryBuilder("SY9", "Caches keep copies of data closer to where it is needed. Every layer, from browser to CDN to Redis, cuts latency and load, but every copy can go stale, so invalidation and expiry are part of the design.")
            .Setup(s =>
            {
                string[] layers = ["browser", "CDN edge", "reverse proxy", "app memory", "Redis", "database"];
                string[] lat = ["0 ms", "~10-30 ms", "~1 ms", "~0.1 µs", "~0.5 ms", "~5-50 ms"];
                for (var i = 0; i < layers.Length; i++)
                {
                    s.Box("l" + i, layers[i], 30 + i * 160, 60, 145, 60, lat[i], i == 5 ? Warn : Info);
                    if (i > 0)
                    {
                        s.Connect("l" + (i - 1), "l" + i, arrow: true);
                    }
                }
            })
            .Code("""
                public async Task<User> GetUser(int id)
                {
                    var key = $"user:{id}";
                    var cached = await cache.GetStringAsync(key);
                    if (cached is not null) return Deserialize(cached);              // hit
                    var user = await db.Users.FindAsync(id);                         // miss: go to the source
                    await cache.SetStringAsync(key, Serialize(user),
                        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
                    return user;
                }
                """)
            .Step("Caches at every layer",
                "A request can be answered by the browser cache, a CDN edge near the user, a reverse proxy, the app's memory, a shared Redis cache, or finally the database.",
                "The further left a request is answered, the faster it is and the less load reaches the expensive layers. Typical latencies are shown under each box (orders of magnitude, not guarantees).",
                [])
            .Step("Cache-aside: miss, load, store",
                "The app looks in Redis first. On a miss it reads the database, stores the result with a 5-minute TTL, and returns it. The next request is a hit.",
                "Cache-aside (lazy loading) only caches what is actually read. Combine it with single-flight or locking to avoid stampedes on hot keys (see the Resource Management track).",
                [3, 4, 5, 6, 7, 8],
                s =>
                {
                    s.Wipe();
                    s.Box("app", "app", 380, 230, 220, 70, tone: Info);
                    s.Box("cache", "Redis", 720, 80, 220, 64, "user:7");
                    s.Box("db", "database", 720, 380, 220, 64, tone: Warn);
                    s.Connect("app", "cache").Connect("app", "db");
                    s.Move("shape:app", "shape:cache", "GET user:7: miss", Warn).Move("shape:app", "shape:db", "SELECT", Warn).Move("shape:db", "shape:app", "row", Info);
                    s.Say("Miss: read the source, then populate the cache", Info);
                })
            .Step("Hit",
                "The second request finds user:7 in Redis and never touches the database.",
                "A good cache hit ratio is often above 90-99%. Watch the ratio as a key metric: a sudden drop means the database is about to receive many times its normal load.",
                [5],
                s =>
                {
                    s.Paint("cache", Good).SubText("cache", "user:7  (TTL 5 min)");
                    s.Move("shape:app", "shape:cache", "GET user:7", Info).Move("shape:cache", "shape:app", "hit: 0.5 ms", Good);
                    s.Ghost("db");
                })
            .Step("Writes and invalidation",
                "When the user changes their name, the cached copy is now wrong. Either delete it on write, update it on write, or accept staleness up to the TTL.",
                "Classic race: request A reads the old value from the database, request B updates the database and deletes the key, then A writes the old value back into the cache. Short TTLs bound how long such bugs last.",
                [],
                s =>
                {
                    s.Ghost("db", false);
                    s.Table("writes", "write strategies", 40, 420, 640,
                        "cache-aside + delete   write DB, then delete key",
                        "write-through          write cache and DB together",
                        "write-behind           write cache, flush DB later (risk: loss)");
                    s.Paint("cache", Warn).SubText("cache", "stale until deleted or expired");
                })
            .Step("Why hit ratio matters so much",
                "With hits at 0.5 ms and misses at 20 ms, average latency falls from 20 ms (no cache) to about 2.5 ms at a 90% hit ratio and 0.7 ms at 99%.",
                "Going from 90% to 99% hits cuts database load by 10x (from 10% of requests to 1%). Small ratio changes have large effects on the layer behind the cache.",
                [],
                s =>
                {
                    s.Wipe();
                    (double H, string Label)[] ratios = [(0, "0%"), (0.5, "50%"), (0.9, "90%"), (0.99, "99%")];
                    s.LabeledBars("avg", ratios.Select(r => (r.Label + " hits", r.H * 0.5 + (1 - r.H) * 20, $"{r.H * 0.5 + (1 - r.H) * 20:0.##} ms")).ToArray(), 160, 470, 140, 50, 360);
                    s.Paint("avg0", Bad).Paint("avg3", Good);
                    s.Caption("cap", "average latency (hit 0.5 ms, miss 20 ms)", 160, 60, 600);
                })
            .Lesson("Caching layers and strategies", "Scaling", "Browser, CDN and Redis caches, cache-aside, invalidation races and the maths of hit ratios.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Queues() =>
        new StoryBuilder("SY10", "Queues decouple services in time: producers publish and move on, consumers process at their own pace. Delivery is at-least-once in practice, so consumers must be idempotent; partitions give ordering and parallelism; dead-letter queues catch poison messages.")
            .Setup(s =>
            {
                s.Box("api", "Orders API", 40, 230, 200, 70, tone: Info);
                s.Box("email", "Email service", 720, 230, 220, 70, "slow: 3 s per call", Warn);
                s.Connect("api", "email", "synchronous call", Warn);
            })
            .Code("""
                await producer.ProduceAsync("orders", new Message<string, OrderPlaced>
                {
                    Key = order.Id.ToString(),            // same key -> same partition -> ordered
                    Value = new OrderPlaced(order.Id, order.Total)
                });

                // consumer: idempotent handling
                if (await db.ProcessedMessages.AnyAsync(m => m.Id == msg.Id)) return;     // duplicate: skip
                await SendConfirmation(msg.Value);
                db.ProcessedMessages.Add(new(msg.Id));
                await db.SaveChangesAsync();
                """)
            .Step("Synchronous coupling",
                "Placing an order calls the email service directly. The user waits 3 seconds for an email to be sent, and if email is down, ordering fails.",
                "Every synchronous dependency multiplies latency and failure probability. Work the user does not need to wait for (emails, analytics, thumbnails) is a candidate for a queue.",
                [],
                s => s.Say("The slowest dependency sets everyone's latency", Bad))
            .Step("Publish and move on",
                "The API publishes OrderPlaced to a queue and returns 202 Accepted immediately. A worker consumes the message and sends the email when it can.",
                "If the email service is down, messages simply wait. The queue absorbs bursts (load levelling), and consumers can be scaled independently.",
                [1, 2, 3, 4, 5],
                s =>
                {
                    s.ClearLinks();
                    s.Caption("qcap", "queue: orders", 300, 180);
                    s.Cells("q", ["#41", "#42", "#43"], 300, 210, 110, 56, 8, tags: false, tone: Info);
                    s.Write("email", "Email worker", "consumes at its own pace").Paint("email", Good);
                    s.Connect("api", "q0", "publish").Connect("q2", "email", "consume");
                    s.Move("shape:api", "shape:q0", "OrderPlaced", Info);
                    s.Say("API answers in milliseconds; email happens later", Good);
                })
            .Step("At-least-once means duplicates",
                "The worker sends the email, then crashes before acknowledging the message. The broker redelivers it, and the customer gets two emails.",
                "At-most-once (ack first) loses messages on a crash; at-least-once (ack after) duplicates them. 'Exactly-once' in practice means at-least-once delivery plus an idempotent consumer that records processed message ids.",
                [8, 9, 10, 11],
                s =>
                {
                    s.Paint("email", Bad).SubText("email", "crashed before ack");
                    s.Move("shape:q2", "shape:email", "#43 again", Warn);
                    s.Box("dedupe", "processed ids: #41 #42 #43", 680, 400, 280, 56, "skip duplicates", Good);
                    s.Connect("email", "dedupe", "check first", Good);
                })
            .Step("Partitions: ordering and parallelism",
                "A Kafka topic is split into partitions. Messages with the same key (order id) always go to the same partition, so events for one order stay in order, while different partitions are processed in parallel.",
                "Within a consumer group, each partition is read by one consumer at a time, so parallelism is capped by the partition count: a 4th consumer on 3 partitions sits idle.",
                [3],
                s =>
                {
                    s.Wipe();
                    s.Box("prod", "producer", 40, 230, 160, 60, tone: Info);
                    for (var p = 0; p < 3; p++)
                    {
                        s.Caption("pc" + p, $"partition {p}", 260, 80 + p * 150, 200);
                        s.Cells("p" + p + "m", [$"order {p * 3 + 1}", $"order {p * 3 + 1}", $"order {p * 3 + 4}"], 260, 105 + p * 150, 110, 50, 6, tags: false, tone: Info);
                        s.Box("con" + p, $"consumer {p + 1}", 720, 105 + p * 150, 200, 50, tone: Good);
                        s.Connect("p" + p + "m2", "con" + p);
                        s.Tint("p" + p + "m0", p).Tint("p" + p + "m1", p).Tint("p" + p + "m2", p);
                    }

                    s.Box("con3", "consumer 4 (idle)", 720, 470, 200, 50);
                    s.Ghost("con3");
                })
            .Step("Poison messages and dead letters",
                "One malformed message fails every time. After 5 attempts it is moved to a dead-letter queue, so it stops blocking the messages behind it.",
                "Alert on DLQ size and on the age of the oldest message, not just queue length. A growing backlog means consumers are too slow: scale them out or apply backpressure to producers.",
                [],
                s =>
                {
                    s.Paint("p1m2", Bad).Tint("p1m2", -1).Write("p1m2", "malformed");
                    s.Box("dlq", "dead-letter queue", 460, 470, 220, 50, "after 5 attempts", Bad);
                    s.Move("shape:p1m2", "shape:dlq", "give up", Bad);
                })
            .Lesson("Message queues, delivery guarantees and idempotency", "Distributed systems", "Async decoupling, at-least-once duplicates, idempotent consumers, partitions and dead letters.");

    // ------------------------------------------------------------------------------------------
    private static Lesson DistributedTransactions() =>
        new StoryBuilder("SY11", "Updating a database and sending a message cannot be atomic without help. The outbox pattern makes them atomic locally; two-phase commit locks everyone and can block; sagas instead run local steps with compensating actions on failure.")
            .Setup(s =>
            {
                s.Box("svc", "Order service", 380, 60, 240, 64, tone: Info);
                s.Box("db", "orders DB", 120, 330, 220, 64);
                s.Box("bus", "message bus", 660, 330, 220, 64);
                s.Connect("svc", "db", "1. INSERT order").Connect("svc", "bus", "2. publish OrderPlaced");
            })
            .Code("""
                await using var tx = await db.Database.BeginTransactionAsync();
                db.Orders.Add(order);
                db.Outbox.Add(new OutboxMessage("OrderPlaced", JsonSerializer.Serialize(evt)));   // same transaction
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                // relay (background): publish pending outbox rows, then mark them sent
                foreach (var m in await db.Outbox.Where(m => m.SentAt == null).Take(100).ToListAsync())
                {
                    await bus.PublishAsync(m.Type, m.Payload);
                    m.SentAt = DateTime.UtcNow;
                }
                """)
            .Step("The dual-write problem",
                "The service inserts the order and commits, then crashes before publishing OrderPlaced. Inventory and billing never hear about the order.",
                "Publishing first and writing second has the mirror problem: an event about an order that does not exist. Two independent systems cannot be updated atomically by simply calling both.",
                [],
                s =>
                {
                    s.Paint("db", Good).SubText("db", "order saved");
                    s.Box("crash", "crash", 460, 200, 100, 44, tone: Bad);
                    s.Paint("bus", Bad).SubText("bus", "event never sent");
                    s.Say("The database and the bus now disagree", Bad);
                })
            .Step("The transactional outbox",
                "Write the order and an outbox row in the same local transaction. A relay publishes outbox rows to the bus and marks them sent. Either both rows exist or neither does.",
                "The relay may publish a message twice (crash after publishing, before marking), so consumers must be idempotent. Change data capture (reading the database log, for example with Debezium) is an alternative relay.",
                [1, 2, 3, 4, 5, 7, 8, 9, 10, 11],
                s =>
                {
                    s.Remove("crash");
                    s.ClearLinks();
                    s.Write("db", "orders DB", "orders + outbox tables").Paint("db", Good);
                    s.Box("relay", "outbox relay", 400, 420, 200, 56, tone: Info);
                    s.Paint("bus", Good).SubText("bus", "event delivered");
                    s.Connect("svc", "db", "1 local transaction").Connect("relay", "db", "poll", dashed: true).Connect("relay", "bus", "publish");
                })
            .Step("Two-phase commit",
                "A coordinator asks every participant to prepare (lock and promise), then tells all to commit. If the coordinator dies after 'prepare', participants wait in doubt, holding their locks.",
                "2PC gives atomicity across databases but couples their availability: the slowest or failed participant blocks everyone. It is rarely used across services; within one database cluster, consensus-based commits are common.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Box("coord", "coordinator", 380, 50, 240, 64, tone: Info);
                    s.Box("p0", "payments DB", 120, 330, 220, 64, "prepared: locks held", Warn);
                    s.Box("p1", "inventory DB", 660, 330, 220, 64, "prepared: locks held", Warn);
                    s.Move("shape:coord", "shape:p0", "PREPARE", Info).Move("shape:coord", "shape:p1", "PREPARE", Info);
                    s.Connect("p0", "coord", "vote yes").Connect("p1", "coord", "vote yes");
                    s.Paint("coord", Bad).SubText("coord", "crashed before COMMIT");
                    s.Say("Participants are stuck in doubt, holding locks", Bad);
                })
            .Step("Saga: local steps plus compensations",
                "Create order, charge payment, reserve stock, book shipping: each is a local transaction. Reserving stock fails, so completed steps are undone in reverse: refund the payment, cancel the order.",
                "Sagas trade isolation for availability: other requests can see intermediate states (a charged but not yet shipped order), so design for that with status fields. Compensations must be idempotent and retried until they succeed.",
                [],
                s =>
                {
                    s.Wipe();
                    string[] steps = ["create order", "charge payment", "reserve stock", "book shipping"];
                    for (var i = 0; i < steps.Length; i++)
                    {
                        s.Box("st" + i, steps[i], 40 + i * 240, 120, 200, 60, tone: i < 2 ? Good : i == 2 ? Bad : Neutral);
                        if (i > 0)
                        {
                            s.Connect("st" + (i - 1), "st" + i);
                        }
                    }

                    s.Ghost("st3");
                    s.Box("comp1", "refund payment", 280, 320, 200, 56, "compensation", Warn);
                    s.Box("comp0", "cancel order", 40, 320, 200, 56, "compensation", Warn);
                    s.Connect("st2", "comp1", "failed: undo", Bad).Connect("comp1", "comp0", tone: Warn);
                })
            .Step("Orchestration vs choreography",
                "An orchestrator (a workflow engine or saga class) tells each service what to do next. In choreography, each service reacts to the previous service's events.",
                "Orchestration keeps the workflow visible in one place and is easier to change; choreography is more decoupled but the flow is scattered across services. Durable workflow engines (Temporal, Azure Durable Functions) persist saga state for you.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("styles", "saga styles", 40, 60, 920,
                        "orchestration   central coordinator sends commands        flow in one place, easy to monitor",
                        "choreography    services react to each other's events     loosely coupled, flow is implicit",
                        "both            local transactions + compensations        eventual consistency, idempotent steps");
                })
            .Lesson("Distributed transactions: outbox, 2PC and sagas", "Distributed systems", "Why dual writes break, the transactional outbox, blocking two-phase commit, and compensating sagas.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Raft()
    {
        (double X, double Y) Pos(int i)
        {
            var rad = (-90 + i * 72) * Math.PI / 180;
            return (500 + 210 * Math.Cos(rad), 285 + 210 * Math.Sin(rad));
        }

        void Node(SceneState s, int i, string sub, Tone tone = Neutral)
        {
            var (x, y) = Pos(i);
            s.Circle("S" + i, $"S{i + 1}", x, y, 50, sub, tone);
        }

        void All(SceneState s, string sub)
        {
            for (var i = 0; i < 5; i++)
            {
                Node(s, i, sub);
            }
        }

        return new StoryBuilder("SY12", "Consensus lets a group of machines agree on one ordered log despite crashes. Raft elects a leader by majority vote, the leader replicates log entries, and an entry is committed once a majority has it, so any two majorities overlap and no committed entry is lost.")
            .Setup(s => All(s, "follower · term 1"))
            .NoCode()
            .Step("Followers wait for heartbeats",
                "Five servers start as followers. Each has a randomised election timeout (for example 150-300 ms). If it hears nothing from a leader before the timeout, it starts an election.",
                "Randomised timeouts make it unlikely that two servers start elections at the same moment, which keeps split votes rare.")
            .Step("A candidate asks for votes",
                "S3 times out first, increments the term to 2, votes for itself and asks the others. S1, S2 and S4 grant their votes: 4 of 5 is a majority.",
                "Each server votes at most once per term, and only for a candidate whose log is at least as up to date as its own. That rule guarantees a new leader already has every committed entry.",
                s =>
                {
                    Node(s, 2, "candidate · term 2", Warn);
                    foreach (var i in new[] { 0, 1, 3, 4 })
                    {
                        s.Move("shape:S2", "shape:S" + i, "RequestVote", Warn);
                    }

                    foreach (var i in new[] { 0, 1, 3 })
                    {
                        Node(s, i, "voted S3 · term 2");
                        s.Connect("S" + i, "S2", "yes", Good);
                    }
                })
            .Step("The leader replicates the log",
                "S3 is leader. A client writes x = 5. The leader appends it and sends AppendEntries. Once 3 of 5 servers (a majority, counting itself) have stored it, the entry is committed and applied.",
                "Committed entries survive any minority of failures. Heartbeats are just empty AppendEntries. Followers that were down catch up from the leader's log.",
                s =>
                {
                    s.ClearLinks();
                    Node(s, 2, "leader · log [x=5]", Good);
                    Node(s, 0, "log [x=5]", Good);
                    Node(s, 1, "log [x=5]", Good);
                    Node(s, 3, "log [ ] (slow)");
                    Node(s, 4, "log [ ] (slow)");
                    foreach (var i in new[] { 0, 1, 3, 4 })
                    {
                        s.Move("shape:S2", "shape:S" + i, "AppendEntries x=5", Info);
                    }

                    s.Say("3 of 5 have it: x = 5 is committed", Good);
                })
            .Step("The leader fails",
                "S3 crashes. Followers stop receiving heartbeats; S1 times out, becomes candidate for term 3, and wins with votes from S2 and S4. Because S1 has x = 5, nothing committed is lost.",
                "A 5-node cluster tolerates 2 failures; 3 nodes tolerate 1. Even numbers add cost without extra tolerance, which is why clusters have 3, 5 or 7 members.",
                s =>
                {
                    Node(s, 2, "crashed", Bad);
                    s.Ghost("S2");
                    Node(s, 0, "leader · term 3 · [x=5]", Good);
                    Node(s, 1, "follower · [x=5]");
                    Node(s, 3, "follower · [x=5] caught up");
                    s.Move("shape:S0", "shape:S1", "RequestVote", Warn).Move("shape:S0", "shape:S3", "RequestVote", Warn);
                })
            .Step("A minority cannot act",
                "A partition isolates S4 and S5. They cannot gather 3 votes, so they cannot elect a leader or commit anything. The majority side keeps working. No split brain.",
                "This is the CP choice from the CAP lesson, made automatically by majority quorums. When the partition heals, the minority adopts the leader's log.",
                s =>
                {
                    s.Box("cut", null, 436, 140, 6, 400, tone: Bad);
                    Node(s, 3, "isolated: no quorum", Warn);
                    Node(s, 4, "isolated: no quorum", Warn);
                    s.Say("2 of 5 can never form a majority", Info);
                })
            .Step("Where consensus runs",
                "You rarely implement Raft yourself, but you depend on it: Kubernetes stores cluster state in etcd, and many databases replicate each shard with Raft.",
                "Kafka replaced ZooKeeper with its own Raft-based KRaft controller; Kafka 4.0 (2025) removed ZooKeeper support entirely. Paxos is the older, equivalent family of algorithms (used in Google's Chubby and Spanner).",
                s =>
                {
                    s.Wipe();
                    s.Table("uses", "systems built on consensus", 40, 60, 920,
                        "etcd                 Raft   Kubernetes cluster state, leader election",
                        "Consul               Raft   service discovery, configuration",
                        "CockroachDB, TiKV    Raft   one Raft group per data range",
                        "Kafka (KRaft)        Raft   cluster metadata, replacing ZooKeeper",
                        "Spanner, Chubby      Paxos  Google's databases and lock service");
                })
            .Lesson("Consensus with Raft", "Distributed systems", "Leader election by majority vote, log replication, commit rules, failover and partitions.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Observability() =>
        new StoryBuilder("SY13", "You cannot fix what you cannot see. Logs, metrics and traces explain what a system is doing; percentiles expose the slow tail that averages hide; SLOs and error budgets turn reliability into an explicit, measurable target.")
            .Setup(s =>
            {
                s.Box("logs", "logs", 40, 120, 280, 100, "discrete events with context", Info);
                s.Box("metrics", "metrics", 360, 120, 280, 100, "numbers over time: rate, errors, latency", Good);
                s.Box("traces", "traces", 680, 120, 280, 100, "one request across many services", Warn);
            })
            .Code("""
                using var activity = ActivitySource.StartActivity("orders.place");     // a span in the trace
                activity?.SetTag("order.id", order.Id);
                logger.LogInformation("Order {OrderId} placed for {Total}", order.Id, order.Total);   // structured log
                ordersPlaced.Add(1, new KeyValuePair<string, object?>("region", region));            // metric
                """)
            .Step("Three pillars",
                "Logs record what happened, metrics show trends cheaply, and traces follow one request through every service it touches.",
                "OpenTelemetry is the vendor-neutral standard for all three. .NET's ILogger, Meter and ActivitySource map onto it directly, so you can switch backends without changing code.",
                [1, 2, 3, 4])
            .Step("A distributed trace",
                "A checkout request takes 240 ms. The trace shows its spans: auth, then order placement, which calls the database and payments. The fraud check inside payments takes 140 ms: there is the problem.",
                "Trace context (the W3C traceparent header) is passed on every call so spans from different services join into one tree. Put the trace id in every log line to jump from a log to its trace.",
                [1, 2],
                s =>
                {
                    s.Wipe();
                    (string Name, double Start, double Len, Tone Tone)[] spans =
                    [
                        ("GET /checkout   240 ms", 0, 240, Info), ("auth   15 ms", 5, 15, Neutral), ("orders.place   200 ms", 25, 200, Neutral),
                        ("SQL INSERT   20 ms", 30, 20, Neutral), ("payments.charge   160 ms", 60, 160, Warn), ("fraud.check   140 ms", 70, 140, Bad)
                    ];
                    for (var i = 0; i < spans.Length; i++)
                    {
                        s.Box("sp" + i, spans[i].Name, 120 + spans[i].Start * 3.4, 60 + i * 62, Math.Max(80, spans[i].Len * 3.4), 50, tone: spans[i].Tone);
                    }

                    s.Label("axis", "0 ms                                                      time                                                      240 ms", 120, 440, 820, 11);
                })
            .Step("Percentiles, not averages",
                "Average latency is 70 ms and looks fine. But p99 is 850 ms: one request in a hundred is painfully slow, and a page making 30 calls hits that tail most of the time.",
                "Track p50, p95, p99 and max per endpoint. With 30 calls per page, the chance at least one is slower than p99 is 1 - 0.99^30, about 26%. Averages also hide bimodal behaviour such as cache hits vs misses.",
                [],
                s =>
                {
                    s.Wipe();
                    s.LabeledBars("pct", [("average", 70, "70 ms"), ("p50", 40, "40 ms"), ("p95", 120, "120 ms"), ("p99", 850, "850 ms"), ("max", 3200, "3.2 s")], 120, 470, 140, 40, 360, log: true, logMin: 10);
                    s.Paint("pct0", Good).Paint("pct3", Bad).Paint("pct4", Bad);
                    s.Caption("cap", "latency of the same endpoint (log scale)", 120, 60, 600);
                })
            .Step("SLOs and error budgets",
                "A 99.9% monthly availability SLO allows 43.2 minutes of failure per 30 days. That allowance is the error budget: spend it on deploys and experiments, and slow down when it runs out.",
                "Each extra nine costs roughly 10x more effort. Alert on budget burn rate (how fast you are consuming it) rather than on every CPU spike: page people for symptoms users feel, not for causes.",
                [],
                s =>
                {
                    s.Wipe();
                    s.LabeledBars("budget", [("99%", 432, "7.2 h"), ("99.9%", 43.2, "43.2 min"), ("99.99%", 4.32, "4.3 min"), ("99.999%", 0.432, "26 s")], 160, 470, 150, 50, 360, log: true, logMin: 0.1);
                    s.Paint("budget1", Good);
                    s.Caption("cap", "allowed downtime per 30 days (log scale)", 160, 60, 600);
                })
            .Step("Practices that pay off",
                "Instrument from day one, keep signals cheap and consistent, and make every alert actionable.",
                "Golden signals for services: latency, traffic, errors, saturation (RED for request-driven services, USE for resources). Dashboards answer 'is it broken?'; traces and logs answer 'why?'.",
                [3],
                s =>
                {
                    s.Wipe();
                    s.Table("practices", "observability practices", 40, 60, 920,
                        "structured logs      key/value fields, trace id on every line, no secrets",
                        "golden signals       latency, traffic, errors, saturation per service",
                        "SLO-based alerts     page on fast budget burn, ticket on slow burn",
                        "health endpoints     liveness and readiness, shallow and fast",
                        "OpenTelemetry        one SDK; export to Prometheus, Jaeger, Azure Monitor...");
                })
            .Lesson("Observability: logs, metrics, traces and SLOs", "Reliability", "Distributed traces, percentiles vs averages, error budgets, and alerting on what users feel.");

    // ------------------------------------------------------------------------------------------
    private static Lesson UrlShortener() =>
        new StoryBuilder("SY14", "System design is a sequence of decisions: clarify requirements, estimate load, define the API, pick data and id strategies, then add caches, partitions and async pipelines where the numbers demand them. A URL shortener exercises all of it.")
            .Setup(s => s.Table("req", "requirements and back-of-the-envelope numbers", 40, 60, 920,
                "write        100 M new short links per day     -> ~1,160 / s average, ~3x at peak",
                "read         10 redirects per new link          -> ~11,600 / s average",
                "retention    5 years x 365 x 100 M              -> ~183 billion links",
                "storage      x ~500 bytes per link               -> ~91 TB (before replication)",
                "id space     7 characters of base62             -> 62^7 = 3.5 trillion codes",
                "latency      redirects in < 50 ms at p99, available 99.99%"))
            .Code("""
                POST /api/links      { "url": "https://example.com/a/very/long/path" }
                -> 201 Created       { "code": "aZ3k9Qx", "short": "https://sho.rt/aZ3k9Qx" }

                GET  /aZ3k9Qx
                -> 302 Found         Location: https://example.com/a/very/long/path

                string Base62(long n) { var sb = new StringBuilder(); do { sb.Insert(0, Alphabet[(int)(n % 62)]); n /= 62; } while (n > 0); return sb.ToString(); }
                """)
            .Step("Requirements and estimates",
                "Clarify scope first, then estimate. 100 M links per day is about 1,160 writes per second; 10 reads per write is about 11,600 redirects per second. Five years of links need about 91 TB.",
                "The numbers drive every later choice: reads dominate, so caching matters most; 91 TB does not fit on one machine, so storage must be partitioned; 3.5 trillion codes is plenty for 183 billion links.",
                [])
            .Step("API",
                "Two endpoints: create a short link, and redirect. The redirect is the hot path and must be tiny and fast.",
                "Extras such as custom aliases, expiry and analytics are separate concerns layered on later. Rate-limit creation per user and scan target URLs for malware to prevent abuse.",
                [1, 2, 4, 5],
                s =>
                {
                    s.Wipe();
                    s.Lifelines("client", "shortener", 330);
                    s.Msg("msg1", true, 110, "POST /api/links { url }").Msg("msg2", false, 170, "201 { short: sho.rt/aZ3k9Qx }", Good)
                        .Msg("msg3", true, 230, "GET /aZ3k9Qx").Msg("msg4", false, 280, "302 Location: https://example.com/...", Good);
                })
            .Step("Generating unique codes",
                "Hashing the URL and truncating risks collisions. A global counter needs coordination. A Snowflake-style 64-bit id (timestamp, machine, sequence) is unique without coordination; encode it in base62.",
                "Snowflake ids are roughly time-ordered, which helps storage locality. Base62 of a 64-bit id can take up to 11 characters; reserving ranges per machine from a counter keeps codes to 7 characters.",
                [7],
                s =>
                {
                    s.Wipe();
                    s.Fields("sf", [("sign", "0", 1), ("milliseconds since epoch", "41 bits", 41), ("machine", "10 bits", 10), ("sequence per ms", "12 bits", 12)], 40, 80, 920, 70, Info);
                    s.Table("idopts", "id strategies", 40, 220, 920,
                        "hash(url), take 7 chars   no coordination    collisions must be checked and retried",
                        "global counter            short, dense       single point of contention",
                        "counter ranges per node   short, scalable    node reserves 1,000 ids at a time",
                        "Snowflake id + base62     no coordination    longer codes (up to 11 chars)");
                })
            .Step("Architecture",
                "Stateless app servers behind a load balancer; a cache for hot links; a key-value store partitioned by code; click events sent to a stream for analytics instead of slowing redirects.",
                "Redirect lookups are pure key-value reads, perfect for a partitioned store (DynamoDB, Cassandra, or sharded SQL) and for caching. Analytics are written asynchronously so a slow analytics pipeline never slows redirects.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Box("user", "users", 40, 230, 140, 60);
                    s.Box("lb", "load balancer", 230, 230, 170, 60, tone: Info);
                    s.Box("app", "app servers x N", 450, 230, 180, 60, "stateless", Info);
                    s.Box("cache", "cache", 700, 80, 180, 56, "hot links", Good);
                    s.Box("kv", "KV store", 700, 230, 180, 60, "partitioned by code", Warn);
                    s.Box("stream", "click stream", 700, 400, 180, 56, "async analytics");
                    s.Connect("user", "lb").Connect("lb", "app").Connect("app", "cache").Connect("app", "kv").Connect("app", "stream", dashed: true);
                })
            .Step("The read path",
                "GET /aZ3k9Qx: the app checks the cache (hit for popular links) and answers 302 in about a millisecond. On a miss it reads the KV store and fills the cache.",
                "A small fraction of links gets most of the traffic (a power-law distribution), so even a modest cache achieves a very high hit ratio.",
                [4, 5],
                s =>
                {
                    s.Move("shape:user", "shape:lb", "GET /aZ3k9Qx", Info).Move("shape:app", "shape:cache", "lookup", Info).Move("shape:cache", "shape:app", "hit", Good).Move("shape:app", "shape:stream", "click event", Info);
                    s.Say("Cache hit: redirect in ~1 ms; the click is logged asynchronously", Good);
                })
            .Step("Trade-offs to discuss",
                "301 (permanent) redirects are cached by browsers, which saves load but hides repeat clicks from analytics; 302 keeps every click visible.",
                "Other questions: deleting or expiring links, custom aliases (uniqueness checks), multi-region reads, and protecting against enumeration of sequential codes.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("trade", "decisions and trade-offs", 40, 60, 920,
                        "301 vs 302           browser caching vs complete click analytics",
                        "id strategy          short codes vs coordination-free generation",
                        "storage              KV store for lookups; analytics in a separate OLAP store",
                        "multi-region         replicate links everywhere (read-heavy, rarely changed)",
                        "abuse                rate limits, malware scanning, unguessable codes",
                        "expiry               TTL on records, background cleanup");
                })
            .Lesson("Case study: designing a URL shortener", "Case study", "Estimates, API, id generation, caching, partitioning and async analytics in one design.");
}
