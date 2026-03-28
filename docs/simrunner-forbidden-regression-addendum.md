# SimRunner Optimization — Forbidden Regression Addendum

**Generated:** 2026-03-28  
**Applies to:** Optimized profile `F5DA3947` vs baseline `47FE6B78`  
**Artifact sources:** Baseline `23677049778`, Optimizer `23677051879`, Optimized eval `23691060472`, Fuzzer `23691063729` (all on commit `3159cd0b`)  
**Artifact files used:** `forbidden-regression-diff.json`, `state-delta-report.json`, `optimizer/failures.json` (with `topCandidates` + `qualityModelDecomposition` + `thinkAboutSuppliesAnalysis`), `optimizer-comparison.json` (with `activeTuningParameters`), `fuzzer/trait-profile-breakdown.json`

---

## 1. Newly Introduced Forbidden Regressions

**Total new forbidden violations: 1** (source: `forbidden-regression-diff.json`)

| sampleKey | label | Desired | Baseline winner | Optimized winner | Score delta |
|---|---|---|---|---|---|
| `trait:13fcd090\|FoodAvailableNow\|s65\|h90\|e80\|m65` | training/stable-flavor/high-instinct/safe | Fun | `sleep_under_tree` (Rest) | `explore_beach` (Preparation) | 0 → −30 |

No forbidden violations were resolved. No other newly forbidden cases.

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
| Δ from top | −0.271 | −0.197 |
| Forbidden violated | No | **Yes** |
| Score | 0 | −30 |

Fun moved up one rank (10→9) and the gap slightly improved (−0.271→−0.197). **The regression is caused entirely by the winning category switching from the non-violating Rest to the forbidden Preparation** — not by Fun getting worse.

### Candidate ranking (optimized — from `optimizer/failures.json`)

| Rank | Action ID | Score | Dominant Categories | Key contribution |
|---|---|---|---|---|
| 1 | `explore_beach` | **0.5567** | Preparation, Efficiency | Prep: 0.78 × 0.3948 = **0.3079** |
| 2 | `go_fishing` | 0.5251 | FoodAcquisition, Efficiency, Fun | Fun: 0.30 × 0.1921 = **0.0576** |
| 3 | `sleep_under_tree` | 0.5160 | Rest, Safety | Rest: 1.0 × 0.3000 = **0.3000** |
| 4 | `sit_and_watch_waves` | 0.4728 | Comfort, Safety, Fun | Comfort: 0.55 × 0.6000 = **0.3300** |
| 5 | `bash_and_eat_coconut` | 0.4268 | FoodConsumption, Comfort | — |
| 9 | *(best Fun-rank action)* | — | Fun | Fun rank: 9 |
| 15 | `think_about_supplies` | 0.1632 | Preparation (fallback), Efficiency | Prep: 0.15 × 0.3948 = **0.0592** |

`think_about_supplies` is **not the winner, not close (rank 15), and not the cause of the regression.** Its Preparation contribution (0.0592) is 5.2× smaller than `explore_beach`'s (0.3079). This is confirmed by the optimizer not selecting `PrepTimePressureCap` even when it was available in the tunable set — capping `think_about_supplies` has zero effect on the winner.

### Winner comparison: baseline vs optimized

| Metric | Baseline | Optimized |
|---|---|---|
| Winning action | `sleep_under_tree` | `explore_beach` |
| Winning category | Rest | Preparation |
| Forbidden | No | **Yes** |
| Fun desired rank | 10 | 9 |
| Δ from top | −0.271 | −0.197 |
| `sleep_under_tree` score | ~0.816 (estimated at baseline) | **0.516** |
| Gap: `explore_beach` vs `sleep_under_tree` | — | **0.041** |

`explore_beach` scores 0.5567 in both baseline and optimized — it was always the Preparation ceiling in this state. At baseline, `sleep_under_tree` scored approximately 0.816 (estimated from `FatiguePressureRestScale=0.015`, Rest needAdd≈0.6). The `FatiguePressureRestScale` cut to 0.010 reduced Rest needAdd from ~0.6 to 0.3, dropping `sleep_under_tree` to 0.516. Rest no longer covers Preparation. Only 0.041 separates the two.

### Category decomposition (optimized — from `qualityModelDecomposition`)

