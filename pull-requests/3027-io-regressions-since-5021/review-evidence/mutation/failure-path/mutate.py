import sys, subprocess, os, shutil, time
W = os.environ.get('MUT_W', '$SCRATCH/rv-fail')
WW = 'LiteDB/Engine/Disk/DiskService.WalWrite.cs'
FV = 'LiteDB/Engine/Disk/DiskService.FileVersion.cs'
TS = 'LiteDB/Engine/Services/TransactionService.cs'
TR = 'LiteDB/Engine/Engine/Transaction.cs'
CK = 'LiteDB/Engine/Services/WalIndexService.Checkpoint.cs'
CS = 'LiteDB/Engine/Disk/DiskService.Checksums.cs'
DS = 'LiteDB/Engine/Disk/DiskService.cs'
CC = 'LiteDB/Engine/Disk/Streams/ConcurrentStream.cs'
DF = 'LiteDB/Engine/Disk/DiskService.DurableFlush.cs'
M = {
 'M0_none': [],
 'M1_append_never_uncertain': [(WW, "                uncertain = true;\n                stream.Write(", "                uncertain = overwrite;\n                stream.Write(")],
 'M2_truncate_despite_journal': [(WW, "if (previousStreamLength.HasValue && _checksums.JournalBytes == 0)", "if (previousStreamLength.HasValue)")],
 'M3_torn_overwrite_cleared_by_truncation': [(WW, "if (!overwrite) uncertain = false;", "uncertain = false;")],
 'M4_truncation_never_clears': [(WW, "if (!overwrite) uncertain = false;", "")],
 'M5_no_reset_after_successful_write': [(WW, "                uncertain = false;\n                this.CrashPoint(", "                this.CrashPoint(")],
 'M6_catch_when_false': [(WW, "catch (Exception ex) when (uncertain)", "catch (Exception ex) when (false && uncertain)")],
 'M7_promotion_no_stop': [(FV, "catch (Exception ex) when (_checksums.JournalBytes != 0)", "catch (Exception ex) when (false && _checksums.JournalBytes != 0)")],
 'M8_commit_ignores_writefailure': [(TR, "if (transaction.WriteFailure != null)", "if (false && transaction.WriteFailure != null)")],
 'M9_safepoint_ignores_writefailure': [(TS, "if (this.WriteFailure != null) throw WriteFailed(this.WriteFailure);", "")],
 'M10_writefailure_never_recorded': [(TS, "this.WriteFailure = ex;", "")],
 'M14_commit_throws_without_rollback': [(TR, "                        this.RollbackAndReleaseTransaction(transaction);\n                        throw TransactionService.WriteFailed", "                        throw TransactionService.WriteFailed")],
 'M15_no_position_release': [(WW, "                    Interlocked.Exchange(ref _logLength, previousLogLength);\n", "")],
 'M16_position_release_after_truncation': [(WW, "                    Interlocked.Exchange(ref _logLength, previousLogLength);\n                    // The stream length", "                    // The stream length"), (WW, "                        if (!overwrite) uncertain = false;\n                    }\n", "                        if (!overwrite) uncertain = false;\n                    }\n                    Interlocked.Exchange(ref _logLength, previousLogLength);\n")],
 'N1_checkpoint_stop_after_release': [(CK, "catch (Exception error) when (writerEntered)", "catch (Exception error) when (false && writerEntered)")],
 'N2_begun_without_beginstop': [(CS, "            owned = _state.BeginStop(exception);\n", "")],
 'N3_no_teardown_when_begun': [(CS, "if (begun) _state.CompleteStop(exception, owned);", "if (begun) { }")],
 'N4_rethrow_cleanup_error': [(WW, "ExceptionDispatchInfo.Capture(failure).Throw();", "throw;")],
 'N5_never_buffering': [(DS, "_logMayBuffer = settings.LogStream != null && !(settings.LogStream is MemoryStream);", "_logMayBuffer = false;")],
 'N6_reset_after_write_always': [(WW, "if (!_logMayBuffer) uncertain = false;", "uncertain = false;")],
 'N7_flush_failure_not_stopping': [(WW, "when (count > 0 && _logMayBuffer)", "when (false)")],
 'N8_truncation_always_clears': [(WW, "if (!overwrite && (count == 0 || !_logMayBuffer)) uncertain = false;", "if (!overwrite) uncertain = false;")],
 'N9_no_createsnapshot_check': [(TS, "            ENSURE(_state == TransactionState.Active, \"transaction must be active to create new snapshot\");\n            // Its snapshots", "            ENSURE(_state == TransactionState.Active, \"transaction must be active to create new snapshot\");\n            if (false)\n            // Its snapshots")],
 'N10_always_buffering': [(DS, "_logMayBuffer = settings.LogStream != null && !(settings.LogStream is MemoryStream);", "_logMayBuffer = true;")],
 'N11_truncation_never_clears_buffering': [(WW, "if (!overwrite && (count == 0 || !_logMayBuffer)) uncertain = false;", "if (!overwrite && !_logMayBuffer) uncertain = false;")],
 'N12_truncate_despite_journal': [(WW, "if (previousStreamLength.HasValue && _checksums.JournalBytes == 0)", "if (previousStreamLength.HasValue)")],
 'N13_no_stop_in_writer': [(WW, "catch (Exception ex) when (uncertain)", "catch (Exception ex) when (false && uncertain)")],
 'P1_no_write_through': [(CC, "                if (_writeThrough) _stream.Flush();\n", "")],
 'P2_length_unlocked': [(CC, "get { lock (_stream) return _stream.Length; }", "get { return _stream.Length; }")],
 'P3_flush_unlocked': [(CC, "            lock (_stream) _stream.Flush();", "            _stream.Flush();")],
 'P4_no_final_batch_flush': [(WW, "                else if (flushFailure == null) stream.Flush();\n", "")],
 'P5_write_through_memory_too': [(CC, "_writeThrough = !(stream is MemoryStream);", "_writeThrough = true;")],
 'Q1_no_first_sync_proof': [(DF, "            else if (!_dataSyncProven && _dataIsFile) this.ProveDataFile();\n", "")],
 'Q2_raw_log_ungated': [(DF, "            if (!this.ProveDataBeforeLog())\n            {\n                this.SkipLogSync(raw);\n                return;\n            }\n", "            this.ProveDataBeforeLog();\n")],
 'Q3_barrier_ungated': [(DF, "            if (this.ProveDataBeforeLog()) this.SyncLogBarrierUnproven(log);\n            else this.SkipLogSync(log);", "            this.ProveDataBeforeLog(); this.SyncLogBarrierUnproven(log);")],
 'Q4_no_retry': [(DF, "            if (!_dataBarrierSynced) this.SyncDataFile();\n            else if", "            if (false) this.SyncDataFile();\n            else if")],
 'Q5_skip_keeps_synced_flag': [(DF, "            _logBarrierSynced = false;\n            log.Flush();", "            log.Flush();")],
 'M11_unused': [(WW, "if (!overwrite) uncertain = false;\n", "if (!overwrite) uncertain = false;\n                    }\n                    else if (!overwrite && _checksums.JournalBytes == 0) { }\n                    if (false) {\n")],
}
name = sys.argv[1]
filt = sys.argv[2]
backups = {}
try:
    for f, old, new in M[name]:
        p = os.path.join(W, f)
        if p not in backups:
            backups[p] = open(p).read()
        s = open(p).read()
        assert s.count(old) == 1, (name, f, s.count(old))
        open(p, 'w').write(s.replace(old, new))
    env = dict(os.environ, PATH='/root/.dotnet:' + os.environ['PATH'], DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
    b = subprocess.run(['dotnet','build','LiteDB.Tests/LiteDB.Tests.csproj','-c','Release','-f','net10.0','-p:TestingEnabled=true'], cwd=W, env=env, capture_output=True, text=True)
    if b.returncode != 0:
        print(name, 'BUILD FAILED'); print('\n'.join(l for l in b.stdout.splitlines() if ' error ' in l)[:2000]); sys.exit(0)
    t = subprocess.run(['timeout','3500','dotnet','test','LiteDB.Tests/LiteDB.Tests.csproj','-c','Release','-f','net10.0','-p:TestingEnabled=true','--no-build','--settings',os.environ.get('MUT_SETTINGS','tests.runsettings'),'--filter',filt], cwd=W, env=env, capture_output=True, text=True)
    out = t.stdout
    failed = [l.strip() for l in out.splitlines() if l.strip().startswith('Failed ') and '[' in l]
    summ = [l.strip() for l in out.splitlines() if l.strip().startswith(('Passed!','Failed!')) or 'Abort' in l or 'timed out' in l.lower() or 'Total tests' in l]
    print(name, '|', summ)
    for l in failed[:40]: print('   ', l)
    lines = out.splitlines()
    for i,l in enumerate(lines):
        if 'Error Message' in l and os.environ.get('MUT_MSG'):
            print('      ', lines[i+1].strip()[:400])
finally:
    for p, s in backups.items():
        open(p, 'w').write(s)
        os.utime(p, None)
