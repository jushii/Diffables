namespace Diffables.Core
{
    public class Repository
    {
        private readonly Dictionary<int, IDiffable> _objectReferenceMap = new();

        public void Add(IDiffable instance)
        {
            if (_objectReferenceMap.TryGetValue(instance.RefId, out _))
            {
                throw new Exception($"Instance with RefId {instance.RefId} is already added to the object reference map.");
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
                throw new Exception($"Problem trying to remove instance with RefId {refId} from object reference map.");
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
