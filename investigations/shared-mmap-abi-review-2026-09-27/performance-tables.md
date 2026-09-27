
### Ordinary latency versus merged stack

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Linux .NET 10.0 / mixed | 0.268 | 0.126 | -52.78% [-53.31, -52.26] |
| Linux .NET 10.0 / point | 0.103 | 0.018 | -82.31% [-82.51, -82.12] |
| Linux .NET 10.0 / scan | 2.834 | 1.552 | -45.55% [-46.66, -44.66] |
| Linux .NET 10.0 / write | 0.636 | 0.653 | +2.05% [-2.55, +7.10] |
| Linux .NET 8.0 / mixed | 0.248 | 0.167 | -16.78% [-46.25, +12.69] |
| Linux .NET 8.0 / point | 0.084 | 0.015 | -82.36% [-82.67, -82.14] |
| Linux .NET 8.0 / scan | 2.370 | 1.416 | -40.18% [-41.39, -38.96] |
| Linux .NET 8.0 / write | 0.581 | 0.581 | -1.24% [-4.86, +1.15] |
| Windows .NET 10.0 / mixed | 0.209 | 0.102 | -43.04% [-53.17, -26.98] |
| Windows .NET 10.0 / point | 0.088 | 0.013 | -85.33% [-85.86, -84.79] |
| Windows .NET 10.0 / scan | 1.486 | 0.770 | -48.73% [-50.30, -46.52] |
| Windows .NET 10.0 / write | 0.599 | 0.608 | -2.36% [-10.42, +2.43] |
| Windows .NET 8.0 / mixed | 0.448 | 0.211 | -53.03% [-53.85, -52.27] |
| Windows .NET 8.0 / point | 0.219 | 0.034 | -84.94% [-85.29, -84.59] |
| Windows .NET 8.0 / scan | 2.994 | 1.749 | -42.26% [-46.49, -38.94] |
| Windows .NET 8.0 / write | 0.441 | 0.431 | -0.22% [-2.42, +3.83] |

Metric: mean milliseconds per operation.

### Largest ordinary tail increases versus merged stack

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Windows .NET 10.0 / open-close | 6.015 | 6.177 | +41.64% [+0.98, +120.78] |
| Windows .NET 8.0 / checkpoint | 5.935 | 7.130 | +28.65% [-7.77, +73.57] |
| Windows .NET 10.0 / mixed | 0.612 | 0.711 | +18.84% [+0.85, +36.83] |
| Linux .NET 8.0 / open-close | 7.155 | 7.171 | +8.11% [-21.19, +37.40] |
| Windows .NET 8.0 / open-close | 6.013 | 6.424 | +6.01% [-33.87, +51.49] |
| Linux .NET 10.0 / checkpoint | 4.473 | 4.670 | +5.50% [-2.96, +17.12] |
| Linux .NET 10.0 / write | 2.649 | 2.601 | +5.34% [-3.14, +14.27] |
| Linux .NET 10.0 / open-close | 7.779 | 6.987 | +5.06% [-16.89, +30.92] |

Metric: p99 milliseconds per operation.

### Saturation versus merged stack: lowest paired writer throughput changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Windows .NET 10.0-writer / medium / 4 readers | 50.855 | 36.772 | -26.46% [-37.50, -11.02] |
| Windows .NET 10.0-checkpoint / large / 4 readers | 36.459 | 29.460 | -23.85% [-31.25, -18.57] |
| Linux .NET 8.0-writer / point / 4 readers | 67.403 | 52.704 | -21.58% [-22.41, -20.53] |
| Windows .NET 10.0-checkpoint / medium / 4 readers | 42.518 | 34.760 | -21.43% [-31.96, -11.72] |
| Linux .NET 10.0-writer / point / 4 readers | 70.617 | 55.234 | -21.29% [-22.97, -19.43] |
| Windows .NET 8.0-checkpoint / large / 4 readers | 35.389 | 31.785 | -17.15% [-40.98, -0.43] |

Metric: writer transactions per second.

### Saturation versus merged stack: highest paired writer p99 changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Windows .NET 8.0-writer / large / 4 readers | 43.205 | 65.160 | +192.19% [+45.77, +422.69] |
| Windows .NET 8.0-checkpoint / large / 4 readers | 51.145 | 71.708 | +180.47% [+20.65, +481.04] |
| Windows .NET 10.0-checkpoint / large / 4 readers | 50.625 | 78.831 | +158.59% [+52.80, +342.81] |
| Windows .NET 10.0-writer / medium / 4 readers | 34.416 | 70.440 | +131.84% [+66.45, +197.22] |
| Windows .NET 8.0-writer / medium / 4 readers | 34.541 | 56.757 | +85.56% [+45.05, +145.53] |
| Windows .NET 10.0-writer / large / 4 readers | 41.518 | 76.254 | +72.09% [+18.26, +136.03] |

Metric: writer transaction p99 milliseconds.

