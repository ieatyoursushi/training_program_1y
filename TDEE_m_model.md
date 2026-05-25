# Mechanistic TDEE Derivation Draft
*Deadlift-Priority Trainee | Loaded Commute Context |  Recreational Intermediate*

---

## Overview

Standard TDEE calculators apply a single activity multiplier to BMR. This approach fails in contexts with high structural specificity: loaded biking and walking alongside resistance training, fatigue-compounded cardio costs, and asymmetric intra- vs. post-session caloric demands of different training modalities. This document derives TDEE from mechanistic first principles, component by component, and validates against the empirical Winter '25 macro log.

**Core asymmetry to internalize:**

> Aerobic exercise burns most of its calories *during* the activity (direct substrate oxidation).  
> Resistance training burns relatively fewer calories *during* the session, but significantly more *afterward* (EPOC, glycogen resynthesis, elevated protein turnover, connective tissue repair).

The commute is non-negotiable and stacks on top of both.

---

## Vector Space: $\mathbb{R^{12}}$

| Symbol | Type | Meaning |
|---|---|---|
| $\text{TDEE}$ | $\mathbb{R^+}$, kcal/day | Total daily energy expenditure |
| $B$ | $\mathbb{R^+}$, kcal/day | Basal metabolic rate |
| $\text{TEF}$ | $\mathbb{R^+}$, kcal/day | Thermic effect of food |
| $E_{\text{RT}}$ | $\mathbb{R^+}$, kcal/day (averaged) | Resistance training expenditure |
| $E_{\text{bike}}$ | $\mathbb{R^+}$, kcal/day | Biking commute expenditure |
| $E_{\text{walk}}$ | $\mathbb{R^+}$, kcal/day | Walking/step expenditure |
| $E_{\text{NEAT}}$ | $\mathbb{R^+}$, kcal/day | Incidental NEAT (fidgeting, restlessness) |
| $\Delta E_{\text{interact}}$ | $\mathbb{R^+}$, kcal/day | Interaction term: fatigue-compounded cardio cost |
| $m_b$ | $\mathbb{R^+}$, kg | Body mass (80 kg) |
| $m_L$ | $\mathbb{R^+}$, kg | External load / backpack (13.6 kg ≈ 30 lb) |
| $\eta$ | $\mathbb{R}$, dimensionless ∈ (0,1) | Muscle mechanical efficiency |
| $W_{\text{mech}}$ | $\mathbb{R^+}$, J or kcal | Mechanical work output (locomotion) |

---

## Component 1 — Basal Metabolic Rate (B)

### Prediction vs. Empirical

Mifflin-St Jeor (male):

$$B_{\text{MSJ}} = 10 m_b + 6.25 h - 5a + 5$$

With $m_b = 80$ kg, $h = 185$ cm, $a \approx 20$:

$$B_{\text{MSJ}} = 800 + 1156.25 - 100 + 5 = \mathbf{1861} \text{ kcal/day}$$

The bioelectrical impedance estimate (~1950–2000 kcal) consistently runs ~90–140 kcal above MSJ. This is consistent with:
- Above-average lean mass fraction at this height/weight
- Pre-existing posterior chain development (elevated resting muscle mass → elevated resting ATP turnover)
- Self-reported lifetime high-metabolism phenotype (likely elevated uncoupling protein expression or sympathetic tone)

**Use empirical value:** $B = 1975$ kcal/day.

---

## Component 2 — Thermic Effect of Food (TEF)

TEF is the metabolic cost of digesting, absorbing, and storing macronutrients. It is not a flat constant — it varies by macronutrient:

| Macronutrient | TEF % of kcal |
|---|---|
| Protein | 20–30% |
| Carbohydrate | 5–10% |
| Fat | 0–3% |

From the Winter '25 log: mean intake ≈ 4240 kcal/day with macro distribution ~22% protein, ~47% carb, ~14% fat (by kcal).

$$\text{TEF} = 0.25 \times (0.22 \times 4240) + 0.075 \times (0.47 \times 4240) + 0.015 \times (0.14 \times 4240)$$

$$= 0.25 \times 933 + 0.075 \times 1993 + 0.015 \times 594 \approx 233 + 149 + 9 = \mathbf{391} \text{ kcal/day}$$

