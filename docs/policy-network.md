# P5.2 Policy Network and SO-ISMCTS PUCT

## Reproduction

```powershell
python tools/Policy/train.py work/self-play-cross-1000 work/policy-v1 --ood work/value-ood --epochs 12
.\scripts\check-policy.ps1
.\scripts\probe-policy.ps1
$env:TOUCHFISH_POLICY_MODEL='work/policy-v1/policy-v1.bin'
.\scripts\value-arena.ps1 -Opponent freeze -Count 20 -Iterations 100 -Modes 'Heuristic,Puct,PuctNeural' -Output work/policy-arena/freeze20
$env:TOUCHFISH_VALUE_ARENA_MS='45'
.\scripts\value-arena.ps1 -Opponent freeze -Begin 100 -Count 8 -Iterations 10000 -Modes 'Heuristic,Puct,PuctNeural' -Output work/policy-arena/time
Remove-Item Env:TOUCHFISH_VALUE_ARENA_MS
python tools/Policy/report.py work/policy-arena
```

Zoo and Miracle opponents use the same harness with -Count 8 -Iterations 32. The OOD comparison uses -Opponent ood -Count 12 -Iterations 32 and the P5.1 board Mage / Zoo variant Deckstrings via -EnemyDeck and -OwnDeck. Each experiment uses a fresh output directory per condition; existing game receipts are reused. Fixed-iteration results are seeded; the cooperative time cap depends on machine scheduling.

## Model and search

The native binary stores little-endian magic 0x31504c54, policy schema 1, P5.1 observation schema 1, action width 63 and hidden width 32. The remaining float32 arrays are the state and action projection weights, shared bias and output weights. The architecture scores one candidate with an additive state/action projection, ReLU and linear output. The state projection is evaluated once per candidate list. Legal actions are softmaxed with a 5% uniform mixture. The candidate schema includes action kind, source and target slots, side and zone, position, option index, hashed visible source identity and choice, and a mulligan subset mask. Unknown IDs hash without a vocabulary lookup. Card hashes can collide. The policy encoder and logits accept mulligan candidates; P5.0 supplied no root-visit targets for mulligans, and the existing opening search is unchanged. Dynamic candidates have runtime support, with synthetic coverage and no supervised examples in the fixed builds.

Pass the immutable instance from PolicyNetwork.TryLoad to ExtremeSearch(policyNetwork: model). The default or a null model uses the original UCT branch. A failed inference uses UCT for that selection. The node cache requires the same ordered legal keys and exact encoded root-player observation; no cross-search cache is used. Scoring only occurs at root-player decision nodes using Q + c * P * sqrt(action availability)/(1+visits). Unvisited Q is 0.5 and existing progressive widening gates new actions. Available actions accumulate credit only when legal in the sampled world. Every legal action receives positive prior, but finite budget and widening can still leave actions unvisited. Opponent behavior, rollout, determinization, terminal rewards and rules are unchanged.

## Target quality and offline results

The 18-position probe covers early, middle and late Zoo versus Freeze positions. Top action agreement was 8/18 for 16 vs 100 iterations, 9/18 for 16 vs 200, and 12/18 for 100 vs 200. Mean total variation between visit distributions was .273, .293 and .079 respectively. Independent candidate continuations used eight common P2 samples per position with the unchanged rollout policy and a 100-action cap. This is a noisy counterfactual check under a fixed continuation policy. We used the existing data as a weak baseline without adding high-budget training labels. The probe supports collecting a small, separately validated set of higher-budget labels before further model work.

The original 76,156 records are unchanged. Training uses normalized actual visits, no fallback or forced rows, and game-normalized weights. The archive preserves original visits and search failure fields. P5.0 did not export per-candidate availability, so those counts cannot be reconstructed; live benchmark traces retain real availability. Unvisited actions receive zero visit mass, without a fabricated quality estimate. Splits group both initiatives by seed. Usable rows were 47,961 train, 5,563 validation and 7,079 test. Test CE was 1.829 versus uniform 1.936, entropy 1.823 versus target entropy 1.597, and top-visit action agreement 30.5%. The separate OOD set had 2,939 usable rows, CE 1.766 versus uniform 1.835, entropy 1.756 and top agreement 31.8%. These measure imitation. Manifest hashes and both feature versions are in metrics.json.

## Ablation and play

| Matchup | Budget | Pairs | A UCT heuristic | B PUCT heuristic | C PUCT neural |
|---|---|---:|---:|---:|---:|
| Built-in Freeze Mage | 100 iterations | 20 | 8 | 6 | 3 |
| Built-in Freeze Mage, separate seeds | 45 ms cap | 8 | 1 | 2 | 1 |
| Built-in Zoo | 32 iterations | 8 | 1 | 3 | |
| Built-in Miracle | 32 iterations | 8 | 3 | 0 | |
| Zoo variant vs board Mage fixed Heuristic Extreme | 32 iterations | 12 | 11 | 11 | |

On the original first 20 Freeze seeds, A/B/C first-hand wins were 4/4/2 and second-hand wins 4/2/1. B gained one game and lost three relative to A (exact paired p=.625). C lost five and gained none (p=.0625). The pilot gave no reason to extend this model to the 100-game run; the previous 28/100 benchmark is context only. The 45 ms games averaged 37.44/36.96/37.04 ms over all decisions, including forced decisions. On 18 common positions, searches averaged 45.08/45.09 ms and 59.6/54.5 completed iterations for A/B. Their selected actions won 113/107 of 144 common sampled continuations, with 23/28 losses and 8/9 capped. No evidence of improved tactical discovery at equal time.

In the 20-game fixed-iteration test, A/B/C mean depth was 2.613/3.267/3.161 and mean top root share 26.3%/43.2%/45.2%. A/B mean visited root actions were 8.01/5.47 and effective root widths 7.76/4.41. B made 15,457 policy calls and used 151,049 cached priors; encoding, cache lookup and policy inference together accounted for 1.54 s of 86.96 s measured search time. The terminal iteration shares were 13.1%/12.8%/14.2%. The reports include full depth and expansion histograms, raw per-search diagnostics, root counts and per-seed results. All measured games completed with zero search fallbacks, sampling failures or illegal live actions.

The model learned weak but transferable preferences and concentrated visits. This concentration did not establish that the branches were better. The one OOD play matchup showed no drop, with too few games and too much baseline advantage to demonstrate robustness. Keep UCT default. Before increasing model size or budget, check the moves that were over-concentrated in the paired losses and test whether independently validated higher-budget targets improve equal-time decisions.

Policy parity, positive prior, dynamic actions, placements, distinct targets, unknown IDs, hidden substitution determinism and model-load fallback passed. Rule regression passed 19,510 checks. Production build and WPF appearance preview passed.
