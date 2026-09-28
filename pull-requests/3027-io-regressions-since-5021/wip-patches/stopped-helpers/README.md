# Partial test updates of three stopped helpers (LiteDB#3027)

Three helper agents were updating existing tests to the durability decisions
(docs/decisions/durability-policy.md) when they were stopped. Their unfinished work, kept per
decision 7 (base commit in parentheses):

- `a0c898fb710c5b8ef.patch` (31f4d0927): engine stop -> read-only reopen group (TornWalAppend,
  WalDurability, TornSlotRewrite, FailedPromotionJournal, KeptWalStop, UnsyncedPromotion,
  EncryptedWalCreationFailure; helper ReadOnlyAfterWriteFailure.cs).
- `abcbf10ea2fa72d2a.patch` (0629175fb): data-file-cannot-sync group (UnsyncedBackfillPowerLoss,
  UnsyncableDataFile, FreshEngineDurability, new WalLimit_Tests.cs; ZzScratchB_Tests.cs is scratch).
- `abeca791a9c6f495f.patch` (31f4d0927): log-cannot-sync group (Issue2242_UnsyncableLog, helper
  WriteFailureAssert.cs).

Unfinished and unvalidated: apply with `git apply` on the base commit and review before use.