### Saturation versus merged stack: lowest paired reader throughput changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Linux .NET 10.0-checkpoint / large / 4 readers | 339.657 | 592.969 | +74.02% [+71.12, +76.30] |
| Linux .NET 8.0-checkpoint / large / 4 readers | 300.977 | 541.387 | +82.18% [+78.45, +86.70] |
| Linux .NET 10.0-writer / large / 4 readers | 261.411 | 537.086 | +104.47% [+102.34, +106.60] |
| Windows .NET 10.0-checkpoint / large / 1 readers | 54.772 | 113.537 | +106.92% [+94.15, +118.33] |
| Linux .NET 8.0-writer / large / 4 readers | 246.010 | 516.384 | +110.05% [+106.29, +114.16] |
| Windows .NET 8.0-checkpoint / large / 1 readers | 53.040 | 111.503 | +115.92% [+98.84, +133.72] |

Metric: aggregate reader calls per second.

### Saturation versus archived mmap prototype: lowest paired writer throughput changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Linux .NET 10.0-checkpoint / large / 4 readers | 52.821 | 49.716 | -5.46% [-6.26, -4.85] |
| Linux .NET 10.0-writer / point / 4 readers | 58.201 | 55.988 | -4.13% [-4.96, -3.31] |
| Linux .NET 10.0-writer / medium / 4 readers | 57.218 | 55.274 | -3.18% [-3.78, -2.55] |
| Linux .NET 10.0-checkpoint / large / 1 readers | 69.088 | 67.317 | -2.91% [-3.76, -1.81] |
| Windows .NET 10.0-writer / point / 1 readers | 60.586 | 59.693 | -1.96% [-3.31, -0.51] |
| Linux .NET 10.0-checkpoint / medium / 1 readers | 69.975 | 68.651 | -1.89% [-2.67, -1.20] |

Metric: writer transactions per second.

### Saturation versus archived mmap prototype: highest paired writer p99 changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Windows .NET 10.0-writer / medium / 4 readers | 73.022 | 110.463 | +57.35% [+23.27, +92.64] |
| Windows .NET 10.0-checkpoint / medium / 4 readers | 52.366 | 61.721 | +19.18% [+6.30, +31.76] |
| Linux .NET 10.0-checkpoint / large / 4 readers | 29.243 | 33.531 | +14.98% [+5.59, +23.65] |
| Linux .NET 10.0-writer / large / 4 readers | 24.477 | 26.239 | +9.85% [+6.57, +14.08] |
| Linux .NET 10.0-checkpoint / medium / 4 readers | 28.015 | 31.853 | +8.58% [+1.51, +16.99] |
| Linux .NET 10.0-checkpoint / medium / 1 readers | 24.982 | 26.255 | +7.70% [+0.04, +15.56] |

Metric: writer transaction p99 milliseconds.

### Saturation versus archived mmap prototype: lowest paired reader throughput changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Linux .NET 10.0-checkpoint / point / 4 readers | 11509.822 | 8879.843 | -22.73% [-25.47, -19.46] |
| Windows .NET 10.0-checkpoint / point / 4 readers | 5304.254 | 5206.262 | -1.00% [-11.47, +8.22] |
| Linux .NET 10.0-idle / large / 4 readers | 3244.783 | 3223.491 | -0.96% [-1.81, -0.26] |
| Linux .NET 10.0-idle / large / 1 readers | 2187.495 | 2189.555 | -0.74% [-2.41, +0.93] |
| Linux .NET 10.0-idle / medium / 4 readers | 12727.474 | 12843.941 | +0.76% [-0.13, +1.38] |
| Windows .NET 10.0-idle / medium / 4 readers | 10637.646 | 11133.294 | +1.05% [-5.16, +6.02] |

Metric: aggregate reader calls per second.

### Connection churn versus reviewed head: lowest paired writer throughput changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Windows .NET 8.0-churn / point / 1 readers | 43.800 | 45.387 | +3.70% [+3.06, +4.31] |
| Windows .NET 10.0-churn / point / 1 readers | 47.523 | 48.472 | +3.84% [-2.00, +9.45] |
| Linux .NET 8.0-churn / point / 1 readers | 58.364 | 61.186 | +3.92% [-1.42, +8.35] |
| Linux .NET 8.0-churn / medium / 1 readers | 55.653 | 58.896 | +4.15% [-4.36, +13.71] |
| Windows .NET 10.0-churn / medium / 1 readers | 45.003 | 49.296 | +10.46% [+8.89, +11.58] |
| Linux .NET 10.0-churn / point / 1 readers | 59.799 | 67.234 | +12.20% [+10.85, +13.20] |

Metric: writer transactions per second.