Note: TEF is partially circular — it depends on the intake level, which is itself determined by TDEE. The fixed-point is resolved by empirical validation at the end.

**Estimate:** $\text{TEF} \approx 385$–$410$ kcal/day.

---

## Component 3 — Resistance Training Expenditure ($E_{\text{RT}}$)

This is the most structurally complex component because it has four subterms with different temporal profiles.

### 3a — Intra-Session Direct Expenditure

The direct caloric cost of lifting is estimated via MET values. Heavy compound resistance training MET ≈ 5.0–6.5 (varies enormously by rest period density and intensity).

$$E_{\text{session}} = \text{MET} \times m_b \times t_{\text{session}}$$

| Session type | MET | Duration (hr) | $E_{\text{session}}$ (kcal) |
|---|---|---|---|
| Heavy DL day (Day 1) | 5.8 | 1.25 | ~580 |
| Bench/Shoulder day (Day 2) | 4.5 | 1.0 | ~360 |
| Leg day — isolation dominant (Day 3) | 4.0 | 0.9 | ~288 |
| Accessory/secondary day (Day 4) | 4.2 | 0.9 | ~302 |

4-session total: ~1530 kcal intra-session per microcycle.

### 3b — EPOC (Excess Post-Exercise Oxygen Consumption)

EPOC represents elevated oxygen consumption (and thus caloric expenditure) above resting baseline in the hours-to-days following a session. It is not a uniform decay — it has a fast component (1–2 hr, ~70% of magnitude) and a slow component (up to 24–48 hr for heavy sessions).

For resistance training, EPOC magnitude scales with session intensity and volume:

$$E_{\text{EPOC}} \approx k_{\text{session}} \cdot E_{\text{session}}$$

where $k_{\text{session}}$ ≈ 0.15–0.35 for resistance training (higher for heavy compound, lower for machine isolation).

| Session | $k$ | $E_{\text{EPOC}}$ (kcal) |
|---|---|---|
| Heavy DL day | 0.30 | ~174 |
| Bench/Shoulder | 0.15 | ~54 |
| Leg day | 0.12 | ~35 |
| Accessory day | 0.12 | ~36 |

4-session EPOC total: ~299 kcal per microcycle.

### 3c — Elevated Protein Turnover and MPS Cost

The biochemical cost of peptide bond formation is approximately 4 ATP per bond (aminoacyl-tRNA loading + ribosomal translocation). For protein (~110 Da/residue average), synthesizing 1 g of protein costs:

$$\text{ATP cost} \approx \frac{4 \text{ ATP/bond} \times (1000/110) \text{ bonds/g}}{0.159 \text{ mol ATP/kcal}} \approx 0.23 \text{ kcal per gram of net synthesis}$$

But *whole-body protein turnover* (not just net accretion) also requires energy — degradation and re-synthesis of proteins that don't result in net gain. During the 24–48 hr post-training hypertrophy window, whole-body protein synthesis elevates ~30–50% above baseline.

Rough baseline whole-body protein synthesis: ~200–250 g/day at rest. Elevated post-training: +60–100 g/day above baseline over the 24–48 hr window.

Additional caloric cost of elevated turnover:
$$\Delta E_{\text{MPS}} \approx 80 \text{ g} \times 0.23 \text{ kcal/g} \approx 18 \text{ kcal}$$

This is biochemically small in isolation. However, the *total futile cycle cost* (synthesis + partial degradation + remodeling) in skeletal muscle is empirically estimated at 50–150 kcal per hard session per 24–48 hr window, which is larger than the bare ATP calculation because it includes:
- Heat dissipation from futile cycling
- Ion pump restoration (Na⁺/K⁺ ATPase, Ca²⁺ ATPase) after high motor unit recruitment
- Neurotransmitter/neurochemical replenishment (dopamine, norepinephrine resynthesis post-heavy CNS loading)

**Conservative estimate:** ~80–120 kcal per hard session elevated cost. Per microcycle (2 hard sessions, 2 light): ~220 kcal.

### 3d — Glycogen Resynthesis

A heavy training session depletes 75–150 g of muscle glycogen (predominantly type II fiber glycogen for compound movements).

