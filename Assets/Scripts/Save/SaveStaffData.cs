public class SaveStaffData
{
    private string _id;
    public string Id => _id;

    private int _level;
    public int Level => _level;

    private string _skinId = string.Empty;
    public string SkinId => _skinId;


    public SaveStaffData(string id, int level)
    {
        _id = id;
        _level = level;
    }


    public void LevelUp()
    {
        _level += 1;
    }

    internal void SetLevelFromMigration(int level)
    {
        _level = level;
    }

    public void SetSkinId(string skinId)
    {
        _skinId = skinId;
    }
}

public static class StaffSaveMigrationA2
{
    private const string Staff02Id = "STAFF02";
    private const int LegacyLevel = 6;

    public static bool TryApply(ServerStageData data, EStage stage)
    {
        if (data == null || data.GiveStaffList == null)
            return false;

        bool changed = false;
        for (int index = 0; index < data.GiveStaffList.Count; index++)
        {
            SaveStaffData staff = data.GiveStaffList[index];
            if (staff == null || staff.Id != Staff02Id)
                continue;

            if (staff.Level == LegacyLevel)
            {
                staff.SetLevelFromMigration(StaffData.OfficialMaxLevel);
                changed = true;
                DebugLog.LogWarning(
                    "[Staff Save Migration A2][LEGACY_LEVEL_6_MIGRATED] "
                    + stage + " STAFF02 Lv.6 -> Lv.5");
            }
            else if (staff.Level < 0 || staff.Level > LegacyLevel)
            {
                DebugLog.LogWarning(
                    "[Staff Save Migration A2][INVALID_LEVEL_PRESERVED] "
                    + stage + " STAFF02 level=" + staff.Level);
            }
        }

        return changed;
    }
}
