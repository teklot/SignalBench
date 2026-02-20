namespace SignalBench.Core.Decoding;

public class BinaryPacketScanner
{
    public IEnumerable<long> ScanForSyncWord(Stream stream, uint syncWord, int syncWordSize = 2)
    {
        byte[] buffer = new byte[4096];
        int bytesRead;
        long position = 0;

        // Simple scanner - can be optimized for performance
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i <= bytesRead - syncWordSize; i++)
            {
                bool match = true;
                for (int j = 0; j < syncWordSize; j++)
                {
                    byte expected = (byte)((syncWord >> (8 * (syncWordSize - 1 - j))) & 0xFF);
                    if (buffer[i + j] != expected)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    yield return position + i;
                }
            }
            position += bytesRead;
            // Seek back a bit to not miss sync word split across buffers
            stream.Position = position - (syncWordSize - 1);
            position -= (syncWordSize - 1);
        }
    }
}
