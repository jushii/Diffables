namespace Diffables.Core
{
    public class Serializer
    {
        public Repository Repository => _repository;

        private readonly Repository _repository;

        public Serializer(Repository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public byte[] Serialize<T>(T instance) where T : IDiffable
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            using MemoryStream memoryStream = new MemoryStream();
            using var context = SerializationContext.CreateWriterContext(memoryStream, _repository);

            instance.Encode(context);

            return memoryStream.ToArray();
        }
    }

    public class Deserializer
    {
        public Repository Repository => _repository;

        private readonly Repository _repository;

        public Deserializer(Repository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public T Deserialize<T>(byte[] bytes) where T : IDiffable, new()
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            using MemoryStream memoryStream = new MemoryStream(bytes);
            using var context = SerializationContext.CreateReaderContext(memoryStream, _repository);

            int refId = context.Reader.ReadInt32();
            if (context.Repository.TryGet(refId, out var existingInstance))
            {
                existingInstance.Decode(context);
                return (T)existingInstance;
            }
            else
            {
                T newInstance = new T { RefId = refId };
                context.Repository.Add(newInstance);
                newInstance.Decode(context);
                return newInstance;
            }
        }
    }
}
