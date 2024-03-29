﻿using System.Collections.Generic;

public class Skill
{
    public string name;

    public TargettingType targetType;

    public int aEtherCost;

    public int unlockLevel;
    public int unlockCost;

    //how large the aoe is (even numbers put the extra space on right and bottom respectively)
    public int xRange;
    public int yRange;

    public int targettingRange;

    //What needs to be unlocked before this one can be, n = id of dependency
    public List<int> dependencies = new List<int>();
    //List of skill parts in order of execution
    public List<SkillPartBase> partList = new List<SkillPartBase>();

    public string flavorText;

    public Skill(string name, TargettingType target, int cost, int targetRange, int xAOE, int yAOE, int unlockCost, int unlockLvl, string flavorText = "")
    {
        this.name = name;
        targetType = target;
        aEtherCost = cost;
        targettingRange = targetRange;
        xRange = xAOE;
        yRange = yAOE;
        this.unlockCost = unlockCost;
        unlockLevel = unlockLvl;
        this.flavorText = flavorText;
    }
}
