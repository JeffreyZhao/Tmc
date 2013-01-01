namespace Tmc
{
    using System.Collections.Generic;

    public class NullableKeyComparer<T> : IComparer<NullableKey<T>> {
        private readonly IComparer<T> _valueComparer;

        public NullableKeyComparer(IComparer<T> valueComparer) {
            _valueComparer = valueComparer;
        }

        public int Compare(NullableKey<T> x, NullableKey<T> y) {
            return _valueComparer == null ? x.CompareTo(y) : _valueComparer.Compare(x.Value, y.Value);
        }
    }
}
