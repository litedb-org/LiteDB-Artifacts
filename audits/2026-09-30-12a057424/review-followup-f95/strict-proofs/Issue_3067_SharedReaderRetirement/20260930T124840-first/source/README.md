# Last leased-reader retirement versus forced pin shutdown

Public black-box proof of [review P1](https://github.com/JKamsker/LiteDB/pull/133#pullrequestreview-5366098967), pinned to actual reviewed commit `f95f0b0cd59d35d285ac72183589975751e35229`.

The same thread retains an ordinary leased snapshot, performs a write to establish a pin, starts a legacy transaction, and pauses a second reader's later callback. A foreign disposer reaches a wait and a public call confirms disposal has been published. The callback then disposes the other leased reader. The bug requires both callback and disposer to remain waiting, with no unexpected error, after the callback has demonstrably started reader retirement. A generic setup timeout is never accepted as reproduction.

The public API has no pin identity or idle-duration override. The setup stopwatch therefore starts **before** the write and requires write plus BeginTrans to finish within 50 ms, below the production pin's 100-ms idle lifetime. Slow setup is cleaned up and recorded, with at most three fresh-file attempts. No native waiter is introduced before the transaction retains the pin. An exhausted setup budget fails the proof; it is neither a known-bad nor a fixed result. Permanent unit tests separately establish actual pin/core drain barriers and test the non-pin owner-exit equivalent; this historical proof makes no claim to prove that additional variant.

Plain (null password) and encrypted cases must agree. Successful independent retirement is a control on both revisions. Fixed callbacks must unwind and connection disposal must finish; a separate Shared writer then commits, and two cold Direct reopens check exact indexed records, an unrelated sentinel and absence of an uncommitted marker. The known-bad process exits with its blocked background threads under a bounded runner deadline.

Strict outcomes: `0` plus `BUG_REPRODUCED` on the pinned package; `10` plus `VERIFIED_FIXED` on source; setup/unexpected failures exit `2`. Databases use printed short paths on the same system temporary volume. This is bounded thread/process/exception evidence, not power-loss evidence. No product hooks or private reflection are used.
