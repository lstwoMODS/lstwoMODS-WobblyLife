using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using lstwoMODS_Core;
using UnityEngine;
using UnityEngine.UI;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class ArtStudioJobManager : BaseJobManager
    {
        private HacksUIHelper.LIBTrio moneyLIB;
        private List<GameObject> objects = new();
        private QuickReflection<ArtStudioJobMission> reflect;

        public override Type missionType => typeof(ArtStudioJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Art Studio Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            moneyLIB = ui.CreateLIBTrio("Set Art Studio Job Money", "lstwo.JobManager.ArtStudioJobManager.SetMoney", "30");
            moneyLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
            moneyLIB.Button.OnClick = () => SetMoney(int.Parse(moneyLIB.Input.Text));
            objects.Add(moneyLIB.Root);

            objects.Add(ui.AddSpacer(6));

            var spawnToolsLB = ui.CreateLBDuo("Spawn All Tools", "lstwo.JobManager.ArtStudioJobManager.SpawnTools", SpawnAllTools, "Spawn", "SpawnToolsButton");
            objects.Add(spawnToolsLB.Root);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (!b)
            {
                return;
            }
            
            reflect = new((ArtStudioJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);
            moneyLIB.Input.Text = ((int)reflect.GetField("money")).ToString();
        }

        public void SetMoney(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("money", money);
            }
        }

        public void SpawnAllTools()
        {
            if (CheckMission())
            {
                reflect.GetMethod("ServerSpawnAllTools");
            }
        }
    }
}
