using LiteDB.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace LiteDB
{
    public class SharedDataReader : IBsonDataReader
    {
        private readonly IBsonDataReader _reader;
        private readonly Action _dispose;
        private readonly LiteEngine _ownedSnapshot;
        private readonly SharedEngine _mutexOwner;

        private int _disposed;

        public SharedDataReader(IBsonDataReader reader, Action dispose) : this(reader, dispose, null)
        {
        }

        internal SharedDataReader(IBsonDataReader reader, Action dispose, LiteEngine ownedSnapshot, SharedEngine mutexOwner = null)
        {
            _reader = reader;
            _dispose = dispose;
            _ownedSnapshot = ownedSnapshot;
            _mutexOwner = mutexOwner;
        }

        public BsonValue this[string field] => _reader[field];

        public string Collection => _reader.Collection;

        public BsonValue Current => _reader.Current;

        public bool HasValues => _reader.HasValues;

        public bool Read()
        {
            using var callback = new SharedEngine.CallbackScope(_mutexOwner);
            return _reader.Read();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SharedDataReader()
        {
            this.Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            // A leased snapshot has no parent-owned fallback for a refused core close.
            // Refuse before mutating the cursor or latching disposal, so the caller can
            // retry after this snapshot's executing callback has unwound.
            if (Volatile.Read(ref _disposed) != 0) return;
            if (disposing && _ownedSnapshot?.IsExecutingOnCurrentThread == true)
                throw new InvalidOperationException("Cannot dispose a leased reader from inside its executing operation.");

            // Atomic admission: the callback ends one mutex recursion and one engine user.
            // Two threads disposing at once must not both run it, or the second would end
            // another reader's ownership and could close the engine under it.
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            if (disposing)
            {
                try { _reader.Dispose(); }
                finally { _dispose(); }
            }
        }
    }
}
