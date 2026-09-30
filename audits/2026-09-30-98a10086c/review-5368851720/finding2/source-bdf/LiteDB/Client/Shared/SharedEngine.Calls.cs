using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace LiteDB
{
    public partial class SharedEngine
    {
        /// <summary>How long Dispose waits for admitted calls of other threads to return.</summary>
        internal static readonly TimeSpan DisposeCallWait = TimeSpan.FromSeconds(10);

        // Calls admitted to the engine (they own the mutex and passed the disposed check)
        // that have not returned yet, per thread. Guarded by _useLock.
        private readonly Dictionary<int, int> _admitted = new Dictionary<int, int>();
        private int _admittedCalls;
        // A last-reader close drains outside _useLock. Fresh users wait for its
        // publication to retire before opening/counting a replacement core.
        private Engine.LiteEngine _closingCore;

        // Synchronous callbacks can enter another facade for the same native
        // namespace. Keep only executing calls, not idle owners or leased readers.
        [ThreadStatic] private static List<SharedEngine> _executingCalls;

        internal readonly struct CallbackScope : IDisposable
        {
            private readonly bool _entered;
            internal CallbackScope(SharedEngine engine)
            {
                _entered = engine != null;
                if (!_entered) return;
                var calls = _executingCalls ?? (_executingCalls = new List<SharedEngine>());
                calls.Add(engine);
            }
            public void Dispose()
            {
                // RemoveAt clears the reference as well as restoring nested scopes.
                if (_entered) _executingCalls.RemoveAt(_executingCalls.Count - 1);
            }
        }

        private bool CannotWaitForOwnershipOnCurrentThread() =>
            _owner.IsOwnedByCurrentThread || _pin?.IsHeldByCurrentThread == true ||
            this.IsExecutingOwnedCoreOnCurrentThread();

        private void ThrowIfCallbackOwnershipWait()
        {
            var calls = _executingCalls;
            if (calls == null) return;
            for (var i = calls.Count - 1; i >= 0; i--)
            {
                var caller = calls[i];
                if (StringComparer.Ordinal.Equals(caller._mutexName, _mutexName) &&
                    caller.CannotWaitForOwnershipOnCurrentThread())
                    throw new InvalidOperationException("Cannot open a transaction handle from inside an operation retaining its shared writer ownership.");
            }
        }

        /// <summary>
        /// Under _useLock, with the mutex owned: refuse a call once Dispose started, else count
        /// it. Dispose closes the engine only after every counted call of another thread
        /// returned. An engine closed under a live call can neither dispose its busy page
        /// cache (the call's pinned or writable pages leak) nor finish the call's transaction.
        /// </summary>
        private void AdmitLocked()
        {
            while (_closingCore != null)
            {
                if (_closingCore.IsExecutingOnCurrentThread)
                    throw new InvalidOperationException("Cannot reenter a shared core while this reader is closing it.");
                if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(SharedEngine));
                Monitor.Wait(_useLock);
            }
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(SharedEngine));
            var thread = Environment.CurrentManagedThreadId;
            _admitted.TryGetValue(thread, out var depth);
            _admitted[thread] = depth + 1;
            _admittedCalls++;
        }

        private int AdmittedDepth()
        {
            lock (_useLock) return _admitted.TryGetValue(Environment.CurrentManagedThreadId, out var depth) ? depth : 0;
        }

        // User callbacks and custom streams may open readers that escape the call.
        private bool CanScope => _settings.ReadTransform == null && _settings.DataStream == null &&
            _settings.LogStream == null && _settings.TempStream == null;

        private T QueryDatabase<T>(Func<T> Query) => this.Call(() =>
        {
            var use = OpenDatabase(scoped: this.CanScope);
            try
            {
                return Query();
            }
            finally
            {
                CloseDatabase(use);
            }
        });

        /// <summary>Run a public call; the admission it made, if any, ends when it returns.</summary>
        private T Call<T>(Func<T> call, bool rollback = false)
        {
            // A late reader callback must be refused before waiting for either
            // connection bookkeeping or writer ownership that close is draining.
            if (Volatile.Read(ref _disposed) != 0)
            {
                if (rollback) return default;
                throw new ObjectDisposedException(nameof(SharedEngine));
            }
            if (this.IsForeignReaderCallback())
            {
                // A handed-off reader can still be running when its original owner
                // exits. Its callback cannot wait for the native owner draining it.
                // Unrelated new callers retain their normal admission wait/recovery.
                if (rollback) return default;
                throw new InvalidOperationException("Cannot wait for shared ownership from inside a reader executing on another ownership thread.");
            }
            using var callback = new CallbackScope(this);
            var depth = this.AdmittedDepth();
            try
            {
                return call();
            }
            finally
            {
                this.EndAdmissions(depth);
            }
        }

        private void EndAdmissions(int depth)
        {
            var thread = Environment.CurrentManagedThreadId;
            lock (_useLock)
            {
                if (!_admitted.TryGetValue(thread, out var current) || current <= depth) return;
                _admittedCalls -= current - depth;
                if (depth == 0) _admitted.Remove(thread);
                else _admitted[thread] = depth;
                Monitor.PulseAll(_useLock);
            }
        }

        /// <summary>
        /// Dispose's drain: wait until no other thread's admitted call is running. Calls of the
        /// disposing thread itself (Dispose from inside an operation) are not waited for. The
        /// wait is bounded: a call blocked on something only this Dispose would end (another
        /// thread's explicit transaction holding an engine lock) must not hang it; past the
        /// bound Dispose proceeds, and that call fails on the closed engine.
        /// </summary>
        private void WaitForAdmittedCalls()
        {
            var waited = Stopwatch.StartNew();
            var thread = Environment.CurrentManagedThreadId;
            lock (_useLock)
            {
                while (true)
                {
                    _admitted.TryGetValue(thread, out var own);
                    if (_admittedCalls - own <= 0) return;
                    var remaining = DisposeCallWait - waited.Elapsed;
                    if (remaining <= TimeSpan.Zero) return;
                    Monitor.Wait(_useLock, remaining);
                }
            }
        }
    }
}
