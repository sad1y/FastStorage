namespace FeatureStorage.Storage;

public class RamFile : IFile
{
    private readonly RamDirectory _parent;
    private byte[] _content;

    public string Name { get; }
    public bool IsDeleted { get; private set; }

    public RamFile(RamDirectory parent, string name)
    {
        Name = name;
        _content = Array.Empty<byte>();
        _parent = parent;
    }

    public ValueTask Delete(CancellationToken token)
    {
        Delete();
        return new ValueTask();
    }

    public void Delete()
    {
        IsDeleted = true;
        _parent.RemoveChild(this);
    }

    public Stream OpenRead()
    {
        if (IsDeleted)
            throw new IOException("file doesn't exists");
        return new RamStream(this);
    }

    public Stream OpenWrite()
    {
        return new RamStream(this);
    }

    private class RamStream : Stream
    {
        private readonly RamFile _file;
        private readonly MemoryStream _stream;

        public RamStream(RamFile file)
        {
            _file = file;
            _stream = new MemoryStream();
            _stream.Write(_file._content);
            _stream.Position = 0;
        }

        public override void Flush()
        {
            _file._content = _stream.ToArray();
        }

        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

        public override void SetLength(long value) => _stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;

        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            
        }
    }
}