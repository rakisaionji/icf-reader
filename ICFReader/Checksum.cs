namespace ICFReader
{
    class Checksum
    {
        internal static uint CRC32(byte[] data)
        {
            var crc32Hash = new CRC32Managed();
            return crc32Hash.GetCrc32(data);
        }
    }

    public class CRC32Managed
    {
        private static uint[] table;

        public uint GetCrc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                byte index = (byte)(((crc) & 0xFF) ^ data[i]);
                crc = (uint)((crc >> 8) ^ table[index]);
            }
            return ~crc;
        }

        public CRC32Managed()
        {
            uint poly = 0xEDB88320;
            table = new uint[256];
            uint temp = 0;
            for (uint i = 0; i < table.Length; i++)
            {
                temp = i;
                for (int j = 8; j > 0; j--)
                {
                    if ((temp & 1) == 1)
                    {
                        temp = (uint)((temp >> 1) ^ poly);
                    }
                    else
                    {
                        temp >>= 1;
                    }
                }
                table[i] = temp;
            }
        }
    }
}