### Connection churn versus reviewed head: highest paired writer p99 changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Linux .NET 8.0-churn / medium / 1 readers | 34.401 | 44.886 | +81.12% [-12.53, +187.75] |
| Linux .NET 8.0-churn / point / 1 readers | 26.417 | 39.780 | +57.74% [-9.24, +163.63] |
| Windows .NET 8.0-churn / point / 4 readers | 111.760 | 99.995 | +10.07% [-19.32, +40.20] |
| Windows .NET 10.0-churn / point / 1 readers | 41.809 | 38.217 | +2.68% [-12.99, +27.94] |
| Windows .NET 8.0-churn / point / 1 readers | 47.184 | 49.941 | -3.82% [-8.17, +1.13] |
| Linux .NET 8.0-churn / large / 1 readers | 57.645 | 50.717 | -8.16% [-55.82, +46.75] |

Metric: writer transaction p99 milliseconds.

### Connection churn versus reviewed head: lowest paired reader throughput changes

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Windows .NET 8.0-churn / point / 1 readers | 12713.452 | 1075.878 | -91.34% [-91.99, -90.46] |
| Windows .NET 10.0-churn / point / 1 readers | 15133.795 | 1252.232 | -91.15% [-92.63, -89.67] |
| Windows .NET 10.0-churn / medium / 1 readers | 1975.100 | 270.933 | -86.10% [-86.68, -85.60] |
| Windows .NET 8.0-churn / medium / 1 readers | 1632.831 | 242.888 | -84.68% [-85.94, -83.41] |
| Linux .NET 8.0-churn / point / 1 readers | 24062.761 | 3925.194 | -84.11% [-85.06, -82.96] |
| Linux .NET 10.0-churn / point / 1 readers | 22217.042 | 3629.045 | -83.53% [-84.09, -82.99] |

Metric: aggregate reader calls per second.

### Final review head versus pre-ABI optimization

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Linux .NET 10.0 / mixed | 0.122 | 0.123 | +0.73% [-0.57, +2.44] |
| Linux .NET 10.0 / point | 0.018 | 0.018 | +2.62% [+0.73, +4.42] |
| Linux .NET 10.0 / scan | 1.510 | 1.542 | +1.51% [+0.44, +2.95] |
| Linux .NET 8.0 / mixed | 0.132 | 0.114 | +1.70% [-19.66, +38.86] |
| Linux .NET 8.0 / point | 0.016 | 0.016 | +0.33% [-1.26, +2.19] |
| Linux .NET 8.0 / scan | 1.510 | 1.532 | +0.69% [-1.30, +3.24] |
| Windows .NET 10.0 / mixed | 0.201 | 0.211 | +2.10% [+0.13, +4.59] |
| Windows .NET 10.0 / point | 0.030 | 0.031 | +3.65% [-4.88, +13.95] |
| Windows .NET 10.0 / scan | 1.567 | 1.541 | +101.01% [-3.32, +306.57] |
| Windows .NET 8.0 / mixed | 0.209 | 0.213 | +2.18% [-0.46, +4.69] |
| Windows .NET 8.0 / point | 0.032 | 0.033 | +0.96% [-0.19, +2.11] |
| Windows .NET 8.0 / scan | 1.745 | 1.790 | +2.82% [-0.47, +7.75] |

Metric: mean milliseconds per operation.

### Final versus pre-ABI: largest tail increases

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Windows .NET 10.0 / scan | 2.437 | 2.679 | +1182.03% [-1.66, +3541.03] |
| Windows .NET 10.0 / point | 0.076 | 0.074 | +51.46% [-10.65, +163.94] |
| Windows .NET 8.0 / scan | 2.611 | 2.799 | +14.75% [-1.45, +35.41] |
| Linux .NET 8.0 / mixed | 0.807 | 0.996 | +10.35% [-11.46, +32.17] |
| Windows .NET 10.0 / mixed | 1.143 | 1.300 | +9.36% [-2.90, +24.95] |
| Linux .NET 10.0 / point | 0.037 | 0.037 | +1.72% [+0.24, +3.29] |

Metric: p99 milliseconds per operation.

### Unchanged Windows .NET 10 incremental repeat (separate batch)

| Platform/workload | Baseline median | Candidate median | Paired change [exploratory 95% interval] |
| --- | ---: | ---: | ---: |
| Windows .NET 10.0 / mixed / meanMs | 0.203 | 0.207 | +14.01% [-0.45, +40.80] |
| Windows .NET 10.0 / mixed / p99Ms | 1.204 | 1.190 | +27.61% [-21.57, +103.42] |
| Windows .NET 10.0 / point / meanMs | 0.029 | 0.028 | -0.55% [-1.72, +0.22] |
| Windows .NET 10.0 / point / p99Ms | 0.067 | 0.070 | +0.65% [-4.46, +5.45] |
| Windows .NET 10.0 / scan / meanMs | 1.545 | 1.540 | -0.24% [-1.54, +1.07] |
| Windows .NET 10.0 / scan / p99Ms | 2.085 | 2.326 | +6.52% [-2.11, +15.74] |

Metric: mean/p99 milliseconds; see full CSV for metric names.