| Category | needAdd | personalityBase | moodMultiplier | effectiveWeight |
|---|---|---|---|---|
| **Fun** | 0 | 1.0 | **0.1921** | **0.1921** |
| **Preparation** | 0 | 0.42 | 0.94 | **0.3948** |
| Mastery | 0 | 0.36 | 0.93 | 0.3348 |
| **Rest** | **0.30** | 0 | 1.0 | **0.3000** |
| **Comfort** | **0.40** | 0.20 | 1.0 | **0.6000** |
| Safety | 0.20 | 0.18 | 1.0 | 0.3800 |
| Efficiency | 0 | 0.24 | 1.0 | 0.2400 |
| FoodConsumption | 0.0675 | 0.29 | 1.0 | 0.3575 |
| FoodAcquisition | 0.0075 | 0.12 | 1.0 | 0.1275 |

**Critical observation:** Fun has `personalityBase=1.0` — the highest possible value for this high-instinct actor — but `moodMultiplier=0.1921`, giving effectiveWeight=0.1921. Preparation has `personalityBase=0.42` but `moodMultiplier=0.94`, giving effectiveWeight=0.3948. Fun's effective weight is less than half of Preparation's, despite this being the actor profile where Fun should dominate in safe states.

**Structural impossibility:** Even if a hypothetical action had Fun quality=1.0 (perfect), its Fun contribution would be 1.0 × 0.1921 = **0.1921**. `explore_beach`'s Preparation contribution alone is **0.3079**. Fun cannot beat Preparation in this state regardless of candidate availability.

### `think_about_supplies` analysis

**Present?** Yes — ranked 15th with score 0.1632.

**Was it the winner?** No. Rank 15.

**Qualities emitted:**

| Quality | qualityValue | effectiveWeight | contribution |
|---|---|---|---|
| Preparation | 0.15 | 0.3948 | 0.0592 |
| Efficiency | 0.10 | 0.240 | 0.0240 |

These are the fallback qualities (`{Preparation:0.15, Efficiency:0.10}`), which activate when no food or safety recipes are discoverable in this FoodAvailableNow/safe scenario. The starvation suppression gate (×0.2) is **not active** — satiety=65 is well above the starvation threshold. This is correct behavior.

**Suppression/cap inputs applied:** None. The fallback qualities are correctly unapplied-suppressed: score 0.1632 = intrinsic(0.08) + 0.0592 + 0.0240. Working as designed.

