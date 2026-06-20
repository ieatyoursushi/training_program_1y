# Methods — Strength Standards Calculator

All formulas used in this tool are documented here with rigorous mathematical
definitions. Every number the tool produces is reproducible from these equations and
the scraped data files. Source-file cross-references appear at the head of each
section.

---

## Standing Definitions

The following notation is fixed throughout this document.

**Spaces**

| Symbol | Definition |
|--------|-----------|
| $\mathcal{W} \subset \mathbb{R}_{>0}$ | Bodyweight space (lb or kg) |
| $\mathcal{L} \subset \mathbb{R}_{>0}$ | One-rep-max lift space |
| $\mathcal{P} = (0,100) \subset \mathbb{R}$ | Open percentile interval |
| $\mathcal{Z} = \mathbb{R}$ | Latent z-score space |
| $\mathcal{G} = \{M, F\}$ | Sex (male / female) |
| $\mathcal{A} \subset \mathbb{Z}_{>0}$ | Age in years |

**Ordered level set.** The five published strength levels are indexed as

$$\mathcal{K} = \{1,2,3,4,5\} \;\leftrightarrow\; \{\text{Beginner},\;\text{Novice},\;\text{Intermediate},\;\text{Advanced},\;\text{Elite}\}$$

**Anchor quantile vector.** The level boundaries correspond to fixed population
percentile ranks:

$$\boldsymbol{\tau} = (\tau_1,\tau_2,\tau_3,\tau_4,\tau_5) = (0.05,\;0.20,\;0.50,\;0.80,\;0.95)$$

**Anchor z-score vector.** The corresponding standard-normal quantiles are fixed at
build time:

$$\boldsymbol{\zeta} = (\zeta_1,\ldots,\zeta_5) = \bigl(\Phi^{-1}(0.05),\;\Phi^{-1}(0.20),\;0,\;\Phi^{-1}(0.80),\;\Phi^{-1}(0.95)\bigr)$$

$$\approx (-1.6449,\;-0.8416,\;0,\;+0.8416,\;+1.6449)$$

**Standards tables (data source).** Scraped from strengthlevel.com for each exercise
$e$ and sex $g \in \mathcal{G}$; cached in `data/standards/{exercise}.{unit}.{m|f}.json`.

- *By-bodyweight table* $T_g^e = \{(w_j, \mathbf{b}_j^g)\}_{j=1}^{N}$ where $w_j \in \mathcal{W}$
  is a bodyweight bin and $\mathbf{b}_j^g = (b_{j1},\ldots,b_{j5}) \in \mathcal{L}^5$
  are the five level boundaries.

- *By-age table* $A_g^e = \{(a_j, \mathbf{b}_j^{g,\mathrm{age}})\}_{j=1}^{M}$ where
  $a_j \in \mathcal{A}$ and the boundaries are age-specific and bodyweight-independent.

**Normal CDF and probit.** The standard normal CDF is

$$\Phi(z) = \frac{1}{\sqrt{2\pi}}\int_{-\infty}^{z} e^{-t^2/2}\,dt$$

and its inverse (the probit) is $\Phi^{-1}: (0,1) \to \mathbb{R}$. Both are implemented
in `Core/Math/Distributions.cs` — $\Phi$ via the Abramowitz & Stegun rational
approximation (error $< 7.5\times10^{-8}$); $\Phi^{-1}$ via Acklam's approximation
(max absolute error $\approx 3\times10^{-9}$).

---

## Section 0 — One-Rep-Max Estimation

*`Core/Math/OneRepMax.cs`*

Given a performed lift of weight $w_{\mathrm{rep}} \in \mathcal{L}$ for
$r \in \mathbb{Z}_{\geq 1}$ repetitions, the Epley (1985) estimator gives

$$\ell = w_{\mathrm{rep}}\!\left(1 + \frac{r}{30}\right)$$

When $r=1$ this is the identity $\ell = w_{\mathrm{rep}}$. The estimated 1RM $\ell$
replaces the raw performed weight in all downstream computations.

---

## Section 1 — Percentile Model: Monotone Cubic Spline (Production)

*`Core/Math/MonotoneSpline.cs`, `Core/Math/PercentileModel.cs`*

### 1.1 Problem statement

