using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class SoftwareDesignLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return CouplingCohesion();
        yield return OopPillars();
        yield return SrpOcp();
        yield return LspIsp();
        yield return DependencyInversion();
        yield return CreationalPatterns();
        yield return StructuralPatterns();
        yield return StrategyObserverCommand();
        yield return StateChainTemplate();
        yield return CleanArchitecture();
        yield return DomainDrivenDesign();
        yield return CqrsEventSourcing();
        yield return MonolithMicroservices();
        yield return RobustCode();
        yield return Testing();
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson CouplingCohesion()
    {
        string[] names = ["validateOrder", "applyDiscount", "orderTotal", "calcTax", "chargeCard", "refund", "pickWarehouse", "formatAddress", "printLabel", "sendInvoice", "renderTemplate", "smtpSend"];
        int[] messy = [1, 3, 2, 0, 2, 3, 1, 0, 3, 2, 1, 0];
        int[] clean = [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3];
        (int From, int To)[] calls = [(0, 7), (2, 1), (2, 3), (4, 2), (9, 10), (9, 11), (8, 7), (5, 4), (6, 0), (9, 2), (6, 7)];
        (double X, double Y)[] origin = [(30, 30), (520, 30), (30, 300), (520, 300)];
        string[] vague = ["utils", "helpers", "manager", "common"];
        string[] named = ["orders", "billing", "shipping", "notifications"];

        void Modules(SceneState s, string[] titles, params Tone[] tones)
        {
            for (var m = 0; m < 4; m++)
            {
                s.Region("m" + m, titles[m], origin[m].X, origin[m].Y, 450, 240, tones.Length > m ? tones[m] : Neutral);
            }
        }

        void Arrange(SceneState s, int[] home)
        {
            var used = new int[4];
            for (var i = 0; i < names.Length; i++)
            {
                var m = home[i];
                var k = used[m]++;
                s.Box("f" + i, names[i], origin[m].X + 20 + k % 2 * 215, origin[m].Y + 50 + k / 2 * 90, 195, 50);
                s.Shape("f" + i).Mono = true;
            }
        }

        void Calls(SceneState s, Func<(int From, int To), bool> keep)
        {
            s.ClearLinks();
            foreach (var c in calls.Where(keep))
            {
                s.Connect("f" + c.From, "f" + c.To);
            }
        }

        int Cross(int[] home) => calls.Count(c => home[c.From] != home[c.To]);

        return new StoryBuilder("SW1", "Maintainable code keeps things that change together in one place (high cohesion) and lets modules talk through few, small, stable interfaces (low coupling). Then a change stays local instead of rippling through the system.")
            .Setup(s =>
            {
                Modules(s, vague);
                Arrange(s, messy);
                Calls(s, _ => true);
            })
            .NoCode()
            .Step("A big ball of mud",
                "Twelve functions sit in modules named 'utils', 'helpers', 'manager' and 'common'. Arrows show who calls whom: most calls cross module boundaries.",
                "Vague module names are a smell: they attract anything that has no obvious home. Each module ends up depending on every other module, so nothing can be understood, tested or deployed on its own.",
                s => s.Say($"{Cross(messy)} of {calls.Length} calls cross module boundaries", Warn))
            .Step("One change ripples everywhere",
                "Change formatAddress's signature. Its callers live in three different modules, so all three must be edited, rebuilt and retested.",
                "This is 'shotgun surgery': one logical change forces edits in many places. In a team it also means merge conflicts and coordination between people who own different modules.",
                s =>
                {
                    s.Paint("f7", Bad).PaintAll(Warn, "f0", "f8", "f6");
                    Modules(s, vague, Warn, Warn, Neutral, Warn);
                    s.Say("Changing formatAddress touches 3 of 4 modules", Bad);
                })
            .Step("Group what changes together (cohesion)",
                "Move each function to the module whose responsibility it serves: orders, billing, shipping, notifications. The boxes slide into place.",
                "Cohesion means everything in a module serves one purpose. Names become specific, and a new developer can guess where code lives.",
                s =>
                {
                    Modules(s, named);
                    Arrange(s, clean);
                    Calls(s, _ => true);
                    s.Say($"{Cross(clean)} of {calls.Length} calls cross modules now", Info);
                })
            .Step("Talk through narrow interfaces (coupling)",
                "Inside a module, functions call each other freely. Across modules, orders talks to billing, shipping and notifications through one small interface each.",
                "An interface is a promise: as long as its shape stays the same, everything behind it may change. Fewer, smaller contracts mean fewer reasons for another module to break.",
                s =>
                {
                    Calls(s, c => clean[c.From] == clean[c.To]);
                    s.Connect("m0", "m1", "IPayments").Connect("m0", "m2", "IShipping").Connect("m0", "m3", "INotifier");
                    s.Say("3 small contracts instead of tangled calls", Good);
                })
            .Step("Now a change stays local",
                "Change formatAddress again. Only shipping is edited and retested; the IShipping contract did not change, so orders is untouched.",
                "This is the payoff: the cost of a change is proportional to the size of the change, not the size of the system.",
                s =>
                {
                    s.Paint("f7", Bad).PaintAll(Warn, "f8", "f6");
                    Modules(s, named, Good, Good, Warn, Good);
                    s.Say("formatAddress changed: only shipping is rebuilt and retested", Good);
                })
            .Step("Rules of thumb",
                "These principles all serve the same goal: make the next change cheap and safe.",
                "They are heuristics, not laws. Duplication is cheaper than the wrong abstraction, and KISS and YAGNI keep DRY and the patterns in later lessons from turning into over-engineering.",
                s =>
                {
                    s.Wipe();
                    s.Table("rules", "design principles", 60, 60, 880,
                        "High cohesion    things that change together live together",
                        "Low coupling     depend on few, small, stable interfaces",
                        "DRY              one source of truth for each piece of knowledge",
                        "KISS             the simplest design that works",
                        "YAGNI            do not build features 'just in case'",
                        "Law of Demeter   talk to friends, not to friends of friends (a.b.c.d)",
                        "Conway's law     systems mirror the teams that build them");
                })
            .Lesson("Coupling and cohesion: the root of maintainability", "Foundations", "Why tangled modules make every change expensive, and how grouping and narrow interfaces fix it.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson OopPillars() =>
        new StoryBuilder("SW2", "Objects bundle data with the behaviour that protects it. Encapsulation guards invariants, abstraction hides details, inheritance shares a contract, and polymorphism runs the right code for each type. Prefer composition when behaviours vary independently.")
            .Setup(s =>
            {
                s.Table("acct", "class BankAccount", 560, 60, 400, "- _balance : decimal      (private)", "+ Balance { get; }", "+ Deposit(amount)", "+ Withdraw(amount)");
                s.Box("bad", "acct._balance = -500", 40, 70, 380, 56, "outside code", Bad);
                s.Box("good", "acct.Withdraw(500)", 40, 220, 380, 56, "outside code", Info);
                s.Shape("bad").Mono = true;
                s.Shape("good").Mono = true;
                s.Box("inv", "invariant: balance >= 0, always", 560, 260, 400, 56, tone: Good);
            })
            .Code("""
                class BankAccount
                {
                    private decimal _balance;                          // encapsulated state
                    public void Withdraw(decimal amount)
                    {
                        if (amount <= 0 || amount > _balance) throw new InvalidOperationException("Insufficient funds");
                        _balance -= amount;                             // the only way the state changes
                    }
                }

                abstract class Shape { public abstract double Area(); }                     // abstraction
                class Circle(double r) : Shape { public override double Area() => Math.PI * r * r; }
                class Rect(double w, double h) : Shape { public override double Area() => w * h; }

                double total = shapes.Sum(s => s.Area());                                    // polymorphism
                """)
            .Step("Encapsulation: guard the state",
                "The balance is private. Outside code cannot set it to -500; it can only call Withdraw, which checks the rule first.",
                "Encapsulation turns 'balance is never negative' from a hope into a guarantee: there is exactly one code path that changes the field, and it enforces the invariant. Public setters on everything throw that guarantee away.",
                [3, 6, 7],
                s =>
                {
                    s.Connect("bad", "acct", "blocked: private", Bad).Connect("good", "acct", "checked, then applied", Good);
                    s.HotRow("acct", 3);
                })
            .Step("Abstraction: depend on what, not how",
                "A report only needs to ask a shape for its area. How each shape computes it stays hidden behind Shape.Area().",
                "Abstractions let you reason about a piece of code without reading everything beneath it. A good abstraction is small and hard to misuse; a leaky one forces callers to know the details anyway.",
                [11],
                s =>
                {
                    s.Wipe();
                    s.Box("client", "AreaReport", 40, 230, 220, 60);
                    s.Box("abs", "Shape.Area()", 340, 230, 240, 60, "what you can ask", Info);
                    s.Region("hidden", "hidden details", 660, 40, 310, 470);
                    string[] how = ["π · r²", "w · h", "½ · b · h", "Heron's formula"];
                    for (var i = 0; i < how.Length; i++)
                    {
                        s.Box("h" + i, how[i], 690, 90 + i * 100, 250, 60);
                    }

                    s.Connect("client", "abs", "knows only this").Connect("abs", "hidden", "implemented by", dashed: true);
                })
            .Step("Inheritance: an is-a relationship",
                "Circle, Rect and Triangle inherit from Shape. Each promises to provide Area(), and each implements it its own way.",
                "Inheritance shares a contract and some code, but couples the subclass tightly to its base. Change the base class and every subclass may break (the 'fragile base class' problem). Keep hierarchies shallow.",
                [11, 12, 13],
                s =>
                {
                    s.Wipe();
                    s.Table("base", "abstract class Shape", 360, 40, 280, "+ Name : string", "+ abstract Area() : double");
                    s.Table("c", "Circle (r = 1)", 40, 300, 280, "- r", "+ Area() => π · r²");
                    s.Table("r", "Rect (2 x 3)", 360, 300, 280, "- w, h", "+ Area() => w · h");
                    s.Table("t", "Triangle (b = 3, h = 3)", 680, 300, 280, "- b, h", "+ Area() => b · h / 2");
                    s.Connect("c", "base", "is-a").Connect("r", "base", "is-a").Connect("t", "base", "is-a");
                })
            .Step("Polymorphism: one call, the right code",
                "The loop calls s.Area() without knowing the type. At run time each object dispatches to its own implementation through its method table.",
                "No if/switch on type: adding a Hexagon means adding a class, not editing the loop. The virtual call costs one extra indirection (load the method-table pointer, then the slot), which is usually negligible. Sealed classes let the JIT devirtualise it.",
                [15],
                s =>
                {
                    s.Box("loop", "foreach (var s in shapes) total += s.Area();", 40, 470, 920, 50, tone: Info);
                    s.Shape("loop").Mono = true;
                    s.Move("shape:loop", "shape:c", "3.14", Good).Move("shape:loop", "shape:r", "6.00", Good).Move("shape:loop", "shape:t", "4.50", Good);
                    s.HotRow("c", 1).HotRow("r", 1).HotRow("t", 1);
                    s.Say("total = 13.64 with no type checks", Good);
                })
            .Step("Composition over inheritance",
                "A RubberDuck inheriting Duck.Fly() must throw. Instead, give Duck a fly behaviour and a quack behaviour as fields, and plug in NoFly and Squeak.",
                "Composition lets behaviours vary independently and change at run time, without a subclass for every combination (FlyingQuackingDuck, NonFlyingSqueakingDuck...). This is the Strategy pattern, and it is how most real systems reuse behaviour.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("rd", "RubberDuck : Duck  (inheritance)", 40, 60, 420, "override Fly() => throw new NotSupportedException()", "override Quack() => \"squeak\"");
                    s.Paint("rd", Bad).HotRow("rd", 0);
                    s.Table("duck", "class Duck  (composition)", 520, 60, 440, "- IFlyBehavior fly", "- IQuackBehavior quack", "+ PerformFly() => fly.Fly()");
                    s.Box("fw", "FlyWithWings", 520, 300, 200, 50);
                    s.Box("nf", "NoFly", 760, 300, 200, 50, tone: Good);
                    s.Box("qk", "Quack", 520, 400, 200, 50);
                    s.Box("sq", "Squeak", 760, 400, 200, 50, tone: Good);
                    s.Connect("duck", "nf", "has-a", Good).Connect("duck", "sq", "has-a", Good);
                    s.Say("Swap behaviours at run time, no subclass explosion", Good);
                })
            .Lesson("Object-oriented programming: the four pillars", "Object-oriented design", "Encapsulation, abstraction, inheritance and polymorphism, and why composition often beats inheritance.");

    // ------------------------------------------------------------------------------------------
    private static Lesson SrpOcp() =>
        new StoryBuilder("SW3", "A class should have one reason to change (SRP), and new behaviour should arrive as new code, not edits to working code (OCP). Both keep each change small, owned by one person, and safe to ship.")
            .Setup(s =>
            {
                s.Table("god", "OrderService  (1,800 lines)", 300, 170, 400, "+ Validate(order)", "+ CalculatePrice(order)", "+ SaveToSql(order)", "+ SendConfirmationEmail(order)", "+ ExportInvoicePdf(order)");
                string[] actors = ["Sales: pricing rules", "DBA: schema", "Marketing: email copy", "Finance: invoice layout"];
                (double X, double Y)[] pos = [(40, 40), (760, 40), (40, 440), (760, 440)];
                for (var i = 0; i < 4; i++)
                {
                    s.Box("a" + i, actors[i], pos[i].X, pos[i].Y, 200, 56, "reason to change", Warn);
                    s.Connect("a" + i, "god", tone: Warn);
                }
            })
            .Code("""
                public interface IDiscountRule
                {
                    bool Applies(Customer c);
                    decimal Rate { get; }
                }

                public sealed class GoldRule : IDiscountRule { ... }
                public sealed class StudentRule : IDiscountRule { ... }      // added later: nothing else edited

                public decimal Discount(Customer c) =>
                    _rules.FirstOrDefault(r => r.Applies(c))?.Rate ?? 0m;      // closed for modification
                """)
            .Step("One class, four reasons to change",
                "OrderService validates, prices, saves, emails and renders PDFs. Sales, the DBA, marketing and finance all need changes in the same file.",
                "SRP is about people, not methods: 'one reason to change' means one actor whose requests drive the change. A god class makes every team's change risky for every other team.",
                [],
                s => s.Say("Every team edits the same file: merge conflicts and regressions", Bad))
            .Step("Split by responsibility (SRP)",
                "Each concern becomes its own class with one owner. OrderService shrinks to a thin coordinator that calls them in order.",
                "Small classes are easier to name, test and review. The coordinator still knows the workflow, but not the details of SQL, SMTP or PDF layout.",
                [],
                s =>
                {
                    s.ClearLinks();
                    s.Table("god", "OrderService", 380, 230, 240, "+ PlaceOrder(order)");
                    string[] parts = ["PriceCalculator", "OrderRepository", "OrderEmailer", "InvoicePdf", "OrderValidator"];
                    (double X, double Y)[] pos = [(40, 150), (740, 150), (40, 360), (740, 360), (380, 380)];
                    for (var i = 0; i < parts.Length; i++)
                    {
                        s.Box("c" + i, parts[i], pos[i].X, pos[i].Y, 220, 50, tone: Good);
                        s.Connect("god", "c" + i);
                        if (i < 4)
                        {
                            s.Paint("a" + i, Info).Connect("a" + i, "c" + i, tone: Info);
                        }
                    }

                    s.Say("Each class has one owner and one reason to change", Good);
                })
            .Step("Adding a discount means editing (OCP violation)",
                "Discounts live in a switch. A new Student discount means editing, recompiling and retesting code that already works.",
                "Every edit to a switch risks breaking the other cases, and the switch tends to be copied into several places (pricing, invoices, reports), which must all be found and updated.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("calc", "DiscountCalculator.Discount()", 60, 80, 520,
                        "switch (customer.Type)",
                        "  case Regular: return 0%;",
                        "  case Gold:    return 10%;",
                        "  case Vip:     return 20%;",
                        "  // Student? edit, recompile, retest everything").HotRow("calc", 4);
                    s.Box("new", "new requirement: Student 15%", 640, 120, 300, 60, tone: Warn);
                    s.Connect("new", "calc", "modify", Bad);
                })
            .Step("Open for extension: add a class",
                "Each discount becomes a rule behind IDiscountRule. Supporting students means adding StudentRule; the calculator and existing rules stay untouched.",
                "Apply OCP where variation is real and expected (payment methods, discount rules, export formats), not everywhere. Speculative extension points are YAGNI violations.",
                [1, 7, 8, 10, 11],
                s =>
                {
                    s.Wipe();
                    s.Box("calc", "DiscountCalculator", 380, 60, 240, 60, "asks each rule", Info);
                    s.Box("iface", "IDiscountRule", 380, 200, 240, 56, "Applies(c), Rate");
                    s.Connect("calc", "iface");
                    string[] rules = ["RegularRule 0%", "GoldRule 10%", "VipRule 20%", "StudentRule 15%"];
                    for (var i = 0; i < rules.Length; i++)
                    {
                        s.Box("r" + i, rules[i], 40 + i * 240, 360, 200, 56, i == 3 ? "new class" : null, i == 3 ? Good : Neutral);
                        s.Connect("r" + i, "iface", "implements", i == 3 ? Good : Neutral, dashed: true);
                    }

                    s.Say("New behaviour = new class; tested code untouched", Good);
                })
            .Lesson("SOLID 1: single responsibility and open/closed", "SOLID", "Splitting a god class by who asks for changes, and extending behaviour without editing working code.");

    // ------------------------------------------------------------------------------------------
    private static Lesson LspIsp() =>
        new StoryBuilder("SW4", "Subtypes must keep the promises of their base type (LSP), or code written for the base breaks in surprising ways. Interfaces should be small and role-based (ISP), so nobody is forced to implement or depend on methods they do not use.")
            .Setup(s =>
            {
                s.Table("rect", "class Rectangle", 60, 40, 400, "+ virtual Width { get; set; }", "+ virtual Height { get; set; }", "+ Area => Width * Height");
                s.Table("sq", "class Square : Rectangle", 60, 250, 400, "+ override Width  { set => W = H = value; }", "+ override Height { set => W = H = value; }");
                s.Connect("sq", "rect", "is-a");
                s.Box("client", "Resize(r): r.Width = 5; r.Height = 4;\nexpect r.Area == 20", 540, 100, 420, 90, tone: Info);
                s.Shape("client").Mono = true;
            })
            .Code("""
                class Square : Rectangle
                {
                    public override int Width  { set { base.Width = value; base.Height = value; } }
                    public override int Height { set { base.Width = value; base.Height = value; } }
                }

                void Resize(Rectangle r) { r.Width = 5; r.Height = 4; Debug.Assert(r.Area == 20); }   // fails for Square

                interface IPrinter { void Print(Doc d); }        // small role interfaces (ISP)
                interface IScanner { Doc Scan(); }
                """)
            .Step("Code written against Rectangle",
                "Resize sets width 5 and height 4 and expects an area of 20. With a Rectangle, that holds.",
                "The caller relies on an implicit contract: setting Height does not change Width. LSP says every subtype must honour such contracts: same or weaker preconditions, same or stronger postconditions, same invariants.",
                [7],
                s =>
                {
                    s.Move("shape:client", "shape:rect", "Area = 20", Good);
                    s.Box("res", "Rectangle -> 20", 540, 250, 420, 50, tone: Good);
                })
            .Step("Substitute a Square",
                "Square keeps its sides equal, so setting Height to 4 also sets Width to 4. The area is 16, and the assertion fails.",
                "Nothing was 'wrong' in either class on its own; the bug appears only when a Square is used where a Rectangle was expected. These failures surface far from their cause, which is why LSP violations are so costly.",
                [1, 3, 4, 7],
                s =>
                {
                    s.Move("shape:client", "shape:sq", "Area = 16", Bad);
                    s.Box("res2", "Square -> 16  (width silently became 4)", 540, 330, 420, 50, tone: Bad);
                    s.Say("A square is-a rectangle in maths, but not in behaviour", Bad);
                })
            .Step("Fix: model behaviour, not taxonomy",
                "Make shapes immutable and give both their own type behind a common IShape. No setter can break anyone's expectations.",
                "Signs of an LSP violation: overrides that throw NotSupportedException, type checks like 'if (r is Square)', or overrides that silently ignore input.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("ishape", "interface IShape", 360, 60, 280, "+ double Area { get; }");
                    s.Table("rr", "record Rectangle(double W, double H)", 40, 300, 440, ": IShape   Area => W * H");
                    s.Table("ss", "record Square(double Side)", 520, 300, 440, ": IShape   Area => Side * Side");
                    s.Connect("rr", "ishape", "implements", Good).Connect("ss", "ishape", "implements", Good);
                })
            .Step("ISP: a fat interface",
                "IMachine demands Print, Scan, Fax and Staple. A basic printer must implement all four, and three of them just throw.",
                "Fat interfaces also hurt callers: code that only prints still depends on IMachine, so a change to Fax recompiles and redeploys the print queue.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("fat", "interface IMachine", 60, 60, 380, "Print(doc)", "Scan()", "Fax(number)", "Staple(doc)");
                    s.Table("old", "class BasicPrinter : IMachine", 520, 60, 440, "Print(doc)    works", "Scan()        throw NotSupported", "Fax(number)   throw NotSupported", "Staple(doc)   throw NotSupported");
                    s.Paint("old", Bad).HotRow("old", 1, 2, 3);
                    s.Connect("old", "fat", "implements", Bad);
                })
            .Step("Split into role interfaces",
                "IPrinter, IScanner and IFax each describe one capability. BasicPrinter implements only IPrinter; a multifunction device implements all three.",
                "Clients depend on exactly the role they use (the print queue needs IPrinter only). This is also what makes test fakes trivial to write.",
                [9, 10],
                s =>
                {
                    s.Wipe();
                    string[] roles = ["IPrinter", "IScanner", "IFax"];
                    for (var i = 0; i < 3; i++)
                    {
                        s.Box("i" + i, roles[i], 60 + i * 320, 60, 240, 56, tone: Info);
                    }

                    s.Box("bp", "BasicPrinter", 60, 300, 240, 56, tone: Good);
                    s.Box("mf", "MultiFunctionDevice", 540, 300, 300, 56, tone: Good);
                    s.Box("pq", "PrintQueue (client)", 60, 440, 240, 56);
                    s.Connect("bp", "i0", "implements", dashed: true).Connect("mf", "i0", dashed: true).Connect("mf", "i1", dashed: true).Connect("mf", "i2", dashed: true);
                    s.Connect("pq", "i0", "depends on", Good);
                    s.Say("Clients depend only on the methods they use", Good);
                })
            .Lesson("SOLID 2: Liskov substitution and interface segregation", "SOLID", "The Rectangle/Square trap, honouring contracts, and splitting fat interfaces into roles.");

    // ------------------------------------------------------------------------------------------
    private static Lesson DependencyInversion() =>
        new StoryBuilder("SW5", "High-level policy should not depend on low-level details; both should depend on abstractions owned by the policy (DIP). Dependency injection then assembles the real objects in one place, and tests assemble fakes.")
            .Setup(s =>
            {
                s.Region("dom", "domain (high-level policy)", 40, 30, 920, 170, Info);
                s.Region("inf", "infrastructure (low-level details)", 40, 330, 920, 200, Warn);
                s.Box("svc", "OrderService", 100, 90, 240, 60);
                s.Box("sql", "SqlOrderRepository", 100, 410, 260, 60);
                s.Box("db", "SQL Server", 620, 410, 220, 60);
                s.Connect("svc", "sql", "new SqlOrderRepository()", Bad).Connect("sql", "db");
            })
            .Code("""
                public sealed class OrderService(IOrderRepository repo)            // depends on an abstraction
                {
                    public Task PlaceAsync(Order order) => repo.AddAsync(order);
                }

                // Program.cs: the composition root
                builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
                builder.Services.AddScoped<OrderService>();

                // unit test
                var service = new OrderService(new FakeOrderRepository());
                """)
            .Step("Policy depends on details",
                "OrderService creates SqlOrderRepository itself. The arrow points down from business rules into the database layer.",
                "Consequences: you cannot test OrderService without a database, switching storage means editing business code, and a change in the SQL layer can break compilation of the domain.",
                [],
                s => s.Say("Cannot test without a database; cannot swap storage without editing", Bad))
            .Step("Invert the dependency",
                "Define IOrderRepository next to OrderService, in the domain. The SQL class implements it, so its arrow now points up into the domain.",
                "That is the 'inversion': the source-code dependency runs against the flow of control. The domain owns the interface in its own vocabulary (AddAsync(Order)), not the database's.",
                [1, 3],
                s =>
                {
                    s.Box("port", "IOrderRepository", 440, 90, 260, 60, "owned by the domain", Info);
                    s.ClearLinks();
                    s.Connect("svc", "port", "depends on").Connect("sql", "port", "implements", Good).Connect("sql", "db");
                    s.Say("Both depend on the abstraction; arrows point into the domain", Good);
                })
            .Step("The composition root wires it up",
                "At startup, the DI container maps IOrderRepository to SqlOrderRepository. When a request arrives, it builds OrderService and injects the repository.",
                "Constructor injection makes dependencies explicit and visible in the signature. Only the composition root (Program.cs) knows the concrete types.",
                [6, 7, 8],
                s =>
                {
                    s.Box("di", "DI container", 720, 230, 220, 56, "registered at startup", Warn);
                    s.Move("shape:di", "shape:sql", "create", Warn).Move("shape:di", "shape:svc", "inject repo", Warn);
                })
            .Step("Tests inject a fake",
                "A unit test passes an in-memory FakeOrderRepository. OrderService cannot tell the difference, and the test runs in milliseconds.",
                "The same seam lets you add caching or logging decorators, or move to a different database, without touching OrderService.",
                [10, 11],
                s =>
                {
                    s.Box("fake", "FakeOrderRepository", 360, 230, 280, 56, "in memory", Good);
                    s.Connect("fake", "port", "implements", Good, dashed: true);
                    s.Move("shape:fake", "shape:svc", "inject in test", Good);
                    s.Say("Unit tests run in milliseconds, no database", Good);
                })
            .Step("Lifetimes and the captive dependency",
                "Containers create services as singletons (one per app), scoped (one per request) or transient (new every time).",
                "A singleton that captures a scoped DbContext keeps one context alive for the whole app: stale data, thread-safety bugs, and memory growth. .NET's container can detect this when scope validation is on.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("life", "service lifetimes (.NET DI)", 60, 60, 880,
                        "Singleton   one instance for the whole app   caches, IHttpClientFactory, config",
                        "Scoped      one per request / scope          DbContext, unit of work",
                        "Transient   new every time it is resolved    lightweight stateless helpers");
                    s.Box("cap", "captive dependency: Singleton -> holds a Scoped DbContext", 60, 260, 880, 60, tone: Bad);
                    s.Say("A longer-lived service must never capture a shorter-lived one", Warn);
                })
            .Lesson("SOLID 3: dependency inversion and injection", "SOLID", "Making business rules independent of databases, wiring with a DI container, and testing with fakes.");

    // ------------------------------------------------------------------------------------------
    private static Lesson CreationalPatterns() =>
        new StoryBuilder("SW6", "Creational patterns hide which concrete class is created and how. Factories choose the type, builders assemble complex objects step by step, and singletons share one instance (best managed by your DI container).")
            .Setup(s =>
            {
                s.Box("client", "Checkout", 40, 230, 200, 60);
                s.Box("fac", "PaymentFactory.Create(method)", 300, 230, 320, 60, tone: Info);
                string[] products = ["CardPayment", "PayPalPayment", "UpiPayment"];
                for (var i = 0; i < 3; i++)
                {
                    s.Box("p" + i, products[i], 720, 100 + i * 130, 240, 56, "IPayment");
                    s.Connect("fac", "p" + i, dashed: true);
                }

                s.Connect("client", "fac");
            })
            .Code("""
                public static class PaymentFactory
                {
                    public static IPayment Create(string method) => method switch
                    {
                        "card"   => new CardPayment(),
                        "paypal" => new PayPalPayment(),
                        "upi"    => new UpiPayment(),
                        _ => throw new ArgumentException($"Unknown method {method}")
                    };
                }

                var request = new RequestBuilder().Url("/orders").Header("Auth", token).Timeout(TimeSpan.FromSeconds(5)).Build();

                public sealed class AppConfig { public static AppConfig Instance { get; } = new(); private AppConfig() { } }
                """)
            .Step("Factory: choose the class in one place",
                "Checkout asks the factory for 'paypal' and gets back an IPayment. It never writes 'new PayPalPayment()' itself.",
                "When a new payment method arrives, only the factory changes. Checkout keeps working against IPayment. In DI-based apps a keyed service or a Func<string, IPayment> often plays the factory's role.",
                [1, 3, 6],
                s =>
                {
                    s.Move("shape:client", "shape:fac", "\"paypal\"", Info).Move("shape:fac", "shape:p1", "new", Good);
                    s.Paint("p1", Good);
                })
            .Step("Abstract factory: a whole family",
                "A theme factory creates a matching set: dark button, dark checkbox. Switching to the light factory switches every widget consistently.",
                "Use it when objects must be used together and mixing families would be a bug, for example UI themes or cloud providers (AWS vs Azure clients for storage, queues and secrets).",
                [],
                s =>
                {
                    s.Wipe();
                    s.Box("ui", "SettingsScreen", 40, 230, 220, 60);
                    s.Box("dark", "DarkThemeFactory", 330, 110, 260, 56, tone: Good);
                    s.Box("light", "LightThemeFactory", 330, 350, 260, 56);
                    string[] parts = ["Button", "Checkbox"];
                    for (var i = 0; i < 2; i++)
                    {
                        s.Box("d" + i, "Dark" + parts[i], 680, 60 + i * 80, 240, 50, tone: Good);
                        s.Box("l" + i, "Light" + parts[i], 680, 330 + i * 80, 240, 50);
                        s.Connect("dark", "d" + i).Connect("light", "l" + i);
                    }

                    s.Connect("ui", "dark", "theme = dark", Good).Connect("ui", "light", dashed: true);
                    s.Say("A whole family of matching objects from one choice", Info);
                })
            .Step("Builder: assemble step by step",
                "A request with many optional parts is built with readable chained calls, then Build() produces one immutable, validated object.",
                "Builders replace telescoping constructors with ten parameters. Build() is the single place to validate combinations, for example 'retries require a timeout'.",
                [12],
                s =>
                {
                    s.Wipe();
                    string[] chain = ["new RequestBuilder()", ".Url(\"/orders\")", ".Header(\"Auth\", token)", ".Timeout(5 s)", ".Retry(3)", ".Build()"];
                    for (var i = 0; i < chain.Length; i++)
                    {
                        s.Box("b" + i, chain[i], 40, 50 + i * 76, 300, 56, tone: i == 5 ? Good : Info);
                        s.Shape("b" + i).Mono = true;
                    }

                    s.Table("req", "HttpRequest (immutable)", 520, 160, 420, "Url      /orders", "Headers  Auth: ***", "Timeout  5 s", "Retries  3");
                    s.Connect("b5", "req", "validated", Good);
                    for (var i = 1; i <= 4; i++)
                    {
                        s.Move("shape:b" + i, "shape:req", string.Empty, Info);
                    }
                })
            .Step("Singleton: one shared instance",
                "AppConfig.Instance gives everyone the same object. Convenient, but it is global state hidden inside every class that touches it.",
                "Classic singletons make tests interfere with each other and hide dependencies. Register the class as a singleton in the DI container instead, and use Lazy<T> if creation must be thread-safe and deferred.",
                [14],
                s =>
                {
                    s.Wipe();
                    s.Box("single", "AppConfig.Instance", 380, 220, 260, 70, "one shared object", Warn);
                    string[] users = ["OrderService", "EmailSender", "PriceCalculator", "Unit test #1", "Unit test #2"];
                    (double X, double Y)[] pos = [(40, 60), (720, 60), (40, 400), (720, 400), (380, 460)];
                    for (var i = 0; i < users.Length; i++)
                    {
                        s.Box("u" + i, users[i], pos[i].X, pos[i].Y, 240, 50, tone: i >= 3 ? Bad : Neutral);
                        s.Connect("u" + i, "single", i >= 3 ? "tests share state" : null, i >= 3 ? Bad : Neutral);
                    }

                    s.Say("Prefer a DI-registered singleton over a static Instance", Warn);
                })
            .Lesson("Creational patterns: factory, abstract factory, builder, singleton", "Design patterns", "Who decides which class is created, how complex objects are assembled, and the trouble with global singletons.");

    // ------------------------------------------------------------------------------------------
    private static Lesson StructuralPatterns() =>
        new StoryBuilder("SW7", "Structural patterns compose objects: adapters make mismatched interfaces fit, decorators add behaviour by wrapping, proxies control access, facades simplify subsystems, and composites treat groups like single items.")
            .Setup(s =>
            {
                s.Box("client", "WeatherWidget", 40, 230, 200, 60);
                s.Box("port", "IWeather", 290, 230, 200, 60, "Celsius, objects", Info);
                s.Box("adapter", "LegacyWeatherAdapter", 540, 230, 220, 60, "translates", Good);
                s.Box("legacy", "OldXmlService", 800, 230, 170, 60, "Fahrenheit, XML", Warn);
                s.Connect("client", "port").Connect("adapter", "port", "implements", dashed: true).Connect("adapter", "legacy", "wraps");
            })
            .Code("""
                public sealed class CachingRepository(IRepository inner, IMemoryCache cache) : IRepository
                {
                    public Order? GetById(int id) =>
                        cache.GetOrCreate(id, _ => inner.GetById(id));        // decorator: same interface, extra behaviour
                }

                IRepository repo = new RetryRepository(new LoggingRepository(new CachingRepository(new SqlRepository(), cache)));

                using var zip = new GZipStream(new BufferedStream(File.OpenWrite("log.gz")), CompressionLevel.Optimal);
                """)
            .Step("Adapter: make it fit",
                "Your widget expects IWeather in Celsius. An old service speaks XML in Fahrenheit. The adapter implements IWeather and translates both ways.",
                "Adapters isolate legacy or third-party APIs at the edge, so their quirks do not spread through your codebase. When the vendor changes, only the adapter changes.",
                [],
                s => s.Move("shape:legacy", "shape:adapter", "<temp f=\"77\"/>", Warn).Move("shape:adapter", "shape:client", "25 °C", Good))
            .Step("Decorator: wrap to add behaviour",
                "Retry wraps logging, which wraps caching, which wraps the SQL repository. All four implement IRepository, so the caller cannot tell how many layers there are.",
                "Each decorator does one job and can be added or removed at the composition root. .NET streams work the same way: GZipStream over BufferedStream over FileStream.",
                [1, 3, 4, 7, 9],
                s =>
                {
                    s.Wipe();
                    s.Region("d3", "RetryRepository: retries transient failures", 40, 40, 920, 480, Warn);
                    s.Region("d2", "LoggingRepository: logs timing", 90, 100, 820, 370, Info);
                    s.Region("d1", "CachingRepository: returns cached results", 140, 160, 720, 260, Good);
                    s.Box("core", "SqlRepository", 380, 260, 240, 70, "the real work", Info);
                    s.Move("shape:d1", "shape:core", "cache miss: query", Info);
                    s.Say("Same interface at every layer: add features by wrapping, not editing", Good);
                })
            .Step("Proxy: control access",
                "A lazy image proxy has the same interface as the real 40 MB image, but loads it only when Display() is first called.",
                "Other proxies: remote proxies (a gRPC client stub looks like a local object), protection proxies (check permissions first), and EF Core's lazy-loading proxies.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Box("client", "ReportPage", 40, 230, 200, 60);
                    s.Box("proxy", "LazyImageProxy", 320, 230, 260, 60, "same interface", Info);
                    s.Box("real", "HighResImage (40 MB)", 680, 230, 280, 60, "expensive", Warn);
                    s.Connect("client", "proxy", "Display()").Connect("proxy", "real", "loaded on first use", dashed: true);
                })
            .Step("Facade: one simple entry point",
                "Placing an order touches inventory, payments, fraud checks, shipping and email. A facade exposes one PlaceOrder() call and coordinates the rest.",
                "Facades reduce what callers must know, not what the system does. Keep them thin: if the facade grows business rules, it becomes a god class.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Box("client", "CheckoutController", 40, 230, 220, 60);
                    s.Box("facade", "CheckoutFacade.PlaceOrder()", 320, 230, 300, 60, tone: Info);
                    string[] subs = ["Inventory", "Fraud check", "Payments", "Shipping", "Email"];
                    for (var i = 0; i < subs.Length; i++)
                    {
                        s.Box("s" + i, subs[i], 720, 40 + i * 100, 240, 56);
                        s.Connect("facade", "s" + i);
                        s.Move("shape:facade", "shape:s" + i, string.Empty, Info);
                    }

                    s.Connect("client", "facade", "one call", Good);
                })
            .Step("Composite: groups behave like items",
                "A folder and a file both answer Size(). A folder's size is the sum of its children, whatever they are.",
                "Composites appear everywhere trees do: UI element trees, file systems, organisation charts, expression trees in compilers and LINQ.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Box("root", "/ (folder) 13 MB", 360, 40, 280, 56, tone: Info);
                    s.Box("docs", "docs (folder) 5 MB", 140, 200, 280, 56, tone: Info);
                    s.Box("photo", "photo.jpg 8 MB", 580, 200, 280, 56);
                    s.Box("a", "a.pdf 2 MB", 40, 360, 220, 56);
                    s.Box("b", "b.pdf 3 MB", 300, 360, 220, 56);
                    s.Connect("root", "docs").Connect("root", "photo").Connect("docs", "a").Connect("docs", "b");
                    s.Say("Treat a group and a single item the same way", Info);
                })
            .Lesson("Structural patterns: adapter, decorator, proxy, facade, composite", "Design patterns", "Fitting mismatched interfaces, layering behaviour by wrapping, and simplifying complex subsystems.");

    // ------------------------------------------------------------------------------------------
    private static Lesson StrategyObserverCommand() =>
        new StoryBuilder("SW8", "Behavioural patterns decide who does what. Strategy swaps algorithms, Observer broadcasts changes to whoever subscribed, and Command turns actions into objects that can be queued, logged and undone.")
            .Setup(s =>
            {
                s.Box("nav", "Navigator", 380, 60, 240, 60, "strategy: Car", Info);
                string[] strategies = ["CarRoute", "BikeRoute", "WalkRoute"];
                for (var i = 0; i < 3; i++)
                {
                    s.Box("s" + i, strategies[i], 80 + i * 310, 260, 220, 56, "IRouteStrategy");
                }

                s.Box("route", "12 km · 18 min", 380, 420, 240, 56, "result", Good);
                s.Connect("nav", "s0", "uses", Good).Connect("nav", "route");
            })
            .Code("""
                public interface IRouteStrategy { Route Plan(Point from, Point to); }
                navigator.Strategy = new WalkRoute();                       // strategy: swap the algorithm

                ticker.PriceChanged += chart.OnPrice;                         // observer: subscribe...
                ticker.PriceChanged -= chart.OnPrice;                         // ...and unsubscribe, or the ticker keeps chart alive

                public interface ICommand { void Execute(); void Undo(); }
                undo.Push(cmd); cmd.Execute();                              // command: actions as objects
                undo.Pop().Undo();
                """)
            .Step("Strategy: one job, interchangeable algorithms",
                "The navigator delegates route planning to a strategy object. Right now it uses CarRoute.",
                "Strategy replaces a growing if/else over modes with objects behind one interface. It is composition over inheritance in its purest form.",
                [1])
            .Step("Swap at run time",
                "The user switches to walking. The navigator gets a WalkRoute; its own code does not change, and the result is different.",
                "In C# a strategy is often just a delegate (Func<Point, Point, Route>) or a lambda. Sorting with a custom IComparer is the strategy pattern too.",
                [2],
                s =>
                {
                    s.Write("nav", "Navigator", "strategy: Walk");
                    s.Disconnect("nav", "s0").Connect("nav", "s2", "uses", Good);
                    s.Paint("s2", Good).Write("route", "9 km · 1 h 50 min");
                })
            .Step("Observer: tell everyone who cares",
                "The ticker publishes price changes. Chart, alert, audit log and portfolio subscribed earlier; each is notified without the ticker knowing their types.",
                "C# events, IObservable<T> and message buses are all Observer. The subject stays decoupled: add a new subscriber without changing the ticker.",
                [4],
                s =>
                {
                    s.Wipe();
                    s.Box("subj", "StockTicker", 380, 60, 240, 60, "MSFT price", Info);
                    string[] obs = ["Chart", "PriceAlert", "AuditLog", "Portfolio"];
                    for (var i = 0; i < obs.Length; i++)
                    {
                        s.Box("o" + i, obs[i], 40 + i * 240, 320, 200, 56);
                        s.Connect("o" + i, "subj", "subscribe", dashed: true);
                        s.Move("shape:subj", "shape:o" + i, "425.10", Good);
                    }
                })
            .Step("Unsubscribe, or leak",
                "The portfolio page closed but never unsubscribed. The ticker still holds a reference to it, so it is never garbage collected and still receives updates.",
                "This is one of the most common managed-memory leaks (see the Memory track). Unsubscribe in Dispose, use weak event patterns, or scope subscriptions to the subscriber's lifetime.",
                [5],
                s =>
                {
                    s.Paint("o3", Bad).Write("o3", "Portfolio (closed)");
                    s.Connect("o3", "subj", "still referenced", Bad, dashed: true);
                    s.Say("Forgotten subscriptions keep dead objects alive", Bad);
                })
            .Step("Command: actions as objects",
                "Each edit (type 'Hello', type ' World', bold 'World') becomes an object with Execute() and Undo(), pushed on an undo stack.",
                "Commands can be queued, logged, replayed, sent across a network or retried. Job queues, transactional outboxes and CQRS commands are all descendants of this idea.",
                [7, 8],
                s =>
                {
                    s.Wipe();
                    s.Caption("ucap", "undo stack (top on the right)", 60, 80);
                    s.Cells("u", ["Type \"Hello\"", "Type \" World\"", "Bold \"World\""], 60, 110, 260, 56, 20, tags: false, tone: Info);
                    s.Box("doc", "Hello **World**", 300, 330, 400, 70, "document", Good);
                    s.Shape("doc").Mono = true;
                })
            .Step("Undo",
                "Undo pops the last command and calls its Undo(): the bold formatting disappears. Redo would push it onto a redo stack and execute it again.",
                "Each command must capture enough state to reverse itself. For complex documents, editors often store snapshots or inverse operations instead.",
                [9],
                s =>
                {
                    s.Ghost("u2").Paint("u2", Warn);
                    s.Move("shape:u2", "shape:doc", "Undo()", Warn);
                    s.Write("doc", "Hello World");
                })
            .Lesson("Behavioural patterns: strategy, observer, command", "Design patterns", "Swappable algorithms, publish/subscribe notifications and the leak they can cause, and undoable commands.");

    // ------------------------------------------------------------------------------------------
    private static Lesson StateChainTemplate()
    {
        string[] states = ["Created", "Paid", "Shipped", "Delivered", "Cancelled"];
        (double X, double Y)[] pos = [(120, 200), (340, 200), (560, 200), (820, 200), (340, 430)];

        void Current(SceneState s, int index)
        {
            for (var i = 0; i < states.Length; i++)
            {
                s.Paint("st" + i, i == index ? Info : Neutral);
            }
        }

        return new StoryBuilder("SW9", "Model lifecycles as explicit state machines so illegal transitions are impossible, pass requests along a chain of handlers that may each act or stop (middleware), and fix an algorithm's skeleton while letting subclasses fill in steps (template method).")
            .Setup(s =>
            {
                for (var i = 0; i < states.Length; i++)
                {
                    s.Circle("st" + i, states[i], pos[i].X, pos[i].Y, 58);
                }

                s.Connect("st0", "st1", "Pay()").Connect("st1", "st2", "Ship()").Connect("st2", "st3", "Deliver()")
                    .Connect("st0", "st4", "Cancel()").Connect("st1", "st4", "Cancel(): refund");
                Current(s, 0);
            })
            .Code("""
                enum OrderState { Created, Paid, Shipped, Delivered, Cancelled }

                public void Cancel() => State = State switch
                {
                    OrderState.Created or OrderState.Paid => OrderState.Cancelled,
                    _ => throw new InvalidOperationException($"Cannot cancel an order that is {State}")
                };

                app.UseExceptionHandler("/error");       // chain of responsibility: each middleware
                app.UseAuthentication();                  // may handle the request or pass it on
                app.UseRateLimiter();
                app.MapGet("/orders", GetOrders);
                """)
            .Step("An explicit state machine",
                "An order is Created, then Paid, Shipped and Delivered, or Cancelled before it ships. Every allowed transition is an arrow.",
                "Without an explicit model, status is a string plus boolean flags spread through the code, and impossible combinations ('shipped but unpaid') eventually appear in production.",
                [1])
            .Step("Valid transitions move the state",
                "Pay() moves Created to Paid; Ship() moves Paid to Shipped. Each transition can run side effects such as charging the card or reserving stock.",
                "Libraries such as Stateless, or a simple switch expression, keep the transition table in one place, where it can be drawn, reviewed and tested.",
                [3, 4, 5],
                s => Current(s, 2))
            .Step("Invalid transitions are rejected",
                "Cancel() on a shipped order is not an arrow in the diagram, so it throws instead of silently corrupting the order.",
                "Failing loudly at the boundary of the state machine is far cheaper than discovering inconsistent data weeks later. Test every arrow, and at least one missing arrow.",
                [6],
                s =>
                {
                    s.Box("illegal", "Cancel() while Shipped: rejected", 560, 330, 380, 56, tone: Bad);
                    s.Say("Illegal transitions are impossible, not just unlikely", Good);
                })
            .Step("Chain of responsibility: middleware",
                "A request passes through exception handling, authentication, rate limiting and routing before reaching the endpoint. Each handler does one job and calls the next.",
                "ASP.NET Core middleware, HttpClient DelegatingHandlers and MediatR pipeline behaviours are all chains. Order matters: exception handling must come first so it can catch everything after it.",
                [9, 10, 11, 12],
                s =>
                {
                    s.Wipe();
                    string[] mw = ["Exception handler", "Authentication", "Rate limiter", "Routing", "Endpoint"];
                    for (var i = 0; i < mw.Length; i++)
                    {
                        s.Box("p" + i, mw[i], 30 + i * 195, 220, 145, 60, tone: i == 4 ? Good : Neutral);
                        if (i > 0)
                        {
                            s.Connect("p" + (i - 1), "p" + i, "next()");
                            s.Move("shape:p" + (i - 1), "shape:p" + i, string.Empty, Info);
                        }
                    }

                    s.Box("req", "GET /orders + token", 40, 80, 210, 50, tone: Info);
                    s.Move("shape:req", "shape:p0", "request", Info);
                })
            .Step("A handler can stop the chain",
                "Without a valid token, authentication answers 401 itself and the request never reaches routing or the endpoint.",
                "Short-circuiting keeps expensive or sensitive work behind cheap checks. It is also why authorization must come after authentication and before the endpoint.",
                [10],
                s =>
                {
                    s.Write("req", "GET /orders (no token)").Paint("req", Warn);
                    s.Paint("p1", Bad).PaintAll(Neutral, "p4");
                    s.Ghost("p2").Ghost("p3").Ghost("p4");
                    s.Move("shape:p1", "shape:req", "401 Unauthorized", Bad);
                })
            .Step("Template method: fixed skeleton, variable steps",
                "Exporter.Export() always runs Fetch, Transform and Write in that order. CSV and JSON exporters override only Transform.",
                "The base class owns the algorithm's order and invariants. Today this is often done with composition (pass the transform as a delegate or strategy) to avoid inheritance coupling.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("base", "abstract class Exporter", 280, 40, 440, "+ Export() { Fetch(); Transform(); Write(); }", "# Fetch()              shared", "# abstract Transform()", "# Write()              shared");
                    s.HotRow("base", 2);
                    s.Table("csv", "CsvExporter", 80, 340, 360, "override Transform() => CSV rows");
                    s.Table("json", "JsonExporter", 560, 340, 360, "override Transform() => JSON");
                    s.Connect("csv", "base", "is-a").Connect("json", "base", "is-a");
                })
            .Lesson("Behavioural patterns: state machine, chain of responsibility, template method", "Design patterns", "Order lifecycles that reject illegal moves, middleware pipelines, and algorithm skeletons.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson CleanArchitecture() =>
        new StoryBuilder("SW10", "Put business rules at the centre and make every dependency point inward. Frameworks, databases and UIs become replaceable plugins (adapters) around a core that knows nothing about them.")
            .Setup(s =>
            {
                string[] layers = ["Presentation: controllers, UI", "Business logic", "Data access: EF Core, SQL"];
                for (var i = 0; i < 3; i++)
                {
                    s.Box("l" + i, layers[i], 300, 40 + i * 160, 400, 70);
                    if (i > 0)
                    {
                        s.Connect("l" + (i - 1), "l" + i, "depends on");
                    }
                }

                s.Box("db", "Database", 760, 360, 200, 70, tone: Warn);
                s.Connect("l2", "db");
            })
            .Code("""
                // Domain: entities and ports, no framework references
                public interface IOrderStore { Task SaveAsync(Order order); }

                // Application: use cases
                public sealed class PlaceOrder(IOrderStore store, TimeProvider clock)
                {
                    public async Task<OrderId> Handle(PlaceOrderCommand cmd)
                    {
                        var order = Order.Create(cmd.Lines, clock.GetUtcNow());
                        await store.SaveAsync(order);
                        return order.Id;
                    }
                }

                // Infrastructure: adapters
                public sealed class SqlOrderStore(AppDbContext db) : IOrderStore { ... }
                """)
            .Step("Classic layers",
                "UI calls business logic, which calls data access. Simple, but business logic now depends on the database layer and its types.",
                "Over time, entity classes get shaped by the ORM, SQL concerns leak into business rules, and testing a rule requires the database. Layering is fine; the direction of the last arrow is the problem.",
                [],
                s => s.Say("Business rules depend on database details", Warn))
            .Step("Clean architecture: the dependency rule",
                "Entities sit at the centre, use cases around them, then adapters, then frameworks and drivers. Source-code dependencies only ever point inward.",
                "The inner circles define interfaces (ports) for what they need; outer circles implement them. The web framework and the database are details, chosen and replaceable at the edge.",
                [1, 2, 4, 5],
                s =>
                {
                    s.Wipe();
                    s.Region("r3", "frameworks and drivers: ASP.NET, EF Core, SQL, queues", 40, 30, 920, 500, Warn);
                    s.Region("r2", "interface adapters: controllers, presenters, repositories", 120, 90, 760, 370, Info);
                    s.Region("r1", "use cases: PlaceOrder, CancelOrder", 220, 150, 560, 250, Good);
                    s.Box("core", "entities: Order, Money", 370, 250, 260, 70, tone: Good);
                    s.Box("web", "HTTP controller", 60, 474, 200, 44);
                    s.Box("efc", "SqlOrderStore", 740, 474, 200, 44);
                    s.Connect("web", "r1", "calls a use case").Connect("efc", "r1", "implements a port");
                    s.Say("Source-code dependencies point only inward", Good);
                })
            .Step("Hexagonal: ports and adapters",
                "The same idea drawn as a hexagon: driving adapters (REST, CLI, tests) call in through ports; driven adapters (SQL, email, Kafka) implement ports the core defines.",
                "Every way in or out is an adapter, so the core can be exercised end to end by a test harness without any infrastructure.",
                [2, 15],
                s =>
                {
                    s.Wipe();
                    s.Box("core", "domain + use cases", 370, 190, 260, 160, "ports on the boundary", Good);
                    string[] drivers = ["REST controller", "CLI", "Test harness"];
                    string[] driven = ["SQL adapter", "SMTP email", "Kafka publisher"];
                    for (var i = 0; i < 3; i++)
                    {
                        s.Box("a" + i, drivers[i], 40, 100 + i * 140, 240, 56, tone: Info);
                        s.Box("b" + i, driven[i], 720, 100 + i * 140, 240, 56, tone: Warn);
                        s.Connect("a" + i, "core", "driving port").Connect("b" + i, "core", "implements port", dashed: true);
                    }

                    s.Caption("lc", "driving side (calls in)", 40, 60);
                    s.Caption("rc", "driven side (called by the core)", 720, 60);
                })
            .Step("Swap an adapter, keep the core",
                "Moving from SQL Server to Cosmos DB means writing a new adapter. The domain and use cases do not change, and neither do their tests.",
                "Do not over-apply it: a small CRUD service does not need four projects and mapping layers. Use the dependency rule where business rules are rich and long-lived.",
                [16],
                s =>
                {
                    s.Write("b0", "Cosmos DB adapter").Paint("b0", Good);
                    s.Say("Swap infrastructure without touching the domain", Good);
                })
            .Lesson("Clean and hexagonal architecture", "Architecture", "The dependency rule, ports and adapters, and keeping business rules independent of frameworks.");

    // ------------------------------------------------------------------------------------------
    private static Lesson DomainDrivenDesign() =>
        new StoryBuilder("SW11", "Domain-driven design aligns code with the business: each bounded context has its own model and language, entities have identity, value objects are immutable values, aggregates protect invariants, and domain events tell other contexts what happened.")
            .Setup(s =>
            {
                (string Id, string Title, Tone Tone, string[] Rows)[] contexts =
                [
                    ("sales", "Sales context", Info, ["lead source", "discount tier", "account manager"]),
                    ("support", "Support context", Warn, ["open tickets", "SLA level", "preferred channel"]),
                    ("billing", "Billing context", Good, ["payment methods", "invoices", "tax id"])
                ];
                for (var i = 0; i < contexts.Length; i++)
                {
                    s.Region(contexts[i].Id, contexts[i].Title, 30 + i * 320, 40, 300, 280, contexts[i].Tone);
                    s.Table("cust" + i, "Customer", 50 + i * 320, 100, 260, contexts[i].Rows);
                }
            })
            .Code("""
                public sealed record Money(decimal Amount, string Currency);            // value object

                public sealed class Order                                               // aggregate root
                {
                    private readonly List<OrderLine> _lines = [];
                    public void AddLine(Product product, int quantity)
                    {
                        if (Total + product.Price * quantity > _creditLimit)
                            throw new DomainException("Credit limit exceeded");          // invariant
                        _lines.Add(new OrderLine(product.Id, quantity, product.Price));
                        _events.Add(new OrderLineAdded(Id, product.Id, quantity));       // domain event
                    }
                }
                """)
            .Step("Bounded contexts",
                "'Customer' means different things to sales, support and billing. Each context keeps its own small Customer model instead of one giant shared class.",
                "Inside a context, everyone uses the same words with the same meaning (the ubiquitous language), and the code uses them too. Contexts are often good boundaries for teams, modules or services.",
                [],
                s => s.Say("Same word, three models: each context owns its own", Info))
            .Step("Context map and anti-corruption layer",
                "When sales hands a won deal to billing, a translation layer converts sales' model into billing's, so neither is polluted by the other's concepts.",
                "Anti-corruption layers are adapters at context boundaries. They are especially valuable when integrating legacy systems whose model you do not control.",
                [],
                s =>
                {
                    s.Box("acl", "anti-corruption layer", 350, 400, 300, 56, "translate", Warn);
                    s.Connect("sales", "acl", "DealWon").Connect("acl", "billing", "NewAccount");
                })
            .Step("Entities and value objects",
                "An Order has an identity that persists while its contents change. Money(25, EUR) is a value: immutable, and equal to any other Money(25, EUR).",
                "Value objects remove whole bug classes: no adding EUR to USD, no negative quantities, no accidental shared mutation. C# records give value equality for free.",
                [1],
                s =>
                {
                    s.Wipe();
                    s.Table("order", "Entity: Order", 60, 60, 420, "Id = 7f3a... (identity)", "status changes over time", "same items, different id: different orders");
                    s.Table("money", "Value object: Money", 520, 60, 420, "Amount = 25.00, Currency = EUR", "immutable", "equal if all values are equal");
                    s.Box("eq", "new Money(25, \"EUR\") == new Money(25, \"EUR\")   is true", 60, 300, 880, 56, tone: Good);
                    s.Shape("eq").Mono = true;
                })
            .Step("Aggregates protect invariants",
                "Order and its lines form one aggregate. Outside code must go through the root (AddLine), which enforces 'total within credit limit'. Editing a line directly is not allowed.",
                "An aggregate is a consistency boundary: it is loaded, changed and saved as a unit in one transaction. Keep aggregates small, and reference other aggregates by id.",
                [3, 6, 8, 9, 10],
                s =>
                {
                    s.Wipe();
                    s.Region("agg", "Order aggregate", 320, 40, 640, 460, Info);
                    s.Box("root", "Order (aggregate root)", 360, 90, 280, 60, tone: Info);
                    s.Box("l0", "OrderLine: 2 x keyboard", 360, 210, 280, 50);
                    s.Box("l1", "OrderLine: 1 x mouse", 360, 290, 280, 50);
                    s.Box("rule", "invariant: total <= credit limit", 680, 90, 260, 60, tone: Good);
                    s.Box("ext", "outside code", 40, 230, 200, 60);
                    s.Connect("root", "l0").Connect("root", "l1");
                    s.Connect("ext", "root", "AddLine()", Good).Connect("ext", "l1", "line.Quantity = 50", Bad);
                    s.Say("Change the cluster only through its root", Good);
                })
            .Step("Domain events",
                "When an order is placed, the aggregate records OrderPlaced. Billing, inventory and email react to it, without the order knowing they exist.",
                "Domain events decouple contexts and are the natural input for integration events, outboxes and event sourcing (see the next lesson and the System Design track).",
                [11],
                s =>
                {
                    s.Wipe();
                    s.Box("root", "Order.Place()", 40, 230, 220, 60, tone: Info);
                    s.Box("ev", "OrderPlaced { OrderId, Total }", 320, 230, 320, 60, "domain event", Warn);
                    string[] handlers = ["Billing: create invoice", "Inventory: reserve stock", "Email: confirmation"];
                    for (var i = 0; i < 3; i++)
                    {
                        s.Box("h" + i, handlers[i], 720, 100 + i * 130, 240, 56, tone: Good);
                        s.Connect("ev", "h" + i);
                        s.Move("shape:ev", "shape:h" + i, string.Empty, Warn);
                    }

                    s.Connect("root", "ev", "raises");
                })
            .Lesson("Domain-driven design: contexts, aggregates, value objects, events", "Architecture", "Modelling the business in code: bounded contexts, consistency boundaries and domain events.");

    // ------------------------------------------------------------------------------------------
    private static Lesson CqrsEventSourcing()
    {
        string[] events = ["Opened", "Deposited 100", "Withdrew 30", "Deposited 50"];

        return new StoryBuilder("SW12", "CQRS separates the model that changes data from the models that read it, so each can be optimised. Event sourcing stores every change as an immutable event and derives current state by replaying them, giving a perfect audit log and time travel.")
            .Setup(s =>
            {
                s.Box("ui", "UI", 40, 230, 120, 60);
                s.Box("cmd", "Command: PlaceOrder", 220, 110, 240, 56, tone: Warn);
                s.Box("qry", "Query: GetOrderSummary", 220, 360, 240, 56, tone: Info);
                s.Box("wm", "write model", 520, 110, 220, 56, "validates rules");
                s.Box("rm", "read model", 520, 360, 220, 56, "shaped per screen");
                s.Box("wdb", "orders tables", 800, 110, 160, 56);
                s.Box("rdb", "summary view", 800, 360, 160, 56);
                s.Connect("ui", "cmd").Connect("cmd", "wm").Connect("wm", "wdb").Connect("ui", "qry").Connect("qry", "rm").Connect("rm", "rdb");
                s.Connect("wdb", "rdb", "projection (async)", Warn, dashed: true);
            })
            .Code("""
                abstract record AccountEvent;
                record Deposited(decimal Amount) : AccountEvent;
                record Withdrew(decimal Amount) : AccountEvent;

                decimal Balance(IEnumerable<AccountEvent> events) => events.Aggregate(0m, (balance, e) => e switch
                {
                    Deposited d => balance + d.Amount,
                    Withdrew w  => balance - w.Amount,
                    _ => balance
                });
                """)
            .Step("CQRS: separate writes from reads",
                "Commands go to a write model that enforces business rules. Queries read from a separate model shaped exactly for each screen. A projection keeps the read side up to date.",
                "Reads often outnumber writes 100:1 and need different shapes (lists, search, dashboards). With CQRS each side scales and evolves independently. The price is eventual consistency: the read side lags slightly.",
                [])
            .Step("Event sourcing: store what happened",
                "Instead of storing 'balance = 120', store the events: opened, deposited 100, withdrew 30, deposited 50. The balance is computed by replaying them.",
                "Events are facts in past tense and are never updated or deleted. Corrections are new events (for example DepositReversed). The event stream is the source of truth.",
                [1, 2, 3, 5, 6, 7],
                s =>
                {
                    s.Wipe();
                    s.Caption("ecap", "event stream: account 42 (append-only)", 40, 90, 500);
                    s.Cells("ev", events, 40, 120, 220, 56, 10, tags: false, tone: Info);
                    s.Box("state", "balance = 120", 380, 320, 240, 60, "replay of 4 events", Good);
                    for (var i = 1; i < 4; i++)
                    {
                        s.Move("shape:ev" + i, "shape:state", string.Empty, Info);
                    }
                })
            .Step("Time travel",
                "Replay only the first three events and you get the balance as it was then: 70. Any past state can be reconstructed.",
                "This makes auditing, debugging ('how did we get here?') and new reports over historical data straightforward. A new projection can be built by replaying the whole history.",
                [5],
                s =>
                {
                    s.Ghost("ev3");
                    s.Write("state", "balance = 70", "as of event 3").Paint("state", Warn);
                    s.Say("Rebuild any past state; add new projections from history", Info);
                })
            .Step("Snapshots and trade-offs",
                "Long streams are slow to replay, so store a snapshot every N events and replay only what came after it.",
                "Costs: events are forever, so their schemas must be versioned and upcast; queries need projections; and the whole team must think in events. Use event sourcing where history matters (finance, ledgers, workflows), not for every table.",
                [],
                s =>
                {
                    s.Ghost("ev3", false);
                    s.Box("snap", "snapshot @ event 3: balance 70", 40, 220, 400, 50, tone: Warn);
                    s.Write("state", "balance = 120", "snapshot + 1 event").Paint("state", Good);
                    s.Move("shape:snap", "shape:state", "+ Deposited 50", Good);
                })
            .Lesson("CQRS and event sourcing", "Architecture", "Separate read and write models, storing events instead of state, replay and time travel.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson MonolithMicroservices() =>
        new StoryBuilder("SW13", "Monoliths are simple to build, test and deploy; microservices buy independent deployment and scaling at the cost of network calls, partial failures and distributed data. A modular monolith is usually the right starting point.")
            .Setup(s =>
            {
                s.Region("mono", "one deployable", 200, 40, 600, 300, Info);
                string[] mods = ["Orders", "Billing", "Catalog", "Users"];
                for (var i = 0; i < 4; i++)
                {
                    s.Box("m" + i, mods[i], 240 + i % 2 * 280, 100 + i / 2 * 110, 240, 70);
                }

                s.Box("db", "one database", 380, 430, 240, 60);
                s.Connect("mono", "db");
                s.Connect("m0", "m1", "in-process call").Connect("m0", "m2");
            })
            .NoCode()
            .Step("Monolith",
                "All modules run in one process and share one database. A call between modules is a function call: nanoseconds, no network, one transaction.",
                "Monoliths are easy to debug, refactor and deploy. Their problems come with size: long builds, risky big deploys, everyone scaling together, and boundaries eroding without enforcement.")
            .Step("Modular monolith",
                "Keep one deployable, but enforce module boundaries: each module owns its tables (its own schema) and exposes a small public API. No reaching into another module's internals.",
                "This gives most of the design benefits of microservices without the distributed-systems costs, and makes later extraction of a module into a service much easier.",
                s =>
                {
                    s.Write("mono", "one deployable, enforced module boundaries");
                    s.Write("db", "schemas: orders | billing | catalog | users");
                    s.Resize("db", 400, 60).Place("db", 300, 430);
                    s.Write("m0", "Orders", "public API only").Write("m1", "Billing", "public API only");
                })
            .Step("Microservices",
                "Each service is deployed, scaled and owned independently, with its own database. Services talk over HTTP, gRPC or messages, usually behind an API gateway.",
                "Conway's law: service boundaries tend to mirror team boundaries. That is the main reason to choose microservices: many teams shipping independently.",
                s =>
                {
                    s.Wipe();
                    s.Box("gw", "API gateway", 380, 30, 240, 56, tone: Info);
                    string[] svcs = ["Orders", "Billing", "Catalog", "Users"];
                    for (var i = 0; i < 4; i++)
                    {
                        s.Box("s" + i, svcs[i], 40 + i * 240, 200, 200, 60, "own deploy, own team");
                        s.Box("d" + i, svcs[i].ToLowerInvariant() + " db", 60 + i * 240, 380, 160, 50);
                        s.Connect("gw", "s" + i).Connect("s" + i, "d" + i);
                    }

                    s.Connect("s0", "s1", "HTTP / gRPC: ms", Warn).Connect("s0", "s2", "ms", Warn);
                })
            .Step("The distributed tax",
                "One user request may pass through five services. Each hop adds milliseconds and a chance of failure: five services at 99.9% each give about 99.5% overall.",
                "You now need timeouts, retries, circuit breakers, distributed tracing, versioned contracts and eventual consistency across databases (see the System Design track). Services that must deploy together are a 'distributed monolith': all the costs, none of the benefits.",
                s =>
                {
                    s.Move("shape:gw", "shape:s0", "request", Info).Move("shape:s0", "shape:s1", "+20 ms", Warn).Move("shape:s0", "shape:s2", "+20 ms", Warn);
                    s.Paint("s2", Bad).Write("s2", "Catalog", "timeout");
                    s.Say("0.999^5 = 99.5%: availability multiplies down, latency adds up", Warn);
                })
            .Step("Choosing",
                "Start with a well-modularised monolith; extract a service when a module has a clear reason: independent scaling, a separate team, a different release cadence, or isolation of risk.",
                "Many successful systems are monoliths at large scale. The best boundaries are discovered over time, and moving a boundary inside a monolith is a refactor, not a migration.",
                s =>
                {
                    s.Wipe();
                    s.Table("choose", "monolith vs microservices", 40, 60, 920,
                        "                 monolith            modular monolith      microservices",
                        "deploy           all at once          all at once           per service",
                        "calls            in-process (ns)      in-process (ns)       network (ms), can fail",
                        "data             one DB, ACID         schemas per module    DB per service, eventual",
                        "scaling          whole app            whole app             per service",
                        "team fit         1-3 teams            several teams         many autonomous teams",
                        "operational cost low                  low                   high (tracing, mesh, CI/CD)");
                })
            .Lesson("Monolith, modular monolith, microservices", "Architecture", "What each architecture costs and buys, Conway's law, and the distributed-systems tax.");

    // ------------------------------------------------------------------------------------------
    private static Lesson RobustCode() =>
        new StoryBuilder("SW14", "Robust software makes mistakes hard to write: strong types instead of primitives, states that cannot be invalid, immutable shared data, validation once at the boundary, and errors handled deliberately instead of swallowed.")
            .Setup(s =>
            {
                s.Box("sig", "void Transfer(string from, string to, decimal amount)", 60, 60, 880, 56);
                s.Box("call", "Transfer(toAccount, fromAccount, 100);   // compiles!", 60, 170, 880, 56, tone: Bad);
                s.Shape("sig").Mono = true;
                s.Shape("call").Mono = true;
            })
            .Code("""
                public readonly record struct AccountId(Guid Value);
                public readonly record struct Money(decimal Amount, string Currency);
                void Transfer(AccountId from, AccountId to, Money amount);         // swapped arguments no longer compile

                public abstract record Payment;
                public sealed record Unpaid(Money Due) : Payment;
                public sealed record Paid(Money Amount, DateTime At, string Receipt) : Payment;

                var shipped = order with { Status = OrderStatus.Shipped };         // copy, never mutate shared state

                if (!Email.TryParse(input, out var email)) return Results.BadRequest();   // validate once, at the edge
                """)
            .Step("Primitive obsession",
                "Two strings and a decimal: swapping the account arguments compiles and silently moves money the wrong way.",
                "When every id is a string or Guid, the compiler cannot help you. Units are the same story: seconds vs milliseconds, cents vs euros.",
                [],
                s => s.Say("Swapped arguments compile and run", Bad))
            .Step("Strong types catch it at compile time",
                "Wrap ids and amounts in tiny value types. Now passing an amount where an AccountId is expected is a compile error.",
                "readonly record structs cost nothing at run time, and they are also the natural place for validation (a Money cannot be negative, a Currency must be a known code).",
                [1, 2, 3],
                s =>
                {
                    s.Write("sig", "void Transfer(AccountId from, AccountId to, Money amount)");
                    s.Write("call", "Transfer(amount, from, to);   // compile error").Paint("call", Good);
                    s.Box("vo", "readonly record struct AccountId(Guid Value);", 60, 280, 880, 56, tone: Info);
                    s.Shape("vo").Mono = true;
                })
            .Step("Make illegal states unrepresentable",
                "A Payment with IsPaid = true but no receipt is possible with flags. Model each state as its own type carrying exactly its data, and the bad combination cannot exist.",
                "Pattern matching with a switch over a closed set of records makes the compiler point out every place that forgot to handle a new state.",
                [5, 6, 7],
                s =>
                {
                    s.Wipe();
                    s.Table("bad", "class Payment (flags)", 40, 60, 440, "bool IsPaid", "DateTime? PaidAt", "string? ReceiptNo", "IsPaid = true, ReceiptNo = null  possible!");
                    s.Paint("bad", Bad).HotRow("bad", 3);
                    s.Table("good", "Payment as distinct states", 520, 60, 440, "record Unpaid(Money Due)", "record Paid(Money Amount, DateTime At, Receipt No)", "record Refunded(Paid Original, DateTime At)", "each state has exactly the data it needs");
                    s.Paint("good", Good).HotRow("good", 3);
                })
            .Step("Shared mutable state breaks",
                "Thread A adds to a List<Order> while thread B iterates it. B throws 'Collection was modified', or worse, reads a half-updated state.",
                "Mutable objects shared across threads, caches or callers are the source of races and 'spooky action at a distance' (see the Concurrency track).",
                [],
                s =>
                {
                    s.Wipe();
                    s.Box("list", "shared List<Order>", 380, 230, 240, 64, "mutable", Bad);
                    s.Box("ta", "thread A: Add()", 60, 100, 220, 56, tone: Warn);
                    s.Box("tb", "thread B: foreach", 60, 380, 220, 56, tone: Warn);
                    s.Connect("ta", "list", "writes", Warn).Connect("tb", "list", "reads", Warn);
                    s.Box("ex", "InvalidOperationException: Collection was modified", 520, 380, 440, 56, tone: Bad);
                })
            .Step("Immutable data is safe to share",
                "Use records, ImmutableArray or frozen collections. Changes create a new version (with-expressions), so readers always see a complete, consistent snapshot.",
                "Immutability also makes caching, hashing and reasoning trivial: an object that cannot change cannot be changed behind your back.",
                [9],
                s =>
                {
                    s.Write("list", "ImmutableArray<Order>", "new version per change").Paint("list", Good);
                    s.Paint("ta", Info).Paint("tb", Info);
                    s.Write("ex", "B keeps reading its snapshot; A publishes v2").Paint("ex", Good);
                })
            .Step("Validate once, at the boundary",
                "Raw input from HTTP, files or queues is parsed at the edge into types that are valid by construction. Invalid input is rejected immediately with a clear error.",
                "'Parse, don't validate': after the boundary, code receives an Email, not a string that might be an email, so the same check is never repeated or forgotten. Nullable reference types extend this to null.",
                [11],
                s =>
                {
                    s.Wipe();
                    s.Region("edge", "boundary: HTTP, files, queues", 40, 40, 300, 460, Warn);
                    s.Region("core", "core: already-valid types", 380, 40, 580, 460, Good);
                    s.Box("raw", "{ \"email\": \"bob@\" }", 60, 100, 260, 56, "raw input");
                    s.Box("parse", "Email.TryParse()", 60, 230, 260, 56, tone: Info);
                    s.Box("reject", "400 Bad Request", 60, 380, 260, 56, tone: Bad);
                    s.Box("typed", "Email (valid by construction)", 480, 230, 380, 56, tone: Good);
                    s.Connect("raw", "parse").Connect("parse", "reject", "invalid", Bad).Connect("parse", "typed", "valid", Good);
                })
            .Step("Handle errors deliberately",
                "Expected failures are values (Result, TryParse). Bugs throw and fail fast. Transient faults are retried with backoff. Nothing is ever swallowed silently.",
                "An empty catch block turns a loud, debuggable failure into silent data corruption. Log with context, rethrow or translate, and let the top-level handler decide the response.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("err", "error handling", 40, 60, 920,
                        "expected (not found, invalid input)   return Result<T> / TryX, no exception",
                        "bug (null, broken invariant)          throw, fail fast, fix the code",
                        "transient (timeout, 503)              retry with backoff and jitter",
                        "catastrophic (out of memory)          let the process crash and restart",
                        "never                                 catch { } and carry on");
                    s.HotRow("err", 4);
                })
            .Lesson("Robust code: strong types, invalid states, immutability, boundaries", "Quality", "Design techniques that make whole classes of bugs impossible to write.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Testing() =>
        new StoryBuilder("SW15", "Fast, focused tests are what make change safe. Keep many unit tests, fewer integration tests and very few end-to-end tests; isolate dependencies with test doubles; and let a red-green-refactor loop drive the design.")
            .Setup(s =>
            {
                s.Box("e2e", "end-to-end / UI", 400, 40, 200, 70, "few · minutes · brittle", Bad);
                s.Box("integ", "integration", 300, 130, 400, 70, "some · seconds · real DB in a container", Warn);
                s.Box("unit", "unit", 200, 220, 600, 70, "many · milliseconds · pure logic", Good);
            })
            .Code("""
                [Fact]
                public void Withdraw_more_than_balance_throws()
                {
                    var account = new BankAccount(balance: 100m);              // arrange
                    var act = () => account.Withdraw(150m);                    // act
                    Assert.Throws<InvalidOperationException>(act);             // assert
                }

                var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01"));   // deterministic clock
                var service = new SubscriptionService(new FakeRepository(), time);
                """)
            .Step("The test pyramid",
                "Unit tests check logic in milliseconds. Integration tests check real wiring (database, HTTP) in seconds. End-to-end tests check whole user journeys in minutes.",
                "The shape is about feedback speed and reliability: a suite that runs in seconds is run constantly; one that takes an hour and fails randomly is ignored. Containers (Testcontainers) make realistic integration tests cheap.",
                [1, 2, 3, 4, 5, 6, 7])
            .Step("Test doubles",
                "Replace slow or non-deterministic dependencies with doubles: stubs return canned answers, fakes are working in-memory versions, mocks verify that calls happened.",
                "Prefer fakes and state checks over mocks and interaction checks: tests that assert 'method X was called twice' break on every refactor. Inject time (TimeProvider) and randomness so tests are deterministic.",
                [9, 10],
                s => s.Table("doubles", "test doubles", 60, 330, 880,
                    "dummy   passed but never used",
                    "stub    returns canned answers",
                    "fake    working lightweight implementation (in-memory repository)",
                    "mock    verifies that expected calls were made",
                    "spy     records calls for later assertions"))
            .Step("TDD: red",
                "Write a small test for behaviour that does not exist yet, and watch it fail. The failure proves the test can detect the problem.",
                "Writing the test first forces you to design the API from the caller's point of view, and keeps each step small.",
                [1, 2, 3, 4, 5, 6, 7],
                s =>
                {
                    s.Wipe();
                    s.Circle("red", "Red", 250, 360, 80, "write a failing test", Bad);
                    s.Circle("green", "Green", 500, 140, 80, "make it pass, simply");
                    s.Circle("refactor", "Refactor", 750, 360, 80, "clean up, tests stay green");
                    s.Connect("red", "green", "make it pass").Connect("green", "refactor", "improve").Connect("refactor", "red", "next behaviour");
                })
            .Step("TDD: green, then refactor",
                "Write the simplest code that passes. Then improve names and structure while the tests stay green, and repeat.",
                "The refactor step is where design emerges, and the tests are the safety net that makes it safe. Skipping it produces code that works but rots.",
                [],
                s =>
                {
                    s.Paint("red", Neutral).Paint("green", Good).Paint("refactor", Info);
                })
            .Step("Code that is easy to test",
                "Testability is a design property: pure functions, injected dependencies, injected time, no static mutable state, and small units with clear contracts.",
                "Beyond example-based tests: property-based tests generate hundreds of inputs, mutation testing checks that your tests actually catch bugs, and contract tests keep services' APIs compatible.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("testable", "what makes code testable", 60, 60, 880,
                        "pure functions           same input, same output, no side effects",
                        "dependency injection     swap real dependencies for doubles",
                        "inject time and random   TimeProvider, seeded Random",
                        "no static mutable state  tests cannot interfere with each other",
                        "small units              one behaviour per test, clear names");
                })
            .Lesson("Testing: pyramid, test doubles and TDD", "Quality", "How fast, isolated tests make refactoring safe, and how test-first shapes better design.");
}
