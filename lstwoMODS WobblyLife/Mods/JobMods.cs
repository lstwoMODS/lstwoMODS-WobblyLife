using System;
using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using lstwoMODS_WobblyLife;
using lstwoMODS_WobblyLife.Mods.JobManager;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace lstwoMODS_WobblyLife.Mods;

public class JobMods : PlayerBasedMod
{
    public override string Name => "Job Mods";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;

    public static List<BaseJobManager> jobManagers = new();
    private readonly Dictionary<BaseJobManager, Ref<bool>> _managerVisible = new();
    private readonly Dictionary<BaseJobManager, Group> _managerGroup = new();

    public JobMission currentJobMission;

    private readonly Ref<int> _modeIndex = new(0); // 0 = Active Job, 1 = Job Prefab
    private readonly Ref<bool> _prefabSectionVisible = new(false);
    private readonly Ref<string[]> _prefabNames = new(Array.Empty<string>());
    private readonly Ref<int> _prefabIndex = new(0);

    private List<AssetDatabase.AssetEntry> _prefabEntries = new();
    private AsyncOperationHandle<GameObject> _prefabHandle;
    private bool _hasPrefabHandle;
    private JobMission _loadedPrefabMission;

    protected override void OnStaticInit()
    {
        lstwoMODS_Core.Plugin.InitChildClasses<BaseJobManager>();

        // Every manager instantiated above registered itself into jobManagers; expose their
        // macro-callable actions now (once) so they show up in the macro step picker.
        foreach (var jobManager in jobManagers)
        {
            try { jobManager.RegisterMacros(); }
            catch (Exception e) { Plugin.LogSource.LogError($"Error registering Job macros for {jobManager.GetType().Name}: {e}"); }
        }

        AssetDatabase.OnReady += RefreshPrefabList;
        if (AssetDatabase.IsInitialized)
            RefreshPrefabList();
    }

    private void RefreshPrefabList()
    {
        if (AssetDatabase.IsReady)
        {
            _prefabEntries = AssetDatabase.GetAll()
                .Where(e => !string.IsNullOrEmpty(e.LoadKey) &&
                    e.ComponentTypes != null &&
                    e.ComponentTypes.Any(t => t.EndsWith("JobMission", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        else if (AssetDatabase.IsInitialized)
        {
            _prefabEntries = AssetDatabase.GetAll().Where(e => 
                e.IsGameObject && 
                !string.IsNullOrEmpty(e.LoadKey) && 
                e.Address.IndexOf("job", StringComparison.OrdinalIgnoreCase) >= 0 && 
                e.Address.IndexOf("mission", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        _prefabNames.Value = _prefabEntries.Select(e => e.Address).ToArray();
    }

    public override Container BuildPanel(string id)
    {
        _managerGroup.Clear();

        var elements = new List<BaseUIElement>
        {
            new Combo("Mode", new[] { "Active Job", "Job Prefab" }, 0, v =>
            {
                _prefabSectionVisible.Value = v == 1;
                if (v == 0) ReleasePrefabHandle();
                else LoadSelectedPrefab();
                RefreshUI();
            }).WithSelectedIndex(_modeIndex),

            new Group("prefab-section",
                
                new SeparatorText("prefab-sep", "Job Prefab"),
                new SearchableCombo("##prefab", Array.Empty<string>(), 0, _ => LoadSelectedPrefab())
                    .WithItems(_prefabNames)
                    .WithSelectedIndex(_prefabIndex)
                
            ).WithVisible(_prefabSectionVisible)
        };

        foreach (var jobManager in jobManagers)
        {
            if (!_managerVisible.TryGetValue(jobManager, out var visible))
            {
                visible = new Ref<bool>();
                _managerVisible.Add(jobManager, visible);
            }

            var group = new Group($"{jobManager.GetType().Name}-group",
                new SeparatorText($"{jobManager.GetType().Name}-sep", jobManager.DisplayName),
                jobManager.BuildContent($"{jobManager.GetType().Name}-content")
            ).WithVisible(visible).WithId(jobManager.GetType().FullName);

            _managerGroup[jobManager] = group;
            elements.Add(group);
        }

        return new Container(id, elements.ToArray());
    }

    public override void RefreshUI()
    {
        if (Player == null) return;

        bool isPrefabMode = _modeIndex.Value == 1;
        _prefabSectionVisible.Value = isPrefabMode;

        currentJobMission = Player.ControllerEmployment.GetActiveJob();

        foreach (var jobManager in jobManagers)
        {
            try
            {
                if (isPrefabMode)
                {
                    jobManager.SetPlayerController(Player.Controller, _loadedPrefabMission);
                }
                else
                {
                    jobManager.SetPlayerController(Player.Controller);
                }

                jobManager.RefreshUI();

                _managerVisible[jobManager].Value = jobManager.ShouldShow();
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogError("Error refreshing Job Manager: " + e.Message + e.StackTrace);
            }
        }
    }

    private void LoadSelectedPrefab()
    {
        ReleasePrefabHandle();

        if (_prefabEntries.Count == 0) return;
        var index = Math.Max(0, Math.Min(_prefabIndex.Value, _prefabEntries.Count - 1));
        var entry = _prefabEntries[index];

        var handle = Addressables.LoadAssetAsync<GameObject>(entry.LoadKey);
        _prefabHandle = handle;
        _hasPrefabHandle = true;

        handle.Completed += op =>
        {
            if (!_hasPrefabHandle || !_prefabHandle.Equals(op)) return;
            _loadedPrefabMission = op.Status == AsyncOperationStatus.Succeeded
                ? op.Result?.GetComponent<JobMission>()
                : null;
            RefreshUI();
        };
    }

    private void ReleasePrefabHandle()
    {
        _loadedPrefabMission = null;
        if (_hasPrefabHandle && _prefabHandle.IsValid())
            Addressables.Release(_prefabHandle);
        _hasPrefabHandle = false;
    }
}
