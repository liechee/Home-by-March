namespace MyGame.Dungeon
{

public class Dungeon
{
    public string dungeonName;
    public int difficultyLevel;
    public bool isCleared;

    public Dungeon(string name, int difficulty)
    {
        dungeonName = name;
        difficultyLevel = difficulty;
        isCleared = false;
    }

    public void ClearDungeon()
    {
        isCleared = true;
    }
}
}
