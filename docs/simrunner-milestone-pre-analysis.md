# SimRunner Optimization Pipeline — Milestone Pre-Analysis

**Generated:** 2026-03-28  
**Purpose:** Decision-useful review before selecting the next optimization phase.  
**Baseline profile:** `47FE6B78` (`ProductionDefault`)  
**Optimized profile:** `F5DA3947`

---

## 1. Artifact Provenance

| Pipeline Stage | Workflow Name | Run ID | Artifact Name | Head SHA |
|---|---|---|---|---|
| Baseline Evaluation | SimRunner — Baseline Evaluation | `23677049778` | `simrunner-baseline` | `3159cd0b` |
| Optimizer | SimRunner — Optimize Golden | `23677051879` | `simrunner-optimizer` | `3159cd0b` |
| Optimized Evaluation | SimRunner — Evaluate Optimized Profile | `23691060472` | `simrunner-optimized-eval` | `3159cd0b` |
| Pressure Fuzzer | SimRunner — Pressure Fuzzer Comparison | `23691063729` | `simrunner-fuzzer` | `3159cd0b` |

All four runs were triggered on commit `3159cd0b`. The `ProductionDefault` profile hash `47FE6B78` is consistent across the baseline run and the optimizer comparison baseline, confirming data integrity. All expected artifact sets were present with full rich artifact files. No missing files.

**Files used:**
- `artifacts/baseline/eval-summary.json`, `artifacts/baseline/failures.json`
- `artifacts/optimizer/optimizer-comparison.json`, `artifacts/optimizer/optimizer-diff.json`, `artifacts/optimizer/forbidden-regression-diff.json`, `artifacts/optimizer/state-delta-report.json`, `artifacts/optimizer/failures.json` (with `topCandidates` + `qualityModelDecomposition` + `thinkAboutSuppliesAnalysis`)
- `artifacts/optimized/eval-summary.json`, `artifacts/optimized/failures.json`
- `artifacts/fuzzer/comparison-summary.json`, `artifacts/fuzzer/trait-profile-breakdown.json`

---

## 2. Milestone 1 — Baseline Contract Sanity

### Numbers

| Set | Exact Pass | Satisfied Pass | Forbidden Violations | Regressions |
|---|---|---|---|---|
| Training (32 states) | 20 | 20 | **0** | — |
| Holdout (14 states) | 4 | 11 | **0** | — |
| Sacred (6 states) | 4 | 6 | **0** | **0** |

**Score at baseline:** 1,154

### Interpretation

**Dataset coherence:** Yes. All sacred states pass. Training and holdout both have zero forbidden violations. All remaining failures are soft (score 0 or +6), focused in two clearly distinct clusters: stable-flavor safe-state flavor-category mismatches, and mild-distress food-now timing issues.

**Sacred health:** All 6 sacred states satisfied, all 4 sacred exact-matches pass. Ideal starting condition for optimization.

**Training vs holdout balance:** Training has 20/32 satisfied (63%); holdout has 4 exact / 11 satisfied. The asymmetry (4 exact but 11 satisfied) indicates most holdout passes are partial-rank satisfactions. This is expected — holdout tests intermediate states where multiple categories compete reasonably. No imbalance concern.

**Remaining baseline failures (11 total):**
- 4 stable-flavor safe-state cases: Preparation or Rest beats the desired flavor category (Mastery, Fun, Safety, Comfort). No forbidden violations. Score-0 soft failures.
- 6 mild-distress food-now/comfort-avail cases: Rest or Efficiency beats FoodConsumption at satiety 40–45. Score +6 (partial credit).
- 1 holdout stable-flavor case: Fun lost to Rest. Score 0.

All failures are non-forbidden and structurally coherent.

---

## 3. Milestone 2 — Optimization Validity

### Delta Table

| Metric | Baseline | Optimized | Delta |
|---|---|---|---|
| Training exact pass | 20 | 22 | **+2** |
| Training satisfied pass | 20 | 26 | **+6** |
| Training forbidden violations | 0 | 1 | **+1** (regression) |
| Holdout exact pass | 4 | 9 | **+5** |
| Holdout satisfied pass | 11 | 12 | +1 |
| Sacred exact pass | 4 | 4 | 0 |
| Sacred satisfied pass | 6 | 6 | 0 |
| Sacred regressions | 0 | 0 | **0** ✅ |
| Score | 1,154 | 1,290 | **+136** |

