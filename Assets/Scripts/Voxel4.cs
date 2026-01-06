using UnityEngine;

public struct Voxel4
{
    public bool isSolid;
    public bool isSource;
    public Vector3 position;
    public byte fluidLevel;
    public bool isSettled;

    public bool Equals(Voxel4 other)
    {
        return isSolid == other.isSolid &&
               isSource == other.isSource &&
               position == other.position &&
               fluidLevel == other.fluidLevel;
    }
}
