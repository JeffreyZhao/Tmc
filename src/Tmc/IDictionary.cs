namespace Tmc
{
    public interface IDictionary<TKey, TValue> : System.Collections.Generic.IDictionary<TKey, TValue> {
        bool Remove(TKey key, out TValue value);
    }
}