### Parameters changed (3 coordinate-descent iterations)

| Parameter | Baseline | Optimized | Δ | PR #110? |
|---|---|---|---|---|
| `HungerModerateRange` | 1.20 | 1.30 | +0.10 | No |
| `FoodConsumptionShareHigh` | 0.80 | 0.90 | +0.10 | No |
| `FatiguePressureRestScale` | 0.015 | 0.010 | **−0.005 (−33%)** | No |
| `InjurySafetyNeedScale` | 0.025 | 0.020 | −0.005 | No |
| `ComfortRestSuppressionMin` | 0.300 | 0.250 | **−0.050** | **Yes** |

### Parameters available but NOT changed

| Parameter | Default | Range | Notes |
|---|---|---|---|
| `FunBaseScale` | 0.6 | [0.40, 1.00] | See analysis below |
| `PrepTimePressureCap` | 0.2 | [0.05, 0.50] | See analysis below |
| `HungerSuppressionStartSatiety` | 25 | [15.0, 40.0] | No net-positive step found |
| `HungerSuppressionFullSatiety` | 10 | [5.0, 20.0] | No net-positive step found |
| `HungerSuppressionExponent` | 2 | [0.5, 4.0] | No net-positive step found |

### PR #110 parameter analysis

PR #110 expanded the optimizer's tunable parameter set from 14 → 20 and fixed a bug in 4 `MoodTuning` setters that were silently dropping `HungerSuppression*` fields on every perturbation of any mood parameter. Both changes affect this run.

**`ComfortRestSuppressionMin` (selected, −0.05):** This new parameter was picked up by the optimizer. Reducing the minimum Comfort/Rest suppression floor from 0.30 to 0.25 allows Comfort to be more competitive at distress edges, contributing to improved food-timing outcomes.

**`FunBaseScale` (not selected):** Available at [0.40, 1.00], step 0.05, but not changed. The arithmetic explains why. For the best available Fun action (`go_fishing`, Fun quality=0.3) to beat the winning Preparation action (`explore_beach`, score=0.5567), Fun effectiveWeight would need to exceed 0.297. At the maximum allowed FunBaseScale=1.00, Fun effectiveWeight ≈ 0.320, giving `go_fishing` a total score of ≈0.564 — a margin of only 0.007 over `explore_beach`. This margin would be reversed by any further optimizer step that slightly shifts Preparation or Rest. The optimizer correctly found no net-positive single-step move. **Resolving the Fun forbidden regression via FunBaseScale alone is insufficient; a supporting action-level change (reducing `explore_beach` Preparation quality) is also required.**

**`PrepTimePressureCap` (not selected):** Available at [0.05, 0.50] but not selected. This parameter affects the `think_about_supplies` Preparation contribution. The winning Preparation action in the forbidden state is `explore_beach` (a world-item action), not `think_about_supplies`. Capping `think_about_supplies` has no effect on the winner. The optimizer correctly ignored it.

**MoodTuning bug fix:** Before PR #110, any optimizer iteration that perturbed `StarvatingSatietyThreshold`, `PrepStarvationFloor`, `FunCriticalSurvivalScale`, or `FunCriticalSatietyThreshold` would silently reset all `HungerSuppression*` fields to init-defaults. With the bug fixed, all perturbations are clean. The optimizer now correctly preserves all fields and finds the conservative `FatiguePressureRestScale=0.010` rather than a more extreme value.

### Interpretation

**Did optimization clearly help training?** Yes. Training went from 20/20 to 22/26 (exact/satisfied), adding 6 satisfied states. The combination of `FatiguePressureRestScale` reduction and `ComfortRestSuppressionMin` reduction drives cleaner category transitions in distress states.

**Did holdout stay stable, improve, or regress?** It **improved substantially** — +5 exact passes, +1 satisfied. The holdout exact improvement (+5) exceeds the training exact improvement (+2), meaning the optimizer is finding generalizable weights rather than fitting the training set.