The thermodynamic efficiency of glycogen synthesis from glucose is ~0.80 (the Cori cycle and phosphorylation steps dissipate ~20% as heat):

$$E_{\text{glycogen}} = \Delta G_{\text{depleted}} \times (1 - \eta_{\text{synthesis}}) = [g_{\text{depleted}} \times 4 \text{ kcal/g}] \times 0.20$$

For 100 g depleted per hard session:
$$E_{\text{glycogen}} = 100 \times 4 \times 0.20 = 80 \text{ kcal per hard session}$$

Per microcycle (2 hard sessions + 1 moderate): ~200 kcal.

### RT Summary — Per-Day Average

Microcycle duration: ~8–10 days. Total RT cost per cycle:

$$E_{\text{RT, cycle}} = \underbrace{1530}_{\text{intra-session}} + \underbrace{299}_{\text{EPOC}} + \underbrace{220}_{\text{MPS/turnover}} + \underbrace{200}_{\text{glycogen}} = 2249 \text{ kcal}$$

Per-day average (÷ 9 days):

$$\boxed{E_{\text{RT}} \approx 250 \text{ kcal/day}}$$

This is lower than the naive "4 sessions × 600 kcal" estimate because it's amortized over rest days and the daily-average framing captures the post-session recovery cost distribution correctly.

---

## Component 4 — Loaded Commute Cardio

### Theoretical Framework

For any cardio modality, metabolic expenditure is:

$$E_{\text{cardio}} = \frac{W_{\text{mech}}}{\eta}$$

where $W_{\text{mech}}$ is the mechanical work actually performed (locomotion against gravity, air resistance, accelerative forces) and $\eta$ is the gross mechanical efficiency of the locomotor system.

For walking: $\eta \approx 0.25$  
For cycling: $\eta \approx 0.20$–$0.25$ (depends on gear/cadence efficiency)

External load changes $W_{\text{mech}}$ differently for walking vs. cycling:
- **Walking**: load is fully body-borne; you lift and accelerate the full mass every step. Cost scales approximately with $(m_b + m_L)$ proportionally.
- **Cycling**: load adds to rolling resistance and hill climbing cost but not to the pendular cost of leg swing the same way. The load effect is lower (~50–60% of the proportional walking effect on flat terrain).

### 4a — Biking (4–6 mi/day, Zone 2–3, Loaded)

MET-based estimate for zone 2–3 cycling (~12–15 mph): MET ≈ 8.

Without load:
$$E_{\text{bike, unloaded}} = 8 \times 80 \text{ kg} \times \frac{5 \text{ mi}}{12 \text{ mph}} = 8 \times 80 \times 0.417 \approx 267 \text{ kcal}$$

Load adjustment (cycling): carrying $m_L = 13.6$ kg increases effective mass for rolling resistance and hill work. On flat terrain with occasional grades, approximate scaling factor:

$$k_{\text{load, bike}} \approx 1 + 0.10 \cdot \frac{m_L}{m_b} = 1 + 0.10 \times 0.17 = 1.017$$

This is deliberately conservative for flat to mildly hilly terrain. For significant grades it would scale higher.

$$E_{\text{bike}} \approx 267 \times 1.017 \approx 272 \text{ kcal for 5 mi mean}$$

**Range for 4–6 mi:** 218–326 kcal/day.  
**Central estimate:** $E_{\text{bike}} \approx \mathbf{265}$ kcal/day.

### 4b — Walking/Steps (10–15k steps/day, Loaded)

Step count to distance:
$$d_{\text{walk}} = n_{\text{steps}} \times l_{\text{stride}} \approx n_{\text{steps}} \times 0.75 \text{ m}$$

At 12,500 steps (midpoint): $d \approx 9.375$ km = 5.83 mi.

For loaded walking, the Pandolf (1977) equation is the mechanistic standard:

$$\dot{M} = 1.5 m_b + 2.0(m_b + m_L)\left(\frac{m_L}{m_b}\right)^2 + \eta_t (m_b + m_L)\left(1.5V^2 + 0.35VG\right)$$

where $\dot{M}$ is in Watts, $V$ is velocity (m/s), $G$ is grade (%), and $\eta_t$ is terrain factor (1.0 = flat hard surface).

For flat walking at $V \approx 1.4$ m/s (normal walking pace), $G = 0$:

