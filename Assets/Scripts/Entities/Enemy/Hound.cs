public class Hound : EnemyBase
{
    public override void WriteStats()
    {
        Description = "";
        Skillset = ".";
        TooltipsDescription = "A hunting hound trained and utilized by locals since ancient times. " +
            "<color=yellow>Fast movement</color>.";

        base.WriteStats();
    }
}