Let $\mathbf{b}(w) = (b_1(w),\ldots,b_5(w)) \in \mathcal{L}^5$ be the boundary row at
bodyweight $w$ (interpolated between table rows when $w \notin \{w_j\}$; see §1.4).
We seek a strictly monotone $C^1$ function

$$\phi: \mathcal{L} \to \mathcal{Z}, \quad \phi(b_k(w)) = \zeta_k \;\;\text{for all } k\in\mathcal{K}$$

such that the percentile estimate $p = 100\cdot\Phi(\phi(\ell))$ is smooth, shape-preserving,
and passes exactly through every published boundary. The map $\phi$ depends on the row
$\mathbf{b}(w)$ and is rebuilt per evaluation.

### 1.2 PCHIP construction (Fritsch & Carlson, 1980)

Let the five knots be $(x_k, y_k) = (b_k(w), \zeta_k)$ for $k=1,\ldots,5$. Define
interval lengths and finite-difference slopes:

$$h_k = x_{k+1} - x_k, \quad \delta_k = \frac{y_{k+1} - y_k}{h_k}, \quad k = 1,\ldots,4$$

**Step 1 — Initial tangent estimates.** Endpoint tangents $d_1 = \delta_1$,
$d_5 = \delta_4$. Interior tangents for $k = 2,3,4$:

$$d_k = \begin{cases}
  \dfrac{\delta_{k-1} + \delta_k}{2} & \text{if } \operatorname{sgn}(\delta_{k-1}) = \operatorname{sgn}(\delta_k) \\[6pt]
  0 & \text{otherwise (local extremum)}
\end{cases}$$

**Step 2 — Fritsch–Carlson monotonicity correction.** For each interval $k = 1,\ldots,4$,
set scaled tangent ratios $\alpha_k = d_k/\delta_k$, $\beta_k = d_{k+1}/\delta_k$, and apply:

- If $\delta_k = 0$: set $d_k = d_{k+1} = 0$.
- If $\alpha_k < 0$: set $d_k \leftarrow 0$.
- If $\beta_k < 0$: set $d_{k+1} \leftarrow 0$.
- If $\alpha_k^2 + \beta_k^2 > 9$ (outside the radius-3 Fritsch–Carlson circle):

$$d_k \;\leftarrow\; \frac{3\,\alpha_k\,\delta_k}{\sqrt{\alpha_k^2 + \beta_k^2}}, \qquad d_{k+1} \;\leftarrow\; \frac{3\,\beta_k\,\delta_k}{\sqrt{\alpha_k^2 + \beta_k^2}}$$

After Step 2, $\phi$ is guaranteed strictly monotone on $[x_1, x_5]$.

**Step 3 — Cubic Hermite evaluation.** For $\ell \in [x_k, x_{k+1}]$, set
$t = (\ell - x_k)/h_k \in [0,1]$. The spline evaluates as

$$\phi(\ell) = h_{00}(t)\,y_k \;+\; h_{10}(t)\,h_k\,d_k \;+\; h_{01}(t)\,y_{k+1} \;+\; h_{11}(t)\,h_k\,d_{k+1}$$

where the cubic Hermite basis polynomials are

$$h_{00}(t) = 2t^3 - 3t^2 + 1, \qquad h_{10}(t) = t^3 - 2t^2 + t$$
$$h_{01}(t) = -2t^3 + 3t^2, \qquad h_{11}(t) = t^3 - t^2$$

### 1.3 Log-linear tail extension

For $\ell$ outside the anchor range $[x_1, x_5]$, the spline is extended by a
log-linear tail anchored at the nearest edge knot $(\ell_e, \zeta_e)$ with tangent
$d_e$:

$$\phi(\ell) = \zeta_e + (\ell_e\, d_e)\,\ln\!\left(\frac{\ell}{\ell_e}\right)$$

This is the parametric family $\phi(\ell) = a + b\ln\ell$ (lognormal tail), which:
(i) satisfies the boundary condition $\phi(\ell_e) = \zeta_e$,
(ii) is $C^1$-continuous with the spline at $\ell_e$ (no kink), and
(iii) keeps $p = 100\cdot\Phi(\phi(\ell)) \in (0,100)$ as $\ell \to 0^+$ or $\ell \to +\infty$.

### 1.4 Inverse and bodyweight interpolation

**Inverse (percentile to lift).** For $p \in \mathcal{P}$, set $z = \Phi^{-1}(p/100)$
and solve $\phi(\ell) = z$:

