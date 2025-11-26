using System.Text.Json.Serialization;
using TimelineAnimator.Format;

namespace TimelineAnimator.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(KtisisPoseFile))]
[JsonSerializable(typeof(BoneDto))]
[JsonSerializable(typeof(Vector3Dto))]
[JsonSerializable(typeof(QuaternionDto))]
public partial class KtisisJsonContext : JsonSerializerContext
{
    // Keep it empty for some reason cause compiler does magic here.
}