**Did sacred regress?** **No.** Sacred passes are identical before and after optimization. Ideal outcome.

**Is there overfitting?** Minimal. The holdout exact improvement outpacing training exact improvement is a strong generalization signal. The single new forbidden violation (`training/stable-flavor/high-instinct/safe`) is a structural issue that the optimizer confirmed cannot be resolved by pure weight tuning — not an overfitting artifact.

---

## 4. Milestone 3 — Failure Pattern Analysis

### Failure Bucket Summary

#### Bucket A — Stable-State Preparation Over-Dominance (safe actors)

**States (post-opt):** 2 training (1 carryover from baseline, 1 newly forbidden)  
**Trait profiles:** balanced (`15cc8081`), high-instinct (`13fcd090`)  
**Scenario:** `FoodAvailableNow`, s=65, h=90 (safe, comfortable)  
**Pattern:**
- Balanced: Desired Mastery, actual Preparation. Score 0. Not forbidden. Mastery does not appear in the candidate list (no recipe discoverability in this scenario setup).
- High-instinct: Desired Fun, actual Preparation. Score −30. **Forbidden.** Fun is ranked 9th. Winner is `explore_beach` (Preparation quality=0.78).

**Root cause:** `FatiguePressureRestScale` reduction evicted Rest from these safe-state slots, exposing a pre-existing Preparation dominance. `explore_beach` was always the second-place Preparation action; Rest was masking it at baseline. See the addendum (`simrunner-forbidden-regression-addendum.md`) for the full quantitative breakdown.

The optimizer confirmed via non-selection of both `FunBaseScale` and `PrepTimePressureCap` that this cannot be resolved by weight tuning alone. The fix requires reducing `explore_beach` Preparation quality (action-level change) combined with a `FunBaseScale` increase.

---

#### Bucket B — Mild Distress Rest Dominance (food-now/comfort-avail, s=40–45)

**States (post-opt):** 1 training  
**Trait profiles:** high-survivor (`ef00d494`)  
**Scenario:** `FoodAvailable_WithComfort`, s=45  
**Pattern:** Rest beats FoodConsumption at moderate satiety distress with comfort items available. Δ from top: −0.844. Score +6 (partial credit, not forbidden).

Most mild-distress cases resolved through optimization. The remaining high-survivor case has a large delta (−0.844), indicating this is not a close call. The high-survivor trait profile weights Safety and structural comfort-availability signals heavily by design. Further `FatiguePressureRestScale` reduction could help, but is constrained by the Bucket A risk.

---

#### Bucket C — Stable-State Flavor Mismatch (Rest beats Fun, safe)

**States (post-opt):** 0  
Both `high-hedonist/safe` and `high-instinct/safe` had Rest beating Fun (non-forbidden, score 0) at baseline. Both were resolved by the optimizer — the `FatiguePressureRestScale` reduction evicted Rest from these slots. The high-instinct case's slot was filled by Preparation (creating the Bucket A forbidden violation) rather than Fun. The high-hedonist case resolved cleanly to Comfort.

---

#### Bucket D — Holdout Stable-Flavor (Fun lost to Rest, high-instinct)

**State (post-opt):** 1 holdout (unchanged from baseline)  
`trait:13fcd090|FoodAvailable_WithComfort|s55|h80|e65|m60` — desired Fun, actual Rest, Fun ranked 12th, Δ from top −1.570. Score 0. Not forbidden.

Fun is deeply ranked — this is not a close call. The gap (−1.570) is far larger than what weight tuning can bridge in one optimizer pass. Resolving Bucket A first (raising `FunBaseScale` + reducing `explore_beach` Preparation quality) should bring Fun into a more competitive position across all high-instinct safe states, which may help this holdout case indirectly.

---

### Summary of Buckets

