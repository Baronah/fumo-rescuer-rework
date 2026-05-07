using System.Collections.Generic;
using static EnemyBase;
using static LevelDifficultyModifier;
using static StageManager;

public static class EnemyStatsLookup
{
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
                    enemy.mHealth = 200;
                    enemy.bDef /= 2;
                    enemy.bRes = 0;
                    hasChanged = true;
                }
                break;

            case 11:
                break;

            case 12:
                if (enemy is Sudaram su)
                {
                    su.originiumPollutionBonusASPD = 100;
                    su.originiumPollutionDamageMultiplier = 0f;
                    hasChanged = true;
                }
                break;

            case 13:
                break;
        }
    }

    public static void ProcessEnemyDifficultyScaling(EnemyBase enemy, StageManager.StageCompleteCondition StageCompleteConditionType)
    {
        short diffLevel = (short)(CharacterPrefabsStorage.DifficultyLevel - 1);
        if (diffLevel <= 0) return;

        enemy.bAtk = (short)(enemy.bAtk * (1f + CharacterPrefabsStorage.GetEnemiesAtkMultiplier()));
        enemy.mHealth *= 1f + CharacterPrefabsStorage.GetEnemiesHpMultiplier();

        if (diffLevel >= (int)DiffType.EnemiesDefenseSurvivalBuff)
        {
            short defAdd = 10;
            float mspdAdd = enemy.b_moveSpeed * 0.05f;

            if (StageCompleteConditionType == StageCompleteCondition.PROTECT_FUMO || StageCompleteConditionType == StageCompleteCondition.SURVIVE_FOR_GIVEN_TIME)
            {
                mspdAdd *= 4;
                defAdd *= 4;
            }

            enemy.bDef += defAdd;
            enemy.b_moveSpeed += mspdAdd;
        }

        if (diffLevel >= (int)DiffType.EnemiesRescueBuff)
        {
            short resAdd = 10;
            float aspdAdd = 5;
            if (StageCompleteConditionType == StageCompleteCondition.RETRIEVE_FUMO)
            {
                resAdd *= 4;
                aspdAdd *= 4;
            }

            enemy.bRes += resAdd;
            enemy.ASPD += aspdAdd;
        }

        if (diffLevel >= (int)DiffType.EnemiesAnnihilationBuff)
        {
            float hpAdd = enemy.mHealth * 0.05f, atkAdd = enemy.bAtk * 0.05f;
            if (StageCompleteConditionType == StageCompleteCondition.ELIMINATE_ALL_ENEMIES)
            {
                hpAdd *= 4;
                atkAdd *= 4;
            }

            enemy.mHealth += hpAdd;
            enemy.bAtk += (short)atkAdd;
        }

        if (diffLevel >= (int)DiffType.EnemiesStatusResistant
            && (enemy.attackPattern == EntityBase.AttackPattern.MELEE || enemy.attackPattern == EntityBase.AttackPattern.NONE))
            enemy.StatusResistTimer += 9999f;
    }
}