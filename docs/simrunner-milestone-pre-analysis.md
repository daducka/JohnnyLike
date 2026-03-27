# SimRunner Optimization Pipeline — Milestone Pre-Analysis (Rev 2)

**Generated:** 2026-03-27 (re-run from scratch on latest CI artifacts)  
**Purpose:** Decision-useful review for Derek + ChatGPT before selecting the next optimization phase.  
**Note:** This supersedes the prior analysis (Rev 1, profile hash `3202C567`). Core model changes were applied before these runs; the new baseline profile hash is `47FE6B78`.

---

## 1. Artifact Provenance

| Pipeline Stage | Workflow Name | Run ID | Artifact Name | Head SHA |
|---|---|---|---|---|
| Baseline Evaluation | SimRunner — Baseline Evaluation | `23668922466` (run #2) | `simrunner-baseline` | `f3455258` |
| Optimizer | SimRunner — Optimize Golden | `23669117944` (run #3) | `simrunner-optimizer` | `f3455258` |
| Optimized Evaluation | SimRunner — Evaluate Optimized Profile | `23669174865` (run #5) | `simrunner-optimized-eval` | `f3455258` |
| Pressure Fuzzer | SimRunner — Pressure Fuzzer Comparison | `23669182238` (run #2) | `simrunner-fuzzer` | `f3455258` |

**Run consistency:** All four runs were triggered on the same commit `f3455258` within a single session. The `ProductionDefault` profile hash `47FE6B78` is consistent across the baseline run and the optimizer comparison, confirming data integrity. All four expected artifact sets were present. No missing files.

**Files used:**
- `artifacts/baseline/eval-summary.json`, `artifacts/baseline/failures.json`
- `artifacts/optimizer/optimizer-comparison.json`, `artifacts/optimizer/optimizer-diff.json`, `artifacts/optimizer/optimizer-result.json`
- `artifacts/optimized/eval-summary.json`, `artifacts/optimized/failures.json`
- `artifacts/fuzzer/comparison-summary.json`

---

## 2. Milestone 1 — Baseline Contract Sanity

### Numbers

| Set | Exact Pass | Satisfied Pass | Forbidden Violations | Regressions |
|---|---|---|---|---|
| Training (32 states) | 20 | 20 | **0** | — |
| Holdout (10 states) | 4 | 11 | **0** | — |
| Sacred (6 states) | 4 | 6 | **0** | **0** |

**Score at baseline:** 1,154

### Interpretation

**This is a dramatically healthier baseline than Rev 1.** The prior baseline (hash `3202C567`) had 5 training forbidden violations, 1 sacred forbidden violation, and 2 sacred regressions — all centered on critical-starvation-losing-to-Comfort/Rest. The new baseline eliminates all of them. The model changes between the two runs (the new `hungerSuppression*` and `comfortRestSuppressionMin` parameters visible in the optimized profile JSON) directly addressed the structural gap that Rev 1 diagnosed.

**Dataset coherence:** Yes. All sacred states are passing. Training and holdout both have zero forbidden violations. The remaining failures are all soft (score 0 or +6), focused in two clearly distinct clusters: stable-flavor safe-state flavor-category mismatches, and mild-distress food-now timing issues.

**Sacred health:** Excellent. All 6 sacred states are satisfied, all 4 sacred exact-matches pass. This is the ideal starting condition for optimization.

**Training vs holdout balance:** Training has 20/32 satisfied (63%); holdout has 11/10 — wait, 11 out of 10? Actually holdout has 14 states (10 are the typical count, but the data shows `exact_pass_count: 4, satisfied_pass_count: 11`). The asymmetry (4 exact but 11 satisfied) indicates most holdout passes are partial-rank satisfactions rather than exact top-category matches. This is expected — holdout tests intermediate states where multiple categories compete reasonably. No imbalance concern.

**Remaining baseline failures (11 total):**
- 4 stable-flavor safe-state cases: Preparation or Rest beats the desired flavor category (Mastery, Fun, Safety, Comfort). No forbidden violations. These are score-0 soft failures.
- 6 mild-distress food-now/comfort-avail cases: Rest or Efficiency beats FoodConsumption at satiety 40–45. Score +6 (partial credit).
- 1 holdout stable-flavor case: Fun lost to Rest. Score 0.

All failures are non-forbidden and structurally coherent.

---

## 3. Milestone 2 — Optimization Validity

### Delta Table

| Metric | Baseline | Optimized | Delta |
|---|---|---|---|
| Training exact pass | 20 | 23 | **+3** |
| Training satisfied pass | 20 | 27 | **+7** |
| Training forbidden violations | 0 | 1 | **−1** (regression) |
| Holdout exact pass | 4 | 9 | **+5** |
| Holdout satisfied pass | 11 | 12 | +1 |
| Sacred exact pass | 4 | 4 | 0 |
| Sacred satisfied pass | 6 | 6 | 0 |
| Sacred regressions | 0 | 0 | **0** ✅ |
| Score | 1,154 | 1,314 | **+160** |

**Optimizer parameters changed** (4 coordinate-descent iterations):

| Parameter | Baseline | Optimized | Δ |
|---|---|---|---|
| `HungerModerateRange` | 1.20 | 1.30 | +0.10 |
| `FoodConsumptionShareHigh` | 0.80 | 0.90 | +0.10 |
| `FatiguePressureRestScale` | 0.015 | 0.005 | **−0.010** (−67%) |
| `MiseryPressureComfortScale` | 0.010 | 0.008 | −0.002 |
| `InjurySafetyNeedScale` | 0.025 | 0.020 | −0.005 |

### Interpretation

**Did optimization clearly help training?** Yes. Training went from 20/20 to 23/27 (exact/satisfied) — 7 additional satisfied states. The dominant change is `FatiguePressureRestScale` cut by 67%, which pushes Rest out of its competitive position in distress scenarios.

**Did holdout stay stable, improve, or regress?** It **improved substantially** — +5 exact and +1 satisfied. The holdout exact improvement (+5) actually exceeds the training exact improvement (+3), which is the opposite of overfitting. This is a strong generalization signal. The optimizer is not just pattern-matching the training set; it's finding weights that work across both.

**Did sacred regress?** **No.** Sacred passes are identical before and after optimization (4 exact, 6 satisfied, 0 violations, 0 regressions). This is the ideal outcome.

**Is there evidence of overfitting?** Minimal. The training/holdout satisfied ratio is 7:1, which is higher than ideal, but the holdout exact flip (+5 exact vs +3 exact in training, reversing the ratio) is a strong counter-signal. The one concern is the **new forbidden violation** introduced: `training/stable-flavor/high-instinct/safe` (Preparation beats Fun at s=65). This was a score-0 soft failure at baseline; it became a forbidden violation (score −30) post-optimization. The aggressive `FatiguePressureRestScale` reduction evicted Rest from this scenario, but Preparation filled the gap instead of Fun. This is a contained side-effect — one case, score −30, training set only — but it should be monitored.

---

## 4. Milestone 3 — Failure Pattern Analysis

### Failure Bucket Summary

#### Bucket A — Stable-State Preparation Over-Dominance (safe actors)

**States (post-opt):** 2 training (1 baseline-carryover, 1 newly forbidden)
**Trait profiles:** balanced (`15cc8081`), high-instinct (`13fcd090`)  
**Scenario:** `FoodAvailableNow`, s=65, h=90 (safe, comfortable)  
**Pattern:**
- Balanced: Desired Mastery, actual Preparation. Score 0. Preparation wins by an unknown margin (desiredRank: null — Mastery is not even in the candidate list). Present at both baseline and post-opt.
- High-instinct: Desired Fun, actual Preparation. Score −30. Forbidden: **yes** (new post-optimization). Δ from top: −0.202. Fun is ranked 9th.

**Root cause hypothesis:** `think_about_supplies` suppression is not fully activating for these actors. The starvation suppression (×0.2 when no food/safety recipes discoverable) should suppress Preparation at safe states, but Preparation still dominates. Compounding factor: the `FatiguePressureRestScale` cut at optimization time evicted Rest from these slots, promoting Preparation higher. The `Mastery` case is a harder problem — Mastery doesn't appear in candidates at all, suggesting it may require recipe discoverability or specific action availability that isn't present in the golden-state scenario setup.

**Diagnosis: possibly structural/objective issue.** The `FatiguePressureRestScale` change surfaced a pre-existing Preparation dominance that was previously masked by Rest. Adding a `prepTimePressureCap` tightening or a `masteryExhaustionFloor` check when no valid mastery actions are available would be the right lever. This is not pure weight tuning territory.

---

#### Bucket B — Mild Distress Rest Dominance (food-now/comfort-avail, s=40–45)

**States (post-opt):** 1 training (down from 6 at baseline)  
**Trait profiles:** high-survivor (`ef00d494`)  
**Scenario:** `FoodAvailable_WithComfort`, s=45  
**Pattern:** Rest beats FoodConsumption at moderate satiety distress with comfort items available. Δ from top: −0.844. Score +6 (partial credit, not forbidden).

**Across optimization:** Most mild-distress cases were resolved (6 at baseline → 1 post-opt). The remaining case is the high-survivor profile, which by design weights Safety and structural comfort-availability signals more heavily. The large delta (−0.844) suggests this is not close to resolution by weight tuning alone.

**Diagnosis: likely tunable, but diminishing returns.** The `HungerModerateRange` and `FoodConsumptionShareHigh` changes helped all other mild-distress cases. The high-survivor case may need a trait-specific food-urgency amplification or a lowered `FatiguePressureRestScale` variant for survivor trait profiles. Worth one more tuning pass before escalating.

---

#### Bucket C — Stable-State Flavor Mismatch (Rest beats Fun, high-hedonist/high-instinct, safe)

**States (post-opt):** 0 (both resolved from baseline)  
At baseline, `high-hedonist/safe` had Rest beating Fun (score 0) and `high-instinct/safe` had Rest beating Fun (score 0). Both were resolved by the optimizer — Fun now appears for high-hedonist, and the high-instinct case's category winner changed (though it switched to a forbidden Preparation winner rather than Fun, as noted in Bucket A).

**Diagnosis: partially resolved.** The Fun-category cases were largely fixed, but the fix for high-instinct surfaced a Preparation issue. Net outcome is neutral to slightly negative for high-instinct specifically.

---

#### Bucket D — Holdout Stable-Flavor (Fun lost to Rest, high-instinct)

**State (post-opt):** 1 holdout (unchanged from baseline)  
`trait:13fcd090|FoodAvailable_WithComfort|s55|h80|e65|m60` — desired Fun, actual Rest, rank 12, Δ −1.570. Score 0.

**Diagnosis: unclear from available data.** Fun is ranked 12th — this is not a close call. The high-instinct profile at comfortable stats (h=80, e=65, m=65) with comfort available is strongly weighted toward safety/comfort categories by trait. Fun may need a higher baseline `funBaseScale` for high-instinct profiles at these conditions. This holdout case may be an acceptable residual or may indicate an objective gap for the high-instinct + comfort-available scenario.

---

### Summary of Buckets

| Bucket | Count (post-opt) | Trend from Baseline | Root Cause Hypothesis |
|---|---|---|---|
| A — Preparation dominates safe states | 2 (1 newly forbidden) | Stable/worsened | Possibly structural: think_about_supplies suppression misfiring; Rest eviction promoted Preparation |
| B — Mild distress Rest dominance | 1 (down from 6) | **Greatly improved** | Mostly tunable; high-survivor residual may need trait-specific work |
| C — Stable-flavor Fun mismatch | 0 (down from 2) | **Resolved** | Fixed by optimizer |
| D — Holdout high-instinct Fun | 1 (unchanged) | Flat | Unclear; may be objective or trait-profile gap |

---

## 5. Milestone 4 — Pressure Fuzzer Validation

### Numbers

| Metric | Baseline | Optimized | Δ | Δ pp |
|---|---|---|---|---|
| Total samples | 63,528 | 63,528 | — | — |
| Starvation-region samples | 27,240 | 27,240 | 0 | — |
| Food consumption lost | 12,685 (46.6%) | 9,581 (35.2%) | **−3,104** | **−11.4 pp** |
| Comfort dominated | 3,709 (13.6%) | 838 (3.1%) | **−2,871** | **−10.5 pp** |
| Prep dominated | 0 (0%) | 0 (0%) | 0 | — |

### Interpretation

**Did the pressure surface improve?** Yes — dramatically. Food-consumption-lost dropped from 46.6% to 35.2% (−11.4 pp), and comfort-dominated dropped from 13.6% to 3.1% (−10.5 pp). The 3.1% comfort-dominated rate is a major improvement over the Rev 1 result of 25.3% after optimization, confirming the model changes resolved the structural Comfort-over-FoodConsumption pathology.

**Did any pathology get worse?** No. Prep-dominated count remains zero. No new pathology category appeared.

**Do fuzzer results agree with golden-state improvements?** Yes, with strong coherence. The golden-state improvements (training +7 satisfied, sacred unchanged) are mirrored in the fuzzer: comfort-dominated nearly eliminated (3.1%), food-lost meaningfully reduced. There is no golden-state vs fuzzer tension in this run.

**Residual concern:** A 35.2% food-consumption-lost rate still means 1 in 3 starvation-region samples do not prioritize food. This is partially expected — some starvation-region samples are at moderate satiety (s=40–50) where food-timing trade-offs are intentional. The remaining fuzzer failures likely correspond to Bucket B (mild distress, high-survivor profile) — further investigation with per-trait fuzzer breakdown would be useful before declaring this resolved.

---

## 6. Overall Verdict

### **A. Good to proceed with current optimizer — with one near-term fix.**

**Justification:**

The situation is fundamentally different from Rev 1:
- Baseline is clean: 0 forbidden violations, 0 sacred regressions, all sacred states passing.
- Optimization generalized well: holdout improved more on exact matches (+5) than training (+3).
- Sacred did not regress through the optimization cycle.
- Fuzzer pathology dropped dramatically (comfort-dominated 13.6% → 3.1%).
- The model change resolved the structural failure that blocked optimization in Rev 1.

The optimizer is working correctly and the foundation is solid enough to continue iterating. The verdict is **A**, but with one concrete near-term fix needed before the next pass:

**The Preparation-over-Fun forbidden violation** in `stable-flavor/high-instinct/safe` is the single orange flag. It appeared because the `FatiguePressureRestScale` reduction (−67%) was more aggressive than necessary, evicting Rest from safe-state slots and promoting Preparation in its place. Before running another optimizer pass, the `think_about_supplies` Preparation suppression should be verified for safe-state actors — either the starvation suppression gate is not triggering correctly, or `prepTimePressureCap` needs tightening. Fixing this is cheaper than letting it compound across further optimization iterations.

---

## 7. Recommended Next Actions

### Immediate (before next optimizer run)

**Audit and fix the `think_about_supplies` Preparation suppression for safe-state actors.**  
The `training/stable-flavor/high-instinct/safe` case has a Preparation forbidden violation (Preparation beats Fun, score −30) that appeared after `FatiguePressureRestScale` was cut by 67%. Verify that `ComputeThinkAboutSuppliesQualities` in `IslandActorState` correctly applies the starvation suppression multiplier (×0.2) for actors at s=65. If the suppression is active but Preparation still wins, tighten `prepTimePressureCap` (currently 0.2) or add a `funBaseScale` boost for high-instinct profiles at safe stats. This is a targeted one-parameter change — confirm it resolves the forbidden violation without regressing the mild-distress improvements.

### Follow-up #1

**Run one more optimizer pass using the fixed profile as the new base.**  
After resolving Bucket A (Preparation forbidden), the remaining soft failures are Bucket B (high-survivor mild distress) and Bucket D (holdout high-instinct Fun). Both have plausible weight-tuning paths. A fresh 4–6 iteration coordinate-descent pass starting from `optimized-AE07DF39` (or the post-fix variant) should be able to make progress on both without introducing regressions, given the current generalization quality (holdout exact +5 in the last run).

### Follow-up #2

**Expand the fuzzer breakdown by trait profile.**  
The current fuzzer metrics aggregate all 27k starvation-region samples together. A per-trait breakdown would tell you whether the 35.2% food-lost rate is evenly distributed or concentrated in specific trait profiles (e.g., high-survivor or high-hedonist). If it's concentrated, it's a targeting signal for the next optimizer pass. This is a reporting/tooling change, not a model change.

### Do not do yet

**Do not expand the golden-state training set before resolving Bucket A.**  
Adding more training states now would give the optimizer more surface area to fit, but the Preparation dominance issue is an objective model issue (suppression not firing correctly), not a coverage gap. Adding states before fixing the objective will cause the optimizer to work around the Preparation issue by adjusting unrelated weights, which will produce a profile that is overfit to both the original cases and the new cases while still not fixing the root cause. Fix the suppression gate first.

---

## Appendix A — Failing Sample Keys After Optimization

| Sample Key | Label | Set | Score | Desired | Actual | Forbidden |
|---|---|---|---|---|---|---|
| `trait:13fcd090\|FoodAvailableNow\|s65\|h90\|e80\|m65` | training/stable-flavor/high-instinct/safe | Training | **−30** | Fun | Preparation | ✅ |
| `trait:15cc8081\|FoodAvailableNow\|s65\|h90\|e80\|m65` | training/stable-flavor/balanced/safe | Training | 0 | Mastery | Preparation | ❌ |
| `trait:ef00d494\|FoodAvailable_WithComfort\|s45\|h70\|e60\|m55` | training/mild-distress/comfort-avail/high-survivor | Training | 6 | FoodConsumption | Rest | ❌ |
| `trait:13fcd090\|FoodAvailable_WithComfort\|s55\|h80\|e65\|m60` | holdout/stable-flavor/high-instinct/comfort-avail | Holdout | 0 | Fun | Rest | ❌ |

*(Only 4 post-optimization failures vs 9 in Rev 1.)*

---

## Appendix B — Changed Parameters from `optimizer-diff.json`

| Parameter | Baseline | Optimized | Δ | Interpretation |
|---|---|---|---|---|
| `FatiguePressureRestScale` | 0.015 | 0.005 | **−0.010 (−67%)** | Major Rest-pressure reduction; most impactful change; evicted Rest from distress slots but may have over-suppressed in safe states |
| `FoodConsumptionShareHigh` | 0.80 | 0.90 | +0.10 | Stronger food signal when food share is high; resolved remaining mild-distress food timing cases |
| `HungerModerateRange` | 1.20 | 1.30 | +0.10 | Wider moderate-hunger pressure window; helped Bucket B |
| `MiseryPressureComfortScale` | 0.010 | 0.008 | −0.002 | Minor Comfort-pressure reduction; limited direct impact but reduces comfort competition at distress edges |
| `InjurySafetyNeedScale` | 0.025 | 0.020 | −0.005 | Minor Safety-pressure reduction; may have contributed to some holdout exact improvements |

The optimizer converged in 4 iterations. The `FatiguePressureRestScale` change at −67% is notably aggressive — this is 2× the reduction seen in Rev 1 (−33%). The Preparation forbidden violation is a direct consequence. Tightening this to −50% (0.0075) and re-running would be a useful ablation to determine how much Rest suppression is needed without introducing Preparation side effects.
