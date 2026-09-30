using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LiteDB.Engine;

namespace LiteDB
{
    /// <summary>One explicit transaction and all of its bound objects.</summary>
    internal sealed class LiteTransaction : ILiteTransaction
    {
        private readonly object _gate = new object();
        private TransactionResources _resources;
        private TransactionContext _transaction;
        private volatile LiteTransactionState _outcome;
        private LiteDatabaseContext _client;
        private SessionLifetime _session;
        private readonly HashSet<TransactionReader> _readers = new HashSet<TransactionReader>();
        private Thread _executing;
        private bool _closing, _disposed;

        internal LiteTransaction(TransactionResources resources, BsonMapper mapper, SessionLifetime session)
        {
            _resources = resources;
            _session = session;
            _transaction = new TransactionContext(resources.Engine, resources.Session, resources.SharedMutexName);
            _client = new LiteDatabaseContext(new TransactionEngine(this, resources.Engine), mapper);
            try
            {
                using var context = resources.Session.Enter();
                if (resources.Session.LegacySlot.Transaction != null)
                    throw new InvalidOperationException("Complete the legacy transaction before opening a transaction handle.");
                using var binding = TransactionContext.Enter(_transaction);
                resources.Engine.BeginHandleTransaction();
            }
            catch (Exception error)
            {
                try { resources.Dispose(); }
                catch (Exception cleanup) { error.Data["LiteDB.TransactionOpenCleanup"] = cleanup; }
                throw;
            }
        }

        public LiteTransactionState State => _transaction?.Outcome ?? _outcome;
        internal LiteEngine Storage => _resources?.Engine ?? throw new InvalidOperationException("The transaction has completed.");

        private SessionLifetime.Lease Enter(bool terminalAllowed = false)
        {
            lock (_gate)
            {
                if (_executing != null) throw new InvalidOperationException("Overlapping or reentrant transaction handle use is not supported.");
                if (_closing || _disposed) throw new ObjectDisposedException(nameof(ILiteTransaction));
                if (!terminalAllowed && State != LiteTransactionState.Active)
                    throw new InvalidOperationException("The transaction has completed and its bound objects cannot be reused.");
                var lease = _session.Enter();
                _executing = Thread.CurrentThread;
                return lease;
            }
        }

        private void Exit()
        {
            bool close;
            lock (_gate) { _executing = null; close = _closing && !_disposed; }
            if (close) RequestClose();
        }

        internal T Run<T>(Func<T> action, Action validate = null)
        {
            using var admission = Enter();
            // Child lifetime misuse is an admission refusal, not a statement failure.
            try { validate?.Invoke(); }
            catch { Exit(); throw; }
            return RunCore(action);
        }

        internal bool DisposeBoundObject(Action action)
        {
            using var admission = EnterCleanup();
            return admission.HasValue && RunCore(() => { action(); return true; });
        }

        private T RunCore<T>(Func<T> action)
        {
            using var context = _resources.Session.Enter();
            using var binding = TransactionContext.Enter(_transaction);
            try { return action(); }
            catch (Exception error)
            {
                var intact = ReferenceEquals(_transaction.Slot.Transaction, _transaction.Transaction) &&
                    _transaction.Transaction.State == TransactionState.Active;
                if (!intact || (!(error is TransactionCapabilityException) && !(error is ReadOnlyContextException) &&
                    !(_transaction.Policy.ReadOnly && error is NotSupportedException))) Abort(error);
                throw;
            }
            finally { Exit(); }
        }

        // Only composed client operations may dispatch internally. Public wrappers always use Run.
        internal T Dispatch<T>(Func<T> action, bool authorizeEngine = true)
        {
            lock (_gate)
                if (!ReferenceEquals(_executing, Thread.CurrentThread))
                    throw new InvalidOperationException("A bound engine call requires its transaction operation.");
            using var context = _resources.Session.Enter();
            using var binding = TransactionContext.Enter(_transaction);
            using var dispatch = TransactionContext.Dispatch(authorizeEngine ? _resources.Engine : null);
            return action();
        }

        private void ReleaseResources(Exception cause = null)
        {
            try { _resources.Dispose(); }
            catch (Exception cleanup)
            {
                if (cause == null) throw;
                cause.Data["LiteDB.TransactionReleaseError"] = cleanup;
            }
            finally
            {
                var session = _session;
                _outcome = _transaction.Outcome;
                _transaction = null;
                _resources = null;
                _session = null;
                _client.ReleaseMapper();
                _client = null;
                session.Completed(this);
            }
        }

        private void Abort(Exception cause)
        {
            if (_transaction.Outcome != LiteTransactionState.Active) return;
            _transaction.Outcome = LiteTransactionState.Failed;
            try
            {
                CloseReadersAndRollback(onlyIfActive: true);
            }
            catch (Exception cleanup) { cause.Data["LiteDB.TransactionCleanupError"] = cleanup; }
            finally { ReleaseResources(cause); }
        }

        internal IBsonDataReader Query(string collection, Query query) => Dispatch(() =>
        {
            if ((collection.StartsWith("$") && collection != "$indexes" && collection != "$cols") ||
                query.Into?.StartsWith("$") == true)
                throw new TransactionCapabilityException("External/system collection I/O is not supported inside transaction handles.");
            var reader = new TransactionReader(this, _resources.Engine.Query(collection, query));
            _readers.Add(reader);
            return (IBsonDataReader)reader;
        });

