namespace StandardsCalculator.Core.Math;

/// <summary>
/// Shape-preserving monotone cubic Hermite spline (PCHIP — Fritsch &amp; Carlson, 1980).
///
/// Guarantees:
///   • Interpolates exactly through every knot: Evaluate(x[i]) == y[i].
///   • C¹ smooth: first derivatives are continuous.
///   • Monotone within each interval: no spurious oscillation.
///
/// Extrapolation (outside [x[0], x[n-1]]):
///   Log-linear extension — equivalent to a lognormal tail:
///     y_ext = y_edge + (x_edge · d_edge) · ln(x / x_edge)
///   where d_edge is the tangent at the boundary knot.
///   This guarantees the inverse maps finite z-scores to finite, positive lifts
///   (no unbounded linear extrapolation) and is appropriate for positive-valued
///   strength data that are right-skewed.
///
/// Usage pattern for PercentileModel:
///   x = lift values at the 5 anchor percentiles (always positive, ascending)
///   y = z-scores at those percentiles (ascending, since higher lift → higher z)
///   Evaluate(lift) → z;  Inverse(z) → lift
///
/// Reference: Fritsch &amp; Carlson (1980), SIAM J. Numer. Anal. 17(2):238–246.
/// </summary>
public sealed class MonotoneSpline
{
    private readonly double[] _x;
    private readonly double[] _y;
    private readonly double[] _d;  // tangents at each knot

    /// <summary>
    /// Build a PCHIP spline through the given knots.
    /// </summary>
    /// <param name="x">Knot x-values, strictly ascending.</param>
    /// <param name="y">Knot y-values (need not be monotone, though PCHIP is designed for that).</param>
    public MonotoneSpline(double[] x, double[] y)
    {
        if (x.Length < 2) throw new ArgumentException("At least 2 knots required.", nameof(x));
        if (x.Length != y.Length) throw new ArgumentException("x and y must have the same length.");

        int n = x.Length;
        _x = x;
        _y = y;
        _d = new double[n];

        // Step 1 — finite differences
        double[] h     = new double[n - 1];
        double[] delta = new double[n - 1];
        for (int k = 0; k < n - 1; k++)
        {
            h[k]     = x[k + 1] - x[k];
            delta[k] = (y[k + 1] - y[k]) / h[k];
        }

        // Step 2 — initial tangent estimates
        // Endpoints: one-sided finite difference
        _d[0]     = delta[0];
        _d[n - 1] = delta[n - 2];

        // Interior: arithmetic mean of adjacent slopes, zeroed at sign changes
        for (int k = 1; k < n - 1; k++)
        {
            if (delta[k - 1] * delta[k] <= 0.0)
                _d[k] = 0.0;                      // local extremum or flat region
            else
                _d[k] = 0.5 * (delta[k - 1] + delta[k]);
        }

        // Step 3 — Fritsch-Carlson monotonicity conditions
        for (int k = 0; k < n - 1; k++)
        {
            if (delta[k] == 0.0)
            {
                _d[k] = _d[k + 1] = 0.0;          // flat interval → zero derivatives
                continue;
            }

            double alpha = _d[k]     / delta[k];
            double beta  = _d[k + 1] / delta[k];

            // Clamp derivatives that point against the interval direction
            if (alpha < 0.0) { _d[k]     = 0.0; alpha = 0.0; }
            if (beta  < 0.0) { _d[k + 1] = 0.0; beta  = 0.0; }

            // Scale down if outside the circle of radius 3 (necessary condition for monotonicity)
            double tau2 = alpha * alpha + beta * beta;
            if (tau2 > 9.0)
            {
                double tau = 3.0 / System.Math.Sqrt(tau2);
                _d[k]     *= tau;
                _d[k + 1] *= tau;
            }
        }
    }

    /// <summary>
    /// Evaluate the spline at x. Extrapolates log-linearly outside the knot range.
    /// </summary>
    public double Evaluate(double x)
    {
        int n = _x.Length;

        // Extrapolation: log-linear tails (lognormal equivalent)
        if (x <= _x[0])     return TailValue(_x[0],     _y[0],     _d[0],     x);
        if (x >= _x[n - 1]) return TailValue(_x[n - 1], _y[n - 1], _d[n - 1], x);

        // Binary search for interval k: x in [_x[k], _x[k+1]]
        int k = BinarySearchInterval(_x, x);
        return EvalCubic(k, x);
    }

    /// <summary>
    /// Inverse: given a y-value, return the x such that Evaluate(x) == y.
    /// Requires the spline to be strictly monotone (all delta[k] have the same sign).
    /// </summary>
    public double Inverse(double y)
    {
        int n = _x.Length;

        // Tail inverses are analytically exact
        if (y <= _y[0])     return InverseTail(_x[0],     _y[0],     _d[0],     y);
        if (y >= _y[n - 1]) return InverseTail(_x[n - 1], _y[n - 1], _d[n - 1], y);

        // Binary search for interval k: y in [_y[k], _y[k+1]]
        // (assumes monotone increasing; adjust if decreasing)
        int lo = 0, hi = n - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (_y[mid] <= y) lo = mid; else hi = mid - 1;
        }
        int k = lo;

        // Bisection within [_x[k], _x[k+1]] (guaranteed convergent since cubic is monotone here)
        double xLo = _x[k], xHi = _x[k + 1];
        for (int iter = 0; iter < 64; iter++)
        {
            double xMid = 0.5 * (xLo + xHi);
            double yMid = EvalCubic(k, xMid);
            if (yMid < y) xLo = xMid; else xHi = xMid;
            if (xHi - xLo < 1e-10) break;
        }
        return 0.5 * (xLo + xHi);
    }

    // ── Hermite cubic evaluation ─────────────────────────────────────────────

    private double EvalCubic(int k, double x)
    {
        double h  = _x[k + 1] - _x[k];
        double t  = (x - _x[k]) / h;
        double t2 = t * t;
        double t3 = t2 * t;

        double h00 =  2*t3 - 3*t2 + 1;
        double h10 =    t3 - 2*t2 + t;
        double h01 = -2*t3 + 3*t2;
        double h11 =    t3 -   t2;

        return h00 * _y[k] + h10 * h * _d[k]
             + h01 * _y[k + 1] + h11 * h * _d[k + 1];
    }

    // ── Log-linear (lognormal) tail helpers ──────────────────────────────────

    // y_ext = y_edge + (x_edge * d_edge) * ln(x / x_edge)
    // Slope in ln(x) space: d(y)/d(ln x) = x * dy/dx = x_edge * d_edge at x_edge.
    private static double TailValue(double xEdge, double yEdge, double dEdge, double x)
    {
        if (xEdge <= 0 || x <= 0) return yEdge; // safety: can't take log of non-positive
        double logSlope = xEdge * dEdge;
        return yEdge + logSlope * System.Math.Log(x / xEdge);
    }

    // x = x_edge * exp((y - y_edge) / (x_edge * d_edge))
    private static double InverseTail(double xEdge, double yEdge, double dEdge, double y)
    {
        double logSlope = xEdge * dEdge;
        if (System.Math.Abs(logSlope) < 1e-12) return xEdge; // degenerate: flat boundary
        return xEdge * System.Math.Exp((y - yEdge) / logSlope);
    }

    // ── Utilities ────────────────────────────────────────────────────────────

    private static int BinarySearchInterval(double[] xs, double x)
    {
        int lo = 0, hi = xs.Length - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (xs[mid] <= x) lo = mid; else hi = mid - 1;
        }
        return lo;
    }
}
