using System;
using lstwoMODS_Core.UI;
using lstwoMODS_Core.UI.Elements;

namespace lstwoMODS_WobblyLife.Mods.JobManager;

public class QuizMasterJobManager : BaseJobManager
{
    private Ref<int> maxQuestions = new(5);
    private Ref<int> score = new();
    private Ref<int> reward = new(30);

    public override Type missionType => typeof(QuizMasterJobMission);

    public override void RegisterMacros()
    {
        RegisterJobAction<QuizMasterJobMission, int>("setMaxQuestions", "Set Max Questions", "Max Questions", (m, v) => m.maxQuestions = v);
        RegisterJobAction<QuizMasterJobMission, int>("setScore", "Set Current Score", "Score", (m, v) => m.SetScore(v));
        RegisterJobAction<QuizMasterJobMission, int>("setReward", "Set Reward Money", "Money", (m, v) => m.rewardMoney = v);
    }

    public override Container BuildContent(string id)
    {
        return new Container(id,
            
            new HStack("max-questions",
                new DragInt("##Max Questions").WithValue(maxQuestions),
                WithMacroMenu(new Button("Set Max Questions", () => SetMaxQuestions(maxQuestions.Value)).WithContentWidth(), "setMaxQuestions", "Set Max Questions")
            ).WithContentWidth(),

            new HStack("score",
                new DragInt("##Current Score").WithValue(score),
                WithMacroMenu(new Button("Set Current Score", () => SetScore(score.Value)).WithContentWidth(), "setScore", "Set Current Score")
            ).WithContentWidth(),

            new HStack("reward",
                new DragInt("##Reward Money").WithValue(reward),
                WithMacroMenu(new Button("Set Reward Money", () => SetReward(reward.Value)).WithContentWidth(), "setReward", "Set Reward Money")
            ).WithContentWidth()
        );
    }

    public override void RefreshUI()
    {
        var b = CheckMission();
        if (!b) return;

        var m = (QuizMasterJobMission)Mission;
        maxQuestions.Value = (int)m.maxQuestions;
        score.Value = (int)m.score;
        reward.Value = (int)m.rewardMoney;
    }

    public void SetMaxQuestions(int i)
    {
        if (CheckMission())
            ((QuizMasterJobMission)Mission).maxQuestions = i;
    }

    public void SetScore(int score)
    {
        if (CheckMission())
            ((QuizMasterJobMission)Mission).SetScore(score);
    }

    public void SetReward(int money)
    {
        if (CheckMission())
            ((QuizMasterJobMission)Mission).rewardMoney = money;
    }
}