- *Interior* ($z \in [\zeta_1,\zeta_5]$): bisection within the unique cubic segment
  containing $z$.
- *Tail* ($z < \zeta_1$ or $z > \zeta_5$): analytically invert the log-linear extension,

$$\ell = \ell_e\,\exp\!\left(\frac{z - \zeta_e}{\ell_e\,d_e}\right)$$

**Bodyweight interpolation.** If $w \notin \{w_j\}$, locate the enclosing table rows
$w_j \leq w \leq w_{j+1}$ and linearly interpolate each boundary:

$$b_k(w) = b_{jk} + \frac{w - w_j}{w_{j+1} - w_j}\,(b_{j+1,k} - b_{jk}), \quad k\in\mathcal{K}$$

---

## Section 2 — Percentile Model: LMS / BCCG (Research / Diagnostic Mode)

*`Core/Math/LmsModel.cs`*

### 2.1 The BCCG family (Cole & Green, 1992)

A random variable $Y > 0$ follows the **Box-Cox Cole-Green (BCCG)** distribution with
parameters $(L, M, S)$ if the transformation

$$Z = \begin{cases}
  \dfrac{(Y/M)^L - 1}{L\,S} & L \neq 0 \\[8pt]
  \dfrac{\ln(Y/M)}{S} & L = 0
\end{cases}$$

satisfies $Z \sim N(0,1)$. The parameter $M > 0$ is the median (since $Z=0 \Rightarrow Y=M$),
$S > 0$ governs spread, and $L\in\mathbb{R}$ governs skewness: $L=1$ corresponds to a
(truncated) normal on the original scale, $L=0$ to lognormal, and $L=1/2$ to a
square-root-normal distribution.

The percentile function (inverse) is

$$Y = \begin{cases}
  M\,(1 + L\,S\,Z)^{1/L} & L \neq 0 \\[6pt]
  M\,\exp(S\,Z) & L = 0
\end{cases}$$

### 2.2 Per-row fitting

Given boundary row $\mathbf{b} = (b_1,\ldots,b_5)$, fix

$$M = b_3 \quad \text{(the published 50th-percentile boundary)}$$

For each $k\in\mathcal{K}$, define the power-transformed deviation from the median:

$$f_k(L) = \begin{cases}
  \dfrac{(b_k/M)^L - 1}{L} & L \neq 0 \\[8pt]
  \ln(b_k/M) & L = 0
\end{cases}$$

Under the BCCG model, $f_k(L)/\zeta_k = S$ for all $k\neq 3$. With $M$ fixed, the
optimal parameters are:

**Optimal $S$ given $L$:**

$$S^*(L) = \operatorname{mean}_{k\neq 3}\left\{\frac{f_k(L)}{\zeta_k}\right\}$$

**Optimal $L$:** minimize the variance of the implied $S$ estimates across the four
non-median anchors,

$$L^* = \arg\min_{L\,\in\,[-2,\,2]}\;\operatorname{Var}_{k\neq 3}\!\left(\frac{f_k(L)}{\zeta_k}\right)$$

This is a univariate optimization solved by **golden-section search** to tolerance $10^{-6}$.

**Goodness-of-fit.** After fitting $(L^*, M, S^*)$, the RMSE at the 5 anchors is

$$\text{RMSE} = \sqrt{\frac{1}{5}\sum_{k=1}^{5}\!\left(\hat{z}_k - \zeta_k\right)^2}, \quad \hat{z}_k = \frac{f_k(L^*)}{L^* S^*}$$

Note $\hat{z}_3 = \zeta_3 = 0$ by construction; the error concentrates on the four
non-median boundaries.

**Empirical result (deadlift):** $L^*\approx 0.49$–$0.53$ uniformly across all
bodyweight rows and both sexes, with overall RMSE $< 0.004$. The near-constant $L$
near $1/2$ confirms a square-root-normal regime: lifting strength on a square-root
scale is approximately normally distributed. $S$ decreases with bodyweight
($\approx 0.38$ at 110 lb to $\approx 0.24$ at 310 lb), indicating heavier lifters
are a more homogeneous sub-population in relative strength.

### 2.3 Two-dimensional centile surface

After fitting $(L^*_j, M_j, S^*_j)$ at each bodyweight row $w_j$:

- $M(w)$: PCHIP monotone spline fit to $\{(w_j, M_j)\}$ (strength is non-decreasing
  in bodyweight by assumption).