$$\dot{M} = 1.5(80) + 2.0(93.6)\left(\frac{13.6}{80}\right)^2 + 1.0(93.6)(1.5 \times 1.4^2)$$

$$= 120 + 187.2 \times 0.0289 + 93.6 \times 2.94$$

$$= 120 + 5.41 + 275.2 = 400.6 \text{ W}$$

Converting to kcal/hr: $400.6 \times 0.860 = 344.5$ kcal/hr.

Duration for 5.83 mi at 1.4 m/s:
$$t = \frac{5.83 \text{ mi} \times 1609 \text{ m/mi}}{1.4 \text{ m/s}} \approx 6692 \text{ s} = 1.858 \text{ hr}$$

$$E_{\text{walk}} = 344.5 \times 1.858 \approx 640 \text{ kcal}$$

Cross-check with simpler formula ($0.53 \times \text{BW}_{\text{lbs+load}} \times \text{miles}$):
$$E = 0.53 \times (176+30) \times 5.83 = 0.53 \times 206 \times 5.83 \approx 636 \text{ kcal} \checkmark$$

**Range for 10–15k steps:** 510–770 kcal/day.  
**Central estimate:** $E_{\text{walk}} \approx \mathbf{635}$ kcal/day.

steps per calorie ratio: ~19.5 under the assumption of no major inefficiency/breakdown in fatigued steps (like outliered 25k+ steps)

---

## Component 5 — NEAT (Incidental, Non-Locomotion)

NEAT here excludes the structured biking and walking (already counted) and captures:
- continuous low-frequency muscle activation)
- same thing but during sleep (repeated postural adjustment, small but sustained)
- Standing vs. sitting micro-adjustments throughout the day

Fidgeting alone has been measured to add 300–350 kcal/day in high-fidget individuals vs. near-zero in sedentary ones (Levine et al., 1999). For a restless sleeper with active fidgeting:

Leg bouncing: ~20–30W sustained during study hours → at 4 hr/day study: $25 \times 4 \times 0.86 \approx 86$ kcal  
Postural adjustments (sitting/standing micromotion): ~50–80 kcal  
Sleep movement: low power but sustained (~20 W mean for active sleeper vs. ~10 W baseline) → $10 \times 7 \text{ hr} \times 0.86 \approx 60$ kcal

**Estimate:** $E_{\text{NEAT}} \approx \mathbf{200}$–$\mathbf{275}$ kcal/day (central: $235$ kcal/day).

---

## Component 6 — Interaction Term: Fatigue-Compounded Cardio Cost ($\Delta E_{\text{interact}}$)

This is the most mechanistically interesting component and the one most systematically ignored by activity multiplier models.

### Mechanism

Muscle mechanical efficiency is defined as:

$$\eta = \frac{W_{\text{mech}}}{E_{\text{metabolic}}}$$

Fresh skeletal muscle: $\eta \approx 0.25$ for walking, $\eta \approx 0.22$–$0.25$ for cycling.

Under fatigue (post-resistance training session, especially leg day or heavy DL day), several mechanisms reduce $\eta$:

1. **Cross-bridge kinetics degradation**: fatigued myosin heads have reduced ATP hydrolysis coupling efficiency — more ATP is consumed per unit of force (or work) produced. Pi accumulation inhibits the power stroke rate without proportionally reducing ATP hydrolysis.

2. **Increased motor unit recruitment**: to produce the same net force, more motor units (including less efficient type II fibers, which have lower efficiency due to faster cross-bridge cycling) must be recruited to compensate for fatigue in already-recruited units. The force per ATP for fast-twitch fibers is lower than slow-twitch.

3. **Substrate shift**: acute post-exercise glycolytic flux remains elevated. Fat oxidation (β-oxidation) is down-regulated by elevated lactate/acidosis; increased reliance on glycolysis produces less ATP per molecule of substrate consumed per unit time, and the substrate cycling (Cori cycle, lactate-glucose futile cycling) incurs additional caloric cost.

4. **Thermoregulatory cost**: core temperature post-RT session remains elevated (~0.5–1.0°C) for 2–4 hr. Every 1°C elevation in core temperature increases BMR ~10–13%, which adds to commute cost if commuting is post-session.

