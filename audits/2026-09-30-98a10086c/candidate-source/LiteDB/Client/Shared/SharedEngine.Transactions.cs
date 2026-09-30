using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LiteDB.Client.Shared;
using LiteDB.Engine;

namespace LiteDB
{
    public partial class SharedEngine
    {
        // Cached entries are inert managed metadata: they own no native admission or files.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> TransactionWriters =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

        // Only the wrapper survives a handle: CloseDatabase closes its storage core
        // and releases native writer ownership before this cache can be published.
        private SharedEngine _cachedTransactionChild;

        private bool ReturnTransactionChild(SharedEngine child)
        {
            lock (_useLock)
            {
                if (_disposed != 0 || _cachedTransactionChild != null) return false;
                _cachedTransactionChild = child;
                return true;
            }
        }

        private void ReleaseTransactionChildAdmission()
        {
            // The original per-handle child released its own mode admission too.
            // In particular a handle-only facade must still permit a Direct owner
            // between handles. Retain metadata, not idle data/log handles or leases.
            _handles?.CloseIdle();
            this.DisposeCoordination();
#if NET8_0_OR_GREATER
            // Each original child was a one-operation participant. Do not let
            // wrapper reuse create or retain a mapped authority between handles.
            _coordinationDemand = 0;
            _coordinationUnavailable = false;
            CoordinationFallbackReason = null;
#endif
            _settings.SharedAdmission.Dispose();
            _settings.SharedAdmission = new SharedModeAdmission(_settings);
        }

        internal TransactionResources OpenTransactionResources(TransactionAdmission admission, object sessionToken)
        {
            // A caller stream can capture the facade; a native holder must not root that
            // graph indefinitely, or perform storage I/O after its external owner is gone.
            if (!SharedModeGuard.IsFile(_settings) || _settings.LogStream != null || _settings.TempStream != null)
                throw new NotSupportedException("Shared transaction handles require filename-backed storage without caller streams.");
            TransactionHolderContext.Validate();
            // An executing callback cannot complete the owner while queued behind it.
            TransactionContext.ThrowIfSharedWait(_mutexName);
            this.ThrowIfCallbackOwnershipWait();
            // First-use default collation must observe the caller's culture, while null
            // settings still accept an existing database's persisted collation.
            RuntimeHelpers.RunClassConstructor(typeof(Collation).TypeHandle);
            lock (_useLock)
            {
                if (_disposed != 0) throw new ObjectDisposedException(nameof(SharedEngine));
                if (this.CannotWaitForOwnershipOnCurrentThread())
                    throw new InvalidOperationException("Complete the legacy transaction or close its locking reader before opening a transaction handle.");
            }
            var name = _mutexName;
            var gate = TransactionWriters.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
            // Pending begins use their caller's synchronous wait, never a holder thread/engine.
            admission.WaitLocal(gate);
            TransactionHolder holder;
            var policyAnchor = _settings.ReadTransform;
            try
            {
                admission.Acquired("local-acquired");
                var settings = _settings.SnapshotForTransactionHolder();
                // The holder thread must not root application callbacks that can capture the
                // facade/handle. The external resource owner retains the delegate while live.
                if (policyAnchor != null)
                {
                    var callback = new WeakReference<Func<string, BsonValue, BsonValue>>(policyAnchor);
                    settings.ReadTransform = (collection, value) => callback.TryGetTarget(out var transform)
                        ? transform(collection, value) : throw new ObjectDisposedException("Transaction read policy");
                }
                settings.CoordinationSignals = null;
                settings.SharedFileHandles = null;
                SharedEngine child;
                lock (_useLock)
                {
                    child = _cachedTransactionChild;
                    _cachedTransactionChild = null;
                }
                // Rebuild updates the parent's effective password/collation. A
                // derived collation can also change its serialized policy. Reuse
                // only a wrapper configured for this begin's detached snapshot.
                if (child != null && (child._settings.Password != settings.Password ||
                    child._settings.Collation?.ToString() != settings.Collation?.ToString()))
                {
                    child.Dispose();
                    child = null;
                }
                child = child ?? new SharedEngine(settings) { _transactionChild = true };
                child._settings.SharedDurability = _settings.SharedDurability;
                child._settings.CheckpointBackoff = _settings.CheckpointBackoff;
                holder = new TransactionHolder(child, gate, admission, sessionToken, this);
            }
            catch { gate.Release(); throw; }
            return holder.Open(policyAnchor);
        }

