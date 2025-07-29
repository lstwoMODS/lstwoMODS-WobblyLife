using System;
using System.Collections.Generic;
using System.Reflection;
using lstwoMODS_Core;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI.Models;

namespace lstwoMODS_WobblyLife.Hacks.JobManager;

public class QuizMasterJobManager : BaseJobManager
{
    private HacksUIHelper.LIBTrio maxQuestionsLIB;
    private HacksUIHelper.LIBTrio scoreLIB;
    private HacksUIHelper.LIBTrio rewardLIB;
    private List<GameObject> objects = new();
    private QuickReflection<QuizMasterJobMission> reflect;

    public override Type missionType => typeof(QuizMasterJobMission);

    public override void ConstructUI()
    {
        base.ConstructUI();

        var title = ui.CreateLabel("Quiz Job", "title", fontSize: 18);
        objects.Add(title.gameObject);
            
        maxQuestionsLIB = ui.CreateLIBTrio("Set Max Questions", "maxQuestions", "5");
        maxQuestionsLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        maxQuestionsLIB.Button.OnClick = () => SetMaxQuestions(int.Parse(maxQuestionsLIB.Input.Text));
        objects.Add(maxQuestionsLIB.Root);

        objects.Add(ui.AddSpacer(5));
            
        scoreLIB = ui.CreateLIBTrio("Set Current Score", "scoreLabel", "0");
        scoreLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        scoreLIB.Button.OnClick = () => SetScore(int.Parse(scoreLIB.Input.Text));
        objects.Add(scoreLIB.Root);

        objects.Add(ui.AddSpacer(5));
            
        rewardLIB = ui.CreateLIBTrio("Set Reward Money", "rewardLIB", "30");
        rewardLIB.Input.Component.characterValidation = InputField.CharacterValidation.Integer;
        rewardLIB.Button.OnClick = () => SetReward(int.Parse(rewardLIB.Input.Text));
        objects.Add(rewardLIB.Root);
    }

    public override void RefreshUI()
    {
        bool b = CheckMission();

        root.SetActive(b);

        if (!b)
        {
            return;
        }
            
        reflect = new((QuizMasterJobMission)Mission, BindingFlags.Instance | BindingFlags.NonPublic);

        maxQuestionsLIB.Input.Text = reflect.GetField("maxQuestions").ToString();
        scoreLIB.Input.Text = reflect.GetField("score").ToString();
        rewardLIB.Input.Text = reflect.GetField("rewardMoney").ToString();
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