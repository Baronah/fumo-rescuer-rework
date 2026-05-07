using System.Collections.Generic;
using UnityEngine;

public class RockGachaSkill : MonoBehaviour
{
    [SerializeField] private Sprite[] skillImage;
    [SerializeField] private List<SkillTree_Manager.SkillName> skillNames;

    public void SetSkill(SkillTree_Manager.SkillName skillName)
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = skillImage[skillNames.IndexOf(skillName)];
    }

    public Sprite GetSkillImage(SkillTree_Manager.SkillName skillName)
    {
        return skillImage[skillNames.IndexOf(skillName)];
    }
}