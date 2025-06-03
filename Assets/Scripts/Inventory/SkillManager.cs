using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public SkillDisplay skillDisplay; // Reference to the SkillDisplay script
    public Skill skillToDisplay; // Skill to display (assign in Inspector or dynamically)

    void Start()
    {
        if (skillToDisplay != null && skillDisplay != null)
        {
            skillDisplay.SetSkill(skillToDisplay);
        }
    }
}
