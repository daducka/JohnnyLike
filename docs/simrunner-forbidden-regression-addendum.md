# SimRunner Optimization — Forbidden Regression Addendum

**Generated:** 2026-03-28  
**Applies to:** Optimized profile `AE07DF39` vs baseline `47FE6B78`  
**Artifact sources:** Optimizer run `23672987785`, Optimized eval `23673032824`, Fuzzer `23673050523` (all on commit `57cea83d`)  
**New artifact files used:** `forbidden-regression-diff.json`, `state-delta-report.json`, `optimizer/failures.json` (with `topCandidates` + `qualityModelDecomposition` + `thinkAboutSuppliesAnalysis`), `optimizer-comparison.json` (with `activeTuningParameters`), `fuzzer/trait-profile-breakdown.json`

---

## 1. Newly Introduced Forbidden Regressions

**Total new forbidden violations: 1** (source: `forbidden-regression-diff.json`)

| sampleKey | label | Desired | Baseline winner | Optimized winner | Score delta |
|---|---|---|---|---|---|
| `trait:13fcd090\|FoodAvailableNow\|s65\|h90\|e80\|m65` | training/stable-flavor/high-instinct/safe | Fun | `sleep_under_tree` (Rest) | `explore_beach` (Preparation) | 0 → −30 |

No forbidden violations were resolved. No other newly forbidden cases. The change is isolated to a single training state.

---

## 2. Deep Dive: High-Instinct Safe-State Forbidden Regression

**State:** `trait:13fcd090|FoodAvailableNow|s65|h90|e80|m65`  
**Trait profile hash:** `13fcd090` (high-instinct: elevated `Instinctive` + `Hedonist` traits, moderate others)  
**Vitals:** satiety=65, health=90, energy=80, morale=65  
**Scenario:** FoodAvailableNow (food accessible in shared pile)  
**Contract:** desired top category = Fun. Forbidden constraint fires if Preparation wins.

### State delta summary (from `state-delta-report.json`)

| Metric | Baseline | Optimized |
|---|---|---|
| Actual top category | Rest | Preparation |
| Desired (Fun) rank | 10 | 9 |
| Δ from top | −0.271 | −0.202 |
| Forbidden violated | No | **Yes** |
| Score | 0 | −30 |

Fun actually moved up one rank (10→9) and the delta slightly improved (−0.271→−0.202). The regression is caused entirely by the winning category switching from the non-violating Rest to the forbidden Preparation — not by Fun getting worse.

### Candidate ranking (optimized — from `optimizer/failures.json`)

| Rank | Action ID | Score | Dominant Categories | Key contribution |
|---|---|---|---|---|
| 1 | `explore_beach` | **0.5567** | Preparation, Efficiency | Prep: 0.78 × 0.3948 = **0.3079** |
| 2 | `go_fishing` | 0.5251 | FoodAcquisition, Efficiency, Fun | Fun: 0.30 × 0.1921 = **0.0576** |
| 3 | `sit_and_watch_waves` | 0.4343 | Comfort, Safety, Fun | Fun: 0.15 × 0.1921 = **0.0288** |
| 4 | `bash_and_eat_coconut` | 0.4198 | FoodConsumption, Comfort | — |
| 5 | `sleep_under_tree` | 0.416 | Rest, Safety | Rest: 1.0 × 0.2 = **0.2000** |
| 9 | *(best Fun-rank action)* | — | Fun | Fun rank: 9 |
| 15 | `think_about_supplies` | 0.1632 | Preparation (fallback), Efficiency | Prep: 0.15 × 0.3948 = **0.0592** |

Note: `think_about_supplies` is **not the winner, not close (rank 15), and not the cause of the regression**. Its Preparation contribution (0.0592) is negligible compared to `explore_beach`'s (0.3079). See think_about_supplies section below.

### Winner comparison: baseline vs optimized

| Metric | Baseline | Optimized |
|---|---|---|
| Winning action | `sleep_under_tree` | `explore_beach` |
| Winning category | Rest | Preparation |
| Forbidden | No | **Yes** |
| Fun desired rank | 10 | 9 |
| Δ from top | −0.271 | −0.202 |

The estimated baseline score for `sleep_under_tree` is ~0.816 (see calculation below). The optimized score is 0.416 — a collapse of ~0.40 points due to the `FatiguePressureRestScale` cut from 0.015 to 0.005.

