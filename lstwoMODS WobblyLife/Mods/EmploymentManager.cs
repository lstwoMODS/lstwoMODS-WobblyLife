using System.Collections.Generic;
using System.Linq;
using lstwoMODS_Core.Hacks;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;
using lstwoMODS_Core.UI.TabMenus;
using UnityExplorer;
using UIManager = UnityExplorer.UI.UIManager;

namespace lstwoMODS_WobblyLife.Mods;

public class EmploymentManager : PlayerBasedMod
{
    public override string Name => "Player Employment Manager";
    public override string Description => "";
    public override ModsWindow ModsWindow => Plugin.PlayerModsWindow;
    
    public static List<JobDispensorBehaviour> JobDispensors => JobDispensorManager.Instance.jobDispensors;

    private Ref<string[]> jobDispensorDropdownItems = new([]);
    private Ref<int> jobDispensorDropdownIndex = new();
    
    private Ref<int> giveMoneyAmount = new();
    private Ref<int> spawnMoneyAmount = new();
    
    [ModAction(Order = 10, SeparatorText = "Complete / Fail Job")]
    public void CompleteJob()
    {
        Player?.Controller?.GetPlayerControllerEmployment()?.GetActiveJob()?.ServerJobCompleted();
    }

    [ModAction(Order = 20)]
    public void FailJob()
    {
        Player?.ControllerEmployment?.GetActiveJob()?.ServerJobFailed("");
    }
    
    [ModAction(ShowInUI = false, SeparatorText = "")]
    public void StartJob(JobDispensorBehaviour jobDispensorBehaviour)
    {
        if (Player?.Controller)
        {
            jobDispensorBehaviour.StartJob(Player.Controller, _ => {});
        }
    }
    
    [ModAction("Give Money", ShowInUI = false)]
    public void _GiveMoney(int money)
    {
        if (SaveGuard.On) return;
        if (Player == null) return;
        if (!Player.Controller.networkObject.IsOwner()) return;

        Player.ControllerEmployment.UpdateMoney(money);
    }

    [ModAction("Spawn Money", ShowInUI = false)]
    public void SpawnMoney(int amount)
    {
        if (SaveGuard.On) return;

        RewardManagerInstance.Instance.ServerReward(Player, RewardType.MoneyBag, amount);
    }

    [ModAction("Reset Money", ShowInUI = false)]
    public void ResetMoney()
    {
        if (SaveGuard.On) return;
        if (Player == null) return;
        if (!Player.Controller.networkObject.IsOwner()) return;

        Player.ControllerEmployment.UpdateMoney(-Player.ControllerEmployment.GetLocalMoney());
    }

    public override void RefreshUI()
    {
        base.RefreshUI();
        
        jobDispensorDropdownIndex.Value = 0;
        jobDispensorDropdownItems.Value = JobDispensors.Select(x => ModRegistry.NicifyName(x.GetType().Name.Replace("JobDispensor", ""))).ToArray();
    }

    public override Container BuildPanel(string id)
    {
        return new Container(id,
            
            new SeparatorText("sep-1", "Money"),

            SaveGuard.Notice("employment-money-guard-notice"),

            SaveGuard.Guard(new HStack("Give Money",
                new DragInt("###Amount").WithValue(giveMoneyAmount),
                ActionMenu(new Button("Give Money", () => _GiveMoney(giveMoneyAmount.Value)), nameof(_GiveMoney))
            ).WithId("Give Money").WithContentWidth()),

            SaveGuard.Guard(new HStack("Spawn Money",
                new DragInt("###Amount").WithValue(spawnMoneyAmount),
                ActionMenu(new Button("Spawn Money", () => SpawnMoney(spawnMoneyAmount.Value)), nameof(SpawnMoney))
            ).WithId("Spawn Money").WithContentWidth()),

            SaveGuard.Guard(ActionMenu(new Button("Reset Money", ResetMoney).WithContentWidth(), nameof(ResetMoney))),
            
            base.BuildPanel(id),

            new SeparatorText("sep-2", "Start Job"),
            
            new HStack("stack",
                
                new Combo("", []).WithItems(jobDispensorDropdownItems).WithSelectedIndex(jobDispensorDropdownIndex).WithId("Select Job"),
                ActionMenu(new Button("Start Job", () => StartJob(JobDispensors[jobDispensorDropdownIndex.Value])).WithContentWidth(), nameof(StartJob))

            ).WithContentWidth(),
            
            new Spacing("spacing"),
            
            new Button("Inspect \"Player Employment\" Component", () =>
            {
                if (Player != null && Player.ControllerEmployment)
                {
                    InspectorManager.Inspect(Player.ControllerEmployment);
                    UIManager.ShowMenu = true;
                }
                
            }).WithContentWidth()
        );
    }
}