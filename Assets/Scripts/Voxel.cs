using UnityEngine;

public struct Voxel
{
    public bool isSolid;
    public bool isSource;
    public byte fluidLevel;
    public bool isSettled;
    public bool isFalling;

    public bool Equals(Voxel other)
    {
        return isSolid == other.isSolid &&
               fluidLevel == other.fluidLevel;
    }
}
