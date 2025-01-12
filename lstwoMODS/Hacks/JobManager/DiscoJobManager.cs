using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace NotAzzamods.Hacks.JobManager
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

            var missBtn = ui.CreateButton("Miss Tile", MissTile);
            objects.Add(missBtn.GameObject);
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
