# Final candidate compared with the archived mmap prototype

[Run 36327591678](https://github.com/litedb-org/LiteDB/actions/runs/36327591678)
compares final `186cdeec6bbc76d949b77c726c54a492dbc6d3cf` with
`16131953114971557b44e9deaa78bd8ce9d45ab1`, a byte-identical library restoration of
archive `130b339523cbe61efcef52b5d67252aee5a6cb53` on the merged stack.

Each cell has five alternating production Release pairs, ten seconds warmup and
ten seconds measurement, on one hosted machine. These are saturated readers;
writers are unthrottled. All 360 records pass logical/index validation and final
WAL cleanup. Absolute values are medians, changes are means of paired percentages,
and brackets are exploratory 95% bootstrap intervals. They are different
statistics, especially with variable timing. Do not pool hosts or treat an
interval containing zero as proof of equivalence. Reader p99, allocation, CPU,
WAL and memory records remain in the raw bundle; additional latency metrics are
in [reader-comparisons.csv](reader-comparisons.csv).

## Idle

| Host / read shape / processes | Reader calls/s | Reader change | Writer txn/s | Writer change | Writer p99 change |
| --- | ---: | ---: | ---: | ---: | ---: |
| Linux / large / 1 | 3,720.59 → 3,725.36 | -0.80% [-1.77, +0.18] | — | — | — |
| Linux / large / 4 | 5,996.09 → 6,004.93 | +0.27% [-0.17, +0.67] | — | — | — |
| Linux / medium / 1 | 13,055.72 → 13,350.34 | +2.47% [+1.52, +3.26] | — | — | — |
| Linux / medium / 4 | 24,241.92 → 24,703.09 | +1.66% [+1.32, +1.94] | — | — | — |
| Linux / point / 1 | 82,592.92 → 103,594.22 | +25.58% [+24.58, +26.44] | — | — | — |
| Linux / point / 4 | 229,336.13 → 278,376.78 | +21.29% [+20.14, +22.27] | — | — | — |
| Windows / large / 1 | 1,352.53 → 1,402.49 | +1.38% [-5.60, +9.34] | — | — | — |
| Windows / large / 4 | 2,782.68 → 2,859.46 | +6.98% [+0.96, +18.03] | — | — | — |
| Windows / medium / 1 | 5,433.78 → 5,768.04 | +5.82% [+4.01, +7.41] | — | — | — |
| Windows / medium / 4 | 9,524.24 → 10,041.51 | +5.93% [+2.80, +9.06] | — | — | — |
| Windows / point / 1 | 27,122.91 → 35,547.69 | +29.98% [+27.01, +31.85] | — | — | — |
| Windows / point / 4 | 44,999.93 → 61,917.08 | +33.38% [+28.11, +40.03] | — | — | — |

## Writer

| Host / read shape / processes | Reader calls/s | Reader change | Writer txn/s | Writer change | Writer p99 change |
| --- | ---: | ---: | ---: | ---: | ---: |
| Linux / large / 1 | 27.84 → 157.54 | +483.78% [+358.74, +608.82] | 27.74 → 25.57 | -4.09% [-27.06, +15.77] | +24.10% [-24.27, +100.13] |
| Linux / large / 4 | 127.01 → 471.37 | +301.66% [+197.33, +426.29] | 20.50 → 27.46 | +38.02% [-0.37, +86.38] | -16.87% [-53.50, +35.67] |
| Linux / medium / 1 | 25.53 → 556.76 | +2203.04% [+1716.79, +2682.19] | 25.43 → 21.37 | -12.59% [-31.57, +8.50] | +17.99% [-4.50, +36.81] |
| Linux / medium / 4 | 765.80 → 1,961.26 | +216.87% [+154.79, +287.55] | 19.47 → 18.22 | +6.65% [-29.76, +56.00] | +9.99% [-16.31, +39.13] |
| Linux / point / 1 | 45.48 → 8,056.43 | +16509.94% [+13645.25, +19220.16] | 22.29 → 20.50 | -0.35% [-24.89, +30.91] | +21.98% [-27.18, +71.14] |
| Linux / point / 4 | 7,243.70 → 23,901.54 | +432.97% [+159.45, +728.59] | 20.24 → 21.08 | +29.89% [+2.54, +58.63] | -2.93% [-29.73, +23.87] |
| Windows / large / 1 | 49.81 → 108.00 | +115.94% [+108.40, +124.43] | 55.40 → 54.90 | -1.08% [-4.56, +3.47] | +2.34% [-1.27, +5.77] |
| Windows / large / 4 | 452.72 → 305.23 | -33.37% [-35.84, -31.00] | 36.24 → 44.46 | +23.22% [+21.43, +24.90] | -17.21% [-18.45, -15.97] |
| Windows / medium / 1 | 52.44 → 376.89 | +620.97% [+578.55, +665.43] | 49.33 → 45.02 | -6.98% [-13.84, -0.11] | +35.88% [+4.11, +93.54] |
| Windows / medium / 4 | 2,867.20 → 1,151.99 | -58.53% [-60.26, -56.62] | 24.88 → 39.78 | +58.54% [+52.41, +63.91] | -51.08% [-59.18, -40.40] |
| Windows / point / 1 | 104.34 → 1,985.49 | +1808.08% [+1640.49, +1944.58] | 51.47 → 50.51 | -1.48% [-3.67, +0.84] | -0.32% [-9.17, +6.35] |
| Windows / point / 4 | 11,510.00 → 4,499.14 | -59.07% [-63.01, -54.20] | 35.92 → 44.67 | +22.95% [+17.62, +26.69] | -37.56% [-44.09, -29.78] |

## Checkpoint

| Host / read shape / processes | Reader calls/s | Reader change | Writer txn/s | Writer change | Writer p99 change |
| --- | ---: | ---: | ---: | ---: | ---: |
| Linux / large / 1 | 68.56 → 164.28 | +138.53% [+134.96, +142.21] | 68.57 → 66.85 | -2.64% [-3.49, -1.87] | +4.64% [+2.63, +7.30] |
| Linux / large / 4 | 315.84 → 470.09 | +47.78% [+44.67, +50.07] | 52.65 → 50.98 | -3.53% [-4.12, -2.91] | +6.97% [+4.24, +9.70] |
| Linux / medium / 1 | 68.91 → 464.07 | +572.84% [+557.39, +587.68] | 68.21 → 67.57 | -1.10% [-1.88, -0.32] | -0.07% [-2.99, +3.40] |
| Linux / medium / 4 | 1,409.54 → 1,573.39 | +11.24% [+9.44, +12.30] | 53.08 → 55.36 | +4.24% [+3.23, +5.27] | -0.38% [-2.73, +1.97] |
| Linux / point / 1 | 194.27 → 2,107.29 | +989.48% [+948.46, +1017.64] | 72.06 → 72.82 | -0.61% [-2.62, +1.04] | +2.07% [-1.64, +5.85] |
| Linux / point / 4 | 11,582.36 → 7,389.65 | -36.40% [-40.45, -33.34] | 54.93 → 63.09 | +15.29% [+13.52, +17.14] | -14.59% [-16.25, -12.93] |
| Windows / large / 1 | 49.97 → 104.49 | +111.24% [+89.04, +129.87] | 52.97 → 52.85 | -2.03% [-7.62, +1.21] | +2.28% [-1.46, +7.01] |
| Windows / large / 4 | 490.76 → 351.95 | -12.14% [-30.06, +16.99] | 27.45 → 35.98 | +68.78% [+29.41, +113.89] | -50.01% [-74.02, -32.30] |
| Windows / medium / 1 | 57.56 → 370.08 | +549.83% [+510.10, +593.82] | 56.09 → 54.30 | -2.00% [-6.11, +3.95] | +14.73% [-1.38, +42.13] |
| Windows / medium / 4 | 2,341.62 → 1,106.52 | -53.06% [-55.77, -50.34] | 29.99 → 42.55 | +48.08% [+37.25, +60.11] | -43.12% [-48.70, -38.50] |
| Windows / point / 1 | 177.65 → 1,885.45 | +931.44% [+823.21, +1039.66] | 57.22 → 58.70 | -0.66% [-2.18, +1.26] | +7.31% [+2.76, +14.39] |
| Windows / point / 4 | 10,049.18 → 4,138.75 | -58.82% [-65.70, -53.76] | 40.74 → 47.66 | +26.91% [+13.52, +46.84] | -25.35% [-37.33, -15.75] |
