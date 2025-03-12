namespace Diffables.Core
{
    public enum Operation : byte
    {
        None = 0,
        Update = 1,
        Add = 2,
        AddByRefId = 3,
        Delete = 4
    }

    public class Serializer
    {
        private Repository _repository;

        public Serializer(Repository repository)
        {
            _repository = repository;
        }

        public byte[] Serialize<T>(T instance) where T : IDiffable
        {
            MemoryStream memoryStream = new MemoryStream();

            using SerializationContext context = new SerializationContext(memoryStream, _repository, isReader: false);

            instance.Encode(context);

            return memoryStream.ToArray();
        }
    }

    public class Deserializer
    {
        private Repository _repository;

        public Deserializer(Repository repository)
        {
            _repository = repository;
        }

        public void Deserialize<T>(byte[] bytes) where T : IDiffable, new()
        {
            MemoryStream memoryStream = new MemoryStream(bytes);

            using SerializationContext context = new SerializationContext(memoryStream, _repository, isReader: true);

            int refId = context.Reader.ReadInt32();
            if (context.Repository.TryGet(refId, out var existingInstance))
            {
                existingInstance.Decode(context);
            }
            else
            {
                // This is the first time we deserialize the root level object.
                T instance = new T { RefId = refId };
                context.Repository.Add(instance);
                instance.Decode(context);
            }
        }

        //public IDictionary<int, IDiffable> ObjectReferenceMap { get; private set; } = new Dictionary<int, IDiffable>();

        //public void Deserialize<T>(MemoryStream stream) where T : IDiffable, new()
        //{
        //    using SerializationContext context = new SerializationContext(stream, ObjectReferenceMap, true);
        //    DeserializeInternal<T>(context);
        //}

        //private void DeserializeInternal<T>(SerializationContext context) where T : IDiffable, new()
        //{
        //    int refId = context.Reader.ReadInt32();

        //    if (context.ObjectReferenceMap.TryGetValue(refId, out var existingInstance))
        //    {
        //        existingInstance.DecodeV2(context);
        //    }
        //    else
        //    {
        //        T instance = new T();
        //        instance.RefId = refId;
        //        // Add the new instance to the object reference map.
        //        context.ObjectReferenceMap[refId] = instance;
        //        instance.DecodeV2(context);
        //    }
        //}
    }
}
