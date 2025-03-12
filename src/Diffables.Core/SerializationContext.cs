namespace Diffables.Core
{
    public struct SerializationContext : IDisposable
    {
        public MemoryStream Stream { get; private set; }
        public BinaryWriter Writer { get; private set; }
        public BinaryReader Reader { get; private set; }
        public Repository Repository { get; private set; }

        public SerializationContext(MemoryStream memoryStream, Repository repository, bool isReader)
        {
            Stream = memoryStream;
            Repository = repository;

            if (isReader)
            {
                Reader = new BinaryReader(memoryStream, System.Text.Encoding.UTF8, leaveOpen: true);
            }
            else
            {
                Writer = new BinaryWriter(memoryStream, System.Text.Encoding.UTF8, leaveOpen: true);
            }
        }

        public void Dispose()
        {
            Writer?.Dispose();
            Reader?.Dispose();
        }
    }
}
