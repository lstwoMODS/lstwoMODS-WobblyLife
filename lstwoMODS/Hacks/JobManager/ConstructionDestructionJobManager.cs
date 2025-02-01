using ShadowLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class ConstructionDestructionJobManager : BaseJobManager
    {
        public override Type missionType => typeof(ConstructionDestructionJobMission);

        private List<GameObject> objects = new List<GameObject>();
        private QuickReflection<ConstructionDestructionJobMission> reflect;

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Construction Destruction Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Current Job Money", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            var moneyInput = ui.CreateInputField("0", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply Money", () => SetMoney(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);

            objects.Add(ui.AddSpacer(10));

            var spawnTools = ui.CreateButton("Spawn Tools", SpawnTools);
            objects.Add(spawnTools.GameObject);

            objects.Add(ui.AddSpacer(5));

            var destroyTools = ui.CreateButton("Destroy Tools", DestroyTools);
            objects.Add(destroyTools.GameObject);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((ConstructionDestructionJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        public void SpawnTools()
        {
            if (CheckMission())
            {
                Plugin._StartCoroutine((IEnumerator)reflect.GetMethod("SpawnTools"));
            }
        }

        public void DestroyTools()
        {
            if (CheckMission())
            {
                reflect.GetMethod("DestroyAllTools");
            }
        }

        public void SetMoney(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("money", money);
            }
        }
    }
}
