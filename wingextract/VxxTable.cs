using System.Collections.Generic;

namespace wingextract
{
    public class VxxTable
    {
        public uint BlockLength { get; private set; }
        public List<uint> Offsets { get; set; }

        public VxxTable(uint blockLength)
        {
            BlockLength = blockLength;
            Offsets = new List<uint>();
        }
    }
}
