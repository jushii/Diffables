namespace Diffables.Core
{
    public class SerializationContext : IDisposable
    {
        public MemoryStream Stream { get; private set; }
        public BinaryWriter Writer { get; private set; }
        public BinaryReader Reader { get; private set; }
        public Repository Repository { get; private set; }

        private SerializationContext(MemoryStream memoryStream, Repository repository)
        {
            Stream = memoryStream;
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public static SerializationContext CreateReaderContext(MemoryStream memoryStream, Repository repository)
        {
            var context = new SerializationContext(memoryStream, repository);
            context.Reader = new BinaryReader(memoryStream, System.Text.Encoding.UTF8, leaveOpen: true);
            return context;
        }

        public static SerializationContext CreateWriterContext(MemoryStream memoryStream, Repository repository)
        {
            var context = new SerializationContext(memoryStream, repository);
            context.Writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, leaveOpen: true);
            return context;
        }

        public void Dispose()
        {
            Writer?.Dispose();
            Reader?.Dispose();
        }
    }
}