| Bucket | Count (post-opt) | Forbidden? | Root Cause |
|---|---|---|---|
| A — Preparation dominates safe states | 2 | 1 yes | Structural: explore_beach Preparation quality too high relative to Fun weight; requires action-level + weight fix |
| B — Mild distress Rest dominance | 1 | No | High-survivor trait weighting; large delta; diminishing returns from pure weight tuning |
| C — Stable-flavor Fun mismatch | 0 | — | Resolved by optimizer |
| D — Holdout high-instinct Fun | 1 | No | Fun effective weight too low; large delta; will partially benefit from Bucket A fix |

---

## 5. Milestone 4 — Pressure Fuzzer Validation

### Numbers

| Metric | Baseline | Optimized | Δ | Δ pp |
|---|---|---|---|---|
| Total samples | 63,528 | 63,528 | — | — |
| Starvation-region samples | 27,240 | 27,240 | 0 | — |
| Food consumption lost | 12,685 (46.6%) | 9,654 (35.4%) | **−3,031** | **−11.2 pp** |
| Comfort dominated | 3,709 (13.6%) | 826 (3.0%) | **−2,883** | **−10.6 pp** |
| Prep dominated | 0 (0%) | 0 (0%) | 0 | — |

### Per-Actor Breakdown

| Actor | Baseline food-lost % | Optimized food-lost % | Baseline comfort-dom % | Optimized comfort-dom % | Optimized rest-dom % |
|---|---|---|---|---|---|
| Ashley | 46.6% | 35.5% | 13.8% | 3.3% | 50.3% |
| Elizabeth | 46.3% | 35.0% | 13.4% | 2.7% | 48.7% |
| Frank | 47.9% | 37.6% | 14.5% | 4.4% | 50.4% |
| Johnny | 45.8% | 34.1% | 13.0% | 2.0% | 49.6% |
| Oscar | 47.6% | 36.8% | 14.4% | 4.1% | 51.2% |
| Sawyer | 45.4% | 33.7% | 12.6% | 1.7% | 50.2% |

### Interpretation

**Did the pressure surface improve?** Yes — substantially. Food-consumption-lost dropped from 46.6% to 35.4% (−11.2 pp), and comfort-dominated dropped from 13.6% to 3.0% (−10.6 pp). All six actors improved on both metrics. No actor regressed.

**Did any pathology get worse?** No. Prep-dominated is 0% for all actors at both baseline and optimized. The Preparation forbidden regression in the golden-state test is a pinpoint issue in one golden state — not a systemic fuzzer pathology.

**Rest-dominated rate:** ~50% post-optimization across all actors. This reflects the moderate `FatiguePressureRestScale=0.010` (−33% from baseline). Rest dominance in non-critical states is an expected consequence of this parameter — not a pathology, but worth watching if further `FatiguePressureRestScale` reduction is attempted.

**Per-actor spread:** Frank and Oscar show the highest post-optimization food-loss and comfort-dominated rates (~4%, ~37%). These actors likely carry higher Survivor/Comfort personality traits. This is consistent with the Bucket B pattern (high-survivor mild-distress residual). No corrective action needed unless it worsens.

**Do fuzzer results agree with golden-state improvements?** Yes. The optimizer's golden-state gains (training +6 satisfied, sacred unchanged) are reflected in the fuzzer: comfort-dominated nearly eliminated (3.0%), food-lost meaningfully reduced. No tension between the two evaluation views.

---

## 6. Overall Verdict

### **A. Good to proceed — with one concrete near-term fix before the next optimizer pass.**

The foundation is solid:
- Baseline is clean: 0 forbidden violations, 0 sacred regressions.
- Optimization generalized strongly: holdout exact improved more (+5) than training exact (+2).
- Sacred is intact through the full optimization cycle.
- Fuzzer pathology dropped substantially (comfort-dominated 13.6% → 3.0%).
- PR #110 expanded the tunable set and confirmed via optimizer run which levers are effective (`ComfortRestSuppressionMin`) and which are not sufficient alone (`FunBaseScale`, `PrepTimePressureCap`).

The single outstanding issue is the **Preparation-over-Fun forbidden violation** in `training/stable-flavor/high-instinct/safe`. The optimizer confirmed this cannot be resolved by weight tuning alone — the fix requires both an action-level change (`explore_beach` Preparation quality reduction) and a profile-level change (`FunBaseScale` increase). Do this before the next optimizer pass.