        /// <summary>One internal native owner per database, independent of application threads.</summary>
        private sealed class TransactionHolder
        {
            private readonly SharedEngine _child;
            // An abandoned handle must be able to collect the facade/session even
            // while this holder waits for its TransactionResources finalizer.
            private readonly WeakReference<SharedEngine> _cacheOwner;
            private readonly SemaphoreSlim _gate;
            private TransactionAdmission _admission;
            private readonly object _sessionToken;
            private readonly ManualResetEventSlim _opened = new ManualResetEventSlim();
            private readonly ManualResetEventSlim _close = new ManualResetEventSlim();
            private readonly TaskCompletionSource<bool> _done =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private Exception _error;
            private LiteEngine _engine;
#if DEBUG || TESTING
            private readonly Func<string, bool, Action<string>> _streamProbe = NativeAdmissionStreamProbe.Attach;
#endif

            internal TransactionHolder(SharedEngine child, SemaphoreSlim gate, TransactionAdmission admission, object sessionToken, SharedEngine cacheOwner)
            {
                _child = child; _gate = gate; _admission = admission; _sessionToken = sessionToken;
                _cacheOwner = new WeakReference<SharedEngine>(cacheOwner);
            }

            internal TransactionResources Open(object policyAnchor)
            {
                try
                {
                    // An internal idle holder must not retain application AsyncLocals.
                    if (ExecutionContext.IsFlowSuppressed()) SharedHolderScheduler.Queue(Run);
                    else using (ExecutionContext.SuppressFlow()) SharedHolderScheduler.Queue(Run);
                }
                catch (Exception error)
                {
                    try { _child.Dispose(); }
                    catch (Exception cleanup) { error.Data["LiteDB.TransactionOpenCleanup"] = cleanup; }
                    finally { _gate.Release(); _opened.Dispose(); _close.Dispose(); }
                    throw;
                }
                _opened.Wait();
                if (_error != null) Release();
                return new TransactionResources(_engine, _engine.CurrentContext, Release, () => _close.Set(), policyAnchor, _child._mutexName);
            }

            private void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception error)
                {
                    if (_error == null) _error = error;
                    else _error.Data["LiteDB.SharedCleanup." + _error.Data.Count] = error;
                }
            }

            private void Run()
            {
                using var dependency = new SessionCloseDependency(_sessionToken);
#if DEBUG || TESTING
                var previousProbe = NativeAdmissionStreamProbe.Attach;
                NativeAdmissionStreamProbe.Attach = _streamProbe;
#endif
                var acquired = false;
                try
                {
                    Acquire(ref acquired);
                    _engine = _child._engine;
                    _opened.Set();
                    _close.Wait();
                }
                catch (Exception error) { _error = error; }
                finally
                {
                    _admission = null;
                    if (acquired) Cleanup(() => _child.CloseDatabase(reportErrors: true));
                    Cleanup(() => _child.EndAdmissions(0));
                    if (_error == null) Cleanup(_child.ReleaseTransactionChildAdmission);
                    if (_error != null || !_cacheOwner.TryGetTarget(out var owner) || !owner.ReturnTransactionChild(_child))
                    {
                        Cleanup(_child.Dispose);
                    }
                    _gate.Release();
                    // Failed-open publication follows all cleanup and preserves its original error.
                    _opened.Set();
#if DEBUG || TESTING
                    NativeAdmissionStreamProbe.Attach = previousProbe;
#endif
                    _done.TrySetResult(true);
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void Acquire(ref bool acquired)
            {
                // Keep admission-token temporaries off the long-lived idle stack.
                try
                {
                    _child.OpenDatabase(scoped: true, writing: !_child._settings.ReadOnly, admission: _admission);
                    acquired = true;
                    _admission.Acquired("storage-opened");
                }
                finally { _admission = null; }
            }

            private void Release()
            {
                _close.Set();
                // Join this job, not the reusable thread. Task completion has no
                // disposable wait handle that could race its final Set operation.
                _done.Task.GetAwaiter().GetResult();
                _opened.Dispose();
                _close.Dispose();

                if (_error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_error).Throw();
            }
        }
    }
}
