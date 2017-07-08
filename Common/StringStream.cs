using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Octagon.Formatik.API
{
    public class StringStream : Stream
    {
        private string source;
        private Encoding encoding;
        private int position, length;
        private int charPosition;
        private int maxBytesPerChar;
        private byte[] encodeBuffer;
        private int encodeOffset, encodeCount;

        internal StringStream(string source, Encoding encoding)
        {
            this.source = source;
            this.encoding = encoding;
            length = encoding.GetByteCount(source);
            maxBytesPerChar = encoding.GetMaxByteCount(1);
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return length; } }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override long Position { get { return position; } set { throw new NotSupportedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int readCount = 0;
            for (int byteCount; readCount < count && position < length; position += byteCount, readCount += byteCount)
            {
                if (encodeCount == 0)
                {
                    int charCount = Math.Min((count - readCount) / maxBytesPerChar, source.Length - charPosition);
                    if (charCount > 0)
                    {
                        byteCount = encoding.GetBytes(source, charPosition, charCount, buffer, offset + readCount);
                        Debug.Assert(byteCount > 0 && byteCount <= (count - readCount));
                        charPosition += charCount;
                        continue;
                    }
                    if (encodeBuffer == null) encodeBuffer = new byte[maxBytesPerChar];
                    encodeCount = encoding.GetBytes(source, charPosition, 1, encodeBuffer, encodeOffset = 0);
                    Debug.Assert(encodeCount > 0);
                }
                byteCount = Math.Min(encodeCount, count - readCount);
                for (int i = 0; i < byteCount; i++)
                    buffer[offset + readCount + i] = encodeBuffer[encodeOffset + i];
                encodeOffset += byteCount;
                if ((encodeCount -= byteCount) == 0) charPosition++;
            }
            return readCount;
        }
    }
}