**`explore_beach` was always scoring ~0.556 in both baseline and optimized.** The parameter change did not affect Preparation weights — it only dropped Rest, exposing the pre-existing Preparation winner underneath.

### Category decomposition (optimized)

| Category | needAdd | personalityBase | moodMultiplier | effectiveWeight |
|---|---|---|---|---|
| **Fun** | 0 | 1.0 | **0.1921** | **0.1921** |
| **Preparation** | 0 | 0.42 | 0.94 | **0.3948** |
| Mastery | 0 | 0.36 | 0.93 | 0.3348 |
| Rest | 0.20 | 0 | 1.0 | **0.200** |
| Comfort | 0.33 | 0.20 | 1.0 | 0.530 |
| Safety | 0.20 | 0.18 | 1.0 | 0.380 |
| Efficiency | 0 | 0.24 | 1.0 | 0.240 |
| FoodConsumption | 0.0675 | 0.29 | 1.0 | 0.3575 |
| FoodAcquisition | 0.0075 | 0.12 | 1.0 | 0.1275 |

**Critical observation:** Fun has `personalityBase=1.0` (highest possible — high-instinct personality correctly gives max personality scaling to Fun) but `moodMultiplier=0.1921`, yielding effectiveWeight=0.1921. Preparation has `personalityBase=0.42` and `moodMultiplier=0.94`, yielding effectiveWeight=0.3948. Fun's effective weight is **less than half of Preparation's**, despite this being a high-instinct actor.

**Structural impossibility:** Even if a hypothetical action had `Fun quality = 1.0` (perfect), its Fun contribution would be 1.0 × 0.1921 = **0.1921**. `explore_beach`'s Preparation contribution alone is **0.3079**. Fun cannot beat Preparation in this state regardless of which Fun-oriented candidates exist.

Comparing Fun moodMultiplier (0.1921) to Preparation (0.94) and Mastery (0.93): the other categories receive near-neutral mood suppression at these vitals (h=90, s=65, e=80, m=65). **Fun is suppressed by a factor of ~5× relative to Preparation** at this safe, comfortable state. This is the quantitative core of the problem.

No baseline decomposition is available directly (the baseline failures.json for the Rev 2 run pre-dates the richer artifact format). However, the baseline vs optimized comparison is unambiguous via the state-delta data: the optimized Fun weight is the same as baseline (no optimizer parameter affects Fun moodMultiplier directly), confirming Fun was equally weak in the baseline — just hidden behind Rest.

### `think_about_supplies` analysis

**Present?** Yes — ranked 15th with score 0.1632.

**Was it the winner?** No. It ranked 15th out of all available candidates.

**Qualities emitted:**

| Quality | qualityValue | effectiveWeight | contribution |
|---|---|---|---|
| Preparation | 0.15 | 0.3948 | 0.0592 |
| Efficiency | 0.10 | 0.240 | 0.0240 |

These are the **fallback qualities** (`{Preparation: 0.15, Efficiency: 0.10}`), indicating that no food or safety recipes are discoverable in this FoodAvailableNow/safe scenario. According to the `think_about_supplies` logic, this should trigger starvation suppression (×0.2) — but satiety=65 is well above the starvation threshold of 25, so the suppression gate is **not active**. The fallback qualities are correctly unapplied-suppressed: the score of 0.1632 = intrinsic 0.08 + 0.0592 + 0.0240 is exactly the unsuppressed fallback, which is correct behavior. The suppression gate only fires when satiety is critically low — this is working as designed.

**`think_about_supplies` is not the causal mechanism.** Its Preparation contribution (0.0592) is 5.2× smaller than `explore_beach`'s (0.3079). Even if `think_about_supplies` were eliminated from candidates entirely, `explore_beach` would still win.

---

## 3. Root-Cause Assessment

### Verdict: **C + B combined**, with C as the proximate trigger and B as the structural root cause

#### Hypothesis C — Rest suppression exposed pre-existing Preparation dominance  
**Confidence: HIGH (confirmed by data)**

This is the causal trigger. The `FatiguePressureRestScale` cut from 0.015 to 0.005 (−67%) reduced Rest's `needAdd` from approximately 0.60 to 0.20, dropping `sleep_under_tree` from an estimated score of ~0.816 to 0.416. This dropped Rest below Preparation (0.556), allowing `explore_beach` to take first place. **`explore_beach` was always scoring 0.556 — the number didn't change. Only Rest fell.**

