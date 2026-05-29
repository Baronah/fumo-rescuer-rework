using System.Collections.Generic;
using UnityEngine;
using static EnemyBase;
using static LevelDifficultyModifier;
using static StageManager;

public static class EnemyStatsLookup
{
    public static float DifficultyHpMultiplierBase => 0.06f;
    public static float DifficultyAtkMultiplierBase => 0.03f;

    static int GetDiff => Mathf.Min(CharacterPrefabsStorage.DifficultyLevel - 1, 15);

    public static float GetEnemiesHpMultiplier()
    {
        if (CharacterPrefabsStorage.DifficultyLevel <= 1) return 0;

        int Diff = GetDiff;

        float finalMul = 0;
        for (int i = 1; i <= Diff; i++)
        {
            if (i == 1 || i == 15) finalMul += 0.2f;
            
            finalMul += DifficultyHpMultiplierBase;
        }

        return finalMul;
    }

    public static float GetEnemiesAtkMultiplier()
    {
        if (CharacterPrefabsStorage.DifficultyLevel <= 1) return 0;

        int Diff = GetDiff;

        float finalMul = 0;
        for (int i = 1; i <= Diff; i++)
        {
            if (i == 1 || i == 15) finalMul += 0.1f;
             
            finalMul += DifficultyAtkMultiplierBase;
        }

        return finalMul;
    }

    public static bool HasStatsChange(EnemyCode code, int levelIndex)
    {
        HashSet<EnemyCode> codeWithStatsChange = null;
        switch (levelIndex)
        {
            case 0:
                break;

            case 1:
                break;

            case 2:
                break;

            case 3:
                codeWithStatsChange = new() { EnemyCode.BLOODBOIL_KNIGHT };
                break;

            case 4:
                break;

            case 5:
                break;

            case 6:
                codeWithStatsChange = new() { EnemyCode.ORIGINIUM_SPIDER_ALPHA };
                break;

            case 7:
                codeWithStatsChange = new() { EnemyCode.SENTINEL, EnemyCode.SUDARAM, EnemyCode.ORIGINIUM_SPIDER, EnemyCode.ORIGINIUM_SPIDER_ALPHA };
                break;

            case 8:
                codeWithStatsChange = new() { EnemyCode.BLOODBOIL_KNIGHT, EnemyCode.SUDARAM, EnemyCode.ORIGINIUM_SPIDER_ALPHA };
                break;

            case 9:
                codeWithStatsChange = new() { EnemyCode.ARCHER, EnemyCode.HOUND, EnemyCode.WETWORK, EnemyCode.HEIR, EnemyCode.MATTERLLURGIST };
                break;

            case 10:
                codeWithStatsChange = new() { EnemyCode.SUDARAM };
                break;

            case 11:
                break;

            case 12:
                codeWithStatsChange = new() { EnemyCode.SUDARAM };
                break;

            case 13:
                break;

            case 14:
                codeWithStatsChange = new() { EnemyCode.SENTINEL };
                break;
        }

        if (codeWithStatsChange == null) return false;
        return codeWithStatsChange.Contains(code);
    }

    public static void GetStats(EnemyBase enemy, int levelIndex, out bool hasChanged)
    {
        hasChanged = false;
        switch (levelIndex)
        {
            case 0:
                break;

            case 1:
                break;

            case 2:
                break;

            case 3:
                if (enemy as BloodboilKnight)
                {
                    enemy.mHealth = 350;
                    enemy.bRes = 10;
                    hasChanged = true;
                }
                break;

            case 4:
                break;

            case 5:
                break;

            case 6:
                if (enemy is OriginiumSpiderAlpha alp)
                {
                    alp.mHealth = 60;
                    hasChanged = true;
                }
                break;

            case 7:
                if (enemy is Sudaram sr)
                {
                    sr.originiumPollutionBonusASPD = 100f;
                    sr.originiumPollutionDamageMultiplier = 0f;
                    hasChanged = true;
                }
                else if (enemy as OriginiumSpider || enemy as OriginiumSpiderAlpha)
                {
                    enemy.bAtk = (short)(enemy.bAtk * 0.85f);
                    hasChanged = true;
                }
                else if (enemy as Sentinel)
                {
                    enemy.bDef += 20;
                    enemy.bRes += 30;
                    hasChanged = true;
                }
                break;

            case 8:
                if (enemy is Sudaram s)
                {
                    s.detectionRange *= 0.6f;
                    s.originiumPollutionBonusASPD += 40f;
                    s.originiumPollutionDamageMultiplier = 0f;
                    s.mHealth *= 0.75f;
                    hasChanged = true;
                }
                else if (enemy as OriginiumSpiderAlpha)
                {
                    enemy.bAtk = (short)(enemy.bAtk * 0.85f);
                    hasChanged = true;
                }
                else if (enemy is BloodboilKnight b)
                {
                    b.maxStackCount *= 2;
                    b.mspdAddPerEnemyKilled /= 2;
                    b.aspdAddPerEnemyKilled /= 2;
                    b.atkAddPerEnemyKilled /= 2;
                    hasChanged = true;
                }
                break;

            case 9:
                if (enemy as Matterllurgist)
                {
                    enemy.ASPD += 40;
                    hasChanged = true;
                }

                if (enemy as Hound || enemy as Wetwork || enemy as Archer || enemy as BloodthirstyHeir)
                {
                    enemy.mHealth *= 1.3f;
                    hasChanged = true;
                }
                break;

            case 10:
                if (enemy is Sudaram sud)
                {
                    enemy.bAtk = (short)(enemy.bAtk * 0.6f);
                    enemy.mHealth = 150;
                    enemy.bDef /= 2;
                    enemy.bRes = 0;
                    sud.originiumPollutionDamageMultiplier = 0f;
                    hasChanged = true;
                }
                break;

            case 11:
                break;

            case 12:
                if (enemy is Sudaram su)
                {
                    su.mHealth = 200;
                    su.originiumPollutionBonusASPD = 100;
                    su.originiumPollutionDamageMultiplier = 0f;
                    hasChanged = true;
                }
                break;

            case 13:
                break;

            case 14:
                if (enemy as Sentinel)
                {
                    enemy.b_moveSpeed += 50f;
                    hasChanged = true;
                }
                break;
        }
    }