Net result: under moderate leg/back fatigue, $\eta$ drops to approximately 0.20–0.22.

### Quantification

Let $\Delta\eta = \eta_{\text{fresh}} - \eta_{\text{fatigued}}$ and $E_0$ = baseline commute cost (unimpaired).

Additional metabolic cost due to impaired efficiency:

$$\Delta E_{\text{interact}} = E_0 \left(\frac{\eta_{\text{fresh}}}{\eta_{\text{fatigued}}} - 1\right)$$

With $\eta_{\text{fresh}} = 0.25$, $\eta_{\text{fatigued}} = 0.21$ (conservative, only moderate fatigue):

$$\Delta E_{\text{interact}} = 900 \times \left(\frac{0.25}{0.21} - 1\right) = 900 \times 0.190 \approx 171 \text{ kcal on a fatigued day}$$

This applies selectively to training days (or the day following high-fatigue sessions). For the 4-day split over ~9-day microcycle, approximately 4 "elevated-cost" commute days:

$$\Delta E_{\text{interact, per day avg}} = \frac{4 \times 171}{9} \approx \mathbf{76} \text{ kcal/day}$$

Note: on leg day specifically, this is the worst case — erectors, hamstrings, and glutes are compromised and carry both the biking load and the walking load directly. Week 1 training log confirms this: "significant doms, fatigue most present in lower/mid back, hamstrings" while the commute demands remained constant. The interaction cost is real, not theoretical.

---

## Component 7 — TDEE Synthesis

$$\boxed{\text{TDEE} = B + \text{TEF} + E_{\text{RT}} + E_{\text{bike}} + E_{\text{walk}} + E_{\text{NEAT}} + \Delta E_{\text{interact}}}$$

| Component | Central Estimate (kcal/day) | Range |
|---|---|---|
| BMR $(B)$ | 1,975 | 1,950–2,000 |
| TEF | 395 | 370–420 |
| RT expenditure $(E_{\text{RT}})$ | 250 | 200–320 |
| Biking $(E_{\text{bike}})$ | 265 | 218–326 |
| Walking/steps $(E_{\text{walk}})$ | 635 | 510–770 |
| NEAT $(E_{\text{NEAT}})$ | 235 | 200–275 |
| Fatigue-interaction $(\Delta E_{\text{interact}})$ | 76 | 40–130 |
| **TDEE (central)** | **3,831** | **3,488–4,241** |

---

## Empirical Validation — Winter '25 Macro Log

From the macro log (n=66 days, Jan 9 – Mar 20, 2026):

- Mean caloric intake: 4,240 kcal/day
- Body weight change: 170.8 → 175.9 lbs = +5.1 lbs over 66 days

**Implied caloric surplus:**

Assuming lean bulk composition (~55% lean mass, 45% fat):
$$\text{kcal/lb gained} \approx 0.55 \times 700 + 0.45 \times 3500 = 385 + 1575 = 1960 \text{ kcal/lb}$$

(700 kcal/lb for lean tissue accounting for glycogen/water co-accretion; 3500 for fat)

$$\text{Surplus} = \frac{5.1 \text{ lb} \times 1960 \text{ kcal/lb}}{66 \text{ days}} \approx 151 \text{ kcal/day}$$

$$\text{Implied TDEE} = 4240 - 151 = \mathbf{4089} \text{ kcal/day}$$

**Mechanistic estimate vs. empirical:** 3,831 vs. 4,089 kcal/day → discrepancy of ~258 kcal/day (~6%).

### Sources of Residual Discrepancy

This 6% gap is within normal model error but worth decomposing:

1. **Logging underestimation**: restaurant/dining hall logging consistently underestimates fat content. A systematic 3–5% undercount on fat (a common finding in food diary studies) accounts for ~50–80 kcal/day.

2. **Walking undercount**: the step estimate used stride = 0.75m, but loaded walking with 30 lb pack may actually lengthen effective stride slightly; also the backpack load while biking was treated conservatively. A 10% upward revision to $E_{\text{walk}}$ adds ~64 kcal.

3. **NEAT underestimate**: the 235 kcal/day figure is conservative. Reported high restlessness in bed + constant fidgeting during studying could plausibly reach 300+ kcal/day for the upper end.

