public interface ISaveable
{
    void LoadData(GameData data);
    void SaveData(ref GameData data);
}