namespace Tmc {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    [DebuggerTypeProxy(typeof (CollectionDebugView<>))]
    [DebuggerDisplay("Count = {Count}")]
    public sealed class HashedLinkedList<T> : ICollection<T>, ICollection {
        private readonly IEqualityComparer<T> _comparer;

        private object _syncRoot;
        private int _count;
        private int _version;

        // This HashedLinkedList is a doubly-linked circular list.
        internal HashedLinkedListNode<T> _head;

        public HashedLinkedList() { }

        public HashedLinkedList(IEqualityComparer<T> comparer) {
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public HashedLinkedList(IEnumerable<T> collection) : this(collection, null) { }

        public HashedLinkedList(IEnumerable<T> collection, IEqualityComparer<T> comparer) : this(comparer) {
            if (collection == null) {
                throw new ArgumentNullException("collection");
            }

            foreach (var item in collection) {
                AddLast(item);
            }
        }

        public int Count {
            get { return _count; }
        }

        public HashedLinkedListNode<T> First {
            get { return _head; }
        }

        public HashedLinkedListNode<T> Last {
            get { return _head == null ? null : _head._prev; }
        }

        bool ICollection<T>.IsReadOnly {
            get { return false; }
        }

        void ICollection<T>.Add(T value) {
            AddLast(value);
        }

        public HashedLinkedListNode<T> AddAfter(HashedLinkedListNode<T> node, T value) {
            ValidateNode(node);

            var result = new HashedLinkedListNode<T>(this, value);
            InsertNodeBefore(node._next, result);
            return result;
        }

        public void AddAfter(HashedLinkedListNode<T> node, HashedLinkedListNode<T> newNode) {
            ValidateNode(node);
            ValidateNewNode(newNode);

            InsertNodeBefore(node._next, newNode);
            newNode._list = this;
        }

        public HashedLinkedListNode<T> AddBefore(HashedLinkedListNode<T> node, T value) {
            ValidateNode(node);

            var result = new HashedLinkedListNode<T>(this, value);
            InsertNodeBefore(node, result);

            if (node == _head) {
                _head = result;
            }

            return result;
        }

        public void AddBefore(HashedLinkedListNode<T> node, HashedLinkedListNode<T> newNode) {
            ValidateNode(node);
            ValidateNewNode(newNode);

            InsertNodeBefore(node, newNode);
            newNode._list = this;

            if (node == _head) {
                _head = newNode;
            }
        }

        public HashedLinkedListNode<T> AddFirst(T value) {
            var result = new HashedLinkedListNode<T>(this, value);

            if (_head == null) {
                InsertNodeToEmptyList(result);
            }
            else {
                InsertNodeBefore(_head, result);
                _head = result;
            }

            return result;
        }

        public void AddFirst(HashedLinkedListNode<T> node) {
            ValidateNewNode(node);

            if (_head == null) {
                InsertNodeToEmptyList(node);
            }
            else {
                InsertNodeBefore(_head, node);
                _head = node;
            }

            node._list = this;
        }

        public HashedLinkedListNode<T> AddLast(T value) {
            var result = new HashedLinkedListNode<T>(this, value);

            if (_head == null) {
                InsertNodeToEmptyList(result);
            }
            else {
                InsertNodeBefore(_head, result);
            }

            return result;
        }

        public void AddLast(HashedLinkedListNode<T> node) {
            ValidateNewNode(node);

            if (_head == null) {
                InsertNodeToEmptyList(node);
            }
            else {
                InsertNodeBefore(_head, node);
            }

            node._list = this;
        }

        public void Clear() {
            var current = _head;
            while (current != null) {
                var temp = current;
                current = current.Next; // use Next the instead of "_next", otherwise it will loop forever 
                temp.Invalidate();
            }

            _head = null;
            _count = 0;
            _version++;
        }

        public bool Contains(T value) {
            return Find(value) != null;
        }

        public void CopyTo(T[] array, int index) {
            if (array == null) {
                throw new ArgumentNullException("array");
            }

            if (index < 0 || index > array.Length) {
                throw new ArgumentOutOfRangeException("index", String.Format(ErrorMessages.IndexOutOfRange, index));
            }

            if (array.Length - index < _count) {
                throw new ArgumentException(ErrorMessages.InsufficientSpace);
            }

            var node = _head;
            if (node != null) {
                do {
                    array[index++] = node.Value;
                    node = node._next;
                } while (node != _head);
            }
        }

        public HashedLinkedListNode<T> Find(T value) {
            if (_head == null) return null;

            var node = _head;

            do {
                if (_comparer.Equals(node.Value, value)) {
                    return node;
                }
                node = node._next;
            } while (node != _head);

            return null;
        }

        public HashedLinkedListNode<T> FindLast(T value) {
            if (_head == null) return null;

            var last = _head._prev;
            var node = last;

            do {
                if (_comparer.Equals(node.Value, value)) {
                    return node;
                }
                node = node._prev;
            } while (node != last);

            return null;
        }

        public IEnumerator<T> GetEnumerator() {
            return GetEnumerator(_version);
        }

        public bool Remove(T value) {
            var node = Find(value);
            if (node != null) {
                RemoveNode(node);
                return true;
            }

            return false;
        }

        public void Remove(HashedLinkedListNode<T> node) {
            ValidateNode(node);
            RemoveNode(node);
        }

        public void RemoveFirst() {
            if (_head == null) {
                throw new InvalidOperationException(ErrorMessages.HashedLinkedListEmpty);
            }

            RemoveNode(_head);
        }

        public void RemoveLast() {
            if (_head == null) {
                throw new InvalidOperationException(ErrorMessages.HashedLinkedListEmpty);
            }

            RemoveNode(_head._prev);
        }

        private IEnumerator<T> GetEnumerator(int currentVersion) {
            if (currentVersion != _version) {
                throw new InvalidOperationException(ErrorMessages.EnumFailedVersion);
            }

            var node = _head;
            if (node != null) {
                do {
                    if (currentVersion != _version) {
                        throw new InvalidOperationException(ErrorMessages.EnumFailedVersion);
                    }

                    yield return node.Value;
                    node = node._next;
                } while (node != _head);
            }
        }

        private void InsertNodeBefore(HashedLinkedListNode<T> node, HashedLinkedListNode<T> newNode) {
            newNode._next = node;
            newNode._prev = node._prev;
            node._prev._next = newNode;
            node._prev = newNode;

            _version++;
            _count++;
        }

        private void InsertNodeToEmptyList(HashedLinkedListNode<T> newNode) {
            Debug.Assert(_head == null && _count == 0, "The list must be empty when this method is called!");

            newNode._next = newNode;
            newNode._prev = newNode;
            _head = newNode;

            _version++;
            _count++;
        }

        private void RemoveNode(HashedLinkedListNode<T> node) {
            Debug.Assert(node._list == this, "Deleting the node from another list!");
            Debug.Assert(_head != null, "This method shouldn't be called on empty list!");

            if (node._next == node) {
                Debug.Assert(_count == 1 && _head == node, "this should only be true for a list with only one node");
                _head = null;
            }
            else {
                node._next._prev = node._prev;
                node._prev._next = node._next;

                if (_head == node) {
                    _head = node._next;
                }
            }

            node.Invalidate();
            _count--;
            _version++;
        }

        private static void ValidateNewNode(HashedLinkedListNode<T> node) {
            if (node == null) {
                throw new ArgumentNullException("node");
            }

            if (node._list != null) {
                throw new InvalidOperationException(ErrorMessages.HashedLinkedListNodeIsAttached);
            }
        }

        private void ValidateNode(HashedLinkedListNode<T> node) {
            if (node == null) {
                throw new ArgumentNullException("node");
            }

            if (node._list != this) {
                throw new InvalidOperationException(ErrorMessages.ExternalHashedLinkedListNode);
            }
        }

        bool ICollection.IsSynchronized {
            get { return false; }
        }

        object ICollection.SyncRoot {
            get {
                if (_syncRoot == null) {
                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                }

                return _syncRoot;
            }
        }

        void ICollection.CopyTo(Array array, int index) {
            if (array == null) {
                throw new ArgumentNullException("array");
            }

            if (array.Rank != 1) {
                throw new ArgumentException(ErrorMessages.MultiRank);
            }

            if (array.GetLowerBound(0) != 0) {
                throw new ArgumentException(ErrorMessages.NonZeroLowerBound);
            }

            if (index < 0) {
                throw new ArgumentOutOfRangeException("index", String.Format(ErrorMessages.IndexOutOfRange, index));
            }

            if (array.Length - index < _count) {
                throw new ArgumentException(ErrorMessages.InsufficientSpace);
            }

            var tArray = array as T[];
            if (tArray != null) {
                CopyTo(tArray, index);
            }
            else {
                //
                // Catch the obvious case assignment will fail. 
                // We can found all possible problems by doing the check though.
                // For example, if the element type of the Array is derived from T,
                // we can't figure out if we can successfully copy the element beforehand.
                // 

                var targetType = array.GetType().GetElementType();
                var sourceType = typeof (T);

                if (!(targetType.IsAssignableFrom(sourceType) || sourceType.IsAssignableFrom(targetType))) {
                    throw new ArgumentException(ErrorMessages.InvalidArrayType);
                }

                var objects = array as object[];
                if (objects == null) {
                    throw new ArgumentException(ErrorMessages.InvalidArrayType);
                }

                var node = _head;
                try {
                    if (node != null) {
                        do {
                            objects[index++] = node.Value;
                            node = node._next;
                        } while (node != _head);
                    }
                }
                catch (ArrayTypeMismatchException) {
                    throw new ArgumentException(ErrorMessages.InvalidArrayType);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    public sealed class HashedLinkedListNode<T> {
        private readonly T _value;

        internal HashedLinkedList<T> _list;
        internal HashedLinkedListNode<T> _next;
        internal HashedLinkedListNode<T> _prev;

        public HashedLinkedListNode(T value) {
            _value = value;
        }

        internal HashedLinkedListNode(HashedLinkedList<T> list, T value) {
            _list = list;
            _value = value;
        }

        public HashedLinkedList<T> List {
            get { return _list; }
        }

        public HashedLinkedListNode<T> Next {
            get { return _next == null || _next == List._head ? null : _next; }
        }

        public HashedLinkedListNode<T> Previous {
            get { return _prev == null || this == List._head ? null : _prev; }
        }

        public T Value {
            get { return _value; }
        }

        internal void Invalidate() {
            _list = null;
            _next = null;
            _prev = null;
        }
    }
}