# SimRunner Optimization Pipeline — Milestone Pre-Analysis

**Generated:** 2026-03-27  
**Purpose:** Decision-useful review for Derek + ChatGPT before selecting the next optimization phase.

---

## 1. Artifact Provenance

| Pipeline Stage | Workflow Name | Run ID | Artifact Name | Head SHA |
|---|---|---|---|---|
| Baseline Evaluation | SimRunner — Baseline Evaluation | `23664187091` (run #1) | `simrunner-baseline` | `88c9c0ca` |
| Optimizer | SimRunner — Optimize Golden | `23665498861` (run #2) | `simrunner-optimizer` | `c45b00e1` |
| Optimized Evaluation | SimRunner — Evaluate Optimized Profile | `23665539358` (run #4) | `simrunner-optimized-eval` | `c45b00e1` |
| Pressure Fuzzer | SimRunner — Pressure Fuzzer Comparison | `23665936934` (run #1) | `simrunner-fuzzer` | `c45b00e1` |

**Notes on run consistency:**
- The baseline ran on commit `88c9c0ca` (PR #102 merge); the optimizer, optimized eval, and fuzzer all ran on the subsequent `c45b00e1` (PR #103 merge). The baseline profile `ProductionDefault` (hash `3202C567`) is correctly referenced by both the optimizer comparison and optimized eval as the baseline, confirming cross-run consistency despite the one-commit gap.
- All four expected artifact sets were present. No missing files.

**Provenance note:** The baseline run was on an earlier commit than the other three runs. The golden-state dataset and `DecisionTuningProfile.Default` are embedded resources and were unchanged between the two commits (PR #103 only updated a workflow file to surface run IDs). The `ProductionDefault` profile hash `3202C567` matching across both the baseline run and the optimizer comparison confirms data consistency.

**Files used:**
- `artifacts/baseline/eval-summary.json`, `artifacts/baseline/failures.json`
- `artifacts/optimizer/optimizer-comparison.json`, `artifacts/optimizer/optimizer-diff.json`, `artifacts/optimizer/optimizer-result.json`
- `artifacts/optimized/eval-summary.json`, `artifacts/optimized/failures.json`
- `artifacts/fuzzer/comparison-summary.json`

---

## 2. Milestone 1 — Baseline Contract Sanity

### Numbers

| Set | Exact Pass | Satisfied Pass | Forbidden Violations |
|---|---|---|---|
| Training | 12 | 12 | 5 |
| Holdout | 4 | 10 | 0 |
| Sacred | 2 | 4 | 1 |

**Sacred regressions (baseline):** 2 of 4 sacred states failing.

1. `trait:ca4e875a|FoodAvailable_WithComfort|s10|h70|e50|m50` — *sacred/food-beats-comfort/comfort-leaning/critical*  
   Desired: FoodConsumption | Actual: **Rest** | Forbidden: **yes** | Rank: 3 | Δ from top: −0.885
2. `trait:74ae3b4d|FoodAvailableNow|s5|h60|e40|m35` — *sacred/starvation-no-fun/high-hedonist/extreme*  
   Desired: FoodConsumption | Actual: **Rest** | Forbidden: no | Rank: 2 | Δ from top: −0.303

### Interpretation

**Dataset coherence:** The baseline is coherent — it correctly reflects a system that has food urgency weighted but not strongly enough to overcome Rest in high-satiety-pressure-with-comfort scenarios. The 5 training forbidden violations and 1 sacred forbidden violation are all in the same structural bucket (food-pressure + Comfort/Rest competition), which suggests a single tunable cluster rather than noise.

**Sacred health:** Two out of four sacred states failing is meaningful but not catastrophic. The forbidden violation on the comfort-leaning critical case is the primary concern — the system chooses Rest even at satiety=10, which directly contradicts the core survival contract. The hedonist extreme case is less alarming (rank 2, no forbidden).

**Training vs holdout balance:** Training has a high forbidden rate (5/48 training states failing with forbidden violations); holdout has zero forbidden violations. This asymmetry suggests the forbidden constraints are clustered in the training scenarios and the holdout set tests different edge cases. It is not an obvious imbalance — holdout having a 0% forbidden violation rate is expected if holdout probes stable/intermediate states rather than critical pressure states.

---

## 3. Milestone 2 — Optimization Validity

### Delta Table

| Metric | Baseline | Optimized | Delta |
|---|---|---|---|
| Training exact pass | 12 | 19 | **+7** |
| Training satisfied pass | 12 | 23 | **+11** |
| Holdout exact pass | 4 | 5 | +1 |
| Holdout satisfied pass | 10 | 11 | +1 |
| Sacred exact pass | 2 | 3 | +1 |
| Sacred satisfied pass | 4 | 5 | +1 |
| Sacred forbidden violations | 1 | 1 | 0 |
| Sacred regressions | 2 | 1 | −1 |
| Score | 440 | 789 | **+349** |

**Optimizer parameters changed** (3 coordinate-descent iterations):

| Parameter | Baseline | Optimized | Δ |
|---|---|---|---|
| `HungerMildMax` | 0.30 | 0.35 | +0.05 |
| `HungerModerateRange` | 1.20 | 1.30 | +0.10 |
| `FoodConsumptionShareHigh` | 0.80 | 0.90 | +0.10 |
| `FatiguePressureRestScale` | 0.015 | 0.010 | −0.005 |

### Interpretation

**Did optimization help training?** Yes — clearly. Training went from 12/12 to 19/23 (exact/satisfied). Reducing `FatiguePressureRestScale` (−33%) and amplifying `FoodConsumptionShareHigh` (+12.5%) moved many Rest-beats-FoodConsumption failures into correct territory. The score jump of +349 is large.

**Did holdout stay stable?** Yes, marginally improving (+1 exact, +1 satisfied). No new holdout failures were introduced and no holdout forbidden violations emerged. This is a good sign, but a holdout improvement of only +1 versus training improvement of +11 (ratio 11:1) is a strong **overfitting signal**. The optimizer is fitting the 32 training states much better than it is generalizing.

**Did sacred regress?** The net sacred regression count went from 2 to 1 — one case was resolved (`starvation-no-fun/high-hedonist/extreme`), but the most critical sacred case (`food-beats-comfort/comfort-leaning/critical`, forbidden violation) **persists**. The nature of the failure shifted (baseline chose Rest; optimized chooses **Comfort** instead — a different wrong answer), meaning the increased `FoodConsumptionShareHigh` dislodged Rest but Comfort filled the gap. This sacred case has not improved substantively; the delta from top improved from −0.885 to −0.38, but it still violates the forbidden constraint.

**Is there evidence of overfitting?** Yes. The 11:1 training/holdout satisfied improvement ratio is the primary signal. A second signal: a **new forbidden violation appeared** in the optimized set — `training/stable-flavor/high-instinct/safe` now has Preparation beating Fun with a forbidden constraint triggered. This case was a non-forbidden score-0 failure in baseline; the optimizer introduced a regression at score −30 in a safe-state flavor scenario that was not in the pressure zone being tuned. This is a classic weight-shift side effect.

---

## 4. Milestone 3 — Failure Pattern Analysis

### Failure Bucket Summary

#### Bucket A — Critical Starvation Losing to Comfort / Rest (`FoodAvailable_WithComfort`, s=10)

**States:** 4 training + 1 sacred  
**Trait profiles:** comfort-leaning (`ca4e875a`), balanced (`15cc8081`), high-hedonist (`74ae3b4d`), high-planner (`f34d05d4`)  
**Scenario:** `FoodAvailable_WithComfort`  
**Satiety:** 10 (critical)  
**Pattern:** All four trait profiles choose Rest or Comfort over FoodConsumption when Comfort actions are available, even at critical satiety. Delta from top ranges from −0.16 to −0.38.

**Across all runs:** This bucket was present at baseline (5 failures) and is still present post-optimization (4–5 failures), with the sacred violation unresolved. The optimizer reduced Rest wins but shifted some to Comfort wins — a lateral move, not a fix.

**Diagnosis: likely objective issue.** The problem is not that the weights are slightly off — it's that the scoring model allows Comfort to compete with FoodConsumption at satiety=10 (critical). No amount of `FoodConsumptionShareHigh` tuning will reliably suppress this if the comfort quality signal is structurally comparable. This points to a missing **starvation suppression gate** on Comfort/Rest categories: when satiety is below a critical threshold, Comfort and Rest should have their quality scores floored down (or FoodConsumption should have a floor-up multiplier).

---

#### Bucket B — Starvation with No Immediate Food (`FoodAvailableNow`, s=5–15, extreme profiles)

**States:** 2 baseline training + 1 baseline sacred (partially resolved in optimized)  
**Trait profiles:** high-hedonist (`74ae3b4d`), balanced (`15cc8081`), comfort-leaning (`ca4e875a`)  
**Scenario:** `FoodAvailableNow`  
**Pattern:** Rest beats FoodConsumption even at extreme satiety pressure (s=5–15). In the optimized run, most of these resolved (the `FoodConsumptionShareHigh` increase helped), but the comfort-leaning boundary case (`s=15`, `ca4e875a`) still fails with a forbidden violation at score −39 (baseline) → resolved in optimized.

**Diagnosis: likely tunable by weights.** These were mostly resolved by the optimizer in this run. Remaining `s=15` edge cases may respond to further `HungerMildMax` or `HungerModerateRange` tuning.

---

#### Bucket C — Mild Distress / Rest Dominance (`FoodAvailableNow`, s=40–45)

**States:** 2–3 training + 1 holdout  
**Trait profiles:** balanced (`15cc8081`), high-survivor (`ef00d494`)  
**Scenario:** `FoodAvailableNow` and `FoodAvailable_WithComfort`  
**Pattern:** At moderate satiety distress (s=40–45), Rest still outcompetes FoodConsumption. Deltas are −0.08 to −1.44. These are not forbidden violations, so they score +6 (partial credit), but they remain unsatisfied.

**Diagnosis: likely tunable by weights.** The moderate-pressure zone (`HungerModerateRange`) was already nudged and partially helped; further tuning of the ramp parameters should address these.

---

#### Bucket D — Stable-State Preparation Over-Dominance (safe actors)

**States:** 2 training  
**Trait profiles:** balanced (`15cc8081`), high-instinct (`13fcd090`)  
**Scenario:** `FoodAvailableNow`, s=65, h=90 (safe/comfortable)  
**Pattern:** Desired Mastery (balanced) and Fun (high-instinct) are beaten by Preparation. In the optimized run, the high-instinct case now **violates a forbidden constraint** (Preparation beats Fun, score −30) — this was not a forbidden violation at baseline.

**Diagnosis: maybe structural/action-logic issue.** The `think_about_supplies` suppression (starvation suppression ×0.2 when no food/safety recipes are discoverable) is supposed to suppress Preparation at safe states. If Preparation is rising in safe states after the optimizer tuning, the `prepTimePressureCap` or `prepStarvationFloor` may need adjustment, or the `think_about_supplies` quality model is not being correctly suppressed. This is worth investigating before further weight tuning.

---

#### Bucket E — HighHedonist Fun Preservation (stable-flavor, high-hedonist, safe state)

**States:** 1 training (baseline only; resolved in optimized)  
**Trait profile:** high-hedonist (`74ae3b4d`)  
**Pattern:** At baseline, Rest beat Fun for a safe-state high-hedonist. Resolved post-optimization.

---

### Summary of Buckets

| Bucket | Count (post-opt) | Root Cause Hypothesis |
|---|---|---|
| A — Critical starvation losing to Comfort/Rest | 5 (unchanged) | Objective issue: no starvation suppression gate on Comfort |
| B — Extreme starvation, food now | ~0 (resolved) | Tunable; largely fixed |
| C — Mild distress, food now | 2–3 | Tunable; needs further ramp work |
| D — Preparation dominates safe states | 2 (+new forbidden) | Possibly structural: think_about_supplies suppression misfiring |
| E — HighHedonist Fun preservation | 0 (resolved) | Tunable; fixed |

---

## 5. Milestone 4 — Pressure Fuzzer Validation

### Numbers

| Metric | Baseline | Optimized | Δ | Δ% |
|---|---|---|---|---|
| Total samples | 63,528 | 63,528 | — | — |
| Starvation-region samples | 27,240 | 27,240 | 0 | — |
| Food consumption lost count | 17,189 | 16,067 | **−1,122** | **−4.1 pp** |
| Comfort dominated count | 7,770 | 6,888 | **−882** | **−3.2 pp** |
| Prep dominated count | 0 | 0 | 0 | — |

### Interpretation

**Did the pressure surface improve?** Yes — the optimization produced a measurable and consistent improvement across both metrics. The food-consumption-lost rate dropped from 63.1% to 59.0% (−4.1 percentage points) and the comfort-dominated rate dropped from 28.5% to 25.3% (−3.2 pp). These are real improvements at scale across 27k starvation-pressure samples.

**Did any pathology get worse?** No. There are zero regressions in the fuzzer metrics — prep-dominated count was and remains zero; no new pathology class appeared.

**Do fuzzer results agree with golden-state improvements?** Broadly yes: the golden-state optimizer resolved most starvation/extreme failures (Buckets B and E), and the fuzzer confirms the comfort-dominated rate fell. The directional agreement is clean.

**Tension / concern:** The fuzzer improvement is real but the comfort-dominated rate is still **25.3%** — more than 1 in 4 starvation-region samples end up comfort-dominated. This directly corresponds to Bucket A (critical starvation losing to Comfort) which the optimizer did not structurally fix. The golden-state failures in Bucket A are the "tip of the iceberg" for this 25% fuzzer rate. Tuning `FoodConsumptionShareHigh` further will have diminishing returns; what's needed is a categorical suppression mechanism.

---

## 6. Overall Verdict

### **B. Good foundation, but objective refinement should be next.**

**Justification:**

The optimizer is functioning correctly and produced a legitimate +349 score improvement, resolved several starvation failures, and improved the fuzzer pressure surface. The coordinate-descent mechanism is working.

However, three signals argue against continuing pure weight tuning as the next phase:

1. **Sacred violation persists** after 3 iterations. The `food-beats-comfort/comfort-leaning/critical` case is the canary — it has not been fixed, only shifted (Rest → Comfort). This is diagnostic of a missing suppression mechanism, not a miscalibrated weight.

2. **Overfitting is already visible.** The 11:1 training/holdout improvement ratio means the optimizer is fitting known patterns well but will not generalize cleanly. The stable-flavor high-instinct regression (Bucket D, new forbidden violation) is a concrete side-effect of this overfitting.

3. **25% comfort-dominated rate in the fuzzer** is structurally too high. The optimizer moved the needle by ~3 pp per run. Getting below 20% by weight tuning alone would require many more iterations, each with increasing regression risk.

The right next phase is to fix the **objective/scoring model** for the critical-starvation scenario (add a starvation suppression gate on Comfort and Rest), then re-evaluate. That change will unlock clean weight optimization that actually generalizes.

---

## 7. Recommended Next Actions

### Immediate (do this before the next optimizer run)

**Add a critical-satiety suppression multiplier to Comfort and Rest quality scoring.**  
When satiety is below a configurable threshold (suggested: ≤15–20, matching the existing `starvatingSatietyThreshold`), multiply Comfort and Rest quality outputs by a suppression factor (e.g., ×0.3–0.5). This directly targets Bucket A (all 5 critical-starvation failures) and should resolve the sacred forbidden violation without side effects on safe-state scenarios. This is a single-point change in `IslandDomainPack` quality model or `DecisionTuningProfile` (a new `comfort/rest starvation suppression` parameter). Verify immediately against the golden-state eval.

### Follow-up #1

**Investigate the `think_about_supplies` suppression regression (Bucket D).**  
The stable-flavor/high-instinct case gained a forbidden violation post-optimization. Before running more optimizer iterations, confirm whether the `starvationSuppression` gate in `ComputeThinkAboutSuppliesQualities` is correctly activating for safe-state actors. If `FoodConsumptionShareHigh` indirectly shifted the category ranking in a way that makes Preparation appear above Fun even when suppression is active, this is an objective model bug that will keep recurring.

### Follow-up #2

**Expand the holdout set to include more comfort-trap critical scenarios across trait profiles.**  
The current holdout has zero forbidden violations and improved by only +1 across all changes. This means the holdout is not testing the failure modes that matter most. Adding 4–6 holdout states covering the FoodAvailable_WithComfort × critical-satiety cluster will make the overfitting signal clearer and the optimizer's generalization measurable.

### Do not do yet

**Do not increase optimizer iterations or try a new base profile until the objective fix is in.**  
Running more coordinate-descent passes without fixing the Comfort/Rest suppression gap will produce progressively larger training improvements with proportionally smaller holdout improvements (the overfitting trend is already established). Any new profile produced before the objective fix will embed the same structural failure and the sacred regression will persist.

---

## Appendix A — Top 10 Failing Sample Keys After Optimization

| Rank | Sample Key | Label | Set | Score | Desired | Actual | Forbidden |
|---|---|---|---|---|---|---|---|
| 1 | `trait:ca4e875a\|FoodAvailable_WithComfort\|s10\|h70\|e50\|m50` | sacred/food-beats-comfort/comfort-leaning/critical | Sacred | −65 | FoodConsumption | Comfort | ✅ |
| 2 | `trait:74ae3b4d\|FoodAvailable_WithComfort\|s10\|h70\|e50\|m50` | training/comfort-trap/high-hedonist/critical | Training | −65 | FoodConsumption | Comfort | ✅ |
| 3 | `trait:15cc8081\|FoodAvailable_WithComfort\|s10\|h70\|e50\|m50` | training/comfort-trap/balanced/critical | Training | −52 | FoodConsumption | Rest | ✅ |
| 4 | `trait:f34d05d4\|FoodAvailable_WithComfort\|s10\|h70\|e50\|m50` | training/comfort-trap/high-planner/critical | Training | −52 | FoodConsumption | Rest | ✅ |
| 5 | `trait:13fcd090\|FoodAvailableNow\|s65\|h90\|e80\|m65` | training/stable-flavor/high-instinct/safe | Training | −30 | Fun | Preparation | ✅ (NEW) |
| 6 | `trait:15cc8081\|FoodAvailableNow\|s65\|h90\|e80\|m65` | training/stable-flavor/balanced/safe | Training | 0 | Mastery | Preparation | ❌ |
| 7 | `trait:15cc8081\|FoodAvailableNow\|s45\|h70\|e60\|m55` | training/mild-distress/food-now/balanced | Training | 6 | FoodConsumption | Rest | ❌ |
| 8 | `trait:ef00d494\|FoodAvailable_WithComfort\|s45\|h70\|e60\|m55` | training/mild-distress/comfort-avail/high-survivor | Training | 6 | FoodConsumption | Rest | ❌ |
| 9 | `trait:ef00d494\|FoodAvailable_WithComfort\|s15\|h60\|e50\|m40` | holdout/boundary/comfort-avail/high-survivor/s15 | Holdout | 6 | FoodConsumption | Rest | ❌ |

*(Ranked by score ascending; "NEW" = regression introduced by optimization.)*

---

## Appendix B — Top Changed Parameters from `optimizer-diff.json`

| Parameter | Baseline | Optimized | Δ | Interpretation |
|---|---|---|---|---|
| `FoodConsumptionShareHigh` | 0.80 | 0.90 | +0.10 | Stronger food signal when food share is high — helped Buckets B/E |
| `HungerModerateRange` | 1.20 | 1.30 | +0.10 | Wider moderate-hunger pressure window — helped Bucket C |
| `HungerMildMax` | 0.30 | 0.35 | +0.05 | Raised ceiling for mild-hunger signaling — helped Bucket B |
| `FatiguePressureRestScale` | 0.015 | 0.010 | −0.005 | Reduced fatigue→Rest pressure by 33% — the single most impactful change; resolved multiple Rest-beats-FoodConsumption failures |

All 4 changes are directionally consistent with suppressing the Rest category's competitive advantage in food-pressure scenarios. The optimizer converged quickly (3 iterations) on a coherent local optimum. The plateau is meaningful — further improvement in this parameter space requires changing the objective, not just the parameters.
