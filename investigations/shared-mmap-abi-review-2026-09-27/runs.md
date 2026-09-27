# Final-source run index

Candidate: `4c84f44ed4adb1df57cc6d797d443b8978747f13`; tree `7f6f5b865881c5f846c14daa8e7b230c2d18c544`.

| Purpose | Baseline | Selected jobs | Run |
| --- | --- | ---: | --- |
| ci | `—` | 47 | [36342371373](https://github.com/litedb-org/LiteDB/actions/runs/36342371373) |
| fuzz | `—` | 12 | [36342371157](https://github.com/litedb-org/LiteDB/actions/runs/36342371157) |
| compatibility | `—` | 4 | [36342371209](https://github.com/litedb-org/LiteDB/actions/runs/36342371209) |
| ordinary | `94bf30948a865edee75482265195cca7af18a954` | 8 | [36342371175](https://github.com/litedb-org/LiteDB/actions/runs/36342371175) |
| saturation | `94bf30948a865edee75482265195cca7af18a954` | 12 | [36342403982](https://github.com/litedb-org/LiteDB/actions/runs/36342403982) |
| archived | `16131953114971557b44e9deaa78bd8ce9d45ab1` | 6 | [36342405871](https://github.com/litedb-org/LiteDB/actions/runs/36342405871) |
| incremental | `186cdeec6bbc76d949b77c726c54a492dbc6d3cf` | 4 | [36342407959](https://github.com/litedb-org/LiteDB/actions/runs/36342407959) |
| churn | `e06fc9d2d061e412fa6782fb8c993f3b75ce35ec` | 4 | [36342409690](https://github.com/litedb-org/LiteDB/actions/runs/36342409690) |

The incremental workflow has one additional unchanged Windows .NET 10 job execution (attempt 2). The first batch and repeat are retained separately in `hosted-incremental` and `hosted-incremental-repeat`; the repeat never replaces the first batch in the main tables.

CI ran the synthetic merge `44f113be3e6e8e60637c5df8703bde23f368daab`; its tree equals the candidate. The subsequently fetched PR merge ref `76604f01fcc1993ab2841dbcdb8878ee1cf56ee3` also has that same tree. The base remains `94bf30948a865edee75482265195cca7af18a954`.

Historical `e06fc9d2d` and `8c038860c` run metadata, first/repeated ABI-only measurements, cancelled superseded runs, local negative controls and rejected pacing experiments are retained under their own evidence scopes. They are not validation of the final source.