4. **Elevated protein turnover in bulking state**: net MPS during active bulking at this rate is higher than the conservative ~80–120 kcal/session estimate; the futile cycling component of rapid lean mass accretion adds additional overhead.

Adjusted for these factors, the mechanistic range upper bound (~4,241 kcal/day) overlaps with the empirically implied TDEE of ~4,089 kcal/day. The model is **validated within the range of normal measurement uncertainty**.

---

## Key Structural Observations

### The Cardio Asymmetry

Resistance training and aerobic work have inverted temporal caloric profiles:

```
Aerobic (biking/walking):
  During:    ████████████████████████  ~85% of total cost
  After:     ██                         ~15% (minor EPOC)

Resistance training:
  During:    ██████████                 ~45% of total cost  
  After:     ████████████               ~55% (EPOC + MPS + resynthesis)
```

This means caloric intake timing matters differently for each. Eating post-RT to fuel recovery is more critical than post-cardio, because the caloric "demand" from RT is more distributed into the 24–48 hr post-session window. The current practice of maintaining consistent ~4200+ kcal/day rather than spiking intake only on training days reflects this correctly — you're funding recovery for yesterday's session while performing today's commute.

### Commute + Training Interaction

At current activity levels, the commute alone (~900 kcal/day base) represents:

$$\frac{E_{\text{bike}} + E_{\text{walk}}}{B} = \frac{900}{1975} \approx 45.6\%$$

Nearly half of basal metabolic rate again is consumed by non-optional loaded locomotion. This is a structural factor that cannot be optimized away during the school year. It means:

- Any period of illness, injury, or forced rest has *less* TDEE reduction than expected — the body is not primarily running on gym volume, it's running on commute volume. (This may partially explain why performance recovered faster than expected post-illness if eating was maintained.)
- The interaction term becomes significant specifically on leg/DL day — the ~171 kcal additional cost on those days is real and represents why those sessions produce disproportionate fatigue that extends into the following days.

### TDEE vs. Training Age Trajectory

As bodyweight increases toward 190–200 lbs (Year 2 target):

$$\Delta B \approx \frac{dB}{dm_b} \cdot \Delta m = \frac{10 \text{ kcal/kg}}{1} \times 8 \text{ kg} = +80 \text{ kcal/day BMR}$$

$$\Delta E_{\text{walk}} \approx \frac{\partial E}{\partial m_b} \cdot \Delta m = \frac{0.53 \times \Delta m_{\text{lbs}} \times d_{\text{walk}}}{1} \approx 0.53 \times 17.6 \times 5.83 \approx +54 \text{ kcal/day}$$

Projected TDEE at 190 lbs ≈ **4,200–4,400 kcal/day** holding commute constant and assuming similar training intensity. This is approximately in line with a caloric target of **4,500–4,700 kcal/day** for a lean bulk rate of ~0.5 lb/week at the higher body weight.

---

## Recommended Target Intake

| Goal | Target (kcal/day) |
|---|---|
| Lean bulk (current, ~176 lbs) | 4,200–4,400 |
| Lean bulk (Year 2, ~190 lbs) | 4,500–4,700 |
| Maintenance / deload | 3,700–3,900 |
| Full rest block (no training, commute ongoing) | 3,600–3,750 |
| Full rest block (no training, no commute) | 2,900–3,100 |

The drop during full rest blocks should be primarily in carbohydrates (reduced glycogen demand + lower insulin-mediated anabolism), not protein (MPS ongoing).

---

## Summary Formula

$$\text{TDEE} \approx \underbrace{1975}_{B} + \underbrace{395}_{\text{TEF}} + \underbrace{250}_{E_{\text{RT}}} + \underbrace{265}_{E_{\text{bike}}} + \underbrace{635}_{E_{\text{walk}}} + \underbrace{235}_{E_{\text{NEAT}}} + \underbrace{76}_{\Delta E_{\text{interact}}} \approx \mathbf{3,831 \text{ kcal/day}}$$

Empirically implied from macro log: **~4,089 kcal/day** (6% above mechanistic central estimate; consistent with model uncertainty and measurement bias in food logging).

**Operational estimate: 3,900–4,100 kcal/day maintenance. +150–250 kcal/day surplus for lean bulk = 4,050–4,350 kcal/day target.**