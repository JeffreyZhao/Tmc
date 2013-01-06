namespace Tmc
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

    [DebuggerTypeProxy(typeof(DictionaryDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    public class HashDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private struct Entry {
            public int HashCode; // Lower 31 bits of hash code, -1 if unused 
            public int Next; // Index of next entry, -1 if last
            public TKey Key; // Key of entry 
            public TValue Value; // Value of entry
        }

        private readonly IEqualityComparer<TKey> _comparer;

        private int[] _buckets;
        private Entry[] _entries;
        private int _count;
        private int _version;
        private int _freeList;
        private int _freeCount;
        private KeyCollection _keys;
        private ValueCollection _values;

        public HashDictionary() : this(0, null) { }

        public HashDictionary(int capacity) : this(capacity, null) { }

        public HashDictionary(IEqualityComparer<TKey> comparer) : this(0, comparer) { }

        public HashDictionary(int capacity, IEqualityComparer<TKey> comparer) {
            if (capacity < 0) throw new ArgumentOutOfRangeException("capacity");
            if (capacity > 0) Initialize(capacity);
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        public HashDictionary(ICollection<KeyValuePair<TKey, TValue>> dictionary) : this(dictionary, null) { }

        public HashDictionary(ICollection<KeyValuePair<TKey, TValue>> dictionary, IEqualityComparer<TKey> comparer) :
            this(dictionary != null ? dictionary.Count : 0, comparer) {

            if (dictionary == null) {
                throw new ArgumentNullException("dictionary");
            }

            foreach (var pair in dictionary) {
                Add(pair.Key, pair.Value);
            }
        }

        public IEqualityComparer<TKey> Comparer {
            get { return _comparer; }
        }

        public int Count {
            get { return _count - _freeCount; }
        }

        public KeyCollection Keys {
            get { return _keys ?? (_keys = new KeyCollection(this)); }
        }

        ICollection<TKey> System.Collections.Generic.IDictionary<TKey, TValue>.Keys {
            get { return Keys; }
        }

        public ValueCollection Values {
            get { return _values ?? (_values = new ValueCollection(this)); }
        }

        ICollection<TValue> System.Collections.Generic.IDictionary<TKey, TValue>.Values {
            get { return Values; }
        }

        public TValue this[TKey key] {
            get {
                var i = FindEntry(key);
                if (i >= 0) return _entries[i].Value;

                throw new KeyNotFoundException();
            }
            set { Insert(key, value, false); }
        }

        public void Add(TKey key, TValue value) {
            Insert(key, value, true);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> pair) {
            Add(pair.Key, pair.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> pair) {
            var i = FindEntry(pair.Key);
            return i >= 0 && EqualityComparer<TValue>.Default.Equals(_entries[i].Value, pair.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> pair) {
            var i = FindEntry(pair.Key);
            if (i >= 0 && EqualityComparer<TValue>.Default.Equals(_entries[i].Value, pair.Value)) {
                Remove(pair.Key);
                return true;
            }

            return false;
        }

        public void Clear() {
            if (_count > 0) {
                for (int i = 0; i < _buckets.Length; i++)
                    _buckets[i] = -1;

                Array.Clear(_entries, 0, _count);

                _freeList = -1;
                _count = 0;
                _freeCount = 0;
                _version++;
            }
        }

        public bool ContainsKey(TKey key) {
            return FindEntry(key) >= 0;
        }

        public bool ContainsValue(TValue value) {
// ReSharper disable CompareNonConstrainedGenericWithNull
            if (value == null) {
                for (var i = 0; i < _count; i++) {
                    if (_entries[i].HashCode >= 0 && _entries[i].Value == null) return true;
                }
            }
// ReSharper restore CompareNonConstrainedGenericWithNull
            else {
                var comparer = EqualityComparer<TValue>.Default;
                for (var i = 0; i < _count; i++) {
                    if (_entries[i].HashCode >= 0 && comparer.Equals(_entries[i].Value, value)) return true;
                }
            }

            return false;
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index) {
            if (array == null) {
                throw new ArgumentNullException("array");
            }

            if (index < 0 || index > array.Length) {
                throw new ArgumentOutOfRangeException("index");
            }

            if (array.Length - index < Count) {
                throw new ArgumentException(ErrorMessages.ArrayPlusOffTooSmall);
            }

            var count = _count;
            var entries = _entries;

            for (var i = 0; i < count; i++) {
                if (entries[i].HashCode >= 0) {
                    array[index++] = new KeyValuePair<TKey, TValue>(entries[i].Key, entries[i].Value);
                }
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            return GetEnumerator(_version);
        }

        private int FindEntry(TKey key) {
// ReSharper disable CompareNonConstrainedGenericWithNull
            if (key == null) {
                throw new ArgumentNullException("key");
            }
// ReSharper restore CompareNonConstrainedGenericWithNull

            if (_buckets != null) {
                var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
                for (var i = _buckets[hashCode % _buckets.Length]; i >= 0; i = _entries[i].Next) {
                    if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key)) return i;
                }
            }

            return -1;
        }

        private IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator(int currentVersion) {
            var count = _count;
            var entries = _entries;

            for (var index = 0; index < count; index++)
            {
                ValidateUnmutation(currentVersion);

                if (entries[index].HashCode >= 0) {
                    yield return new KeyValuePair<TKey, TValue>(entries[index].Key, entries[index].Value);
                }
            }

            ValidateUnmutation(currentVersion);
        }

        private void Initialize(int capacity) {
            var size = HashHelpers.GetPrime(capacity);

            _buckets = new int[size];
            for (var i = 0; i < _buckets.Length; i++)
                _buckets[i] = -1;

            _entries = new Entry[size];
            _freeList = -1;
        }

        private void Insert(TKey key, TValue value, bool add) {
// ReSharper disable CompareNonConstrainedGenericWithNull
            if (key == null) {
                throw new ArgumentNullException("key");
            }
// ReSharper restore CompareNonConstrainedGenericWithNull

            if (_buckets == null) Initialize(0);
            var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
// ReSharper disable PossibleNullReferenceException
            var targetBucket = hashCode % _buckets.Length;
// ReSharper restore PossibleNullReferenceException

            for (var i = _buckets[targetBucket]; i >= 0; i = _entries[i].Next) {
                if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key)) {
                    if (add) {
                        throw new ArgumentNullException(ErrorMessages.AddingDuplicate);
                    }

                    _entries[i].Value = value;
                    _version++;
                    return;
                }
            }

            int index;
            if (_freeCount > 0) {
                index = _freeList;
                _freeList = _entries[index].Next;
                _freeCount--;
            }
            else {
                if (_count == _entries.Length) {
                    Resize();
                    targetBucket = hashCode % _buckets.Length;
                }
                index = _count;
                _count++;
            }

            _entries[index].HashCode = hashCode;
            _entries[index].Next = _buckets[targetBucket];
            _entries[index].Key = key;
            _entries[index].Value = value;
            _buckets[targetBucket] = index;
            _version++;
        }

        private void Resize() {
            Resize(HashHelpers.ExpandPrime(_count), false);
        }

        private void Resize(int newSize, bool forceNewHashCodes) {
            Debug.Assert(newSize >= _entries.Length, "The new size should be greater than the current size");

            var newBuckets = new int[newSize];
            for (var i = 0; i < newBuckets.Length; i++)
                newBuckets[i] = -1;

            var newEntries = new Entry[newSize];
            Array.Copy(_entries, 0, newEntries, 0, _count);

            if (forceNewHashCodes) {
                for (var i = 0; i < _count; i++) {
                    if (newEntries[i].HashCode != -1) {
                        newEntries[i].HashCode = (_comparer.GetHashCode(newEntries[i].Key) & 0x7FFFFFFF);
                    }
                }
            }

            for (var i = 0; i < _count; i++) {
                var bucket = newEntries[i].HashCode % newSize;
                newEntries[i].Next = newBuckets[bucket];
                newBuckets[bucket] = i;
            }

            _buckets = newBuckets;
            _entries = newEntries;
        }

        public bool Remove(TKey key) {
            TValue value;
            return Remove(key, out value);
        }

        public bool Remove(TKey key, out TValue value) {
// ReSharper disable CompareNonConstrainedGenericWithNull
            if (key == null) {
                throw new ArgumentNullException("key");
            }
// ReSharper restore CompareNonConstrainedGenericWithNull

            if (_buckets != null) {
                var hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
                var bucket = hashCode % _buckets.Length;
                var last = -1;

                for (var i = _buckets[bucket]; i >= 0; last = i, i = _entries[i].Next) {
                    if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key)) {
                        if (last < 0) {
                            _buckets[bucket] = _entries[i].Next;
                        }
                        else {
                            _entries[last].Next = _entries[i].Next;
                        }

                        value = _entries[i].Value;

                        _entries[i].HashCode = -1;
                        _entries[i].Next = _freeList;
                        _entries[i].Key = default(TKey);
                        _entries[i].Value = default(TValue);
                        _freeList = i;
                        _freeCount++;
                        _version++;

                        return true;
                    }
                }
            }

            value = default(TValue);
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) {
            var i = FindEntry(key);
            if (i >= 0) {
                value = _entries[i].Value;
                return true;
            }

            value = default(TValue);
            return false;
        }

// ReSharper disable UnusedParameter.Local
        private void ValidateUnmutation(int version) {
            if (_version != version) {
                throw new InvalidOperationException(ErrorMessages.EnumFailedVersion);
            }
        }
// ReSharper restore UnusedParameter.Local

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly {
            get { return false; }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        [DebuggerTypeProxy(typeof(DictionaryDebugView<,>.KeyCollectionView))]
        [DebuggerDisplay("Count = {Count}")]
        public sealed class KeyCollection : ICollection<TKey> {
            private readonly HashDictionary<TKey, TValue> _dictionary;

            public KeyCollection(HashDictionary<TKey, TValue> dictionary)
            {
                if (dictionary == null) {
                    throw new ArgumentNullException("dictionary");
                }

                _dictionary = dictionary;
            }

            public IEnumerator<TKey> GetEnumerator() {
                return GetEnumerator(_dictionary._version);
            }

            private IEnumerator<TKey> GetEnumerator(int currentVersion) {
                var count = _dictionary._count;
                var entries = _dictionary._entries;

                for (var index = 0; index < count; index++) {
                    _dictionary.ValidateUnmutation(currentVersion);

                    if (entries[index].HashCode >= 0) {
                        yield return entries[index].Key;
                    }
                }

                _dictionary.ValidateUnmutation(currentVersion);
            }

            public void CopyTo(TKey[] array, int index) {
                if (array == null) {
                    throw new ArgumentNullException("array");
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException("index");
                }

                if (array.Length - index < Count) {
                    throw new ArgumentException(ErrorMessages.ArrayPlusOffTooSmall);
                }

                var count = _dictionary._count;
                var entries = _dictionary._entries;

                for (var i = 0; i < count; i++) {
                    if (entries[i].HashCode >= 0) {
                        array[index++] = entries[i].Key;
                    }
                }
            }

            public int Count {
                get { return _dictionary.Count; }
            }

            bool ICollection<TKey>.IsReadOnly {
                get { return true; }
            }

            void ICollection<TKey>.Add(TKey item) {
                throw new NotSupportedException(ErrorMessages.DictionaryMutateKeyCollectionNotSupported);
            }

            void ICollection<TKey>.Clear() {
                throw new NotSupportedException(ErrorMessages.DictionaryMutateKeyCollectionNotSupported);
            }

            bool ICollection<TKey>.Contains(TKey item) {
                return _dictionary.ContainsKey(item);
            }

            bool ICollection<TKey>.Remove(TKey item) {
                throw new NotSupportedException(ErrorMessages.DictionaryMutateKeyCollectionNotSupported);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }

        [DebuggerTypeProxy(typeof(DictionaryDebugView<,>.ValueCollectionView))]
        [DebuggerDisplay("Count = {Count}")]
        public sealed class ValueCollection : ICollection<TValue> {
            private readonly HashDictionary<TKey, TValue> _dictionary;

            public ValueCollection(HashDictionary<TKey, TValue> dictionary) {
                if (dictionary == null) {
                    throw new ArgumentNullException("dictionary");
                }

                _dictionary = dictionary;
            }

            public IEnumerator<TValue> GetEnumerator() {
                return GetEnumerator(_dictionary._version);
            }

            private IEnumerator<TValue> GetEnumerator(int currentVersion) {
                var count = _dictionary._count;
                var entries = _dictionary._entries;

                for (var index = 0; index < count; index++) {
                    _dictionary.ValidateUnmutation(currentVersion);

                    if (entries[index].HashCode >= 0) {
                        yield return entries[index].Value;
                    }
                }

                _dictionary.ValidateUnmutation(currentVersion);
            }

            public void CopyTo(TValue[] array, int index) {
                if (array == null) {
                    throw new ArgumentNullException("array");
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException("index");
                }

                if (array.Length - index < Count) {
                    throw new ArgumentException(ErrorMessages.ArrayPlusOffTooSmall);
                }

                var count = _dictionary._count;
                var entries = _dictionary._entries;

                for (var i = 0; i < count; i++) {
                    if (entries[i].HashCode >= 0) {
                        array[index++] = entries[i].Value;
                    }
                }
            }

            public int Count {
                get { return _dictionary.Count; }
            }

            bool ICollection<TValue>.IsReadOnly {
                get { return true; }
            }

            void ICollection<TValue>.Add(TValue item) {
                throw new NotSupportedException(ErrorMessages.DictionaryMutateValueCollectionNotSupported);
            }

            void ICollection<TValue>.Clear() {
                throw new NotSupportedException(ErrorMessages.DictionaryMutateValueCollectionNotSupported);
            }

            bool ICollection<TValue>.Contains(TValue item) {
                return _dictionary.ContainsValue(item);
            }

            bool ICollection<TValue>.Remove(TValue item) {
                throw new NotSupportedException(ErrorMessages.DictionaryMutateValueCollectionNotSupported);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }
        }
    }
}