**`PrepTimePressureCap` confirmation:** The optimizer did not select `PrepTimePressureCap` even when it was available (PR #110, range [0.05, 0.50]). This is direct confirmation that adjusting the `think_about_supplies` Preparation contribution would have no impact on the outcome. `think_about_supplies` is not the lever here.

---

## 3. Root-Cause Assessment

### Verdict: **C + B combined** — C is the proximate trigger, B is the structural root cause

#### Hypothesis C — Rest suppression exposed pre-existing Preparation dominance  
**Confidence: HIGH (confirmed)**

This is the proximate trigger. The `FatiguePressureRestScale` cut from 0.015 to 0.010 (−33%) reduced Rest needAdd from ~0.60 to 0.30, dropping `sleep_under_tree` from an estimated ~0.816 to 0.516. This moved Rest below Preparation (0.5567), which was already in second place. `explore_beach` was always scoring 0.5567 — it did not change. Only Rest fell.

Evidence:
- State delta confirms baseline winner was `sleep_under_tree` (Rest); optimized winner is `explore_beach` (Preparation).
- `FatiguePressureRestScale` is the only changed parameter that affects Rest needAdd.
- `explore_beach` score is stable (no optimizer parameter affects Preparation effectiveWeight for this actor profile).
- The high-survivor safe state (`training/stable-flavor/high-survivor/safe`) also moved to a Preparation winner — same mechanism, different trait profile.

#### Hypothesis B — Fun pressure is too low in safe states  
**Confidence: HIGH (structural root cause, confirmed by optimizer non-selection of FunBaseScale)**

This is the deeper, underlying problem. Fun moodMultiplier is 0.1921 at morale=65/energy=80 in a non-critical safe state — approximately 5× lower than Preparation (0.94) at the same state. With personalityBase=1.0 (maximum for this high-instinct actor), Fun effective weight is 0.1921. Preparation's effective weight is 0.3948 with only personalityBase=0.42.

The optimizer had `FunBaseScale` available ([0.40, 1.00]) but did not select it. The arithmetic explains why:

| FunBaseScale | Fun effectiveWeight | go_fishing total score | vs explore_beach |
|---|---|---|---|
| 0.60 (current) | 0.1921 | 0.5251 | −0.032 |
| 0.80 | 0.2561 | 0.5443 | −0.012 |
| 0.95 | 0.3042 | 0.5559 | −0.001 |
| **1.00 (max)** | **0.3202** | **0.5635** | **+0.007** |

Even at the maximum FunBaseScale=1.00, `go_fishing` beats `explore_beach` by only 0.007 — a margin that would be reversed by any subsequent optimizer step. The optimizer correctly determined no step along the FunBaseScale axis was reliably net-positive. Fun was ranked 10th at baseline (well before any optimization) — this is a pre-existing structural gap, not a side effect of optimization.

#### Hypothesis A — Preparation pressure is too high in safe states  
**Confidence: MEDIUM (contributing, confirmed by PrepTimePressureCap non-selection)**

`explore_beach` Preparation contribution is 0.3079 (quality=0.78 × effectiveWeight=0.3948). The effectiveWeight is not anomalous — it reflects the high-instinct actor's personalityBase=0.42 for Preparation plus near-neutral moodMultiplier=0.94. What is high is the action-level quality value (0.78) for `explore_beach`. A beach exploration carrying Preparation quality=0.78 is the primary fixable component.

The optimizer did not select `PrepTimePressureCap` — confirming that capping `think_about_supplies` (which is what `PrepTimePressureCap` affects) has no impact on this regression. The problem is the world-item action quality, not the time-pressure component.

#### Hypothesis D — Candidate availability for Fun is too weak  
**Confidence: LOW (not supported)**

Fun candidates are present: `go_fishing` (rank 2, Fun contribution 0.0576) and `sit_and_watch_waves` (rank 4, Fun contribution 0.0288). The problem is not availability — it is the Fun effective weight (0.1921). Even if a perfect Fun-quality action (quality=1.0) existed, its Fun contribution would be 0.1921 — still below `explore_beach`'s Preparation contribution alone (0.3079).

---

## 4. Recommended Next Fix

### Combined fix: reduce `explore_beach` Preparation quality AND raise `FunBaseScale`

This regression requires two simultaneous changes. Neither is sufficient alone.

**Step 1 (action-level):** Reduce `explore_beach` Preparation quality from 0.78 to ~0.55–0.60.

At quality=0.60: Preparation contribution = 0.60 × 0.3948 = **0.2369**.  
New `explore_beach` total ≈ 0.22 + 0.2369 + 0.0288 (Efficiency) = **0.4857**.

**Step 2 (profile-level):** Raise `FunBaseScale` in `DecisionTuningProfile` from 0.60 to 0.80–0.85.

At FunBaseScale=0.80: Fun effectiveWeight = 0.2561.  
`go_fishing` total = 0.22 + 0.1275 + 0.12 + (0.3 × 0.2561) = **0.5443**.

With both changes: `go_fishing` (Fun, 0.5443) beats `explore_beach` (Preparation, 0.4857) by **0.059** — a stable, large margin. This is robust to small subsequent optimizer moves.

**Why both changes are required:**
- `FunBaseScale` alone (even at 1.00 max): margin is 0.007 — too small, risks oscillation.
- Action quality reduction alone: `explore_beach` would score 0.4857, beaten by `go_fishing` (0.5251 at current Fun weight) — this works mathematically, but leaves Fun effective weight structurally weak across all other states.
- Together: Fun wins clearly and the high-instinct actor's Fun personality is correctly reflected in safe states.

**Do NOT:**
- Adjust `think_about_supplies` parameters — confirmed wrong lever (rank 15, not the winner).
- Adjust `PrepTimePressureCap` alone — confirmed wrong lever (optimizer non-selection validates this).
- Reduce `FatiguePressureRestScale` further — gap between `explore_beach` and `sleep_under_tree` is already only 0.041. Further reduction risks oscillation back to a Rest winner without resolving the underlying Fun weakness.

**Risk assessment:** Reducing `explore_beach` Preparation quality affects all actor profiles in `FoodAvailableNow` safe states. The `sacred/prep-preserved/high-planner/safe` test uses `HighRecipeOpportunity` scenario (not `FoodAvailableNow`), so direct collision is unlikely. The `training/stable-flavor/balanced/safe` Mastery failure (Bucket A, score 0) may also benefit — removing Preparation dominance in safe FoodAvailableNow scenarios may allow Mastery-category actions to rank higher for that actor too.

---

## 5. PR #110 Parameter Summary

| Parameter | Available? | Optimizer selected? | Effect |
|---|---|---|---|
| `FunBaseScale` | Yes [0.40, 1.00] | **No** | Marginal margin (0.007) at max; net-negative without action quality change |
| `PrepTimePressureCap` | Yes [0.05, 0.50] | **No** | Affects `think_about_supplies` only; wrong lever for this regression |
| `ComfortRestSuppressionMin` | Yes [0.10, 0.70] | **Yes** (0.30→0.25) | Raised Comfort effectiveWeight (0.53→0.60); helped other states |
| `HungerSuppressionStartSatiety` | Yes [15.0, 40.0] | **No** | No net-positive step found |
| `HungerSuppressionFullSatiety` | Yes [5.0, 20.0] | **No** | No net-positive step found |
| `HungerSuppressionExponent` | Yes [0.5, 4.0] | **No** | No net-positive step found |

The optimizer's non-selection of `FunBaseScale` and `PrepTimePressureCap` is diagnostic, not a limitation. It confirms that these parameters cannot resolve the regression within the current optimizer score function without collateral damage. The fix path is outside pure weight tuning.

---

## 6. Fuzzer Trait-Profile Validation

### Summary

| Metric | Baseline | Optimized |
|---|---|---|
| Total samples | 63,528 | 63,528 |
| Food consumption lost | 12,685 (46.6%) | 9,654 (35.4%) |
| Comfort dominated | 3,709 (13.6%) | 826 (3.0%) |
| Prep dominated | 0 (0%) | 0 (0%) |

### Per-Actor Breakdown

| Actor | Baseline food-lost % | Optimized food-lost % | Baseline comfort-dom % | Optimized comfort-dom % | Optimized rest-dom % |
|---|---|---|---|---|---|
| Ashley | 46.6% | 35.5% | 13.8% | 3.3% | 50.3% |
| Elizabeth | 46.3% | 35.0% | 13.4% | 2.7% | 48.7% |
| Frank | 47.9% | 37.6% | 14.5% | 4.4% | 50.4% |
| Johnny | 45.8% | 34.1% | 13.0% | 2.0% | 49.6% |
| Oscar | 47.6% | 36.8% | 14.4% | 4.1% | 51.2% |
| Sawyer | 45.4% | 33.7% | 12.6% | 1.7% | 50.2% |

### Observations

- Comfort-dominated improvement is consistent across all 6 actors (12.6–14.5% → 1.7–4.4%). No actor regressed.
- Frank and Oscar show the highest post-optimization comfort and food-loss rates (~4%, ~37%). These actors carry higher Survivor/Comfort personality traits. This is consistent with the Bucket B pattern (high-survivor mild-distress residual).
- **Prep-dominated: 0% for all actors at both baseline and optimized.** The Preparation forbidden regression is a pinpoint golden-state issue, not a systemic fuzzer pathology. At fuzzer scale, Preparation is not over-dominating any actor's behavior.
- Rest-dominated rate post-optimization is ~49–51% across all actors. This reflects the moderate `FatiguePressureRestScale=0.010` — Rest is more active in the fuzzer than it was in the golden-state evaluation because the fuzzer covers a wider range of vitals, including moderate-fatigue states where Rest is appropriate.

---

## Summary Table

| Finding | Evidence | Confidence |
|---|---|---|
| 1 forbidden regression: `explore_beach` (Prep) beats Fun in high-instinct safe state | `forbidden-regression-diff.json` | Confirmed |
| `sleep_under_tree` was baseline winner; `FatiguePressureRestScale` cut dropped its score from ~0.816 to 0.516 | State delta; quality decomp | Confirmed |
| `explore_beach` Preparation score (0.5567) was unchanged by optimization | Quality decomp; stable effectiveWeights | Confirmed |
| Gap between `explore_beach` and `sleep_under_tree` is only 0.041 | `optimizer/failures.json` candidate scores | Confirmed |
| Fun effectiveWeight (0.1921) cannot compete even at FunBaseScale=1.0 max (margin 0.007) | Arithmetic; optimizer non-selection of FunBaseScale | Confirmed |
| `think_about_supplies` is NOT the cause (rank 15, 5.2× lower Prep contribution than winner) | `optimizer/failures.json`; optimizer non-selection of PrepTimePressureCap | Confirmed |
| `ComfortRestSuppressionMin` (PR #110 new param) was selected; Comfort effectiveWeight 0.530→0.600 | `optimizer-diff.json` | Confirmed |
| Fix requires BOTH `explore_beach` quality reduction AND `FunBaseScale` raise | Arithmetic: FunBaseScale max alone yields only 0.007 margin | High confidence |
| Preparation dominance is not systemic at fuzzer scale (0% prep-dominated across all actors) | `fuzzer/trait-profile-breakdown.json` | Confirmed |
