namespace Tmc
{
    using System;
    using System.Collections.Generic;

    public struct NullableKey<T> : IComparable<NullableKey<T>>  {
// ReSharper disable StaticFieldInGenericType
        private static readonly bool CouldBeNull = typeof(T).IsClass;
        private static readonly int NullValueHashCode = new object().GetHashCode();
// ReSharper restore StaticFieldInGenericType

        private readonly T _value;

        public NullableKey(T value) {
            _value = value;
        }

        public T Value {
            get { return _value; }
        }

        public override bool Equals(object obj) {
            return obj is NullableKey<T> && Equals((NullableKey<T>)obj);
        }

        public bool Equals(NullableKey<T> that) {
            return CouldBeNull ? Equals(_value, that._value) : _value.Equals(that._value);
        }

        public int CompareTo(NullableKey<T> other) {
            return Comparer<NullableKey<T>>.Default.Compare(this, other);
        }

        public override int GetHashCode() {
// ReSharper disable CompareNonConstrainedGenericWithNull
            return _value == null ? NullValueHashCode : _value.GetHashCode();
// ReSharper restore CompareNonConstrainedGenericWithNull
        }

        public override string ToString() {
// ReSharper disable CompareNonConstrainedGenericWithNull
            return _value == null ? "<null>" : _value.ToString();
// ReSharper restore CompareNonConstrainedGenericWithNull
        }

        public static implicit operator T(NullableKey<T> key) {
            return key._value;
        }

        public static implicit operator NullableKey<T>(T value) {
            return new NullableKey<T>(value);
        }
    }
}
