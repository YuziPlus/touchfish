# P5.1 Extreme Value Network

## Reproduction

```powershell
python tools/Value/train.py work/self-play-cross-1000 work/value-v1 --device cpu --epochs 24
python tools/Value/train.py work/self-play-cross-1000 work/value-v1 --device cpu --epochs 30 --resume
python tools/Value/train.py work/self-play-cross-1000 work/value-v1 --device cpu --evaluate --ood work/value-ood
.\scripts\check-value.ps1 -Model work/value-v1
.\scripts\value-arena.ps1 -Opponent freeze -Count 4 -Iterations 100
```

Training used PyTorch 2.8.0, NumPy 2.3.3, CPU, seed 9051, batch 512, AdamW at 0.001 and game-normalized BCE. The training script stores the input manifest hashes, seed-based split list, epoch history, best validation weights and a resumable epoch checkpoint. Evaluation/export reloads the selected weights. The 1,000-game input was reused and revalidated. Paired initiative games share a split by seed. Train/validation/test had 792/98/110 games and 60,242/6,984/8,930 rows. Validation BCE was best at epoch 6 (0.3837); by epoch 24 it had risen to 0.4545 while train BCE fell to 0.2967.

## Schema and deployment

Feature schema 1 is a 293-vector: 9 root-relative context values; two ordered player blocks each with 16 hero/resource values and seven 13-value board slots; ten 7-value own-hand slots. Empty slots are zero, occupied slots carry a presence bit, numeric features are normalized and clipped to [-4,4]. Values above the standard slot counts are truncated. The encoder uses structural attributes (such as cost, card type, charge, secret, public combat attributes and statuses), not a fixed card vocabulary. It omits class and card identity, full history, initial deck composition and option text. Dynamic options contribute their visible count. This is intentionally lossy, especially for burn reach, card text, known resources and future effects. Unknown IDs need no embedding lookup.

Model v1 is little-endian: four int32 header entries (magic 0x31564c54, schema 1, input 293, hidden 128) followed by three float32 row-major weight matrices and corresponding bias vectors. Layer dimensions are 293/128/128/1 with ReLU on the hidden layers and sigmoid on the output. Native C# runs without Python, ONNX or a .NET upgrade.

Call `ValueNetwork.TryLoad(path, out diagnostic)` once when initializing the search owner, then pass the shared immutable instance and desired `LeafValueMode` into `ExtremeSearch`. Default is Heuristic. Neural substitutes only truncated evaluation. Shadow records network metrics and returns the original heuristic. Both score from `SearchView(rootSeat,false)`; real simulated terminals go directly through rule-engine rewards. The binary is an opt-in artifact and is not copied into the desktop release by build.ps1. No WPF UI was added.

## Offline evaluation

All values are game-weighted. Labels are terminal outcomes under the recorded self-play distribution, with draws mapped to 0.5. The heuristic already maps scores to [0.02,0.98]; its raw output is a bounded score. Logistic calibration is fit only on training games.

| Set | Predictor | Brier | Log Loss | AUC |
|---|---|---:|---:|---:|
| 110 known games | Constant | .2495 | .6922 | .5000 |
| | Heuristic raw | .1876 | .5559 | .7933 |
| | Heuristic calibrated | .1877 | .5553 | .7933 |
| | Neural | .1556 | .4762 | .8550 |
| 48 OOD games | Constant | .2489 | .6909 | .5000 |
| | Heuristic raw | .1666 | .5077 | .8372 |
| | Heuristic calibrated | .1680 | .5115 | .8372 |
| | Neural | .1661 | .4997 | .8403 |

OOD is 12 games per pairing, initiative paired, 3,611 samples. Configs under work/value-ood/configs store exact codes. The Zoo variant replaces two Flame Imps with two Chillwind Yetis. Mage and Rogue board decks are the legal in-engine training decks, distinct from Freeze and Miracle. Pairings are Zoo variant/Freeze, Zoo/Mage board, Miracle/Rogue board, Mage board/Rogue board. OOD data was generated after model selection and used only for evaluation. Metrics JSON includes ten-bin calibration, phase, matchup and class breakdowns. OOD neural Brier by class is Mage .170, Rogue .140, Warlock .195. The model uses no class label; public attributes can still correlate with deck archetypes. OOD retained a small aggregate edge, much smaller than on held-out known builds. These games are evidence of limited transfer, with a small and often one-sided OOD sample.

## Integration checks

32 Python/C# observations agree within 1e-6 per feature and 2e-6 per prediction. C# also matches the offline heuristic. Verified incompatible/missing file fallback, unknown ID handling, hidden card permutation, both root seats, terminal win rewards, and Shadow matching Heuristic root statistics. Four full ice-mage smoke games had identical Shadow and Heuristic action sequences and winners. Neural completed those same four seeds. Per-search predictions (count, range, sum), inference time, and terminal/truncated counts are in work/value-arena.

The native model is 217,108 bytes. 12,000 measured inferences including encoding averaged about 92–135 us with P95 102–154 us across unloaded/contended runs. Ten Gen0 collections occurred in the contended measurement; the managed retained delta was negative after forced collection, so it does not estimate per-inference allocation. Rule tests passed 19,510 checks. The production build and WPF appearance preview passed. The preview checks appearance only; native model loading and real Extreme inference were verified by the headless harness.

## Paired gameplay

| Opponent | Pairs | Heuristic wins (first/second) | Neural wins (first/second) | Loss to win / win to loss |
|---|---:|---:|---:|---:|
| Built-in Freeze Mage | 100 | 28 (15/13) | 26 (16/10) | 12 / 14 |
| Built-in Zoo | 8 | 1 (1/0) | 2 (1/1) | 2 / 1 |
| Built-in Miracle | 8 | 3 (2/1) | 2 (1/1) | 1 / 2 |
| OOD board Mage, fixed Heuristic Extreme; root Zoo variant | 12 | 11 (5/6) | 11 (6/5) | 1 / 1 |

The Freeze run used the original 100 game seeds, opening budget 32, root budget 100, and original initiative and per-shard search seed schedule. The other rows used 32 root iterations. The first seat count was half in each row. The stored paired CSVs link all changed outcomes to seeds. All games in these four rows finished without search fallback, sampling failure, or authoritative action rejection.

On the 100-game test, cumulative search was 617.7 s over 4,341 Heuristic decisions and 557.3 s over 3,715 Neural decisions (142.3/150.0 ms per decision). Six concurrent benchmark processes shared the machine. Different trajectories explain why total search time alone is not a speed comparison. Neural inference consumed 35.8 s over 278,257 calls (128.5 us per call including encoding and instrumentation). Neural returned 34,243 rule terminal evaluations (11.0% of completed iterations); Heuristic returned 38,155 (10.5%). The four-game Shadow comparison preserved all actions and winners while recording 14,069 predictions.

The known-build prediction improvement did not translate into a gameplay improvement. The bounded observational encoder discards card text and known reach, and the outcome targets come from 16-iteration self-play decisions; truncated simulated leaves may have a different distribution. The experiment does not isolate those effects from opponent behavior or search integration. OOD gameplay did not show an aggregate drop in the one 12-pair matchup, which is too small and lopsided to establish robustness. Heuristic remains the default.
