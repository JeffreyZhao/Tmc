namespace Tmc {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    internal class CollectionDebugView<T> {
        private readonly ICollection<T> _collection;

        public CollectionDebugView(ICollection<T> collection) {
            if (collection == null) {
                throw new ArgumentNullException("collection");
            }

            _collection = collection;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items {
            get {
                var items = new T[_collection.Count];
                _collection.CopyTo(items, 0);
                return items;
            }
        }
    }

    internal sealed class DictionaryDebugView<TKey, TValue> {
        public sealed class KeyCollectionView : CollectionDebugView<TKey> {
            public KeyCollectionView(ICollection<TKey> collection) : base(collection) { }
        }

        public sealed class ValueCollectionView : CollectionDebugView<TValue> {
            public ValueCollectionView(ICollection<TValue> collection) : base(collection) { }
        }

        private readonly IDictionary<TKey, TValue> _dict;

        public DictionaryDebugView(IDictionary<TKey, TValue> dictionary) {
            if (dictionary == null) {
                throw new ArgumentNullException("dictionary");
            }

            _dict = dictionary;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<TKey, TValue>[] Items {
            get { return _dict.ToArray(); }
        }
    }
}
