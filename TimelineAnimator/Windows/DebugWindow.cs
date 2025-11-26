using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using TimelineAnimator.Interop;
using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using TimelineAnimator.Format;
using YourPlugin.Serialization;

namespace TimelineAnimator.Windows;

public class DebugWindow : Window, IDisposable
{
    private readonly KtisisIpc ipc;

    private int actorIndex = 0;
    private string boneName = "n_root";
    private string poseJsonInput = "{}";
    private readonly string ipcStatus;
    private string versionResult = "N/A";
    private string isPosingResult = "N/A";
    private string refreshResult = "N/A";
    private string selectedBonesResult = "N/A";
    private string savePoseResult = "N/A";
    private string loadPoseResult = "N/A";
    private string getMatrixResult = "N/A";

    private string jsonTestBuffer = "Paste your Ktisis JSON here...";
    private string jsonTestResult = "Ready.";

    public DebugWindow(Plugin plugin) : base("Timeline Animator Debugger###TADebugWindow")
    {
        this.ipc = plugin.KtisisIpc;
        this.ipcStatus = ipc.IsAvailable ? "Available" : "NOT AVAILABLE";

        Size = new Vector2(600, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.MenuBar;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginMenuBar())
        {
            ImGui.TextDisabled($"IPC Status: {ipcStatus}");
            ImGui.EndMenuBar();
        }

        if (!ipc.IsAvailable)
        {
            ImGui.TextDisabled("IPC is not available. Please ensure Ktisis is installed, running, and up to date.");
            return;
        }

        if (ImGui.BeginTabBar("DebugTabs"))
        {
            if (ImGui.BeginTabItem("IPC Test"))
            {
                DrawIpcTestTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("JSON Serializer Test"))
            {
                DrawJsonSerializerTestTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawIpcTestTab()
    {
        if (ImGui.Button("GetVersion"))
        {
            var (major, minor) = ipc.GetVersion();
            versionResult = $"{major}.{minor}";
        }
        ImGui.SameLine();
        ImGui.Text($"API Version: {versionResult}");

        if (ImGui.Button("RefreshActors"))
        {
            refreshResult = ipc.RefreshActors().ToString();
        }
        ImGui.SameLine();
        ImGui.Text($"Refresh Result: {refreshResult}");

        if (ImGui.Button("IsPosing"))
        {
            isPosingResult = ipc.IsPosing().ToString();
        }
        ImGui.SameLine();
        ImGui.Text($"IsPosing Result: {isPosingResult}");

        ImGui.Separator();

        ImGui.DragInt("Target Actor Index", ref actorIndex, 0.1f, 0, 100);

        if (ImGui.Button("GetSelectedBonesAsync"))
        {
            Task.Run(async () =>
            {
                var result = await ipc.GetSelectedBonesAsync();
                if (result == null || result.Count == 0)
                {
                    selectedBonesResult = "No bones selected or result is null.";
                    return;
                }
                var sb = new StringBuilder();
                foreach (var (idx, bones) in result)
                {
                    sb.AppendLine($"Actor {idx}:");
                    foreach (var bone in bones)
                    {
                        sb.AppendLine($"  - {bone}");
                    }
                }
                selectedBonesResult = sb.ToString();
            });
        }
        ImGui.Text("Selected Bones (from all actors):");
        ImGui.InputTextMultiline("##SelectedBonesResult", ref selectedBonesResult, 10000, new Vector2(-1, 60), ImGuiInputTextFlags.ReadOnly);

        ImGui.Separator();

        if (ImGui.Button("SavePoseAsync (from Target Actor)"))
        {
            savePoseResult = "Loading...";
            Task.Run(async () =>
            {
                var result = await ipc.SavePoseAsync((uint)actorIndex);
                savePoseResult = result ?? "null";
            });
        }
        ImGui.Text("Saved Pose JSON:");
        ImGui.InputTextMultiline("##SavePoseResult", ref savePoseResult, 500000, new Vector2(-1, 80), ImGuiInputTextFlags.ReadOnly);

        ImGui.Text("Pose JSON to Load (to Target Actor):");
        ImGui.InputTextMultiline("##LoadPoseInput", ref poseJsonInput, 500000, new Vector2(-1, 80));
        if (ImGui.Button("LoadPoseAsync (to Target Actor)"))
        {
            Task.Run(async () =>
            {
                var result = await ipc.LoadPoseAsync((uint)actorIndex, poseJsonInput);
                loadPoseResult = result.ToString();
            });
        }
        ImGui.SameLine();
        ImGui.Text($"Load Pose Result: {loadPoseResult}");

        ImGui.Separator();

        ImGui.InputText("Bone Name", ref boneName, 100);
        if (ImGui.Button("GetMatrixAsync (from Target Actor)"))
        {
            getMatrixResult = "Loading...";
            Task.Run(async () =>
            {
                var result = await ipc.GetMatrixAsync((uint)actorIndex, boneName);
                getMatrixResult = result?.ToString() ?? "null";
            });
        }
        ImGui.Text("GetMatrix Result:");
        ImGui.InputTextMultiline("##GetMatrixResult", ref getMatrixResult, 1000, new Vector2(-1, 60), ImGuiInputTextFlags.ReadOnly);
    }

    private void DrawJsonSerializerTestTab()
    {
        ImGui.TextWrapped("Test System.Text.Json Source Generation for KtisisPoseFile");
        ImGui.Separator();

        if (ImGui.Button("Deserialize & Re-serialize Test"))
        {
            TestJsonRoundtrip();
        }
        ImGui.SameLine();
        if (ImGui.Button("Load Sample JSON"))
        {
            GenerateSampleJson();
        }

        ImGui.Text("JSON Data:");
        ImGui.InputTextMultiline("##JsonTestBuffer", ref jsonTestBuffer, 1000000, new Vector2(-1, ImGui.GetContentRegionAvail().Y - 70));

        ImGui.Text("Result:");
        ImGui.TextWrapped(jsonTestResult);
    }

    private void GenerateSampleJson()
    {
        try
        {
            var samplePose = new KtisisPoseFile
            {
                FileExtension = ".pose",
                TypeName = "Ktisis Pose",
                Position = new Vector3Dto { X = 1, Y = 2, Z = 3 },
                Rotation = new QuaternionDto { X = 0, Y = 0, Z = 0, W = 1, IsIdentity = true },
                Bones = new Dictionary<string, BoneDto>
                {
                    ["n_root"] = new BoneDto
                    {
                        Position = new Vector3Dto { X = 0, Y = 0, Z = 0 },
                        Rotation = new QuaternionDto { X = 0, Y = 0, Z = 0, W = 1 },
                        Scale = new Vector3Dto { X = 1, Y = 1, Z = 1 }
                    },
                    ["n_hara"] = new BoneDto
                    {
                        Position = new Vector3Dto { X = 0.1f, Y = 0.2f, Z = 0.3f },
                        Rotation = new QuaternionDto { X = 0, Y = 0, Z = 0, W = 1 },
                        Scale = new Vector3Dto { X = 1, Y = 1, Z = 1 }
                    }
                },
                FileVersion = 1
            };

            string unformattedJson = JsonSerializer.Serialize(samplePose, KtisisJsonContext.Default.KtisisPoseFile);

            using var jsonDoc = JsonDocument.Parse(unformattedJson);
            jsonTestBuffer = JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions { WriteIndented = true });

            jsonTestResult = "Successfully generated and serialized a sample pose file.";
        }
        catch (Exception e)
        {
            jsonTestResult = $"Error generating sample: {e.Message}";
        }
    }

    private void TestJsonRoundtrip()
    {
        try
        {
            var deserializedObject = JsonSerializer.Deserialize(
                jsonTestBuffer,
                KtisisJsonContext.Default.KtisisPoseFile
            );

            if (deserializedObject == null)
            {
                jsonTestResult = "Error: Deserialization returned null.";
                return;
            }

            string unformattedJson = JsonSerializer.Serialize(deserializedObject, KtisisJsonContext.Default.KtisisPoseFile);

            using var jsonDoc = JsonDocument.Parse(unformattedJson);
            jsonTestBuffer = JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions { WriteIndented = true });

            jsonTestResult = $"SUCCESS: Round-trip complete. Deserialized {deserializedObject.Bones?.Count ?? 0} bones.";
        }
        catch (Exception e)
        {
            jsonTestResult = $"Error during round-trip: {e.Message}\n\nCheck your JSON syntax!";
        }
    }
}