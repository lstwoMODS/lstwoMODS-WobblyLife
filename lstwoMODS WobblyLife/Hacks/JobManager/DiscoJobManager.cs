﻿using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class DiscoJobManager : BaseJobManager
    {
        private List<GameObject> objects = new();
        private QuickReflection<DiscoJobMission> reflect;

        public override Type missionType => typeof(DiscoJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Disco Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var missBtn = ui.CreateLBDuo("Miss Tile", "MissTile", MissTile, "Miss", "MissTileButton");
            objects.Add(missBtn.Root);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((DiscoJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        public void MissTile()
        {
            if (CheckMission())
            {
                reflect.GetMethod("TileMiss");
            }
        }
    }
}
