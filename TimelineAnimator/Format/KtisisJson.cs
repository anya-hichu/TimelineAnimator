using System.Collections.Generic;

namespace TimelineAnimator.Format;

public class KtisisPoseFile
{
    public string FileExtension { get; set; }
    public string TypeName { get; set; }
    public Vector3Dto Position { get; set; }
    public QuaternionDto Rotation { get; set; }
    public Dictionary<string, BoneDto> Bones { get; set; }
    // not needed but nice to have maybe in future
    //public object? MainHand { get; set; }
    //public object? OffHand { get; set; }
    //public object? Prop { get; set; }
    public int FileVersion { get; set; }
}

public class BoneDto
{
    public Vector3Dto Position { get; set; }
    public QuaternionDto Rotation { get; set; }
    public Vector3Dto Scale { get; set; }
}

public class Vector3Dto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public class QuaternionDto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }
    public bool IsIdentity { get; set; }
}