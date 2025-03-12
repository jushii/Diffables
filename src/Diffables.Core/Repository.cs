namespace Diffables.Core
{
    public class Repository
    {
        private readonly Dictionary<int, (IDiffable Instance, int Count)> _objectReferenceMap = new();

        public void Add(IDiffable instance)
        {
            if (_objectReferenceMap.TryGetValue(instance.RefId, out var entry))
            {
                _objectReferenceMap[instance.RefId] = (entry.Instance, entry.Count++);
            }
            else
            {
                _objectReferenceMap[instance.RefId] = (instance, 1);
            }
        }

        public void Remove(int refId)
        {
            if (_objectReferenceMap.TryGetValue(refId, out var entry))
            {
                int newCount = entry.Count - 1;
                if (newCount <= 0)
                {
                    _objectReferenceMap.Remove(refId);
                }
                else
                {
                    _objectReferenceMap[refId] = (entry.Instance, newCount);
                }
            }
        }

        public bool TryGet(int refId, out IDiffable instance)
        {
            if (_objectReferenceMap.TryGetValue(refId, out var entry))
            {
                instance = entry.Instance;
                return true;
            }

            instance = null;
            return false;
        }
    }
}