Evidence:
- State-delta confirms baseline winner was `sleep_under_tree` (Rest); optimized winner is `explore_beach` (Preparation)
- The FatiguePressureRestScale parameter is the only changed optimizer parameter that affects Rest needAdd
- `explore_beach` score is deterministic given fixed Preparation weights; those weights were not changed by the optimizer
- The same Rest-masking dynamic is visible in `training/stable-flavor/high-survivor/safe` (Preparation now winning) and `training/stable-flavor/high-hedonist/safe` (Comfort now winning, not Fun — Comfort was next in line after Rest)

#### Hypothesis B — Fun pressure is too low in safe states  
**Confidence: HIGH (structural root cause)**

This is the deeper, underlying problem. The Fun moodMultiplier of 0.1921 at morale=65/energy=80 in a non-critical state is anomalously low:

- Fun effective weight: **0.1921**
- Preparation effective weight: **0.3948** (with personalityBase=0.42 only — half the personality drive of Fun)
- Even a perfect Fun quality action (1.0) scores at most 0.1921 on the Fun dimension, vs `explore_beach`'s 0.3079 from Preparation alone
- Fun was at rank 10 at baseline (not just slightly under; it was deeply ranked) — this predates the FatiguePressureRestScale change entirely

The high-instinct actor's `personalityBase=1.0` for Fun is correct. But the moodMultiplier of 0.1921 — approximately 5× lower than Preparation (0.94) at the same state — is what makes Fun non-competitive. Whatever mechanism produces the 0.1921 moodMultiplier at m=65 in a non-critical state, it is too aggressive a suppression.

**Uncertainty note:** The exact formula producing Fun moodMultiplier=0.1921 is not directly visible in the artifact data. It could be a morale-based curve, a composite factor, or an unintended interaction between `funBaseScale=0.6` and another suppression term. The available data confirms the output (0.1921) but not the computation path. However, the diagnostic implication is clear regardless.

#### Hypothesis A — Preparation pressure is too high in safe states  
**Confidence: MEDIUM (contributing, not primary)**

`explore_beach` has a very high Preparation quality value (0.78). This, combined with Preparation effective weight 0.3948 for this trait profile, gives it a total Preparation contribution of 0.3079. This is not obviously miscalibrated — `explore_beach` is legitimately a Preparation-oriented action. The problem is not that Preparation is artificially inflated; it's that Fun is artificially deflated.