- $L(w)$, $S(w)$: piecewise-linear interpolation (these vary slowly and need not be
  monotone).

The resulting centile surface is the map

$$(w,\, p) \;\longmapsto\; Y^{-1}\!\bigl(\Phi^{-1}(p/100);\; L(w), M(w), S(w)\bigr)$$

Access via `--diagnostics {exercise}`.

**Reference:** Cole, T.J. & Green, P.J. (1992). Smoothing reference centile curves:
The LMS method and penalized likelihood. *Statistics in Medicine*, 11(10), 1305–1319.

---

## Section 3 — Age Adjustment

*`Core/Math/AgeAdjustment.cs`*

### 3.1 Smoothed age curve

The by-age table $A_g^e = \{(a_j, \mathbf{b}_j^{\mathrm{age}})\}$ provides level
boundaries as a function of age. Let $m_j = b_{j3}^{\mathrm{age}}$ be the Intermediate
(50th-percentile) boundary at age $a_j$. Define the smoothed age-median curve
$\hat{m}: \mathcal{A} \to \mathcal{L}$ as the PCHIP spline fit to $\{(a_j, m_j)\}$.

The peak age is found by grid search at 0.1-year resolution:

$$a^* = \arg\max_{a\,\in\,\{a_{\min},\;a_{\min}+0.1,\;\ldots,\;a_{\max}\}}\hat{m}(a)$$

### 3.2 Age coefficient and adjusted 1RM

The multiplicative age coefficient at user age $a$ is

$$c(a) = \frac{\hat{m}(a^*)}{\hat{m}(a)} \geq 1$$

with $c(a^*) = 1$. The coefficient exceeds 1 at all ages above or below peak,
reflecting the performance advantage of a peak-age lifter. The age-adjusted 1RM is

$$\tilde{\ell} = c(a)\cdot\ell$$

$\tilde{\ell}$ replaces $\ell$ in all downstream percentile computations (Sections 1, 2, 6).

### 3.3 Multiplicative-assumption diagnostic

The age adjustment implicitly assumes the age effect scales all five boundary levels
by the same factor. At user age $a$, define the level-wise ratio relative to peak:

$$r_k(a) = \frac{b_k^{\mathrm{age}}(a^*)}{b_k^{\mathrm{age}}(a)}, \quad k\in\mathcal{K}$$

where $b_k^{\mathrm{age}}(a)$ is the raw (un-smoothed) boundary at age $a$. The
assumption is validated when $r_k(a) \approx c(a)$ for all $k$. The degree of
agreement is measured by the coefficient of variation:

$$\mathrm{CV}(a) = \frac{\operatorname{SD}\bigl(r_1(a),\ldots,r_5(a)\bigr)}{\operatorname{mean}\bigl(r_1(a),\ldots,r_5(a)\bigr)}$$

A threshold of $\mathrm{CV}(a) < 0.01$ flags the assumption as empirically validated.
**Empirical result (deadlift):** $\mathrm{CV}(a) < 0.003$ at all tested ages — the
multiplicative assumption holds to within 0.3% across all levels.

---

## Section 4 — Anthropometrics and FFMI

*`Core/Math/Anthropometrics.cs`, `data/anthropometrics.json`*

All body-composition quantities use metric units (kg, metres). Unit conversion is
applied before this section.

### 4.1 Body composition identities

Let $w \in \mathcal{W}$ (kg), $f \in (0,1)$ be body-fat fraction, $h \in \mathbb{R}_{>0}$
be height (m). The lean body mass and Fat-Free Mass Index are

$$\mathrm{LBM} = w(1 - f)$$

$$\mathrm{FFMI} = \frac{\mathrm{LBM}}{h^2} = \frac{w(1-f)}{h^2}$$

The height-normalized FFMI of Butt & Casey (2001) corrects for the lower FFMI that
taller individuals exhibit at equal absolute lean mass:

$$\widetilde{\mathrm{FFMI}} = \mathrm{FFMI} + 6.1(1.8 - h)$$

The inverse identities (used for back-solving in the GUI "Solve for" toggle) are

$$w = \frac{\mathrm{FFMI}\cdot h^2}{1 - f} \qquad \text{(solve for bodyweight given FFMI, } h, f\text{)}$$

$$f = 1 - \frac{\mathrm{FFMI}\cdot h^2}{w} \qquad \text{(solve for body-fat given FFMI, } h, w\text{)}$$