        internal void ReleaseReader(TransactionReader reader)
        {
            reader.Close();
            _readers.Remove(reader);
        }

        private void CloseReaders()
        {
            var errors = new List<Exception>();
            foreach (var reader in _readers.ToArray())
                try { reader.Close(); } catch (Exception error) { errors.Add(error); }
            _readers.Clear();
            if (errors.Count != 0) throw new AggregateException(errors);
        }

        private void CloseReadersAndRollback(bool onlyIfActive = false)
        {
            Exception failure = null;
            try { CloseReaders(); }
            catch (Exception error) { failure = error; }
            try
            {
                if (!onlyIfActive) Dispatch(() => _resources.Engine.Rollback());
                else if (_transaction.Slot.Transaction != null)
                    Dispatch(() => _resources.Engine.RollbackHandleOnDispose());
            }
            catch (Exception error)
            {
                if (failure == null) throw;
                failure.Data["LiteDB.TransactionRollback"] = error;
            }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        public void Commit()
        {
            using var admission = Enter();
            try
            {
                using var context = _resources.Session.Enter();
                using var binding = TransactionContext.Enter(_transaction);
                if (_readers.Count != 0) throw new InvalidOperationException("Close transaction-bound readers before committing.");
                Exception failure = null;
                try { Dispatch(() => _resources.Engine.Commit()); }
                catch (Exception error)
                {
                    failure = error;
                    if (_transaction.Outcome != LiteTransactionState.Committed)
                        _transaction.Outcome = LiteTransactionState.Indeterminate;
                    throw;
                }
                finally { if (_transaction.Outcome != LiteTransactionState.Active) ReleaseResources(failure); }
            }
            finally { Exit(); }
        }

        public void Rollback()
        {
            using var admission = Enter();
            try { RollbackCore(); }
            finally { Exit(); }
        }

        private void RollbackCore(bool onlyIfActive = false)
        {
            using var context = _resources.Session.Enter();
            using var binding = TransactionContext.Enter(_transaction);
            Exception failure = null;
            try
            {
                CloseReadersAndRollback(onlyIfActive);
                _transaction.Outcome = LiteTransactionState.RolledBack;
            }
            catch (Exception error) { failure = error; _transaction.Outcome = LiteTransactionState.Failed; throw; }
            finally { ReleaseResources(failure); }
        }

        // Closing already owns rollback. Cleanup is idempotent across that handoff,
        // but normal overlapping/reentrant user operations remain invalid.
        private SessionLifetime.Lease? EnterCleanup()
        {
            lock (_gate)
            {
                if (_closing || _disposed || State != LiteTransactionState.Active) return null;
                SessionLifetime.Lease admission;
                try { admission = _session.Enter(); }
                catch (ObjectDisposedException) { return null; }
                if (_executing != null)
                {
                    admission.Dispose();
                    throw new InvalidOperationException("Overlapping transaction disposal is not supported.");
                }
                _executing = Thread.CurrentThread;
                return admission;
            }
        }

        public void Dispose()
        {
            lock (_gate)
                if (State != LiteTransactionState.Active) { _disposed = true; return; }
            using var admission = EnterCleanup();
            if (!admission.HasValue) return;
            try { DisposeCore(); }
            finally { Exit(); }
        }

        internal void RequestClose(Exception cause = null)
        {
            lock (_gate)
            {
                _closing = true;
                if (_disposed || _executing != null) return;
                _executing = Thread.CurrentThread;
            }
            var session = _session;
            try { DisposeCore(); }
            catch (Exception error)
            {
                if (cause == null) session?.Report(error);
                else cause.Data["LiteDB.TransactionOpenCleanup"] = error;
            }
            finally { lock (_gate) _executing = null; }
        }

        private void DisposeCore()
        {
            try { if (State == LiteTransactionState.Active) RollbackCore(onlyIfActive: true); }
            finally { lock (_gate) _disposed = true; }
        }

        public ILiteCollection<T> GetCollection<T>(string name = null, BsonAutoId autoId = BsonAutoId.ObjectId) =>
            Run(() => (ILiteCollection<T>)new TransactionCollection<T>(this, new LiteCollection<T>(name, autoId, _client)));
        public ILiteCollection<BsonDocument> GetCollection(string name, BsonAutoId autoId = BsonAutoId.ObjectId) =>
            GetCollection<BsonDocument>(name ?? throw new ArgumentNullException(nameof(name)), autoId);
        public IEnumerable<string> GetCollectionNames() => Run(() => Dispatch(() => _resources.Engine.GetTransactionCollectionNames()));
        public bool CollectionExists(string name) => GetCollectionNames().Contains(name, StringComparer.OrdinalIgnoreCase);
        public bool DropCollection(string name) => throw new TransactionCapabilityException("Dropping collections is not supported inside transactions.");
        public bool RenameCollection(string name, string newName) => throw new TransactionCapabilityException("Renaming collections is not supported inside transactions.");
    }

    internal sealed class TransactionCapabilityException : NotSupportedException
    {
        internal TransactionCapabilityException(string message) : base(message) { }
    }
}