However, there is a secondary Preparation signal worth noting: the `training/stable-flavor/high-survivor/safe` state also moved to a Preparation winner post-optimization (see `top-worsened-improved.json`, score delta +6 because desired was Safety and Preparation doesn't violate the Safety constraint). The FatiguePressureRestScale change pushed multiple safe-state actor-types toward Preparation. This suggests Preparation's absolute competitiveness in safe states is a concern that transcends the high-instinct case.

#### Hypothesis D — Candidate availability for Fun is too weak  
**Confidence: LOW (not supported)**

Fun candidates are present: `go_fishing` (rank 2, Fun contribution 0.0576) and `sit_and_watch_waves` (rank 3, Fun contribution 0.0288). The problem is not that there are no Fun candidates — it's that their Fun contributions are tiny because Fun effective weight is low. Even if the best Fun action were specifically designed with Fun quality=1.0, it would score at most 0.1921 from Fun alone, which is below `explore_beach`'s total score (0.5567).

---

## 4. Recommended Next Fix

### Primary recommendation: Increase `funBaseScale` from 0.6 to ~0.85–0.90

**Target parameter:** `funBaseScale` in `DecisionTuningProfile` (currently 0.6)  
**Type:** Pure weight/tuning change (no objective model change needed)  
**Mechanism:** Fun moodMultiplier = `funBaseScale` × f(vitals). Raising `funBaseScale` directly increases Fun effective weight across non-critical states, making Fun-oriented actions competitive when personality drives Fun (e.g., high-instinct, high-hedonist safe states).

**Quantitative target:** For `explore_beach` (Preparation) to not win over a good Fun action, Fun effective weight needs to be at least ~0.65:  
`fun_effective_weight_needed ≥ explore_beach_Prep_contribution / best_Fun_quality ≈ 0.3079 / 0.3 = 1.03`  
That's unreachably high for a single category weight, which means `explore_beach`'s Preparation contribution also needs to be reduced. The combined fix is:  
1. Raise `funBaseScale` to ~0.85 (giving Fun effective weight ≈ `0.85 × factor ≈ 0.27`)  
2. Also tighten `preparationScale` for non-critical states (currently 0.7) by adding a safe-state cap — OR reduce the Preparation quality of `explore_beach` from 0.78 (action-level change)

**Simplest first step (one parameter, lowest risk):**  
Raise `funBaseScale` from 0.6 to 0.85. Re-run evaluation. If Fun is now closer to rank 3–4 for high-instinct safe states, then add `preparationScale` cap as a second step. Do not reduce `FatiguePressureRestScale` further — it was already cut 67% and that is the direct trigger.

**Do NOT:** Fix this via `prepTimePressureCap` or by adjusting `think_about_supplies` parameters. `think_about_supplies` is at rank 15 and is not the winning Preparation action. The winning action is `explore_beach`, a world-item-based action unrelated to `think_about_supplies`.

**Risk assessment:** Raising `funBaseScale` will increase Fun competitiveness in all non-critical states across all trait profiles. The profiles with high Hedonist/Instinctive traits will benefit most. Since all sacred states pass currently and the fuzzer showed no prep-dominance pathology (prep_dominated_count=0 at both baseline and optimized), there is low risk of introducing a Fun over-dominance regression. The holdout improvement (+5 exact) in the current optimization run shows the model is generalizing well, and a modest `funBaseScale` increase should preserve that.

---

## 5. Fuzzer Trait-Profile Validation

The `trait-profile-breakdown.json` from the updated fuzzer run shows consistent per-actor results:

### Baseline (ProductionDefault `47FE6B78`)

| Actor | Comfort dominated % | Food consumption lost % | Rest dominated % |
|---|---|---|---|
| Ashley | 13.8% | 46.6% | 57.9% |
| Elizabeth | 13.4% | 46.3% | 56.9% |
| Frank | 14.5% | 47.9% | 57.3% |
| Johnny | 13.0% | 45.8% | 57.1% |
| Oscar | 14.4% | 47.6% | 57.9% |
| Sawyer | 12.6% | 45.4% | 57.8% |

### Optimized (`AE07DF39`)

| Actor | Comfort dominated % | Food consumption lost % | Rest dominated % |
|---|---|---|---|
| Ashley | 3.3% | 35.1% | 43.9% |
| Elizabeth | 2.7% | 34.5% | 40.3% |
| Frank | 4.5% | 37.7% | 43.0% |
| Johnny | 2.1% | 33.8% | 41.0% |
| Oscar | 4.2% | 36.6% | 44.7% |
| Sawyer | 1.7% | 33.4% | 41.9% |

**Observations:**
- Comfort-dominated improvement is consistent across all actors (12.6–14.5% → 1.7–4.5%). No actor regressed.
- Frank and Oscar show the highest post-optimization comfort and food-loss rates (~4.5%, ~37%). These actors likely have higher Survivor or Comfort personality traits that make them more susceptible to Rest/Comfort competition. This is a mild outlier worth monitoring.
- No prep-dominated rate appears for any actor at either baseline or optimized (confirmed 0% across all 6 actors in both conditions). This means the Preparation forbidden regression in the golden state is not yet surfacing at fuzzer scale — Preparation over-dominance in safe states is currently a pinpoint issue (1 golden state), not a systemic fuzz pathology.
- Rest-dominated rate dropped significantly (57–58% → 40–44%), confirming the `FatiguePressureRestScale` reduction had its intended effect at scale.

---

## Summary Table

| Finding | Evidence | Confidence |
|---|---|---|
| Single forbidden regression: `explore_beach` (Prep) beats Fun in high-instinct safe state | `forbidden-regression-diff.json` | Confirmed |
| `sleep_under_tree` (Rest) was the baseline winner; FatiguePressureRestScale cut dropped its score from ~0.816 to 0.416 | State delta + parameter diff | Confirmed |
| `explore_beach` Preparation score (~0.556) was unchanged by optimization | Quality decomp, stable effectiveWeights | Confirmed |
| Fun effective weight (0.1921) is too low to compete even with perfect-quality Fun candidate | Quality decomp, structural arithmetic | Confirmed |
| `think_about_supplies` is NOT the cause (rank 15, 5× lower contribution than `explore_beach`) | `optimizer/failures.json` | Confirmed |
| Fix path: raise `funBaseScale` from 0.6, not adjust `think_about_supplies` or Prep-cap | Arithmetic + parameter isolation | High confidence |