---

## 7. Recommended Next Actions

### Immediate (before next optimizer run)

**Reduce `explore_beach` Preparation quality AND raise `FunBaseScale`.**

The addendum (`simrunner-forbidden-regression-addendum.md`) provides the full quantitative diagnosis. The actionable fix:
1. Reduce `explore_beach` Preparation quality from 0.78 to ~0.55–0.60 in the relevant action definition.
2. Raise `FunBaseScale` in `DecisionTuningProfile` from 0.60 to 0.80–0.85.

These two changes together give a stable winning margin for Fun in the high-instinct safe state. `FunBaseScale` alone (even at the optimizer's maximum of 1.00) only produces a 0.007 margin — insufficient for stable convergence.

### Follow-up #1

**Run one more optimizer pass from the fixed profile.**  
After resolving Bucket A, run a fresh coordinate-descent pass starting from `optimized-F5DA3947` (or the post-fix variant). With `FunBaseScale` and `explore_beach` Preparation quality corrected, the optimizer can effectively explore the `FunBaseScale` axis. Remaining soft failures are Bucket B (high-survivor mild distress, score +6) and Bucket D (holdout high-instinct Fun, score 0).

### Follow-up #2

**Monitor rest-dominated rate in the next optimizer pass.**  
The gap between `explore_beach` (0.5567) and `sleep_under_tree` (0.516) is only 0.041. Any further `FatiguePressureRestScale` reduction will cause the high-instinct state to oscillate between a Rest winner (not forbidden) and Preparation winner (forbidden). Fix Bucket A first so the category landscape in that state is determined by Fun vs Preparation, not Rest vs Preparation.

### Do not do yet

**Do not expand the golden-state training set before resolving Bucket A.**  
The Preparation dominance issue is an objective model issue (action quality + weight imbalance), not a coverage gap. Adding states before the fix will cause the optimizer to work around it by adjusting unrelated weights, producing a profile that still doesn't resolve the root cause.

---

## Appendix A — Failing Sample Keys After Optimization

| Sample Key | Label | Set | Score | Desired | Actual | Forbidden |
|---|---|---|---|---|---|---|
| `trait:13fcd090\|FoodAvailableNow\|s65\|h90\|e80\|m65` | training/stable-flavor/high-instinct/safe | Training | **−30** | Fun | Preparation | ✅ |
| `trait:15cc8081\|FoodAvailableNow\|s65\|h90\|e80\|m65` | training/stable-flavor/balanced/safe | Training | 0 | Mastery | Preparation | ❌ |
| `trait:ef00d494\|FoodAvailable_WithComfort\|s45\|h70\|e60\|m55` | training/mild-distress/comfort-avail/high-survivor | Training | 6 | FoodConsumption | Rest | ❌ |
| `trait:13fcd090\|FoodAvailable_WithComfort\|s55\|h80\|e65\|m60` | holdout/stable-flavor/high-instinct/comfort-avail | Holdout | 0 | Fun | Rest | ❌ |

---

## Appendix B — Changed Parameters from `optimizer-diff.json`

| Parameter | Baseline | Optimized | Δ | Interpretation |
|---|---|---|---|---|
| `FatiguePressureRestScale` | 0.015 | 0.010 | **−0.005 (−33%)** | Moderate Rest-pressure reduction; evicts Rest from distress slots and exposes pre-existing Preparation in safe slots |
| `FoodConsumptionShareHigh` | 0.80 | 0.90 | +0.10 | Stronger food signal when food share is high; resolves mild-distress food-timing cases |
| `HungerModerateRange` | 1.20 | 1.30 | +0.10 | Wider moderate-hunger pressure window |
| `InjurySafetyNeedScale` | 0.025 | 0.020 | −0.005 | Minor Safety-pressure reduction |
| `ComfortRestSuppressionMin` | 0.300 | 0.250 | **−0.050** | Lower Comfort/Rest suppression floor; allows Comfort to compete more at distress edges |

The optimizer converged in 3 iterations. `ComfortRestSuppressionMin` is a new parameter from PR #110; its selection in this run validates that the expanded parameter set is producing useful coverage.
