using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffOfficialDataPackageKeys
    {
        internal const string FinalStaff = "FinalStaff";
        internal const string RoleBase = "RoleBase";
        internal const string RoleGrowth = "RoleGrowth";
        internal const string LevelRule = "LevelRule";
        internal const string CostRule = "CostRule";
        internal const string Summary = "Summary";
        internal const string Policy = "Policy";
        internal const string SkillType = "SkillType";
        internal const string GachaUpgradeType = "GachaUpgradeType";

        private static readonly IReadOnlyList<string> OfficialOrder =
            new ReadOnlyCollection<string>(
                new[]
                {
                    FinalStaff,
                    RoleBase,
                    RoleGrowth,
                    LevelRule,
                    CostRule,
                    Summary,
                    Policy,
                    SkillType,
                    GachaUpgradeType
                });

        internal static IReadOnlyList<string> OrderedKeys
        {
            get { return OfficialOrder; }
        }
    }
}
