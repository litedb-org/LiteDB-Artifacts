using System;
using System.Threading;
using LiteDB.Engine;

namespace LiteDB
{
    /// <summary>A transaction's storage dependency, separate from its execution guard.</summary>
    internal sealed class TransactionResources : IDisposable
    {
        internal readonly LiteEngine Engine;
        internal readonly EngineContext Session;
        internal readonly string SharedMutexName;
        private Action _release;
        private Action _abandon;
        private object _policyAnchor;
        internal TransactionResources(LiteEngine engine, EngineContext session, Action release, Action abandon = null, object policyAnchor = null, string sharedMutexName = null)
        { Engine = engine; Session = session; SharedMutexName = sharedMutexName; _release = release; _abandon = abandon; _policyAnchor = policyAnchor; }
        public void Dispose()
        {
            _abandon = null;
            _policyAnchor = null;
            GC.SuppressFinalize(this);
            Interlocked.Exchange(ref _release, null)?.Invoke();
        }
        ~TransactionResources() { try { _abandon?.Invoke(); } catch { } }
    }
}
