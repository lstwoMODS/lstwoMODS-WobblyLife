using ShadowLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager
{
    public class QuizMasterJobManager : BaseJobManager
    {
        private InputFieldRef moneyInput;
        private InputFieldRef scoreInput;
        private InputFieldRef rewardInput;
        private List<GameObject> objects = new();
        private QuickReflection<QuizMasterJobMission> reflect;

        public override Type missionType => typeof(QuizMasterJobMission);

        public override void ConstructUI()
        {
            base.ConstructUI();

            var title = ui.CreateLabel("Quiz Job", "title", fontSize: 18);
            objects.Add(title.gameObject);

            var moneyLabel = ui.CreateLabel("Set Max Questions", "moneyLabel");
            objects.Add(moneyLabel.gameObject);

            moneyInput = ui.CreateInputField("5", "moneyInput");
            objects.Add(moneyInput.GameObject);

            var moneyBtn = ui.CreateButton("Apply", () => SetMaxQuestions(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);

            objects.Add(ui.AddSpacer(5));

            var scoreLabel = ui.CreateLabel("Set Current Score", "scoreLabel");
            objects.Add(moneyLabel.gameObject);

            scoreInput = ui.CreateInputField("0", "scoreInput");
            objects.Add(moneyInput.GameObject);

            var scoreBtn = ui.CreateButton("Apply", () => SetScore(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);

            objects.Add(ui.AddSpacer(5));

            var rewardLabel = ui.CreateLabel("Set Reward Money", "scoreLabel");
            objects.Add(moneyLabel.gameObject);

            rewardInput = ui.CreateInputField("30", "scoreInput");
            objects.Add(moneyInput.GameObject);

            var rewardBtn = ui.CreateButton("Apply", () => SetReward(int.Parse(moneyInput.Text)));
            objects.Add(moneyBtn.GameObject);
        }

        public override void RefreshUI()
        {
            bool b = CheckMission();

            root.SetActive(b);

            if (b)
            {
                reflect = new((QuizMasterJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

                moneyInput.Text = reflect.GetField("maxQuestions").ToString();
                scoreInput.Text = reflect.GetField("score").ToString();
                rewardInput.Text = reflect.GetField("rewardMoney").ToString();
            }
        }

        public void SetMaxQuestions(int i)
        {
            if (CheckMission())
            {
                reflect.SetField("maxQuestions", i);
            }
        }

        public void SetScore(int score)
        {
            if (CheckMission())
            {
                reflect.GetMethod("SetScore", score);
            }
        }

        public void SetReward(int money)
        {
            if (CheckMission())
            {
                reflect.SetField("rewardMoney", money);
            }
        }
    }
}