### 4.2 Height percentile

Height within each sex is modeled as normally distributed. Population parameters
(US CDC NHANES, adults aged 20–49):

| Sex $g$ | $\mu^h_g$ (cm) | $\sigma^h_g$ (cm) |
|---------|---------------|-------------------|
| Male $M$ | 175.3 | 7.4 |
| Female $F$ | 161.8 | 7.1 |

For a user of sex $g$ and height $h$ (cm):

$$p_h = 100\cdot\Phi\!\left(\frac{h - \mu^h_g}{\sigma^h_g}\right)$$

### 4.3 FFMI percentile

Raw (un-normalized) FFMI within each sex is modeled as normally distributed.
Population parameters from Kyle et al. (2003) — Caucasian adults, DXA-measured:

| Sex $g$ | $\mu^F_g$ (kg m$^{-2}$) | $\sigma^F_g$ (kg m$^{-2}$) |
|---------|------------------------|---------------------------|
| Male $M$ | 20.0 | 1.9 |
| Female $F$ | 15.4 | 1.6 |

For a user of sex $g$ with $\mathrm{FFMI} = F$:

$$p_F = 100\cdot\Phi\!\left(\frac{F - \mu^F_g}{\sigma^F_g}\right)$$

**Reference:** Kyle, U.G. et al. (2003). Fat-free mass index and fat mass index
percentiles in Caucasians aged 18–98 y. *International Journal of Obesity*, 27, 953–963.

---

## Section 5 — Cross-Sex Equivalency

*`Core/Math/Anthropometrics.cs`, `Core/Services/EquivalencyCalculator.cs`*

Given a user $(g,\, w,\, h,\, f,\, \tilde{\ell}) \in \mathcal{G}\times\mathcal{W}\times\mathbb{R}_{>0}\times(0,1)\times\mathcal{L}$,
we construct an *equivalent lifter* of sex $g' \neq g$ by preserving: (1) height
percentile rank, then (2) FFMI percentile rank within each sex's own distribution
(Method A, default) or normalized FFMI as a fraction of the drug-free ceiling (Method B,
research comparison).

### 5.1 Height equating

The user's height z-score within their sex's distribution is

$$z_h = \frac{h - \mu^h_g}{\sigma^h_g}$$

The equivalent lifter's height is defined as the value achieving the same z-score in
sex $g'$:

