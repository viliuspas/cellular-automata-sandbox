using UnityEngine;

public struct VoxelTest
{
    public byte fluidLevel;
    private byte flags;

    public bool isSolid
    {
        get => (flags & 0b0000_0001) != 0;
        set
        {
            if (value)
                flags |= 0b0000_0001;
            else
                flags &= 0b1111_1110;
        }
    }

    public bool isSource
    {
        get => (flags & 0b0000_0010) != 0;
        set
        {
            if (value)
                flags |= 0b0000_0010;
            else
                flags &= 0b1111_1101;
        }
    }

    public bool isSettled
    {
        get => (flags & 0b0000_0100) != 0;
        set
        {
            if (value)
                flags |= 0b0000_0100;
            else
                flags &= 0b1111_1011;
        }
    }
}
