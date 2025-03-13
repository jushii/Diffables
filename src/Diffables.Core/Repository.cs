namespace Diffables.Core
{
    public class Repository
    {
        private readonly Dictionary<int, IDiffable> _objectReferenceMap = new();

        public void Add(IDiffable instance)
        {
            if (_objectReferenceMap.ContainsKey(instance.RefId))
            {
                throw new InvalidOperationException($"Instance with RefId {instance.RefId} is already added to the repository.");
            }
            _objectReferenceMap[instance.RefId] = instance;
            instance.OnRefCountChanged += OnInstanceRefCountChanged;
        }

        private void OnInstanceRefCountChanged((int RefId, int RefCount) tuple)
        {
            if (tuple.RefCount <= 0) Remove(tuple.RefId);
        }

        public void Remove(int refId)
        {
            if (_objectReferenceMap.TryGetValue(refId, out IDiffable instance))
            {
                instance.OnRefCountChanged -= OnInstanceRefCountChanged;
                _objectReferenceMap.Remove(refId);
            }
            else
            {
                throw new KeyNotFoundException($"No instance found with RefId {refId} in the repository.");
            }
        }

        public bool TryGet(int refId, out IDiffable instance)
        {
            if (_objectReferenceMap.TryGetValue(refId, out IDiffable existingInstance))
            {
                instance = existingInstance;
                return true;
            }

            instance = null;
            return false;
        }
    }
}