$$h_{g'} = \mu^h_{g'} + z_h\,\sigma^h_{g'}$$

This preserves the user's height percentile rank across sexes — the equivalent lifter
occupies the same position in their own sex's height distribution.

### 5.2 Method A — Equipercentile FFMI equating (default)

The user's FFMI z-score within their sex's FFMI distribution is

$$z_F = \frac{F_g - \mu^F_g}{\sigma^F_g}, \quad F_g = \mathrm{FFMI}$$

The equivalent FFMI is defined analogously to §5.1:

$$F_{g'} = \mu^F_{g'} + z_F\,\sigma^F_{g'}$$

and the height-normalized equivalent is $\widetilde{F}_{g'} = F_{g'} + 6.1(1.8 - h_{g'})$.

This is **equipercentile equating** (Kolen & Brennan, 2004) of the FFMI: the
equivalent lifter occupies the same percentile rank in their own sex's FFMI distribution
as the user does in theirs. It is statistically consistent with the height mapping in
§5.1 — both equate by population percentile rank within the sex-specific marginal
distribution.

**Research note.** The divergence between Method A and Method B (§5.3) is itself a
quantity of interest: it quantifies how much "fraction of the natural ceiling" departs
from "population percentile rank." A user near the drug-free natural ceiling occupies
an extreme upper percentile; Method B over-estimates the equivalent opposite-sex lean
body composition relative to what the actual population looks like. This discrepancy
directly captures the gap between perceived standards and the real distribution —
relevant to the study of body dysmorphia and cross-sex progression in exercise science.

**Reference:** Kolen, M.J. & Brennan, R.L. (2004). *Test Equating, Scaling, and
Linking*. Springer.

### 5.3 Method B — Ceiling-ratio FFMI scaling (research comparison)

Let $C_g$ denote the drug-free natural FFMI ceiling for sex $g$:

| Sex $g$ | $C_g$ (kg m$^{-2}$) | Source |
|---------|-------------------|--------|
| Male $M$ | 25.0 | Kouri et al. (1995) |
| Female $F$ | 22.0 | adapted estimate |

The equivalent normalized FFMI is defined by preserving the user's fraction of their
own sex's ceiling:

$$\widetilde{F}_{g'} = \widetilde{F}_g\cdot\frac{C_{g'}}{C_g}$$

This method is statistically ad hoc (not equivalent to any distributional equating
principle) and inconsistent with the percentile-preserving height mapping. It is
retained for research comparison only.

Selected via `FfmiEquivMethod.CeilingRatio` on `EquivalencyCalculator`.

### 5.4 Reverse bodyweight

Given the equivalent lifter's height $h_{g'}$, normalized FFMI $\widetilde{F}_{g'}$,
and assumed body-fat fraction $f_{g'}$, the equivalent bodyweight follows from §4.1:

$$F_{g'} = \widetilde{F}_{g'} - 6.1(1.8 - h_{g'})$$

$$w_{g'} = \frac{F_{g'}\cdot h_{g'}^2}{1 - f_{g'}}$$

Default assumed body-fat fractions (configurable in `data/anthropometrics.json`):

| Equivalent sex $g'$ | Default $f_{g'}$ |
|---------------------|-----------------|
| Female (of a male user) | 0.22 |
| Male (of a female user) | 0.13 |

Note that $w_{g'}$ is an affine function of $(1-f_{g'})^{-1}$; the GUI exposes
$f_{g'}$ as a live-editable field so users can directly observe the sensitivity.

---

## Section 6 — Equivalency Outputs

*`Core/Services/EquivalencyCalculator.cs`, `Core/Math/PercentileModel.cs`*

The equivalent lifter is fully specified by the tuple $(g', w_{g'}, h_{g'})$ produced
in Section 5. Two distinct equivalency quantities are computed.

### 6.1 Absolute equivalent percentile

Let $\phi_{g'}(\,\cdot\,;\, w_{g'})$ denote the PCHIP spline (§1.2–§1.3) for sex
$g'$ evaluated at the equivalent bodyweight $w_{g'}$. The user's own lift $\tilde{\ell}$
achieves the percentile

$$p_{\mathrm{abs}} = 100\cdot\Phi\!\bigl(\phi_{g'}(\tilde{\ell};\, w_{g'})\bigr)$$

for the equivalent lifter. This answers: *what percentile does the user's absolute
lift represent for an otherwise-equivalent lifter of the other sex?*

### 6.2 Percentile-equivalent lift

The user's percentile and z-score in their own distribution are

$$p_u = 100\cdot\Phi\!\bigl(\phi_g(\tilde{\ell};\, w_g)\bigr), \qquad z_u = \Phi^{-1}(p_u/100)$$

The lift the equivalent lifter must achieve to match the user's population rank is

$$\ell_{g'} = \phi_{g'}^{-1}(z_u;\, w_{g'})$$

computed via the inverse of §1.4. This answers: *what lift is required for an
equivalent lifter to stand at the same percentile in their own population?*

---

## Section 7 — Verification

Reference case:

| Field | Value |
|-------|-------|
| Sex | Male |
| Age | 19 yr |
| Bodyweight | 175 lb |
| Exercise | Deadlift |
| Lift | 405 lb $\times$ 1 rep |
| Height | 6 ft 1 in (185.4 cm) |
| Body-fat | 13% |

Expected outputs:

| Output | Expected |
|--------|---------|
| Estimated 1RM | 405 lb (identity, $r=1$) |
| Level | Advanced |
| Stars | ★★★★ |
| Percentile | $\approx$ 81–82% |
| BW multiple | 2.31× |
| FFMI | 20.09 kg m $^{-2}$ (91st percentile in male FFMI distribution) |
| Equiv female height (Method A) | $\approx$ 5 ft 8 in (173 cm), 91st percentile |
| Equiv female bodyweight (Method A) | $\approx$ 129 lb |
| Equiv female bodyweight (Method B) | $\approx$ 140 lb |

Run `dotnet run --project src/StandardsCalculator.Cli -- --verify-live` to cross-check
against the live site.

Run `dotnet run --project src/StandardsCalculator.Cli -- --diagnostics deadlift` to
print the full LMS centile surface (per-row $L^*$, $M$, $S^*$, RMSE) and the age
multiplicative diagnostic at a range of test ages.
