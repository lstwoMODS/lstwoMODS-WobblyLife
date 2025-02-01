using ShadowLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class ConstructionBuildingJobManager : BaseJobManager
    {
        public override Type missionType => typeof(ConstructionBuildingJobMission);

        private List<GameObject> objects = new List<GameObject>();
        private QuickReflection<ConstructionBuildingJobMission> reflect;

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Construction Building Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Current Job Money", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            var moneyInput = ui.CreateInputField("0", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply Money", () => SetMoney(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);

            objects.Add(ui.AddSpacer(10));

            var spawnResourcesBtn = ui.CreateButton("Spawn Resources", SpawnResources);
            objects.Add(spawnResourcesBtn.GameObject);

            objects.Add(ui.AddSpacer(5));

            var spawnHammersBtn = ui.CreateButton("Spawn Hammers", SpawnHammers);
            objects.Add(spawnHammersBtn.GameObject);

            objects.Add(ui.AddSpacer(5));

            var moneyPerPieceLabel = ui.CreateLabel("Set Current Job Money Per Building Piece", "moneyPerPieceLabel");
            objects.Add(moneyPerPieceLabel.gameObject);

            var moneyPerPieceInput = ui.CreateInputField("2", "moneyPerPieceInput");
            objects.Add(moneyPerPieceInput.GameObject);

            var moneyPerPieceBtn = ui.CreateButton("Apply Money Per Building Piece", () => SetMoneyPerBuildingPiece(int.Parse(moneyInput.Text)));
            objects.Add(moneyPerPieceBtn.GameObject);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((ConstructionBuildingJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        public void SpawnResources()
        {
            if (CheckMission())
            {
                Plugin._StartCoroutine((IEnumerator)reflect.GetMethod("ServerSpawnResources"));
            }
        }

        public void SpawnHammers()
        {
            if (CheckMission())
            {
                Plugin._StartCoroutine((IEnumerator)reflect.GetMethod("ServerSpawnHammers"));
            }
        }

        public void SetMoney(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("money", money);
            }
        }

        public void SetMoneyPerBuildingPiece(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("moneyPerBuildingPiece", money);
            }
        }
    }
}