    public static void ProcessEnemyDifficultyScaling(EnemyBase enemy, StageManager.StageCompleteCondition StageCompleteConditionType)
    {
        short diffLevel = (short)(CharacterPrefabsStorage.DifficultyLevel - 1);
        if (diffLevel <= 0) return;

        enemy.bAtk = (short)(enemy.bAtk * (1f + GetEnemiesAtkMultiplier()));
        enemy.mHealth *= 1f + GetEnemiesHpMultiplier();

        if (diffLevel == (int)DiffType.EnemiesMissionTypesBuffs_1)
        {
            if (StageCompleteConditionType == StageCompleteCondition.PROTECT_FUMO || StageCompleteConditionType == StageCompleteCondition.SURVIVE_FOR_GIVEN_TIME)
            {
                short defAdd = 20;
                float mspdAdd = enemy.b_moveSpeed * 0.1f;
                enemy.bDef += defAdd;
                enemy.b_moveSpeed += mspdAdd;
            }
            else if (StageCompleteConditionType == StageCompleteCondition.RETRIEVE_FUMO)
            {
                short resAdd = 20;
                float aspdAdd = 10;
                enemy.bRes += resAdd;
                enemy.ASPD += aspdAdd;
            }
            else if (StageCompleteConditionType == StageCompleteCondition.ELIMINATE_ALL_ENEMIES)
            {
                float hpAdd = enemy.mHealth * 0.1f, atkAdd = enemy.bAtk * 0.1f;
                enemy.mHealth += hpAdd;
                enemy.bAtk += (short)atkAdd;
            }
        }
        else if (diffLevel >= (int) DiffType.EnemiesMissionTypesBuffs_2)
        {
            if (StageCompleteConditionType == StageCompleteCondition.PROTECT_FUMO || StageCompleteConditionType == StageCompleteCondition.SURVIVE_FOR_GIVEN_TIME)
            {
                short defAdd = 40;
                float mspdAdd = enemy.b_moveSpeed * 0.25f;
                enemy.bDef += defAdd;
                enemy.b_moveSpeed += mspdAdd;
            }
            else if (StageCompleteConditionType == StageCompleteCondition.RETRIEVE_FUMO)
            {
                short resAdd = 40;
                float aspdAdd = 25;
                enemy.bRes += resAdd;
                enemy.ASPD += aspdAdd;
            }
            else if (StageCompleteConditionType == StageCompleteCondition.ELIMINATE_ALL_ENEMIES)
            {
                float hpAdd = enemy.mHealth * 0.25f, atkAdd = enemy.bAtk * 0.2f;
                enemy.mHealth += hpAdd;
                enemy.bAtk += (short)atkAdd;
            }
        }

        if (diffLevel >= (int)DiffType.EnemiesAlertBuff)
        {
            enemy.MoveToOverridePositionSpeedMultiplier += 0.3f;
            enemy.MoveToOverridePositionSpeedMultiplierJump += 0.1f;
        }

        if (diffLevel == (int)DiffType.EnemiesUncombatDRBuff_1)
        {
            enemy.DamageReductionOutsideCombat += 0.3f;
        }
        else if (diffLevel >= (int)DiffType.EnemiesUncombatDRBuff_2)
        {
            enemy.DamageReductionOutsideCombat += 0.6f;
        }

        if (diffLevel >= (int)DiffType.EnemiesStatusResistant
            && (enemy.attackPattern == EntityBase.AttackPattern.MELEE || enemy.attackPattern == EntityBase.AttackPattern.NONE))
            enemy.StatusResistTimer += 9999f;
    }
}