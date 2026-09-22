using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LLLMPluginModern.Infrastructure.Services
{
    internal sealed class ResponseStream : Stream
    {
        private readonly Stream source;
        private readonly int limit;
        private int received;
        public ResponseStream(Stream source, int limit) { this.source = source; this.limit = limit; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        private int Count(int count)
        {
            received += count;
            if (received > limit) throw new InvalidDataException();
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Count(source.Read(buffer, offset, Math.Min(count, limit - received + 1)));
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => Count(await source.ReadAsync(buffer, offset, Math.Min(count, limit - received + 1), token).ConfigureAwait(false));
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}
