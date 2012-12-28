namespace Tmc
{
    using System.Collections.Generic;

    public class NullableKeyEqualityComparer<T> : IEqualityComparer<NullableKey<T>> {
        private readonly IEqualityComparer<T> _valueComparer; 

        public NullableKeyEqualityComparer(IEqualityComparer<T> valueComparer) {
            _valueComparer = valueComparer;
        }

        public bool Equals(NullableKey<T> x, NullableKey<T> y) {
            return _valueComparer == null ? x.Equals(y) : _valueComparer.Equals(x.Value, y.Value);
        }

        public int GetHashCode(NullableKey<T> obj) {
            return _valueComparer == null ? obj.GetHashCode() : _valueComparer.GetHashCode(obj.Value);
        }
    }
}
