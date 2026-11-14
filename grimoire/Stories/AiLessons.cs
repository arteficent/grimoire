using System.Globalization;
using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static partial class AiLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return WhatIsMl();
        yield return LinearRegression();
        yield return LogisticRegression();
        yield return GeometricClassifiers();
        yield return TreesAndEnsembles();
        yield return Unsupervised();
        yield return NeuralNetworks();
        yield return Backpropagation();
        yield return Cnn();
        yield return Rnn();
        yield return TokensAndEmbeddings();
        yield return AttentionAndTransformers();
        yield return LlmTraining();
        yield return Inference();
        yield return ReinforcementLearning();
        yield return GenerativeModels();
        yield return RagToolsAgents();
        yield return FrontierModels();
    }

    // Square plot area used by the 2-D lessons.
    private const double Px = 70, Py = 50, Pw = 440, Ph = 440;

    private static (double X, double Y) P(double x, double y) => (Px + x * Pw, Py + Ph - y * Ph);

    private static string N2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    private static string N3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);

    private static (double X, double Y)[] LineXY(double c) =>
        c >= 1 ? [P(c - 1, 1), P(1, c - 1)] : [P(0, c), P(c, 0)];

    // ------------------------------------------------------------------------------------------
    private static Lesson WhatIsMl()
    {
        static double F(double x) => 0.5 + 0.3 * Math.Sin(2 * Math.PI * x);
        double[] tx = [0.05, 0.14, 0.23, 0.32, 0.41, 0.5, 0.59, 0.68, 0.77, 0.86, 0.95];
        double[] noise = [0.06, -0.05, 0.04, -0.07, 0.05, -0.03, 0.06, -0.05, 0.03, -0.06, 0.04];
        double[] vx = [0.095, 0.365, 0.635, 0.905];
        double[] vnoise = [-0.02, 0.03, -0.03, 0.02];
        var xs = Enumerable.Range(0, 21).Select(j => j % 2 == 0 ? tx[j / 2] : (tx[j / 2] + tx[j / 2 + 1]) / 2).ToArray();

        (double X, double Y)[] Curve(Func<int, double> y) => xs.Select((x, j) => P(x, Math.Clamp(y(j), 0, 1))).ToArray();
        var under = Curve(j => 0.786 - 0.573 * xs[j]);
        var good = Curve(j => F(xs[j]));
        var over = Curve(j => j % 2 == 0 ? F(xs[j]) + noise[j / 2] : F(xs[j]) + (j / 2 % 2 == 0 ? 0.17 : -0.17));

        static double Train(double c) => 0.75 * Math.Exp(-3.5 * c) + 0.04;
        static double Val(double c) => 0.75 * Math.Exp(-3.5 * c) + 0.08 + 0.55 * c * c;
        static (double X, double Y) E(double c, double e) => (580 + c * 360, 360 - e / 0.9 * 280);
        var cs = Enumerable.Range(0, 11).Select(i => i / 10.0).ToArray();
        var best = cs.MinBy(Val);

        void Side(SceneState s, string title, params string[] rows) => s.Table("side", title, 560, 60, 400, rows);

        return new StoryBuilder("AI1", "Machine learning fits a model to examples so it can predict new cases. The goal is generalisation: a too-simple model underfits, a too-flexible one memorises noise and overfits, and only held-out data tells you which you have.")
            .Setup(s =>
            {
                s.Axes("ax", Px, Py, Pw, Ph, "input x", "target y");
                for (var i = 0; i < tx.Length; i++)
                {
                    var (x, y) = P(tx[i], F(tx[i]) + noise[i]);
                    s.Dot("d" + i, x, y, 0);
                }

                for (var i = 0; i < vx.Length; i++)
                {
                    var (x, y) = P(vx[i], F(vx[i]) + vnoise[i]);
                    s.Dot("v" + i, x, y, 1);
                    s.Ghost("v" + i);
                }

                Side(s, "programming vs learning", "traditional:  rules + data -> answers", "learning:     data + answers -> rules", "then:         model + new data -> predictions");
            })
            .NoCode()
            .Step("Learning from examples",
                "Instead of writing the rule by hand, give the computer examples (blue dots: input x, correct answer y) and let it find a function that maps one to the other.",
                "The 'rule' is a model with adjustable parameters. Training means searching for the parameter values that make predictions match the examples. Everything in this track, from linear regression to frontier LLMs, is a variation on that idea.")
            .Step("Three ways to learn",
                "Supervised learning uses labelled examples. Unsupervised learning finds structure without labels. Reinforcement learning learns from rewards for actions.",
                "Large language models are pre-trained with self-supervised learning: the 'label' is simply the next word of real text, so no human labelling is needed, which is why they can learn from trillions of words.",
                s => Side(s, "kinds of machine learning",
                    "supervised     labelled examples: spam?, price",
                    "unsupervised   structure: clusters, compression",
                    "reinforcement  rewards for actions: games, robots",
                    "self-supervised  predict hidden parts of data: LLMs"))
            .Step("Underfitting: too simple",
                "A straight line cannot follow the wave. It is wrong on the training data and will be wrong on new data too.",
                "High bias: the model's assumptions are too strong for the problem. Fixes are more features, a more flexible model, or less regularisation.",
                s =>
                {
                    s.Polyline("fit", under, Warn);
                    Side(s, "underfitting", "model        straight line", "train error  high", "test error   high", "cause        too simple: high bias");
                })
            .Step("A good fit",
                "A smooth curve follows the pattern and ignores the noise. The orange held-out points, which were never used for training, are predicted well.",
                "Generalisation is the only goal that matters: performance on data the model has never seen. Low training error alone proves nothing.",
                s =>
                {
                    s.Polyline("fit", good, Good);
                    for (var i = 0; i < vx.Length; i++)
                    {
                        s.Ghost("v" + i, false);
                    }

                    Side(s, "good fit", "model        smooth curve", "train error  low", "test error   low", "result       captures the signal, not the noise");
                })
            .Step("Overfitting: memorising noise",
                "A very flexible curve passes through every training point exactly, but swings wildly in between. The held-out points are now far from it.",
                "High variance: the model learned the noise of this particular sample. Fixes are more data, a simpler model, regularisation (penalising large weights), dropout, or early stopping.",
                s =>
                {
                    s.Polyline("fit", over, Bad);
                    Side(s, "overfitting", "model        very flexible curve", "train error  zero", "test error   high", "cause        memorised the noise: high variance");
                })
            .Step("Hold out data to measure it",
                "Split the data: train on one part, tune choices on a validation set, and report once on an untouched test set. Training error keeps falling with complexity; validation error falls, then rises.",
                "The bottom of the validation curve is the sweet spot. If you tune against the test set it stops being a fair judge. With little data, k-fold cross-validation rotates which part is held out.",
                s =>
                {
                    s.Polyline("fit", good, Good);
                    s.Remove("side");
                    s.Axes("ex", 580, 80, 360, 280, "model complexity", "error");
                    s.Polyline("tr", cs.Select(c => E(c, Train(c))).ToArray(), Info);
                    s.Polyline("va", cs.Select(c => E(c, Val(c))).ToArray(), Warn);
                    var (bx, by) = E(best, Val(best));
                    s.Dot("best", bx, by, 2, 9, "best");
                    s.Label("trl", "training", 880, E(1, Train(1)).Y - 22, 90, 11, Info);
                    s.Label("val", "validation", 870, E(1, Val(1)).Y - 22, 100, 11, Warn);
                    s.Fields("split", [("train", "70%", 70), ("val", "15%", 15), ("test", "15%", 15)], 580, 420, 360, 56);
                })
            .Lesson("What machine learning is: fitting, underfitting and overfitting", "Foundations", "Learning from examples, the three kinds of learning, bias vs variance and held-out validation.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson LinearRegression()
    {
        double[] xs = [0.05, 0.15, 0.25, 0.35, 0.45, 0.55, 0.65, 0.75, 0.85, 0.95];
        double[] noise = [0.04, -0.03, 0.05, -0.04, 0.02, -0.05, 0.04, -0.02, 0.03, -0.03];
        var ys = xs.Select((x, i) => 0.15 + 0.65 * x + noise[i]).ToArray();
        double Loss(double wv, double bv) => xs.Select((x, i) => Math.Pow(wv * x + bv - ys[i], 2)).Average();

        var snaps = new List<(double W, double B, double L, int It)>();
        var (w, b) = (0.0, 0.5);
        for (var it = 0; it <= 300; it++)
        {
            if (it is 0 or 3 or 20 or 300)
            {
                snaps.Add((w, b, Loss(w, b), it));
            }

            var (cw, cb) = (w, b);
            var gw = xs.Select((x, i) => 2 * (cw * x + cb - ys[i]) * x).Average();
            var gb = xs.Select((x, i) => 2 * (cw * x + cb - ys[i])).Average();
            w -= 0.5 * gw;
            b -= 0.5 * gb;
        }

        static double Bx(double wv) => 560 + (wv + 1.2) / 4.4 * 380;
        static double By(double f) => 470 - f / 5 * 380;
        static double Bowl(double wv) => (wv - 1) * (wv - 1);

        void Fit(SceneState s, int k)
        {
            var (sw, sb, sl, it) = snaps[k];
            s.Polyline("line", [P(0, Math.Clamp(sb, 0, 1)), P(1, Math.Clamp(sw + sb, 0, 1))], Info);
            for (var i = 0; i < xs.Length; i++)
            {
                var (x, y) = P(xs[i], Math.Clamp(sw * xs[i] + sb, 0, 1));
                s.Box("r" + i, null, x - 1, y - 1, 2, 2, tone: Warn);
                s.Connect("d" + i, "r" + i, tone: Warn, dashed: true, arrow: false);
            }

            s.Write("mse", $"iteration {it}:  w = {N2(sw)}, b = {N2(sb)},  mean squared error = {N3(sl)}");
        }

        void Ball(SceneState s, double wv, Tone tone = Good)
        {
            s.Dot("ball", Bx(wv), By(Bowl(wv)), 2, 10);
            s.Paint("ball", tone);
        }

        return new StoryBuilder("AI2", "Linear regression fits a line by minimising squared error. Gradient descent finds the minimum by repeatedly stepping downhill along the slope of the loss; the learning rate decides whether it crawls, converges, or explodes. The same loop trains every neural network.")
            .Setup(s =>
            {
                s.Axes("ax", Px, Py, Pw, Ph, "input x", "y");
                for (var i = 0; i < xs.Length; i++)
                {
                    var (x, y) = P(xs[i], ys[i]);
                    s.Dot("d" + i, x, y, 0);
                }

                s.Label("mse", string.Empty, Px, 8, 900, 13, Info);
                s.Axes("bx", 560, 90, 380, 380, "weight w", "loss for one weight");
                s.Polyline("bowl", Enumerable.Range(0, 23).Select(i => -1.2 + i * 0.2).Select(wv => (Bx(wv), By(Bowl(wv)))).ToArray(), Neutral);
                Fit(s, 0);
                Ball(s, -0.6, Info);
            })
            .Code("""
                double w = 0, b = 0.5, lr = 0.5;
                for (int step = 0; step < 300; step++)
                {
                    double gw = 0, gb = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double error = (w * x[i] + b) - y[i];      // prediction - truth
                        gw += 2 * error * x[i] / n;                   // d(loss)/dw
                        gb += 2 * error / n;                          // d(loss)/db
                    }
                    w -= lr * gw;                                     // step downhill
                    b -= lr * gb;
                }
                """)
            .Step("A line and its errors",
                "The model is y = w·x + b. It starts flat. The dashed lines are the errors (residuals); the loss is the average of their squares.",
                "Squaring makes every error positive and punishes big misses more. The right panel shows the same idea for a single weight: loss as a bowl, with the current guess as a ball.",
                [1, 6])
            .Step("Follow the slope downhill",
                "The gradient says which way the loss increases for each parameter. Step the opposite way, scaled by the learning rate. After 3 steps the line has tilted towards the data.",
                "Each parameter gets its own partial derivative: here dL/dw and dL/db. Computing them for millions of parameters at once is what backpropagation does (two lessons on).",
                [7, 8, 9, 11, 12],
                s =>
                {
                    Fit(s, 1);
                    Ball(s, 0.68, Info);
                })
            .Step("Repeat",
                "After 20 steps the line is close and the loss has dropped sharply. Each step is smaller because the slope flattens near the bottom.",
                "Training curves for real models look the same: fast early progress, then slow refinement. Mini-batch (stochastic) gradient descent estimates the gradient from a small random batch, so each step is cheap and slightly noisy.",
                [2],
                s =>
                {
                    Fit(s, 2);
                    Ball(s, 0.987, Info);
                })
            .Step("Converged",
                "After 300 steps the parameters settle near w = 0.65 and b = 0.15, the values that generated the data. The remaining error is just the noise.",
                "Linear regression also has an exact closed-form solution (the normal equation), but gradient descent scales to models with billions of parameters where no closed form exists.",
                [],
                s =>
                {
                    Fit(s, 3);
                    Ball(s, 1.0);
                })
            .Step("Learning rate too large",
                "With a learning rate that is too large, each step overshoots the bottom further than the last: the loss explodes instead of shrinking.",
                "Too small and training crawls; too large and it diverges (the 'NaN loss' every practitioner has seen). Schedules with warm-up and decay, and adaptive optimisers such as Adam, manage this in practice.",
                [1],
                s =>
                {
                    double[] path = [-0.6, 2.76, -0.936, 3.13];
                    s.Polyline("zig", path.Select(p => (Bx(p), By(Bowl(p)))).ToArray(), Bad, dashed: true);
                    Ball(s, 3.13, Bad);
                    s.Say("Each step overshoots further: divergence", Bad);
                })
            .Lesson("Linear regression and gradient descent", "Classic machine learning", "Squared error, gradients, learning rates and convergence, animated on real numbers.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson LogisticRegression()
    {
        (double X, double Y)[] spam = [(0.70, 0.80), (0.80, 0.60), (0.60, 0.72), (0.90, 0.85), (0.75, 0.42), (0.58, 0.58), (0.86, 0.30), (0.45, 0.50)];
        (double X, double Y)[] ham = [(0.20, 0.20), (0.30, 0.35), (0.15, 0.55), (0.40, 0.25), (0.25, 0.70), (0.62, 0.50), (0.35, 0.10), (0.55, 0.22)];

        (int Tp, int Fn, int Fp, int Tn) Confusion(Func<(double X, double Y), bool> isSpam) =>
            (spam.Count(isSpam), spam.Count(p => !isSpam(p)), ham.Count(isSpam), ham.Count(p => !isSpam(p)));

        void Mistakes(SceneState s, Func<(double X, double Y), bool> isSpam)
        {
            s.RemoveWhere(sh => sh.Id.StartsWith("mis", StringComparison.Ordinal));
            var all = spam.Select(p => (P: p, Spam: true)).Concat(ham.Select(p => (P: p, Spam: false))).ToArray();
            for (var i = 0; i < all.Length; i++)
            {
                if (isSpam(all[i].P) != all[i].Spam)
                {
                    var (x, y) = P(all[i].P.X, all[i].P.Y);
                    s.Circle("mis" + i, null, x, y, 16, tone: Bad);
                    s.Ghost("mis" + i);
                }
            }
        }

        void Report(SceneState s, (int Tp, int Fn, int Fp, int Tn) c)
        {
            var precision = c.Tp / (double)(c.Tp + c.Fp);
            var recall = c.Tp / (double)(c.Tp + c.Fn);
            var f1 = 2 * precision * recall / (precision + recall);
            s.Table("cm", "confusion matrix", 560, 60, 400,
                "              predicted spam   predicted ham",
                $"actual spam   TP = {c.Tp}           FN = {c.Fn}",
                $"actual ham    FP = {c.Fp}           TN = {c.Tn}");
            s.Table("metrics", "metrics", 560, 220, 400,
                $"precision  TP / (TP + FP)  = {N2(precision)}",
                $"recall     TP / (TP + FN)  = {N2(recall)}",
                $"F1         harmonic mean   = {N2(f1)}",
                $"accuracy   correct / all   = {N2((c.Tp + c.Tn) / 16.0)}");
        }

        static (double X, double Y) S(double z, double sig) => (580 + (z + 6) / 12 * 360, 320 - sig * 240);

        return new StoryBuilder("AI3", "Logistic regression turns a weighted sum into a probability with the sigmoid function and draws a straight decision boundary. Judge classifiers with a confusion matrix, precision and recall, not accuracy alone, and pick the threshold for your costs.")
            .Setup(s =>
            {
                s.Axes("ax", Px, Py, Pw, Ph, "links per email", "share of CAPITALS");
                for (var i = 0; i < spam.Length; i++)
                {
                    var (x, y) = P(spam[i].X, spam[i].Y);
                    s.Dot("s" + i, x, y, 7);
                    (x, y) = P(ham[i].X, ham[i].Y);
                    s.Dot("h" + i, x, y, 2);
                }

                s.Axes("sg", 580, 80, 360, 240, "z = w·x + b", "P(spam)");
                s.Polyline("sig", Enumerable.Range(0, 25).Select(i => -6 + i * 0.5).Select(z => S(z, 1 / (1 + Math.Exp(-z)))).ToArray(), Info);
                s.Polyline("half", [S(-6, 0.5), S(6, 0.5)], Neutral, dashed: true);
                s.Label("halfl", "threshold 0.5", 590, 172, 160, 11);
            })
            .NoCode()
            .Step("From score to probability",
                "Red dots are spam, green are legitimate mail. The model computes a score z = w·x + b and squashes it with the sigmoid into a probability between 0 and 1.",
                "Above the 0.5 threshold, predict spam. The set of points where z = 0 is a straight line: logistic regression is a linear classifier, despite the curved sigmoid.")
            .Step("Before training",
                "With untrained weights the boundary is a poor guess. Ringed points are on the wrong side.",
                "Training minimises log loss (cross-entropy): -log of the probability given to the correct class. Confident wrong answers are punished heavily, which pushes the boundary into place.",
                s =>
                {
                    s.Polyline("bnd", [P(0.3, 0), P(0.3, 1)], Warn, dashed: true);
                    Mistakes(s, p => p.X > 0.3);
                })
            .Step("Trained boundary",
                "Gradient descent rotates and shifts the line. Now only two emails are misclassified: one spam slips through, one legitimate email is flagged.",
                "With more features (word counts, sender reputation) the boundary becomes a hyperplane in many dimensions. The learned weights are directly interpretable: which features push towards spam.",
                s =>
                {
                    s.Polyline("bnd", LineXY(1.05), Good);
                    Mistakes(s, p => p.X + p.Y > 1.05);
                })
            .Step("Confusion matrix and metrics",
                "Count the four outcomes: true positives, false negatives, false positives and true negatives. Precision asks 'when we say spam, how often are we right?'; recall asks 'how much spam do we catch?'.",
                "Accuracy misleads on imbalanced data: if 99% of transactions are legitimate, predicting 'never fraud' is 99% accurate and useless. Fraud and disease detection care about recall; spam filters care about precision.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("sg", StringComparison.Ordinal) || sh.Id.StartsWith("sig", StringComparison.Ordinal) || sh.Id.StartsWith("half", StringComparison.Ordinal));
                    Report(s, Confusion(p => p.X + p.Y > 1.05));
                })
            .Step("Moving the threshold",
                "Demand a higher probability before calling something spam. No legitimate mail is flagged any more (precision 1.00), but half the spam now gets through (recall 0.50).",
                "Every threshold gives a different precision/recall pair. The ROC curve plots all of them, and the area under it (AUC) summarises ranking quality independent of the threshold. Choose the threshold from the real costs of each kind of mistake.",
                s =>
                {
                    s.Polyline("bnd", LineXY(1.25), Warn);
                    Mistakes(s, p => p.X + p.Y > 1.25);
                    Report(s, Confusion(p => p.X + p.Y > 1.25));
                })
            .Lesson("Logistic regression, precision and recall", "Classic machine learning", "Sigmoid probabilities, decision boundaries, confusion matrices and choosing a threshold.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson GeometricClassifiers()
    {
        (double X, double Y, int C)[] pts =
        [
            (0.20, 0.30, 0), (0.30, 0.20, 0), (0.25, 0.50, 0), (0.40, 0.35, 0), (0.15, 0.15, 0), (0.45, 0.55, 0),
            (0.70, 0.70, 1), (0.80, 0.60, 1), (0.65, 0.85, 1), (0.60, 0.50, 1), (0.85, 0.80, 1), (0.55, 0.75, 1)
        ];
        const double Qx = 0.5, Qy = 0.5;
        var nearest = pts.Select((p, i) => (I: i, D: Math.Sqrt((p.X - Qx) * (p.X - Qx) + (p.Y - Qy) * (p.Y - Qy)))).OrderBy(t => t.D).Take(5).ToArray();
        var votesA = nearest.Count(n => pts[n.I].C == 0);

        double[] outer = [-0.85, -0.7, 0.7, 0.85];
        double[] inner = [-0.25, -0.1, 0.1, 0.25];
        static double Kx(double x) => 500 + x * 380;
        static double Ky(double y) => 470 - y * 380;

        return new StoryBuilder("AI4", "Some classifiers are pure geometry. k-nearest neighbours votes among the closest examples; support vector machines find the widest street between classes, and kernels bend space so curved boundaries become straight. Naive Bayes instead multiplies word probabilities.")
            .Setup(s =>
            {
                s.Axes("ax", Px, Py, Pw, Ph, "feature 1", "feature 2");
                for (var i = 0; i < pts.Length; i++)
                {
                    var (x, y) = P(pts[i].X, pts[i].Y);
                    s.Dot("p" + i, x, y, pts[i].C == 0 ? 0 : 1);
                }

                var (qx, qy) = P(Qx, Qy);
                s.Circle("q", "?", qx, qy, 14, tone: Warn);
            })
            .NoCode()
            .Step("k-nearest neighbours: ask the neighbours",
                $"To classify the new point, find the k = 5 closest training examples. {votesA} are blue and {5 - votesA} are orange, so the new point is blue.",
                "kNN has no training step at all: it stores the data and does the work at query time. Features must be on comparable scales, or one feature dominates the distance. Vector databases do approximate nearest-neighbour search at scale.",
                s =>
                {
                    var (qx, qy) = P(Qx, Qy);
                    s.Circle("radius", null, qx, qy, nearest[^1].D * Pw + 12, tone: Info);
                    s.Ghost("radius");
                    foreach (var n in nearest)
                    {
                        s.Connect("q", "p" + n.I, tone: Info, dashed: true, arrow: false);
                    }

                    s.Write("q", "A").Tint("q", votesA >= 3 ? 0 : 1);
                })
            .Step("SVM: the widest street",
                "Many lines separate the two classes (faint). A support vector machine picks the one with the largest margin to the nearest points on each side. Those nearest points (ringed) are the support vectors.",
                "Only the support vectors determine the boundary; all other points could move without changing it. A wide margin tends to generalise better. Soft margins tolerate a few points on the wrong side.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id is "q" or "radius");
                    s.ClearLinks();
                    s.Polyline("c1", [P(0.3, 1), P(0.75, 0)], Neutral, dashed: true);
                    s.Polyline("c2", [P(0.4, 1), P(0.65, 0)], Neutral, dashed: true);
                    s.Polyline("svm", LineXY(1.05), Good);
                    s.Polyline("m1", LineXY(1.0), Info, dashed: true);
                    s.Polyline("m2", LineXY(1.1), Info, dashed: true);
                    foreach (var i in new[] { 5, 9 })
                    {
                        var (x, y) = P(pts[i].X, pts[i].Y);
                        s.Circle("sv" + i, null, x, y, 16, tone: Good);
                        s.Ghost("sv" + i);
                    }
                })
            .Step("Not separable by a straight line",
                "Here the orange points sit on both sides of the blue ones. No single threshold on this line can separate them.",
                "Many real problems look like this: the classes are separable, just not with a straight boundary in the original features.",
                s =>
                {
                    s.Wipe();
                    s.Polyline("kx", [(Kx(-1), Ky(0)), (Kx(1), Ky(0))], Neutral);
                    for (var i = 0; i < 4; i++)
                    {
                        s.Dot("o" + i, Kx(outer[i]), Ky(0), 1, 9);
                        s.Dot("i" + i, Kx(inner[i]), Ky(0), 0, 9);
                    }

                    s.Label("kl", "x", Kx(1) + 8, Ky(0) - 10, 30, 12);
                })
            .Step("The kernel trick: lift, then cut",
                "Add a new feature x². The outer points rise high, the inner ones stay low, and a straight horizontal line now separates them perfectly.",
                "Kernels compute similarities as if the data were mapped into a higher-dimensional space, without ever computing the mapping. Before deep learning, SVMs with kernels were state of the art for many tasks.",
                s =>
                {
                    for (var i = 0; i < 4; i++)
                    {
                        s.Dot("o" + i, Kx(outer[i]), Ky(outer[i] * outer[i]), 1, 9);
                        s.Dot("i" + i, Kx(inner[i]), Ky(inner[i] * inner[i]), 0, 9);
                    }

                    s.Polyline("para", Enumerable.Range(0, 21).Select(i => -1 + i * 0.1).Select(x => (Kx(x), Ky(x * x))).ToArray(), Neutral, dashed: true);
                    s.Polyline("sep", [(Kx(-1), Ky(0.25)), (Kx(1), Ky(0.25))], Good);
                    s.Label("kl2", "x²", Kx(-1) - 30, Ky(1) - 10, 30, 12);
                })
            .Step("Naive Bayes: multiply the evidence",
                "An email contains 'free' and 'winner'. Multiply the prior by how likely each word is in spam versus normal mail: spam wins overwhelmingly, about 99.5%.",
                "It is 'naive' because it assumes words are independent given the class, which is false, yet it works remarkably well for text and trains in one pass over the data. It was the classic spam filter of the 2000s.",
                s =>
                {
                    s.Wipe();
                    var spamScore = 0.4 * 0.3 * 0.1;
                    var hamScore = 0.6 * 0.02 * 0.005;
                    s.Table("nb", "P(class | 'free', 'winner')", 60, 60, 880,
                        "                P(class)   P('free' | class)   P('winner' | class)   product",
                        $"spam            0.40       0.30                0.10                  {spamScore:0.000000}",
                        $"not spam        0.60       0.02                0.005                 {hamScore:0.000000}",
                        $"normalise:  P(spam) = {spamScore:0.000000} / ({spamScore:0.000000} + {hamScore:0.000000}) = {spamScore / (spamScore + hamScore):P1}");
                    s.HotRow("nb", 3);
                })
            .Lesson("k-nearest neighbours, SVMs, kernels and naive Bayes", "Classic machine learning", "Voting neighbours, maximum-margin boundaries, the kernel trick and probabilistic text classification.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson TreesAndEnsembles()
    {
        static double Gini(double p) => 1 - p * p - (1 - p) * (1 - p);

        return new StoryBuilder("AI5", "Decision trees ask a sequence of yes/no questions, choosing each split to make the groups purer. One deep tree overfits; random forests average many diverse trees, and gradient boosting adds trees that each fix the previous ones' errors.")
            .Setup(s =>
            {
                s.Box("n0", "income > 50k?", 380, 40, 240, 56, tone: Info);
                s.Box("n1", "has collateral?", 120, 190, 240, 56);
                s.Box("n2", "debt ratio < 0.4?", 640, 190, 240, 56);
                s.Box("l0", "deny", 40, 350, 180, 56, tone: Bad);
                s.Box("l1", "approve: small loan", 250, 350, 200, 56, tone: Good);
                s.Box("l2", "approve", 560, 350, 180, 56, tone: Good);
                s.Box("l3", "deny", 780, 350, 180, 56, tone: Bad);
                s.Connect("n0", "n1", "no").Connect("n0", "n2", "yes").Connect("n1", "l0", "no").Connect("n1", "l1", "yes").Connect("n2", "l2", "yes").Connect("n2", "l3", "no");
            })
            .NoCode()
            .Step("A tree of questions",
                "A decision tree classifies by asking one feature question at each node until it reaches a leaf with an answer. Here: loan approval.",
                "Trees are easy to explain ('denied because debt ratio was above 0.4'), need no feature scaling, and handle mixed numeric and categorical data.")
            .Step("Following one applicant",
                "An applicant earns 72k with a debt ratio of 0.3: yes at the root, yes at the debt question, approved.",
                "Prediction cost is the depth of the tree: a handful of comparisons, which is why trees are used in latency-critical systems such as ad ranking and fraud scoring.",
                s =>
                {
                    s.Box("app", "applicant: income 72k, debt ratio 0.3", 40, 470, 400, 50, tone: Info);
                    s.Paint("n2", Info).Paint("l2", Good);
                    s.Move("shape:app", "shape:n0", "72k > 50k", Info).Move("shape:n0", "shape:n2", "yes", Info).Move("shape:n2", "shape:l2", "0.3 < 0.4", Good);
                })
            .Step("Choosing a split: Gini impurity",
                $"10 applicants, 5 approved and 5 denied: Gini impurity {N2(Gini(0.5))}. Splitting on income gives groups of 4:1 and 1:4, each with impurity {N2(Gini(0.8))}. Impurity drops by {N2(Gini(0.5) - Gini(0.8))}.",
                "At every node the algorithm tries every feature and threshold and keeps the split with the largest impurity drop (Gini or entropy for classification, variance for regression). Growing until every leaf is pure memorises the training set.",
                s =>
                {
                    s.Wipe();
                    s.Box("parent", "10 applicants: 5 approve, 5 deny", 300, 50, 400, 60, $"Gini = {N2(Gini(0.5))}", Warn);
                    s.Box("left", "income <= 50k: 1 approve, 4 deny", 60, 260, 400, 60, $"Gini = {N2(Gini(0.2))}", Info);
                    s.Box("right", "income > 50k: 4 approve, 1 deny", 540, 260, 400, 60, $"Gini = {N2(Gini(0.8))}", Info);
                    s.Connect("parent", "left").Connect("parent", "right");
                    s.Box("gain", $"weighted impurity {N2(Gini(0.8))}: gain {N2(Gini(0.5) - Gini(0.8))}", 300, 420, 400, 56, tone: Good);
                })
            .Step("Random forest: many trees vote",
                "Train hundreds of trees, each on a random sample of rows and a random subset of features, then let them vote. Individual trees overfit in different ways, and the errors cancel.",
                "Bagging (bootstrap aggregating) reduces variance without increasing bias. Random forests (Breiman, 2001) are robust, need little tuning, and give feature importances for free.",
                s =>
                {
                    s.Wipe();
                    string[] votes = ["approve", "approve", "deny", "approve", "approve"];
                    for (var i = 0; i < votes.Length; i++)
                    {
                        s.Box("t" + i, $"tree {i + 1}", 40 + i * 190, 80, 170, 60, "random rows + features", votes[i] == "approve" ? Good : Bad);
                        s.Move("shape:t" + i, "shape:vote", votes[i], votes[i] == "approve" ? Good : Bad);
                    }

                    s.Box("vote", "majority vote: approve (4 of 5)", 300, 330, 400, 64, tone: Good);
                })
            .Step("Gradient boosting: fix the remaining error",
                "Boosting builds trees one after another. Each new small tree is trained on what the ensemble still gets wrong, and its prediction is added with a small weight. The error shrinks round by round.",
                "XGBoost (2014), LightGBM and CatBoost (both 2017) are still the strongest tools for tabular data, often beating deep learning on spreadsheets and database tables.",
                s =>
                {
                    s.Wipe();
                    double[] residual = [10, 6.1, 3.8, 2.3, 1.4, 0.9];
                    s.LabeledBars("res", residual.Select((r, i) => ($"after tree {i + 1}", r, $"error {r}")).ToArray(), 60, 450, 130, 25, 330);
                    s.Paint("res5", Good);
                    s.Caption("cap", "prediction = tree1 + 0.3·tree2 + 0.3·tree3 + ...", 60, 40, 600);
                })
            .Lesson("Decision trees, random forests and gradient boosting", "Classic machine learning", "Splitting by impurity, bagging many trees, and boosting that fixes residual errors.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Unsupervised()
    {
        (double X, double Y)[] blobCentres = [(0.25, 0.72), (0.72, 0.75), (0.5, 0.25)];
        (double X, double Y)[] offsets = [(-0.07, 0.05), (0.06, 0.07), (0.02, -0.08), (-0.05, -0.04), (0.08, -0.01)];
        var pts = blobCentres.SelectMany(c => offsets.Select(o => (X: c.X + o.X, Y: c.Y + o.Y))).ToArray();

        var history = new List<((double X, double Y)[] Cents, int[] Assign)>();
        (double X, double Y)[] cents = [(0.1, 0.5), (0.45, 0.5), (0.9, 0.4)];
        for (var iter = 0; iter < 10; iter++)
        {
            var current = cents;
            var assign = pts.Select(p => Enumerable.Range(0, 3).MinBy(k => Math.Pow(p.X - current[k].X, 2) + Math.Pow(p.Y - current[k].Y, 2))).ToArray();
            history.Add((current, assign));
            var next = Enumerable.Range(0, 3).Select(k =>
            {
                var members = pts.Where((_, i) => assign[i] == k).ToArray();
                return members.Length == 0 ? current[k] : (X: members.Average(m => m.X), Y: members.Average(m => m.Y));
            }).ToArray();
            if (next.SequenceEqual(current))
            {
                break;
            }

            cents = next;
        }

        void Show(SceneState s, int h, bool colour)
        {
            var (cs, assign) = history[h];
            for (var i = 0; i < pts.Length; i++)
            {
                var (x, y) = P(pts[i].X, pts[i].Y);
                s.Dot("pt" + i, x, y, colour ? assign[i] : 6);
                if (!colour)
                {
                    s.Tint("pt" + i, -1);
                }
            }

            for (var k = 0; k < 3; k++)
            {
                var (x, y) = P(cs[k].X, cs[k].Y);
                s.Circle("c" + k, "C" + (k + 1), x, y, 18);
                s.Tint("c" + k, k);
            }
        }

        // PCA on a correlated cloud
        var dir = (X: 1 / Math.Sqrt(1 + 0.36), Y: 0.6 / Math.Sqrt(1 + 0.36));
        double[] along = [-0.36, -0.29, -0.22, -0.16, -0.09, -0.03, 0.03, 0.1, 0.16, 0.23, 0.3, 0.36];
        double[] across = [0.05, -0.06, 0.03, 0.07, -0.04, 0.06, -0.07, 0.04, -0.05, 0.06, -0.03, 0.05];
        var cloud = along.Select((t, i) => (X: 0.5 + t * dir.X - across[i] * dir.Y, Y: 0.5 + t * dir.Y + across[i] * dir.X)).ToArray();
        var mean = (X: cloud.Average(p => p.X), Y: cloud.Average(p => p.Y));
        var sxx = cloud.Average(p => (p.X - mean.X) * (p.X - mean.X));
        var syy = cloud.Average(p => (p.Y - mean.Y) * (p.Y - mean.Y));
        var sxy = cloud.Average(p => (p.X - mean.X) * (p.Y - mean.Y));
        var angle = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        var pc = (X: Math.Cos(angle), Y: Math.Sin(angle));
        var explained = (sxx * pc.X * pc.X + 2 * sxy * pc.X * pc.Y + syy * pc.Y * pc.Y) / (sxx + syy);

        return new StoryBuilder("AI6", "Unsupervised learning finds structure without labels. k-means groups points around centroids by alternating 'assign' and 'move'; PCA finds the directions of greatest variation so data can be compressed or visualised in fewer dimensions.")
            .Setup(s =>
            {
                s.Axes("ax", Px, Py, Pw, Ph, "feature 1", "feature 2");
                Show(s, 0, false);
            })
            .NoCode()
            .Step("Unlabelled data",
                "Fifteen customers, two features each, no labels. Three centroids (C1-C3) are placed at starting positions.",
                "k-means needs k chosen up front. Starting positions matter: k-means++ spreads the initial centroids out, which avoids most bad starts.")
            .Step("Assign: nearest centroid",
                "Each point joins the cluster of its nearest centroid. Some assignments are clearly wrong: C2 has grabbed points from several groups.",
                "This step minimises the within-cluster distance for fixed centroids.",
                s => Show(s, 0, true))
            .Step("Move: centroids to the mean",
                "Each centroid moves to the average of its points, then every point is reassigned. Several points switch clusters.",
                "This step minimises the distance for fixed assignments. Alternating the two steps can only lower the total squared distance, so the algorithm always converges, though possibly to a local optimum.",
                s => Show(s, Math.Min(1, history.Count - 1), true))
            .Step("Converged",
                $"After {history.Count - 1} updates nothing changes: three clean clusters. In a real product these might be customer segments.",
                "Choose k with the elbow method (where adding clusters stops helping much) or silhouette scores. For odd-shaped clusters, DBSCAN or Gaussian mixtures fit better than k-means' round clusters.",
                s => Show(s, history.Count - 1, true))
            .Step("PCA: the main direction of variation",
                $"A different dataset: two correlated features. The first principal component (solid line) points along the spread and captures {explained:P0} of the variance; the second (dashed) is perpendicular.",
                "PCA computes the eigenvectors of the covariance matrix. It decorrelates features, removes noise, and compresses data: keep the top components, drop the rest.",
                s =>
                {
                    s.Wipe();
                    s.Axes("ax", Px, Py, Pw, Ph, "feature 1", "feature 2");
                    for (var i = 0; i < cloud.Length; i++)
                    {
                        var (x, y) = P(cloud[i].X, cloud[i].Y);
                        s.Dot("cl" + i, x, y, 5);
                    }

                    s.Polyline("pc1", [P(mean.X - 0.45 * pc.X, mean.Y - 0.45 * pc.Y), P(mean.X + 0.45 * pc.X, mean.Y + 0.45 * pc.Y)], Good);
                    s.Polyline("pc2", [P(mean.X + 0.15 * pc.Y, mean.Y - 0.15 * pc.X), P(mean.X - 0.15 * pc.Y, mean.Y + 0.15 * pc.X)], Info, dashed: true);
                })
            .Step("Project onto the first component",
                "Drop every point onto the main axis. Two numbers per point become one, and most of the information survives.",
                "This is dimensionality reduction. With hundreds of features, a few components often hold most of the variance. For visualising complex data, non-linear methods such as t-SNE and UMAP preserve neighbourhoods better.",
                s =>
                {
                    for (var i = 0; i < cloud.Length; i++)
                    {
                        var t = (cloud[i].X - mean.X) * pc.X + (cloud[i].Y - mean.Y) * pc.Y;
                        var (x, y) = P(mean.X + t * pc.X, mean.Y + t * pc.Y);
                        s.Dot("cl" + i, x, y, 5);
                    }

                    s.Say($"2 features -> 1 component, keeping {explained:P0} of the variance", Good);
                })
            .Lesson("Unsupervised learning: k-means clustering and PCA", "Classic machine learning", "Assign-and-move clustering animated to convergence, and projecting data onto principal components.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson NeuralNetworks()
    {
        int[] sizes = [3, 4, 4, 2];
        double[] layerX = [150, 380, 610, 840];
        static double Wt(int l, int i, int j) => Math.Round(Math.Sin(l * 7.1 + i * 3.3 + j * 1.7) * 0.9, 2);
        double Ny(int l, int i) => 280 + (i - (sizes[l] - 1) / 2.0) * 100;

        var acts = new List<double[]> { new[] { 0.9, 0.2, 0.6 } };
        for (var l = 1; l < sizes.Length; l++)
        {
            var prev = acts[l - 1];
            var layer = l;
            var z = Enumerable.Range(0, sizes[l]).Select(j => prev.Select((a, i) => a * Wt(layer, i, j)).Sum() + 0.1).ToArray();
            acts.Add(l == sizes.Length - 1 ? z : z.Select(v => Math.Max(0, v)).ToArray());
        }

        var logits = acts[^1];
        var exp = logits.Select(Math.Exp).ToArray();
        var probs = exp.Select(e => e / exp.Sum()).ToArray();
        string[] classes = ["cat", "dog"];

        static double Relu(double x) => Math.Max(0, x);
        static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
        static double Gelu(double x) => 0.5 * x * (1 + Math.Tanh(Math.Sqrt(2 / Math.PI) * (x + 0.044715 * x * x * x)));

        return new StoryBuilder("AI7", "A neuron computes a weighted sum plus a bias and passes it through a non-linear activation. Stacking layers of neurons lets a network represent almost any function, including ones no single line can draw, such as XOR.")
            .Setup(s =>
            {
                double[] x = [1.0, 0.5, -1.0];
                double[] w = [0.8, -0.4, 0.3];
                for (var i = 0; i < 3; i++)
                {
                    s.Box("x" + i, $"x{i + 1} = {N2(x[i])}", 60, 120 + i * 120, 160, 56, tone: Info);
                    s.Connect("x" + i, "n", $"w = {N2(w[i])}");
                }

                var z = x.Zip(w, (a, b) => a * b).Sum() + 0.1;
                s.Circle("n", "Σ + b", 480, 240, 60, $"z = {N2(z)}", Info);
                s.Box("act", $"ReLU(z) = {N2(Math.Max(0, z))}", 680, 210, 260, 60, "activation", Good);
                s.Connect("n", "act");
                s.Label("bias", "b = 0.10", 440, 320, 120, 12);
            })
            .Code("""
                double Neuron(double[] x, double[] w, double b)
                {
                    double z = b;
                    for (int i = 0; i < x.Length; i++) z += w[i] * x[i];   // weighted sum
                    return Math.Max(0, z);                                // ReLU activation
                }

                // a layer is many neurons sharing the same inputs: h = ReLU(W·x + b)
                """)
            .Step("One artificial neuron",
                "Multiply each input by its weight, add them up with a bias: 1.0·0.8 + 0.5·(-0.4) + (-1.0)·0.3 + 0.1 = 0.40. Then apply the activation.",
                "The perceptron (Rosenblatt, 1958) was this unit with a step activation. Weights say how much each input matters; the bias shifts the threshold. Learning means adjusting them.",
                [1, 3, 4, 5])
            .Step("Activation functions",
                "Without a non-linear activation, stacked layers collapse into one linear function. ReLU, sigmoid, tanh and GELU bend the output in different ways.",
                "ReLU (max(0, x)) made deep networks trainable around 2011-2012: cheap, with no vanishing gradient for positive inputs. Transformers typically use GELU or SwiGLU variants.",
                [5],
                s =>
                {
                    s.Wipe();
                    (string Name, Func<double, double> Fn)[] fns = [("ReLU", Relu), ("sigmoid", Sigmoid), ("tanh", Math.Tanh), ("GELU", Gelu)];
                    for (var k = 0; k < fns.Length; k++)
                    {
                        var ox = 40 + k * 240;
                        s.Axes("a" + k, ox, 120, 200, 220, fns[k].Name, string.Empty);
                        var fn = fns[k].Fn;
                        s.Polyline("f" + k, Enumerable.Range(0, 25).Select(i => -3 + i * 0.25).Select(v => (ox + (v + 3) / 6 * 200, 120 + 220 - (fn(v) + 1.2) / 4.2 * 220)).ToArray(), (Tone)(1 + k % 4));
                        s.Label("t" + k, fns[k].Name, ox + 10, 90, 180, 15, Info);
                    }
                })
            .Step("Layers of neurons",
                "A multi-layer perceptron connects every neuron in one layer to every neuron in the next. This one has 3 inputs, two hidden layers of 4, and 2 outputs.",
                "Each hidden layer re-represents the data: early layers detect simple patterns, later layers combine them. 'Deep' learning just means many such layers.",
                [8],
                s =>
                {
                    s.Wipe();
                    for (var l = 0; l < sizes.Length; l++)
                    {
                        for (var i = 0; i < sizes[l]; i++)
                        {
                            s.Circle($"n{l}_{i}", string.Empty, layerX[l], Ny(l, i), 28);
                            if (l > 0)
                            {
                                for (var p = 0; p < sizes[l - 1]; p++)
                                {
                                    s.Connect($"n{l - 1}_{p}", $"n{l}_{i}", arrow: false);
                                }
                            }
                        }
                    }

                    string[] names = ["input", "hidden 1", "hidden 2", "output"];
                    for (var l = 0; l < sizes.Length; l++)
                    {
                        s.Label("ln" + l, names[l], layerX[l] - 40, 60, 100, 12);
                    }
                })
            .Step("Forward pass",
                $"Values flow left to right: each layer computes ReLU(W·x + b). The two outputs pass through softmax, giving {classes[0]} {probs[0]:P0} and {classes[1]} {probs[1]:P0}.",
                "Softmax turns raw scores (logits) into probabilities that sum to 1. A forward pass is just matrix multiplications and element-wise functions, which is why GPUs, built for matrix maths, run neural networks so well.",
                [],
                s =>
                {
                    for (var l = 0; l < sizes.Length; l++)
                    {
                        for (var i = 0; i < sizes[l]; i++)
                        {
                            var label = l == sizes.Length - 1 ? $"{classes[i]} {probs[i]:P0}" : N2(acts[l][i]);
                            s.Write($"n{l}_{i}", label).Paint($"n{l}_{i}", acts[l][i] > 0 ? Info : Neutral);
                        }
                    }

                    s.RetoneLinks(Info);
                })
            .Step("Why depth matters: XOR",
                "XOR is 1 when exactly one input is 1. No single straight line separates the orange points from the blue ones, so one neuron cannot learn it. Two hidden neurons draw two lines, and the band between them is the answer.",
                "Minsky and Papert's 1969 analysis of this limit contributed to the first 'AI winter'. Multi-layer networks with backpropagation (popularised in 1986) solved it. With enough hidden units, a network can approximate any continuous function.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Axes("ax", Px, Py, Pw, Ph, "input a", "input b");
                    (double X, double Y, int C)[] xor = [(0.1, 0.1, 0), (0.9, 0.9, 0), (0.1, 0.9, 1), (0.9, 0.1, 1)];
                    for (var i = 0; i < 4; i++)
                    {
                        var (x, y) = P(xor[i].X, xor[i].Y);
                        s.Dot("xo" + i, x, y, xor[i].C, 12);
                    }

                    s.Polyline("l1", LineXY(0.5), Good);
                    s.Polyline("l2", LineXY(1.5), Good);
                    s.Table("xt", "XOR", 580, 80, 360, "a  b  ->  a XOR b", "0  0  ->  0", "0  1  ->  1", "1  0  ->  1", "1  1  ->  0");
                    s.Say("Two hidden neurons = two lines; the band between them is XOR", Good);
                })
            .Step("Counting parameters",
                "This small network has 3·4 + 4·4 + 4·2 = 36 weights plus 10 biases: 46 parameters. Real networks have millions to trillions.",
                "Parameter count drives memory (2 bytes each in 16-bit precision) and compute per token. More parameters need more data to train well (see scaling laws in the LLM lessons).",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("params", "parameter counts", 60, 60, 880,
                        "this network            46",
                        "LeNet-5 (1998)          ~60 thousand    handwritten digits",
                        "AlexNet (2012)          ~60 million     ImageNet breakthrough",
                        "GPT-2 (2019)            1.5 billion",
                        "GPT-3 (2020)            175 billion",
                        "DeepSeek-V3 (2024)      671 billion, 37 billion active per token (mixture of experts)");
                })
            .Lesson("Neural networks: neurons, activations and layers", "Neural networks", "Weighted sums, non-linear activations, forward passes, softmax and why XOR needs a hidden layer.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Backpropagation()
    {
        double[] x = [1.0, 0.5];
        double[,] w1 = { { 0.6, -0.4 }, { 0.3, 0.8 } };
        double[] w2 = [0.5, 0.9];
        const double Target = 1.0, Lr = 0.2;

        (double[] H, double Y, double L) Forward(double[,] a, double[] b)
        {
            var h = new[] { Math.Max(0, a[0, 0] * x[0] + a[0, 1] * x[1]), Math.Max(0, a[1, 0] * x[0] + a[1, 1] * x[1]) };
            var y = b[0] * h[0] + b[1] * h[1];
            return (h, y, (y - Target) * (y - Target));
        }

        var (h0, y0, l0) = Forward(w1, w2);
        var dy = 2 * (y0 - Target);
        double[] gw2 = [dy * h0[0], dy * h0[1]];
        double[] dh = [dy * w2[0] * (h0[0] > 0 ? 1 : 0), dy * w2[1] * (h0[1] > 0 ? 1 : 0)];
        double[,] gw1 = { { dh[0] * x[0], dh[0] * x[1] }, { dh[1] * x[0], dh[1] * x[1] } };
        double[,] w1New = { { w1[0, 0] - Lr * gw1[0, 0], w1[0, 1] - Lr * gw1[0, 1] }, { w1[1, 0] - Lr * gw1[1, 0], w1[1, 1] - Lr * gw1[1, 1] } };
        double[] w2New = [w2[0] - Lr * gw2[0], w2[1] - Lr * gw2[1]];
        var (h1, y1, l1) = Forward(w1New, w2New);

        void Net(SceneState s, double[,] a, double[] b, double[] h, double y, double loss, Tone linkTone)
        {
            s.Circle("x0", $"x1 = {N2(x[0])}", 120, 170, 46, tone: Info);
            s.Circle("x1", $"x2 = {N2(x[1])}", 120, 400, 46, tone: Info);
            s.Circle("h0", N2(h[0]), 440, 170, 46, "h1");
            s.Circle("h1", N2(h[1]), 440, 400, 46, "h2");
            s.Circle("y", N2(y), 740, 285, 50, "prediction");
            s.Box("loss", $"loss = ({N2(y)} - 1)² = {N3(loss)}", 760, 450, 220, 56, "target = 1.0", loss < l0 ? Good : Warn);
            for (var i = 0; i < 2; i++)
            {
                for (var j = 0; j < 2; j++)
                {
                    s.Connect("x" + j, "h" + i, $"{N2(a[i, j])}", linkTone);
                }

                s.Connect("h" + i, "y", $"{N2(b[i])}", linkTone);
            }

            s.Connect("y", "loss", arrow: true);
        }

        // Optimiser paths on f(x, y) = 0.5 (x² + 12 y²)
        static (double X, double Y) G(double gx, double gy) => (gx, 12 * gy);
        static (double X, double Y) C(double px, double py) => (500 + px * 100, 290 - py * 150);
        (double, double)[] Path(string kind)
        {
            var (px, py) = (-4.0, 1.5);
            var (vx, vy, mx, my, sx, sy) = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
            var path = new List<(double, double)> { C(px, py) };
            for (var t = 1; t <= 25; t++)
            {
                var (gx, gy) = G(px, py);
                switch (kind)
                {
                    case "sgd":
                        px -= 0.15 * gx;
                        py -= 0.15 * gy;
                        break;
                    case "momentum":
                        vx = 0.8 * vx + gx;
                        vy = 0.8 * vy + gy;
                        px -= 0.03 * vx;
                        py -= 0.03 * vy;
                        break;
                    default:
                        mx = 0.9 * mx + 0.1 * gx;
                        my = 0.9 * my + 0.1 * gy;
                        sx = 0.999 * sx + 0.001 * gx * gx;
                        sy = 0.999 * sy + 0.001 * gy * gy;
                        var (mh, nh) = (1 - Math.Pow(0.9, t), 1 - Math.Pow(0.999, t));
                        px -= 0.3 * (mx / mh) / (Math.Sqrt(sx / nh) + 1e-8);
                        py -= 0.3 * (my / mh) / (Math.Sqrt(sy / nh) + 1e-8);
                        break;
                }

                path.Add(C(px, py));
            }

            return path.ToArray();
        }

        return new StoryBuilder("AI8", "Training is a loop: run the network forward, measure the loss, send the error backwards with the chain rule to get every weight's gradient (backpropagation), and nudge every weight downhill. Optimisers such as Adam and tricks such as normalisation make deep networks train reliably.")
            .Setup(s => Net(s, w1, w2, h0, y0, l0, Neutral))
            .Code("""
                // forward
                h = ReLU(W1 · x);          y = W2 · h;          loss = (y - target)²

                // backward (chain rule, from the loss towards the inputs)
                dL/dy  = 2 (y - target)
                dL/dW2 = dL/dy · h
                dL/dh  = dL/dy · W2   (times ReLU'(z) = 1 where z > 0, else 0)
                dL/dW1 = dL/dh · x

                // update
                W -= learningRate · dL/dW
                """)
            .Step("Forward pass and loss",
                $"Inputs flow through the weights to a prediction of {N2(y0)}. The target is 1.0, so the squared-error loss is {N3(l0)}.",
                "The loss is a single number measuring how wrong the network is on this example. Training wants to make it smaller by changing the weights on the arrows.",
                [2])
            .Step("Backward pass: assign blame",
                $"Backpropagation starts at the loss and applies the chain rule layer by layer. dL/dy = {N2(dy)}; the output weights get gradients {N2(gw2[0])} and {N2(gw2[1])}; the error then flows back into the hidden layer.",
                "Each weight's gradient says how much the loss would change if that weight moved slightly. Backprop computes all of them in one backward sweep that costs about as much as the forward pass: that efficiency is what makes training billion-parameter networks possible.",
                [4, 5, 6, 7, 8],
                s =>
                {
                    s.RetoneLinks(Warn);
                    s.SubText("y", $"dL/dy = {N2(dy)}").SubText("h0", $"dL/dh1 = {N2(dh[0])}").SubText("h1", $"dL/dh2 = {N2(dh[1])}");
                    s.Paint("loss", Bad);
                })
            .Step("Update every weight",
                $"Each weight moves against its gradient (learning rate 0.2). Running forward again, the prediction rises to {N2(y1)} and the loss falls from {N3(l0)} to {N3(l1)}.",
                "Repeat this for millions of examples in mini-batches and the network learns. One pass over the whole dataset is an epoch; frontier LLMs see most of their training data roughly once.",
                [10, 11],
                s => Net(s, w1New, w2New, h1, y1, l1, Good))
            .Step("Optimisers: SGD, momentum, Adam",
                "On a long, narrow valley, plain gradient descent (amber) zig-zags across the walls. Momentum (blue) builds up speed along the valley. Adam (green) scales each direction by its recent gradient size and heads more directly to the minimum.",
                "Adam (2014) and its variant AdamW are the default for training transformers. Learning-rate warm-up and decay schedules, gradient clipping and large batches are standard practice for large models.",
                [],
                s =>
                {
                    s.Wipe();
                    foreach (var (c, k) in new[] { (1.0, 0), (4.0, 1), (9.0, 2) })
                    {
                        var (rx, ry) = (Math.Sqrt(2 * c), Math.Sqrt(2 * c / 12));
                        s.Polyline("ell" + k, Enumerable.Range(0, 33).Select(i => i * Math.PI / 16).Select(t => C(rx * Math.Cos(t), ry * Math.Sin(t))).ToArray(), Neutral, dashed: true);
                    }

                    s.Polyline("sgd", Path("sgd"), Warn);
                    s.Polyline("mom", Path("momentum"), Info);
                    s.Polyline("adam", Path("adam"), Good);
                    s.Dot("min", 500, 290, 2, 8, "minimum");
                    s.Dot("start", C(-4, 1.5).X, C(-4, 1.5).Y, 4, 8, "start");
                })
            .Step("Making deep networks trainable",
                "Very deep networks suffered from gradients that vanished or exploded as they passed through many layers, and from overfitting. A toolbox of fixes changed that.",
                "Residual connections (2015) give gradients a shortcut; normalisation layers (BatchNorm 2015, LayerNorm 2016) keep activations well scaled; careful initialisation and ReLU-family activations avoid saturation; dropout (2014) and weight decay fight overfitting.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("fixes", "problems and fixes", 40, 60, 920,
                        "vanishing gradients     ReLU/GELU activations, residual connections, LSTM gates",
                        "exploding gradients     gradient clipping, careful initialisation, lower learning rate",
                        "unstable activations    BatchNorm (vision), LayerNorm / RMSNorm (transformers)",
                        "overfitting             more data, data augmentation, dropout, weight decay, early stopping",
                        "slow convergence        Adam/AdamW, learning-rate warm-up and decay, larger batches");
                })
            .Lesson("Backpropagation and optimisers", "Neural networks", "The chain rule on a real tiny network, weight updates, SGD vs momentum vs Adam, and training fixes.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Cnn()
    {
        int[,] img = new int[6, 6];
        for (var r = 0; r < 6; r++)
        {
            for (var c = 0; c < 6; c++)
            {
                img[r, c] = c < 3 ? 1 : 9;
            }
        }

        int[,] kernel = { { -1, 0, 1 }, { -1, 0, 1 }, { -1, 0, 1 } };
        int Conv(int r, int c)
        {
            var sum = 0;
            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    sum += img[r + i, c + j] * kernel[i, j];
                }
            }

            return sum;
        }

        const double Cell = 54, Ix = 40, Iy = 90, Ox = 620, Oy = 250;

        void Window(SceneState s, int r, int c) =>
            s.Region("win", "3x3 window", Ix + c * Cell - 4, Iy + r * Cell - 22, 3 * Cell + 6, 3 * Cell + 26, Warn);

        void Output(SceneState s, int filled)
        {
            for (var r = 0; r < 4; r++)
            {
                for (var c = 0; c < 4; c++)
                {
                    var index = r * 4 + c;
                    var value = Conv(r, c);
                    s.Write($"o{r}_{c}", index < filled ? value.ToString() : string.Empty)
                        .Paint($"o{r}_{c}", index < filled && value > 0 ? Good : Neutral)
                        .Ghost($"o{r}_{c}", index >= filled);
                }
            }
        }

        return new StoryBuilder("AI9", "Convolutional networks slide small learned filters across an image. Each filter detects a local pattern anywhere it appears; stacking layers turns edges into textures, parts and whole objects. CNNs launched the deep learning boom in 2012.")
            .Setup(s =>
            {
                s.Caption("ic", "image (dark = 1, bright = 9)", Ix, 40, 320);
                for (var r = 0; r < 6; r++)
                {
                    for (var c = 0; c < 6; c++)
                    {
                        s.Box($"p{r}_{c}", img[r, c].ToString(), Ix + c * Cell, Iy + r * Cell, Cell - 4, Cell - 4, tone: img[r, c] > 5 ? Info : Neutral);
                    }
                }

                s.Caption("kc", "vertical-edge filter", Ox, 40, 300);
                for (var r = 0; r < 3; r++)
                {
                    for (var c = 0; c < 3; c++)
                    {
                        s.Box($"k{r}_{c}", kernel[r, c].ToString(), Ox + c * Cell, 70 + r * 50, Cell - 4, 46, tone: kernel[r, c] > 0 ? Good : kernel[r, c] < 0 ? Bad : Neutral);
                    }
                }

                s.Caption("oc", "feature map (4 x 4)", Ox, Oy - 26, 300);
                for (var r = 0; r < 4; r++)
                {
                    for (var c = 0; c < 4; c++)
                    {
                        s.Box($"o{r}_{c}", string.Empty, Ox + c * Cell, Oy + r * Cell, Cell - 4, Cell - 4);
                    }
                }

                Output(s, 0);
            })
            .Code("""
                for (int r = 0; r < H - 2; r++)
                for (int c = 0; c < W - 2; c++)
                {
                    int sum = 0;
                    for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        sum += image[r + i, c + j] * kernel[i, j];     // multiply and add
                    featureMap[r, c] = Math.Max(0, sum);                 // ReLU
                }
                """)
            .Step("An image is a grid of numbers",
                "A 6x6 greyscale image: dark on the left (1), bright on the right (9). There is a vertical edge in the middle. On the right is a 3x3 filter: -1 on the left column, +1 on the right.",
                "Colour images have 3 channels (red, green, blue), so filters are 3x3x3. In a CNN, the filter values are not hand-written: they are weights learned by backpropagation.",
                [])
            .Step("Slide the filter: flat region",
                "Place the filter on the top-left 3x3 patch. Multiply each pixel by the filter value and add: the left and right columns are both 1, so they cancel. Output: 0.",
                "Uniform areas give 0: the filter does not respond where nothing changes.",
                [5, 6, 7],
                s =>
                {
                    Window(s, 0, 0);
                    Output(s, 1);
                })
            .Step("Slide right: the edge",
                $"One step right, the window's right column now covers bright pixels: (9 - 1) x 3 rows = {Conv(0, 1)}. A strong response exactly where the edge is.",
                "The same 9 weights are reused at every position (weight sharing). A pattern is detected wherever it appears, and a convolution layer needs far fewer parameters than a fully connected one.",
                [5, 6, 7, 8],
                s =>
                {
                    Window(s, 0, 1);
                    Output(s, 2);
                })
            .Step("The whole feature map",
                "Sliding over every position fills a 4x4 feature map: zeros in the flat areas, high values along the vertical edge. The filter has found the edge.",
                "A layer applies dozens or hundreds of filters, producing one feature map each. Pooling (for example, the max of each 2x2 block) then shrinks the maps, keeping the strongest responses.",
                [1, 2, 8],
                s =>
                {
                    Window(s, 3, 3);
                    Output(s, 16);
                })
            .Step("From edges to objects",
                "Stack convolution, activation and pooling layers. Early layers find edges and colours, middle layers textures and parts, late layers whole objects. A final classifier outputs class probabilities.",
                "The receptive field grows with depth, so later layers see more of the image. Visualising trained filters shows this hierarchy clearly.",
                [],
                s =>
                {
                    s.Wipe();
                    string[] stages = ["image 224x224x3", "conv + ReLU: edges", "pool", "conv: textures", "conv: parts", "classifier: cat 92%"];
                    for (var i = 0; i < stages.Length; i++)
                    {
                        s.Box("st" + i, stages[i], 30 + i * 160, 240, 145, 70, tone: i == stages.Length - 1 ? Good : Info);
                        if (i > 0)
                        {
                            s.Connect("st" + (i - 1), "st" + i);
                        }
                    }
                })
            .Step("Milestones",
                "CNNs went from reading postcodes to beating humans on some image benchmarks, then handed part of vision over to transformers.",
                "AlexNet won ImageNet 2012 with 15.3% top-5 error versus 26.2% for the runner-up, trained on two GPUs. ResNet (2015) reached 152 layers using residual connections. Vision Transformers (2020) treat image patches as tokens.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("hist", "convolutional networks", 40, 60, 920,
                        "LeNet-5      1998   handwritten digits, bank cheques",
                        "AlexNet      2012   ImageNet: 15.3% top-5 error, GPUs + ReLU + dropout",
                        "VGG / GoogLeNet 2014  deeper, smaller filters",
                        "ResNet       2015   152 layers with residual (skip) connections",
                        "U-Net        2015   segmentation; later the backbone of diffusion models",
                        "ViT          2020   Vision Transformer: patches as tokens");
                })
            .Lesson("Convolutional neural networks", "Neural networks", "Sliding filters computed live, feature maps, weight sharing, pooling and the CNN milestones.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Rnn()
    {
        string[] words = ["The", "cat", "sat", "down"];

        return new StoryBuilder("AI10", "Recurrent networks read a sequence one step at a time, carrying a hidden state forward as memory. Gradients fade over long distances; LSTM gates help, and attention lets a model look back at every step directly, which led to transformers.")
            .Setup(s =>
            {
                for (var t = 0; t < words.Length; t++)
                {
                    s.Box("x" + t, words[t], 90 + t * 230, 420, 150, 56, tone: Info);
                    s.Circle("h" + t, $"h{t + 1}", 165 + t * 230, 280, 44);
                    s.Box("y" + t, $"output {t + 1}", 90 + t * 230, 100, 150, 50);
                    s.Connect("x" + t, "h" + t).Connect("h" + t, "y" + t);
                    if (t > 0)
                    {
                        s.Connect("h" + (t - 1), "h" + t, "state");
                    }
                }
            })
            .Code("""
                h = zeros(d);
                foreach (var x in tokens)
                {
                    h = tanh(Wx · x + Wh · h + b);     // same weights at every step
                    output = Wy · h;
                }
                """)
            .Step("A loop over time",
                "An RNN reads 'The', updates its hidden state, then reads 'cat' with that state, and so on. The hidden state is a running summary of everything read so far.",
                "The same weights are used at every time step, so the network handles sequences of any length. RNNs powered speech recognition and machine translation in the mid-2010s.",
                [1, 2, 4])
            .Step("Long-range memory fades",
                "Gradients flowing back through many steps are multiplied again and again. If each step scales them by 0.5, after 7 steps only 0.5^7 ≈ 0.008 of the signal remains.",
                "In 'The cat, which already ate a whole bowl of food this morning, was full', the network must link 'was' to 'cat' across many words. Vanishing gradients make such long dependencies very hard to learn.",
                [4],
                s =>
                {
                    s.RetoneLinks(Neutral);
                    s.Paint("h3", Warn).Paint("h0", Bad);
                    s.Disconnect("h0", "h1").Connect("h0", "h1", "x 0.5", Bad, dashed: true);
                    s.Disconnect("h1", "h2").Connect("h1", "h2", "x 0.5", Warn, dashed: true);
                    s.Disconnect("h2", "h3").Connect("h2", "h3", "x 0.5", Warn);
                    s.Say("gradient after 7 steps: 0.5^7 ≈ 0.008", Bad);
                })
            .Step("LSTM: gated memory",
                "A long short-term memory cell adds a separate cell state that flows along almost unchanged, plus three learned gates: forget (what to erase), input (what to write) and output (what to reveal).",
                "Because the cell state is updated additively, gradients survive much longer distances. LSTMs (Hochreiter and Schmidhuber, 1997) and the simpler GRU (2014) dominated sequence modelling until 2017.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Box("cell", null, 140, 120, 720, 300, tone: Info);
                    s.Box("hw", "cell state: an information highway", 160, 140, 680, 44, tone: Good);
                    string[] gates = ["forget gate", "input gate", "output gate"];
                    for (var i = 0; i < 3; i++)
                    {
                        s.Box("g" + i, gates[i], 180 + i * 230, 280, 180, 60, "sigmoid: 0 = block, 1 = pass", Warn);
                        s.Connect("g" + i, "hw", dashed: true);
                    }

                    s.Box("xin", "input + previous state", 380, 470, 240, 50);
                    for (var i = 0; i < 3; i++)
                    {
                        s.Connect("xin", "g" + i);
                    }
                })
            .Step("Sequence to sequence and attention",
                "For translation, an encoder RNN squeezes 'je suis étudiant' into one vector and a decoder produces 'I am a student' from it. One vector for a whole sentence is a bottleneck. Attention lets the decoder look back at every encoder state instead.",
                "Seq2seq (2014) and attention (Bahdanau et al., 2014) made neural machine translation beat older systems. Google switched Translate to neural models in 2016. Attention turned out to be the key ingredient.",
                [],
                s =>
                {
                    s.Wipe();
                    string[] src = ["je", "suis", "étudiant"];
                    string[] dst = ["I", "am", "a", "student"];
                    for (var i = 0; i < src.Length; i++)
                    {
                        s.Box("e" + i, src[i], 60 + i * 150, 380, 130, 56, "encoder", Info);
                    }

                    for (var i = 0; i < dst.Length; i++)
                    {
                        s.Box("d" + i, dst[i], 540 + i * 110, 120, 95, 56, "decoder", Good);
                    }

                    s.Box("ctx", "one context vector", 300, 220, 220, 56, "bottleneck", Warn);
                    s.Connect("e2", "ctx").Connect("ctx", "d0");
                    for (var i = 0; i < src.Length; i++)
                    {
                        s.Connect("d3", "e" + i, i == 2 ? "attends most" : null, i == 2 ? Good : Info, dashed: true);
                    }
                })
            .Step("The sequential bottleneck",
                "An RNN must finish step t before starting step t + 1, so it cannot use thousands of GPU cores in parallel along the sequence. Transformers (2017) dropped recurrence and kept only attention.",
                "Removing recurrence made training massively parallel, and the rise of large language models followed. State-space models (such as Mamba, 2023) and modern RNN variants revisit recurrence for very long contexts.",
                [],
                s =>
                {
                    s.Wipe();
                    s.Table("vs", "RNN vs transformer", 60, 80, 880,
                        "                       RNN / LSTM                 transformer",
                        "processing             one token after another    all tokens in parallel",
                        "long-range links       through many steps         one attention hop",
                        "training on GPUs       poorly parallel            highly parallel",
                        "cost per token         constant                   grows with context (attention)");
                })
            .Lesson("Recurrent networks, LSTMs and the road to attention", "Neural networks", "Hidden state over time, vanishing gradients, gated memory, seq2seq and why attention won.");
    }
}
