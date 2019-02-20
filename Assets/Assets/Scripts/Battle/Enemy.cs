﻿using UnityEngine;

public class Enemy : BattleParticipant{
    //Will be used later for advanced AIs
    //See Battle's MoveEnemies() for more information
    int packVar;
    int aggro;
    
    public Enemy(int x, int y, int mT, int aggresion, int pack) : base(x, y, mT)
    {
        aggro = aggresion;
        packVar = pack;
    }
}
