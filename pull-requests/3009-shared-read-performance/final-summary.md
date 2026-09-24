## final-shared-startup.jsonl: 36/36 runs

| Runtime | Mode | Scenario | Build | Runs | Mean ms (median; range) | p99 ms | Allocated bytes/op | CPU ms/op | Close ms |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| .NET 10.0.11 | shared | point | baseline | 3 | 0.4152 (0.4103–0.4218) | 0.8533 | 289,945 | 0.5031 | 0.3879 |
| .NET 10.0.11 | shared | point | candidate | 3 | 0.2227 (0.2193–0.2367) | 0.6019 | 268,251 | 0.2828 | 0.4141 |
| .NET 10.0.11 | shared | point | prestack | 3 | 0.2103 (0.2059–0.2114) | 0.5805 | 277,243 | 0.2308 | 0.0779 |
| .NET 10.0.11 | shared | scan | baseline | 3 | 10.6275 (10.4847–10.7790) | 13.4072 | 5,346,318 | 11.8843 | 0.3392 |
| .NET 10.0.11 | shared | scan | candidate | 3 | 8.4372 (8.4051–8.7476) | 11.9755 | 4,649,396 | 9.6218 | 0.3187 |
| .NET 10.0.11 | shared | scan | prestack | 3 | 8.2688 (8.1578–8.3592) | 11.2362 | 4,846,060 | 9.5169 | 0.0610 |
| .NET 8.0.30 | shared | point | baseline | 3 | 0.4522 (0.4503–0.4576) | 0.9468 | 290,079 | 0.5300 | 0.3798 |
| .NET 8.0.30 | shared | point | candidate | 3 | 0.2380 (0.2254–0.2401) | 0.7043 | 268,362 | 0.2850 | 0.3705 |
| .NET 8.0.30 | shared | point | prestack | 3 | 0.2417 (0.2329–0.2421) | 0.6881 | 277,379 | 0.2600 | 0.0649 |
| .NET 8.0.30 | shared | scan | baseline | 3 | 10.7930 (10.5279–11.5389) | 13.6897 | 5,346,518 | 11.8667 | 0.3133 |
| .NET 8.0.30 | shared | scan | candidate | 3 | 8.6055 (8.1369–9.1687) | 11.3803 | 4,649,621 | 9.8000 | 0.3268 |
| .NET 8.0.30 | shared | scan | prestack | 3 | 8.4257 (7.8982–8.4518) | 11.2655 | 4,846,351 | 9.2000 | 0.0621 |

All figures are medians of individual run statistics; p99 is not a pooled percentile.

## final-shared-steady.jsonl: 54/54 runs

| Runtime | Mode | Scenario | Build | Runs | Mean ms (median; range) | p99 ms | Allocated bytes/op | CPU ms/op | Close ms |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| .NET 10.0.11 | shared | mixed | baseline | 3 | 1.0191 (1.0057–1.0630) | 5.8865 | 371,533 | 0.5128 | 23.3143 |
| .NET 10.0.11 | shared | mixed | candidate | 3 | 0.9201 (0.9038–0.9638) | 5.7110 | 350,305 | 0.4191 | 24.0854 |
| .NET 10.0.11 | shared | mixed | prestack | 3 | 1.1923 (1.1920–1.2014) | 10.5975 | 277,937 | 0.1983 | 0.0910 |
| .NET 10.0.11 | shared | point | baseline | 3 | 0.1656 (0.1635–0.1683) | 0.5159 | 289,384 | 0.2029 | 0.4019 |
| .NET 10.0.11 | shared | point | candidate | 3 | 0.1076 (0.1045–0.1080) | 0.4513 | 268,154 | 0.1433 | 0.4095 |
| .NET 10.0.11 | shared | point | prestack | 3 | 0.0977 (0.0917–0.0979) | 0.4128 | 277,211 | 0.0995 | 0.0893 |
| .NET 10.0.11 | shared | scan | baseline | 3 | 2.8061 (2.7330–2.9501) | 4.2295 | 5,344,977 | 2.9048 | 0.3467 |
| .NET 10.0.11 | shared | scan | candidate | 3 | 2.4432 (2.3927–2.4494) | 5.4412 | 4,514,568 | 2.4638 | 0.3540 |
| .NET 10.0.11 | shared | scan | prestack | 3 | 2.4509 (2.4122–2.4571) | 5.2955 | 4,845,985 | 2.2725 | 0.0729 |
| .NET 8.0.30 | shared | mixed | baseline | 3 | 1.0538 (1.0418–1.0886) | 5.9420 | 372,317 | 0.5500 | 23.8122 |
| .NET 8.0.30 | shared | mixed | candidate | 3 | 0.9363 (0.9289–0.9518) | 5.9873 | 350,530 | 0.4400 | 23.1878 |
| .NET 8.0.30 | shared | mixed | prestack | 3 | 1.2521 (1.2149–1.3251) | 10.7118 | 278,119 | 0.2500 | 0.0655 |
| .NET 8.0.30 | shared | point | baseline | 3 | 0.2082 (0.2044–0.2157) | 0.6119 | 290,077 | 0.2510 | 0.3883 |
| .NET 8.0.30 | shared | point | candidate | 3 | 0.1147 (0.1106–0.1159) | 0.5295 | 268,360 | 0.1540 | 0.3745 |
| .NET 8.0.30 | shared | point | prestack | 3 | 0.1200 (0.1183–0.1210) | 0.5165 | 277,379 | 0.1230 | 0.0743 |
| .NET 8.0.30 | shared | scan | baseline | 3 | 3.3013 (3.2835–3.3187) | 5.5779 | 5,346,478 | 3.4000 | 0.3416 |
| .NET 8.0.30 | shared | scan | candidate | 3 | 2.8595 (2.8582–2.9194) | 5.9290 | 4,649,027 | 2.9100 | 0.3105 |
| .NET 8.0.30 | shared | scan | prestack | 3 | 2.7810 (2.7032–2.8411) | 5.6548 | 4,846,030 | 2.6900 | 0.0553 |

All figures are medians of individual run statistics; p99 is not a pooled percentile.

## final-direct.jsonl: 12/12 runs

| Runtime | Mode | Scenario | Build | Runs | Mean ms (median; range) | p99 ms | Allocated bytes/op | CPU ms/op | Close ms |
| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| .NET 10.0.11 | direct | scan | baseline | 3 | 1.3906 (1.3759–1.4878) | 1.8141 | 3,764,697 | 1.3912 | 0.0840 |
| .NET 10.0.11 | direct | scan | candidate | 3 | 1.2755 (1.2052–1.2998) | 1.5945 | 3,013,353 | 1.2764 | 0.0873 |
| .NET 8.0.30 | direct | scan | baseline | 3 | 1.7380 (1.7129–1.8337) | 2.6234 | 3,764,759 | 1.7400 | 0.0852 |
| .NET 8.0.30 | direct | scan | candidate | 3 | 1.4961 (1.4422–1.5691) | 2.4794 | 3,141,415 | 1.5000 | 0.0900 |

All figures are medians of individual run statistics; p99 is not a pooled percentile.
