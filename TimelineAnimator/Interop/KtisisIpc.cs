using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace TimelineAnimator.Interop;

public class KtisisIpc
{
    private readonly IPluginLog log = Services.Log;
    public bool IsAvailable { get; private set; } = true;

    private readonly ICallGateSubscriber<(int, int)>? _getVersion;
    private readonly ICallGateSubscriber<bool>? _refreshActors;
    private readonly ICallGateSubscriber<bool>? _isPosing;
    private readonly ICallGateSubscriber<uint, string, Task<bool>>? _loadPose;
    private readonly ICallGateSubscriber<uint, Task<string?>>? _savePose;
    private readonly ICallGateSubscriber<uint, string, Matrix4x4, Task<bool>>? _setMatrix;
    private readonly ICallGateSubscriber<uint, string, Task<Matrix4x4?>>? _getMatrix;
    private readonly ICallGateSubscriber<Task<Dictionary<int, HashSet<string>>>>? _getSelectedBones;

    public KtisisIpc()
    {
        try
        {
            IDalamudPluginInterface pluginInterface = Services.PluginInterface;

            _getVersion = pluginInterface.GetIpcSubscriber<(int, int)>("Ktisis.ApiVersion");
            _refreshActors = pluginInterface.GetIpcSubscriber<bool>("Ktisis.RefreshActors");
            _isPosing = pluginInterface.GetIpcSubscriber<bool>("Ktisis.IsPosing");
            _loadPose = pluginInterface.GetIpcSubscriber<uint, string, Task<bool>>("Ktisis.LoadPose");
            _savePose = pluginInterface.GetIpcSubscriber<uint, Task<string?>>("Ktisis.SavePose");
            _setMatrix = pluginInterface.GetIpcSubscriber<uint, string, Matrix4x4, Task<bool>>("Ktisis.SetMatrix");
            _getMatrix = pluginInterface.GetIpcSubscriber<uint, string, Task<Matrix4x4?>>("Ktisis.GetMatrix");
            _getSelectedBones = pluginInterface.GetIpcSubscriber<Task<Dictionary<int, HashSet<string>>>>("Ktisis.SelectedBones");

            log.Information("Ktisis IPC Subscribers initialized.");
        }
        catch (Exception e)
        {
            log.Error("Failed to initialize Ktisis IPC subscribers.");
            log.Error(e, "Ktisis IPC initialization error:");
            IsAvailable = false;
        }
    }

    public (int, int) GetVersion()
    {
        if (!IsAvailable || _getVersion == null) return (0, 0);
        try
        {
            return _getVersion.InvokeFunc();
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.GetVersion"); 
            return (0, 0); 
        }
    }

    // only used for debug
    public bool RefreshActors()
    {
        if (!IsAvailable || _refreshActors == null) return false;
        try
        {
            return _refreshActors.InvokeFunc();
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.RefreshActors");
            return false;
        }
    }

    public bool IsPosing()
    {
        if (!IsAvailable || _isPosing == null) return false;
        try
        {
            return _isPosing.InvokeFunc();
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.IsPosing");
            return false;
        }
    }

    public async Task<bool> LoadPoseAsync(uint actorIndex, string poseJson)
    {
        if (!IsAvailable || _loadPose == null) return false;
        try
        {
            return await _loadPose.InvokeFunc(actorIndex, poseJson);
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.LoadPoseAsync");
            return false;
        }
    }

    public async Task<string?> SavePoseAsync(uint actorIndex)
    {
        if (!IsAvailable || _savePose == null) return null;
        try
        {
            return await _savePose.InvokeFunc(actorIndex);
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.SavePoseAsync");
            return null;
        }
    }

    // will be used once the plugins internal data will be changed
    public async Task<bool> SetMatrixAsync(uint actorIndex, string boneName, Matrix4x4 matrix)
    {
        if (!IsAvailable || _setMatrix == null) return false;
        try
        {
            return await _setMatrix.InvokeFunc(actorIndex, boneName, matrix);
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.SetMatrixAsync");
            return false;
        }
    }

    // will be used once the plugins internal data will be changed
    public async Task<Matrix4x4?> GetMatrixAsync(uint actorIndex, string boneName)
    {
        if (!IsAvailable || _getMatrix == null) return null;
        try
        {
            return await _getMatrix.InvokeFunc(actorIndex, boneName);
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.GetMatrixAsync");
            return null;
        }
    }

    public async Task<Dictionary<int, HashSet<string>>> GetSelectedBonesAsync()
    {
        if (!IsAvailable || _getSelectedBones == null) return new Dictionary<int, HashSet<string>>();
        try
        {
            return await _getSelectedBones.InvokeFunc();
        }
        catch (Exception e)
        {
            log.Error(e, "Error calling Ktisis.GetSelectedBonesAsync");
            return new Dictionary<int, HashSet<string>>();
        }
    }
}