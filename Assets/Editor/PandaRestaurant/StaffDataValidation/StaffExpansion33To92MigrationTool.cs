using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    /// <summary>
    /// Explicit-profile, append-only expansion. Never edits an existing asset, imports a
    /// package, draws a gacha, opens a scene, or accesses account/network services.
    /// Output is an audit manifest under the OS temporary directory, not project data.
    /// </summary>
    internal static class StaffExpansion33To92MigrationTool
    {
        private const string VisualRoot = "Assets/Resources/StaffData/Expansion/STAFF33_92/Visuals";
        private const string LegacyRoot = "Assets/Resources/StaffData/Skin/Sprites";
        private const string CsvPath = "OfficialData/Staff/V18/Final18_Staff_DataList.csv";
        private const string CsvSha = "298F6C9F032B5F7CA73FACD3A6D13649AF0BF6357A5E802064A7F52AC706E51F";
        // Same approved 841 files: ko-KR fixed-sort approval fingerprint is 1EA1E092...597F5760.
        // Their independently verified ordinal fingerprint avoids OS/Mono collation differences.
        private const string ProtectedOrdinalSha = "0A925F6F6030F10774614C3BE9284850DD77E060AEC01CDFB6F8FD036C1B7894";
        private static readonly int[] Breathing = { 54, 55, 58, 64, 65, 68, 74, 75, 78, 84, 85, 86 };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class CopyPlan
        {
            internal string StaffId, Category, Source, Target, SourceGuid, SourceSha, Importer;
            internal int Frame;
        }

        private sealed class StaffPlan
        {
            internal StaffDataDryRunStaffPlan Data;
            internal readonly List<CopyPlan> Copies = new List<CopyPlan>();
            internal string Id { get { return Data.StaffId; } }
            internal string StaffPath { get { return "Assets/Resources/StaffData/" + Id + ".asset"; } }
            internal string SkillPath { get { return "Assets/Scripts/Datas/Staff/Skill/" + Id + "Skill.asset"; } }
        }

        [MenuItem("Tools/Panda Restaurant/Staff Expansion/Dry Run Pilot (Baseline32)")]
        public static void DryRunPilot() { Run("DryRunPilot", StaffExpansionValidationProfiles.Baseline32, StaffExpansionValidationProfiles.Pilot37, false, false); }
        [MenuItem("Tools/Panda Restaurant/Staff Expansion/Apply Pilot (Baseline32 to Pilot37)")]
        public static void ApplyPilot() { Run("ApplyPilot", StaffExpansionValidationProfiles.Baseline32, StaffExpansionValidationProfiles.Pilot37, true, false); }
        [MenuItem("Tools/Panda Restaurant/Staff Expansion/Verify Pilot (Pilot37)")]
        public static void VerifyPilot() { Run("VerifyPilot", StaffExpansionValidationProfiles.Pilot37, StaffExpansionValidationProfiles.Pilot37, false, true); }
        [MenuItem("Tools/Panda Restaurant/Staff Expansion/Dry Run Full (Pilot37)")]
        public static void DryRunFull() { Run("DryRunFull", StaffExpansionValidationProfiles.Pilot37, StaffExpansionValidationProfiles.Full92, false, false); }
        [MenuItem("Tools/Panda Restaurant/Staff Expansion/Apply Full (Pilot37 to Full92)")]
        public static void ApplyFull() { Run("ApplyFull", StaffExpansionValidationProfiles.Pilot37, StaffExpansionValidationProfiles.Full92, true, false); }
        [MenuItem("Tools/Panda Restaurant/Staff Expansion/Verify Full (Full92)")]
        public static void VerifyFull() { Run("VerifyFull", StaffExpansionValidationProfiles.Full92, StaffExpansionValidationProfiles.Full92, false, true); }
        [MenuItem("Tools/Panda Restaurant/Staff Expansion/Verify No-op (Full92)")]
        public static void VerifyNoOp() { Run("VerifyNoOp", StaffExpansionValidationProfiles.Full92, StaffExpansionValidationProfiles.Full92, false, true); }

        private static void Run(string mode, StaffExpansionValidationProfile input, StaffExpansionValidationProfile output, bool apply, bool verify)
        {
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Expansion is Edit Mode only.");
                Require(Sha(CsvPath) == CsvSha && new FileInfo(CsvPath).Length == 25686, "Official CSV hash/size mismatch.");
                Dictionary<string, string> protectedBefore = ProtectedSnapshot();
                Require(protectedBefore.Count == 841, "Expected exactly 841 protected physical files.");
                Require(ShaText(string.Concat(protectedBefore.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "\t" + p.Value + "\n"))) == ProtectedOrdinalSha,
                    "Approved protected physical-file fingerprint differs.");
                StaffDataDryRunPlanSnapshot canonical;
                IReadOnlyList<string> diagnostics;
                Require(StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(input, out canonical, out diagnostics),
                    "Canonical plan/profile rejected: " + string.Join("\n", diagnostics));
                List<StaffPlan> plans = BuildPlans(canonical, output == StaffExpansionValidationProfiles.Pilot37);
                List<string> assets = plans.SelectMany(p => p.Copies.Select(c => c.Target).Concat(new[] { p.StaffPath, p.SkillPath })).ToList();
                List<string> folders = PlannedFolders(assets);
                Preflight(plans, assets, folders, input, verify);
                Dictionary<string, string> existingBefore = Snapshot(assets.SelectMany(p => new[] { p, p + ".meta" }).Concat(folders.Select(p => p + ".meta")));
                string reportDir = OutputDirectory(mode);
                WriteManifest(reportDir, mode, input, output, plans, assets, folders, protectedBefore);
                int created = 0;
                if (apply)
                {
                    foreach (string folder in folders)
                    {
                        if (AssetDatabase.IsValidFolder(folder)) continue;
                        Require(!File.Exists(folder) && !Directory.Exists(folder) && !File.Exists(folder + ".meta"), "Folder collision: " + folder);
                        Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder))), "Cannot create folder: " + folder);
                    }
                    foreach (StaffPlan plan in plans)
                    {
                        foreach (CopyPlan copy in plan.Copies)
                        {
                            if (File.Exists(copy.Target)) continue;
                            Require(AssetDatabase.CopyAsset(copy.Source, copy.Target), "CopyAsset failed: " + copy.Target);
                            AssetDatabase.ImportAsset(copy.Target, ImportAssetOptions.ForceSynchronousImport);
                            VerifyCopy(copy);
                            created++;
                        }
                        if (!File.Exists(plan.SkillPath))
                        {
                            ScriptableObject skill = ExpectedSkill(plan);
                            AssetDatabase.CreateAsset(skill, plan.SkillPath);
                            EditorUtility.SetDirty(skill);
                            created++;
                        }
                        if (!File.Exists(plan.StaffPath))
                        {
                            ScriptableObject staff = ExpectedStaff(plan);
                            AssetDatabase.CreateAsset(staff, plan.StaffPath);
                            EditorUtility.SetDirty(staff);
                            created++;
                        }
                    }
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
                if (apply || verify)
                {
                    foreach (StaffPlan plan in plans) VerifyStaff(plan);
                    VerifyGlobalGuids(assets);
                    VerifyCatalog(output, plans);
                    Require(StaffDataAssetInventoryValidator.ValidateProfile(output), output.Name + " inventory validation failed.");
                    Require(StaffDataDryRunPlanValidator.TryValidateProfile(output, out diagnostics), "Applied canonical plan validation: " + string.Join("\n", diagnostics));
                    WriteActual(reportDir, plans);
                }
                AssertSnapshot(protectedBefore, ProtectedSnapshot(), "Protected existing assets");
                AssertSnapshot(existingBefore, Snapshot(existingBefore.Keys), "Existing targets (must be no-op)");
                Require(Sha(CsvPath) == CsvSha, "Official CSV changed.");
                File.WriteAllText(Path.Combine(reportDir, "ExecutionResult.txt"),
                    "Mode=" + mode + "\nResult=PASS\nCreatedAssets=" + created + "\nModifiedExisting=0\nDeleted=0\nProtectedFiles=841\n" +
                    "VerifiedStaff=" + ((apply || verify) ? plans.Count : 0) + "\nCatalogProfile=" + output.Name + "\n", new UTF8Encoding(false));
                Debug.Log("STAFF EXPANSION " + mode + ": PASS; created=" + created + "; output=" + reportDir);
            }
            catch (Exception exception)
            {
                // Preserve partial products for inspection. No rollback of user files or assets.
                Debug.LogError("STAFF EXPANSION " + mode + ": BLOCKED. No overwrite/cleanup attempted. " + exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static List<StaffPlan> BuildPlans(StaffDataDryRunPlanSnapshot canonical, bool pilot)
        {
            List<StaffPlan> result = new List<StaffPlan>();
            foreach (StaffDataDryRunStaffPlan data in canonical.StaffPlans.OrderBy(p => p.StaffNumber))
            {
                if (data.StaffNumber < 33 || (pilot && !StaffExpansionValidationProfiles.Pilot37.ContainsStaffId(data.StaffId))) continue;
                Require(data.StaffNumber <= 92, "Unauthorized staff: " + data.StaffId);
                Require(data.SkillPlan != null && data.SkillPlan.RequiredClassExists, "Required skill missing: " + data.StaffId);
                StaffPlan plan = new StaffPlan { Data = data };
                string legacy = "SKIN_STAFF" + (data.StaffNumber - 32).ToString("00", Inv);
                string role = data.TargetConcreteTypeName.Replace("Data", string.Empty);
                string root = VisualRoot + "/" + role;
                AddCopy(plan, "Main", LegacyRoot + "/Sprite/" + legacy + ".png", root + "/Main/" + plan.Id + ".png");
                AddCopy(plan, "Thumbnail", LegacyRoot + "/Thumbnail/" + legacy + ".png", root + "/Thumbnail/" + plan.Id + "_Thumbnail.png");
                var idle = FindNumbered(LegacyRoot + "/IdleSprites", "^" + legacy + "[-_](\\d+)\\.png$");
                if (role == "Marketer") Require(idle.Count == 0, "Marketer has unexpected Idle: " + plan.Id);
                else if (Breathing.Contains(data.StaffNumber)) Require(idle.Count == 0, "Shared-breathing staff has unexpected Idle: " + plan.Id);
                else Require(idle.Count >= 3 && idle.Count <= 5, "Raw Idle must contain 3-5 frames: " + plan.Id);
                foreach (var frame in idle)
                    AddCopy(plan, "Idle", frame.Value, root + "/Idle/" + plan.Id + "-" + frame.Key.ToString("00", Inv) + ".png", frame.Key);
                if (role == "Chef")
                {
                    AddCopy(plan, "Chef Back", LegacyRoot + "/Chef/Back/" + legacy + ".png", root + "/Chef Back/" + plan.Id + "_Back.png");
                    AddCopy(plan, "Chef Hand", LegacyRoot + "/Chef/Hand/" + legacy + ".png", root + "/Chef Hand/" + plan.Id + "_Hand.png");
                }
                if (role == "Marketer")
                {
                    AddCopy(plan, "Marketer Animation", LegacyRoot + "/치어리더/AnimationSprites/" + legacy + ".png", root + "/Marketer Animation/" + plan.Id + "_Animation.png");
                    var particles = FindNumbered(LegacyRoot + "/치어리더/Particles", "^" + legacy + "_Effect(\\d+)\\.png$");
                    Require(particles.Count >= 1 && particles.Count <= 3, "Particle count invalid: " + plan.Id);
                    foreach (var particle in particles)
                        AddCopy(plan, "Marketer Particle", particle.Value, root + "/Marketer Particle/" + plan.Id + "_Effect" + particle.Key + ".png", particle.Key);
                }
                result.Add(plan);
            }
            Require(result.Count == (pilot ? 5 : 60), "Staff manifest count mismatch.");
            var counts = result.SelectMany(p => p.Copies).GroupBy(c => c.Category).ToDictionary(g => g.Key, g => g.Count());
            string[] categories = { "Main", "Thumbnail", "Idle", "Chef Back", "Chef Hand", "Marketer Animation", "Marketer Particle" };
            int[] expected = pilot ? new[] { 5, 5, 15, 1, 1, 1, 3 } : new[] { 60, 60, 142, 16, 16, 12, 29 };
            for (int i = 0; i < categories.Length; i++) Require(counts.ContainsKey(categories[i]) && counts[categories[i]] == expected[i], "Manifest category mismatch: " + categories[i]);
            Require(result.SelectMany(p => p.Copies).Select(c => c.SourceGuid).Distinct(StringComparer.Ordinal).Count() == expected.Sum(), "Duplicate source GUID.");
            return result;
        }

        private static SortedDictionary<int, string> FindNumbered(string folder, string expression)
        {
            var result = new SortedDictionary<int, string>();
            foreach (string path in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
            {
                Match match = Regex.Match(Path.GetFileName(path), expression, RegexOptions.CultureInvariant);
                if (!match.Success) continue;
                int number = int.Parse(match.Groups[1].Value, Inv);
                Require(!result.ContainsKey(number), "Ambiguous source frame: " + path);
                result.Add(number, path.Replace('\\', '/'));
            }
            int expected = 1;
            foreach (int key in result.Keys) Require(key == expected++, "Nonconsecutive source frames in " + folder);
            return result;
        }

        private static void AddCopy(StaffPlan plan, string category, string source, string target, int frame = 0)
        {
            Require(File.Exists(source) && File.Exists(source + ".meta"), "Source missing: " + source);
            string guid = AssetDatabase.AssetPathToGUID(source);
            Require(!string.IsNullOrEmpty(guid), "Source GUID missing: " + source);
            Require(AssetDatabase.LoadAssetAtPath<Sprite>(source) != null, "Source sprite unreadable: " + source);
            Texture2D probe = new Texture2D(2, 2);
            try { Require(ImageConversion.LoadImage(probe, File.ReadAllBytes(source)), "Invalid PNG: " + source); }
            finally { Object.DestroyImmediate(probe); }
            plan.Copies.Add(new CopyPlan { StaffId = plan.Id, Category = category, Source = source, Target = target, Frame = frame,
                SourceGuid = guid, SourceSha = Sha(source), Importer = ImporterSignature(source) });
        }

        private static List<string> PlannedFolders(IEnumerable<string> assets)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string asset in assets)
            {
                string folder = Path.GetDirectoryName(asset).Replace('\\', '/');
                while (folder.StartsWith("Assets/Resources/StaffData/Expansion", StringComparison.Ordinal))
                {
                    result.Add(folder);
                    folder = Path.GetDirectoryName(folder).Replace('\\', '/');
                }
            }
            return result.OrderBy(p => p.Count(c => c == '/')).ThenBy(p => p, StringComparer.Ordinal).ToList();
        }

        private static void Preflight(List<StaffPlan> plans, List<string> assets, List<string> folders, StaffExpansionValidationProfile input, bool verify)
        {
            Require(assets.Distinct(StringComparer.OrdinalIgnoreCase).Count() == assets.Count, "Duplicate target path.");
            var actualPaths = Directory.EnumerateFileSystemEntries("Assets", "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).ToLookup(p => p, StringComparer.OrdinalIgnoreCase);
            foreach (string path in assets.Concat(assets.Select(p => p + ".meta")).Concat(folders).Concat(folders.Select(p => p + ".meta")))
            {
                foreach (string actual in actualPaths[path]) Require(actual == path, "Case-insensitive destination collision: " + actual + " / " + path);
            }
            foreach (StaffPlan plan in plans)
            {
                bool mustExist = input.ContainsStaffId(plan.Id);
                foreach (string path in plan.Copies.Select(c => c.Target).Concat(new[] { plan.StaffPath, plan.SkillPath }))
                {
                    Require(!Directory.Exists(path), "Destination is a directory: " + path);
                    Require(File.Exists(path) == mustExist && File.Exists(path + ".meta") == mustExist, "Unexpected/missing destination asset or orphan meta: " + path);
                }
                if (mustExist) VerifyStaff(plan);
                if (verify) Require(mustExist, "Verify-only cannot generate missing staff: " + plan.Id);
                // Construct nonpersistent Skill now to fail on missing serialized fields before any writes.
                ScriptableObject skill = ExpectedSkill(plan);
                Object.DestroyImmediate(skill);
                ScriptableObject schema = ExpectedStaff(plan, true);
                Object.DestroyImmediate(schema);
            }
            VerifyGlobalGuids(assets.Where(File.Exists));
        }

        private static Type SkillDefinition(string id, out string effect, out float value)
        {
            Type type;
            switch (id)
            {
                case "STAFF_SKILL01": type = typeof(SpeedUpSkill); effect = "_speedUpMul"; value = 100; break;
                case "STAFF_SKILL03": type = typeof(TouchAddCustomerButtonSkill); effect = "_touchInterval"; value = .5f; break;
                case "STAFF_SKILL04": type = typeof(AssignedCookingSpeedUpSkill); effect = "_assignedCookingSpeedUpPercent"; value = 250; break;
                case "STAFF_SKILL05": type = typeof(FoodPriceUpSkill); effect = "_foodPriceUpPercent"; value = 50; break;
                case "STAFF_SKILL06": type = typeof(FoodPaymentTipUpSkill); effect = "_foodPaymentTipUpPercent"; value = 50; break;
                case "STAFF_SKILL08": type = typeof(NormalCustomerMoveSpeedUpSkill); effect = "_normalCustomerMoveSpeedUpPercent"; value = 100; break;
                case "STAFF_SKILL09": type = typeof(GlobalRemainingCookingTimeReductionSkill); effect = "_remainingCookingTimeReductionPercent"; value = 50; break;
                case "STAFF_SKILL10": type = typeof(AllStaffMoveSpeedUpSkill); effect = "_allStaffMoveSpeedUpPercent"; value = 50; break;
                default: throw new InvalidOperationException("Unapproved skill: " + id);
            }
            return type;
        }

        private static ScriptableObject ExpectedSkill(StaffPlan plan)
        {
            StaffDataDryRunSkillPlan data = plan.Data.SkillPlan;
            string effect;
            float value;
            Type type = SkillDefinition(data.OfficialSkillId, out effect, out value);
            Require(type.Name == data.RequiredClassName, "Canonical skill class mismatch: " + plan.Id);
            ScriptableObject result = ScriptableObject.CreateInstance(type);
            result.name = plan.Id + "Skill";
            using (SerializedObject serialized = new SerializedObject(result))
            {
                Set(serialized, "_description", data.TargetDescription);
                Set(serialized, "_duration", data.TargetDuration);
                Set(serialized, "_cooldown", data.TargetCooldown);
                Set(serialized, effect, value.ToString("R", Inv));
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return result;
        }

        private static ScriptableObject ExpectedStaff(StaffPlan plan, bool schemaOnly = false)
        {
            Type type;
            string levels;
            switch (plan.Data.TargetConcreteTypeName)
            {
                case "WaiterData": type = typeof(WaiterData); levels = "_waiterLevelData"; break;
                case "ManagerData": type = typeof(ManagerData); levels = "_managerLevelData"; break;
                case "MarketerData": type = typeof(MarketerData); levels = "_marketerLevelData"; break;
                case "ChefData": type = typeof(ChefData); levels = "_chefLevelData"; break;
                case "CleanerData": type = typeof(CleanerData); levels = "_cleanerLevelData"; break;
                default: throw new InvalidOperationException("Unapproved concrete StaffData type: " + plan.Data.TargetConcreteTypeName);
            }
            ScriptableObject result = ScriptableObject.CreateInstance(type);
            result.name = plan.Id;
            using (SerializedObject serialized = new SerializedObject(result))
            {
                Required(serialized, levels).arraySize = 5;
                foreach (StaffDataDryRunFieldPlan field in plan.Data.FieldPlans)
                {
                    if (field.FieldPath.StartsWith("StaffData.", StringComparison.Ordinal)
                        && new[] { "_id", "_name", "_description", "_rank", "_speed" }.Contains(field.FieldPath.Substring(10)))
                        Set(serialized, field.FieldPath.Substring(10), field.TargetValue);
                    Match match = Regex.Match(field.FieldPath, "^Levels\\[(\\d+)\\]\\.(.+)$");
                    if (!match.Success) continue;
                    string leaf = match.Groups[2].Value;
                    string path = levels + ".Array.data[" + match.Groups[1].Value + "].";
                    if (leaf == "_moneyType" || leaf == "_price") path += "_upgradeMoneyData.";
                    Set(serialized, path + leaf, field.TargetValue == "TERMINAL_REVIEW" ? "Gold" : field.TargetValue);
                }
                // Explicit structural defaults, not legacy purchase prices or token-shop data.
                Set(serialized, "_salesLocationType", "None");
                Set(serialized, "_floorType", "None");
                Set(serialized, "_moneyType", "Gold");
                Set(serialized, "_buyScore", "0");
                Set(serialized, "_buyPrice", "0");
                Required(serialized, "_animatorController").objectReferenceValue = null;
                // Validate schema before creating even the first target PNG. This transient
                // object is never saved and never receives legacy visual references.
                if (schemaOnly)
                {
                    foreach (string path in new[] { "_sprite", "_thumbnailSprite", "_skill", "_idleSprites" }) Required(serialized, path);
                    if (type == typeof(ChefData))
                        foreach (string path in new[] { "_backSprite", "_handSprite", "_handOffset" }) Required(serialized, path);
                    if (type == typeof(MarketerData))
                        foreach (string path in new[] { "_uiSprite", "_animationSprite", "_particleSprites", "_particleCount" }) Required(serialized, path);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    return result;
                }
                Required(serialized, "_sprite").objectReferenceValue = SpriteFor(plan, "Main");
                Required(serialized, "_thumbnailSprite").objectReferenceValue = SpriteFor(plan, "Thumbnail");
                SkillBase skill = AssetDatabase.LoadAssetAtPath<SkillBase>(plan.SkillPath);
                Require(skill != null, "Generated Skill reference missing: " + plan.SkillPath);
                Required(serialized, "_skill").objectReferenceValue = skill;
                List<CopyPlan> raw = plan.Copies.Where(c => c.Category == "Idle").OrderBy(c => c.Frame).ToList();
                List<CopyPlan> expanded = new List<CopyPlan>(raw);
                if (raw.Count != 0)
                {
                    expanded.AddRange(Enumerable.Repeat(raw[raw.Count - 1], 3));
                    expanded.AddRange(raw.Take(raw.Count - 1).Reverse());
                }
                SetSprites(serialized, "_idleSprites", expanded);
                if (type == typeof(ChefData))
                {
                    Required(serialized, "_backSprite").objectReferenceValue = SpriteFor(plan, "Chef Back");
                    Required(serialized, "_handSprite").objectReferenceValue = SpriteFor(plan, "Chef Hand");
                    Required(serialized, "_handOffset").vector2Value = new Vector2(-.18f, 1.43f);
                }
                if (type == typeof(MarketerData))
                {
                    Required(serialized, "_uiSprite").objectReferenceValue = SpriteFor(plan, "Main");
                    Required(serialized, "_animationSprite").objectReferenceValue = SpriteFor(plan, "Marketer Animation");
                    SetSprites(serialized, "_particleSprites", plan.Copies.Where(c => c.Category == "Marketer Particle").OrderBy(c => c.Frame).ToList());
                    Set(serialized, "_particleCount", "10");
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return result;
        }

        private static SerializedProperty Required(SerializedObject serialized, string path)
        {
            SerializedProperty property = serialized.FindProperty(path);
            Require(property != null, "Required serialized field missing: " + serialized.targetObject.GetType().Name + "." + path);
            return property;
        }

        private static void Set(SerializedObject serialized, string path, string value)
        {
            SerializedProperty property = Required(serialized, path);
            switch (property.propertyType)
            {
                case SerializedPropertyType.String: property.stringValue = value; break;
                case SerializedPropertyType.Float: property.floatValue = float.Parse(value, Inv); break;
                case SerializedPropertyType.Integer: property.intValue = int.Parse(value, Inv); break;
                case SerializedPropertyType.Enum:
                    int index = Array.IndexOf(property.enumNames, value);
                    Require(index >= 0, "Unknown enum value " + path + "=" + value);
                    property.enumValueIndex = index;
                    break;
                default: throw new InvalidOperationException("Unsupported planned field: " + path + " (" + property.propertyType + ")");
            }
        }

        private static Sprite SpriteFor(StaffPlan plan, string category)
        {
            return LoadSprite(plan.Copies.Single(c => c.Category == category).Target);
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite != null, "Target Sprite missing: " + path);
            return sprite;
        }

        private static void SetSprites(SerializedObject serialized, string path, List<CopyPlan> copies)
        {
            SerializedProperty array = Required(serialized, path);
            array.arraySize = copies.Count;
            for (int i = 0; i < copies.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite(copies[i].Target);
        }

        private static void VerifyStaff(StaffPlan plan)
        {
            foreach (CopyPlan copy in plan.Copies) VerifyCopy(copy);
            CompareExpected(plan.SkillPath, ExpectedSkill(plan));
            CompareExpected(plan.StaffPath, ExpectedStaff(plan));
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(plan.StaffPath);
            Require(staff != null && staff.Id == plan.Id && staff.MaxLevel == 5, "Staff identity/level mismatch: " + plan.Id);
            Require(staff.AnimatorController == null, "Per-staff Animator is prohibited: " + plan.Id);
            HashSet<string> allowed = new HashSet<string>(plan.Copies.Select(c => c.Target), StringComparer.Ordinal) { plan.SkillPath };
            using (SerializedObject serialized = new SerializedObject(staff))
            {
                SerializedProperty iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Require(iterator.objectReferenceValue != null || iterator.objectReferenceInstanceIDValue == 0, "Missing reference: " + plan.Id + " " + iterator.propertyPath);
                    if (iterator.propertyPath == "m_Script") { Require(iterator.objectReferenceValue != null, "Missing Script: " + plan.Id); continue; }
                    if (iterator.objectReferenceValue == null) continue;
                    string path = AssetDatabase.GetAssetPath(iterator.objectReferenceValue);
                    Require(allowed.Contains(path), "Foreign/Legacy reference: " + plan.Id + " " + iterator.propertyPath + "=" + path);
                }
            }
        }

        private static void CompareExpected(string path, ScriptableObject expected)
        {
            try
            {
                ScriptableObject actual = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Require(actual != null && actual.GetType() == expected.GetType(), "Asset missing/wrong class: " + path);
                var actualValues = SerializedValues(actual);
                var expectedValues = SerializedValues(expected);
                AssertSnapshot(expectedValues, actualValues, "Serialized canonical fields: " + path);
            }
            finally { Object.DestroyImmediate(expected); }
        }

        private static Dictionary<string, string> SerializedValues(Object value)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (SerializedObject serialized = new SerializedObject(value))
            {
                SerializedProperty item = serialized.GetIterator();
                while (item.Next(true))
                {
                    string text;
                    switch (item.propertyType)
                    {
                        case SerializedPropertyType.Generic: continue;
                        case SerializedPropertyType.String: text = item.stringValue; break;
                        case SerializedPropertyType.Boolean: text = item.boolValue.ToString(); break;
                        case SerializedPropertyType.Integer:
                        case SerializedPropertyType.ArraySize:
                        case SerializedPropertyType.Enum: text = item.intValue.ToString(Inv); break;
                        case SerializedPropertyType.Float: text = item.floatValue.ToString("R", Inv); break;
                        case SerializedPropertyType.Vector2: text = item.vector2Value.x.ToString("R", Inv) + "," + item.vector2Value.y.ToString("R", Inv); break;
                        case SerializedPropertyType.ObjectReference:
                            Require(item.objectReferenceValue != null || item.objectReferenceInstanceIDValue == 0, "Missing serialized reference: " + item.propertyPath);
                            if (item.objectReferenceValue == null) text = "null";
                            else
                            {
                                string guid;
                                long localId;
                                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item.objectReferenceValue, out guid, out localId), "Non-asset reference: " + item.propertyPath);
                                text = guid + ":" + localId.ToString(Inv);
                            }
                            break;
                        default: throw new InvalidOperationException("Unaudited serialized field type: " + item.propertyPath + " " + item.propertyType);
                    }
                    result.Add(item.propertyPath, text);
                }
            }
            return result;
        }

        private static void VerifyCopy(CopyPlan copy)
        {
            Require(File.Exists(copy.Target) && File.Exists(copy.Target + ".meta"), "Target PNG/meta missing: " + copy.Target);
            Require(Sha(copy.Source) == copy.SourceSha && Sha(copy.Target) == copy.SourceSha, "PNG hash mismatch: " + copy.Target);
            string guid = AssetDatabase.AssetPathToGUID(copy.Target);
            Require(!string.IsNullOrEmpty(guid) && guid != copy.SourceGuid, "Reused/missing target GUID: " + copy.Target);
            Require(ImporterSignature(copy.Source) == copy.Importer && ImporterSignature(copy.Target) == copy.Importer, "Importer mismatch: " + copy.Target);
            Require(AssetDatabase.LoadAssetAtPath<Sprite>(copy.Target) != null, "Unreadable target PNG: " + copy.Target);
        }

        private static string ImporterSignature(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Missing TextureImporter: " + path);
            StringBuilder text = new StringBuilder();
            text.Append(importer.textureType).Append('|').Append(importer.spriteImportMode).Append('|')
                .Append(importer.spritePixelsPerUnit.ToString("R", Inv)).Append('|')
                .Append(importer.spritePivot.x.ToString("R", Inv)).Append(',').Append(importer.spritePivot.y.ToString("R", Inv)).Append('|')
                .Append(importer.spriteBorder.ToString("R")).Append('|').Append(importer.filterMode).Append('|')
                .Append(importer.wrapMode).Append('|').Append(importer.wrapModeU).Append('|').Append(importer.wrapModeV).Append('|').Append(importer.wrapModeW).Append('|')
                .Append(importer.mipmapEnabled).Append('|').Append(importer.alphaIsTransparency).Append('|')
                .Append(importer.textureCompression).Append('|').Append(importer.compressionQuality).Append('|').Append(importer.maxTextureSize).Append('|')
                .Append(importer.isReadable).Append('|').Append(importer.sRGBTexture).Append('|').Append(importer.alphaSource).Append('|').Append(importer.npotScale).Append('\n');
            SortedSet<string> platforms = new SortedSet<string>(StringComparer.Ordinal) { "DefaultTexturePlatform", "Standalone", "Android", "iPhone", "WebGL" };
            using (SerializedObject serialized = new SerializedObject(importer))
            {
                SerializedProperty settings = serialized.FindProperty("m_PlatformSettings") ?? serialized.FindProperty("platformSettings");
                Require(settings != null && settings.isArray, "Cannot inspect platform overrides: " + path);
                for (int i = 0; i < settings.arraySize; i++)
                {
                    SerializedProperty platform = settings.GetArrayElementAtIndex(i).FindPropertyRelative("m_BuildTarget");
                    if (platform == null) platform = settings.GetArrayElementAtIndex(i).FindPropertyRelative("buildTarget");
                    Require(platform != null, "Cannot inspect platform name: " + path);
                    platforms.Add(platform.stringValue);
                }
            }
            foreach (string platform in platforms)
            {
                TextureImporterPlatformSettings settings = platform == "DefaultTexturePlatform" ? importer.GetDefaultPlatformTextureSettings() : importer.GetPlatformTextureSettings(platform);
                text.Append(platform).Append('|').Append(JsonUtility.ToJson(settings)).Append('\n');
            }
            return text.ToString();
        }

        private static void VerifyGlobalGuids(IEnumerable<string> targets)
        {
            var targetGuids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string target in targets)
            {
                string guid = AssetDatabase.AssetPathToGUID(target);
                Require(!string.IsNullOrEmpty(guid) && !targetGuids.ContainsKey(guid), "Duplicate/missing target GUID: " + target);
                targetGuids.Add(guid, target + ".meta");
            }
            var counts = targetGuids.Keys.ToDictionary(g => g, g => 0, StringComparer.Ordinal);
            foreach (string meta in Directory.EnumerateFiles("Assets", "*.meta", SearchOption.AllDirectories))
            {
                Match match = Regex.Match(File.ReadAllText(meta), "^guid: ([0-9a-f]{32})", RegexOptions.Multiline);
                if (!match.Success || !targetGuids.ContainsKey(match.Groups[1].Value)) continue;
                string guid = match.Groups[1].Value;
                Require(meta.Replace('\\', '/') == targetGuids[guid], "Target GUID reused by " + meta);
                counts[guid]++;
            }
            foreach (var count in counts) Require(count.Value == 1, "Target GUID must occur once: " + count.Key);
        }

        private static void VerifyCatalog(StaffExpansionValidationProfile profile, List<StaffPlan> plans)
        {
            StaffData[] catalog = Resources.LoadAll<StaffData>("StaffData");
            List<string> errors = new List<string>();
            Require(StaffExpansionValidationProfiles.ValidateExactIds(catalog.Select(s => s.Id), profile, errors), string.Join("\n", errors));
            string error;
            Require(StaffGachaRandomSelector.TryValidatePurchaseCatalog(catalog, out error), "Catalog selector preflight: " + error);
            foreach (StaffPlan plan in plans)
            {
                StaffData staff = catalog.Single(s => s.Id == plan.Id);
                GachaStaffData wrapper = GachaStaffData.Create(staff);
                try
                {
                    StaffGachaAcquisitionResult result;
                    Require(StaffGachaAcquisitionCalculator.TryCalculate(new[] { staff.Id }, new[] { wrapper }, out result, out error), "Memory-only duplicate token validation: " + error);
                    Require(result.TotalPandaTokens.ToString(Inv) == plan.Data.DuplicationTokenRaw, "CSV/rank token policy mismatch: " + plan.Id);
                }
                finally { Object.DestroyImmediate(wrapper); }
            }
            var save = new StaffAccountSaveData(1, plans.Select(p => new StaffAccountStaffRecord(p.Id, 1)).ToArray(), 0);
            string json;
            Require(StaffAccountSaveConverter.TrySerialize(save, out json, out error), "Memory-only save serialization: " + error);
            StaffAccountSaveReadResult read = StaffAccountSaveConverter.Read(json);
            Require(read.Status == StaffAccountSaveReadStatus.Success && read.Data.Staff.Select(s => s.Id).SequenceEqual(save.Staff.Select(s => s.Id)), "Memory-only new ID save roundtrip mismatch.");
            Debug.Log("SAFE CATALOG: " + catalog.Length + "; selector input valid; new ID save roundtrip=" + plans.Count + "; no RNG/account/server/payment invoked.");
        }

        private static Dictionary<string, string> ProtectedSnapshot()
        {
            List<string> paths = new List<string>();
            for (int i = 1; i <= 32; i++)
            {
                string path = "Assets/Resources/StaffData/STAFF" + i.ToString("00", Inv) + ".asset";
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(path);
                Require(staff != null && staff.Skill != null, "Protected baseline asset/reference missing: " + path);
                string skill = AssetDatabase.GetAssetPath(staff.Skill);
                paths.AddRange(new[] { path, path + ".meta", skill, skill + ".meta" });
            }
            const string skin = "Assets/Resources/StaffData/Skin";
            paths.AddRange(Directory.GetFiles(skin, "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')));
            paths.Add(skin + ".meta");
            Require(paths.Distinct(StringComparer.Ordinal).Count() == 841, "Protected path count differs from approved 841.");
            foreach (string path in paths) Require(File.Exists(path), "Protected file missing: " + path);
            return Snapshot(paths);
        }

        private static Dictionary<string, string> Snapshot(IEnumerable<string> paths)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in paths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
                if (File.Exists(path)) result.Add(path, Sha(path));
            return result;
        }

        private static void AssertSnapshot(Dictionary<string, string> expected, Dictionary<string, string> actual, string label)
        {
            Require(expected.Count == actual.Count, label + " count changed: " + expected.Count + " / " + actual.Count);
            foreach (var pair in expected)
            {
                string found;
                Require(actual.TryGetValue(pair.Key, out found) && found == pair.Value, label + " differs: " + pair.Key + " expected=" + pair.Value + " actual=" + found);
            }
        }

        private static string OutputDirectory(string mode)
        {
            string[] args = Environment.GetCommandLineArgs();
            string requested = null;
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-staffExpansionOutput") requested = args[i + 1];
            string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(requested ?? Path.Combine(root, "PandaRestaurant_StaffExpansion", mode));
            Require(path.StartsWith(root, StringComparison.OrdinalIgnoreCase), "Audit output must be under the OS temporary directory.");
            Require(!path.StartsWith(Path.GetFullPath(".") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Audit output cannot be inside the project.");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WriteManifest(string directory, string mode, StaffExpansionValidationProfile input, StaffExpansionValidationProfile output,
            List<StaffPlan> plans, List<string> assets, List<string> folders, Dictionary<string, string> protection)
        {
            string prefix = output == StaffExpansionValidationProfiles.Pilot37 ? "Pilot" : "Full";
            WriteCsv(Path.Combine(directory, prefix + "Mapping.csv"), new[] { "LegacyId", "StaffId", "Name", "Type", "Rank", "Action" },
                plans.Select(p => new[] { "SKIN_STAFF" + (p.Data.StaffNumber - 32).ToString("00", Inv), p.Id, p.Data.TargetName,
                    p.Data.TargetConcreteTypeName, p.Data.TargetRankName, File.Exists(p.StaffPath) ? "NOOP" : "CREATE" }));
            WriteCsv(Path.Combine(directory, prefix + "AssetCopyPlan.csv"), new[] { "StaffId", "Category", "Frame", "Source", "Target", "SourceGuid", "SourceSHA256", "ExpectedType", "ImporterSHA256", "Action" },
                plans.SelectMany(p => p.Copies).Select(c => new[] { c.StaffId, c.Category, c.Frame.ToString(Inv), c.Source, c.Target, c.SourceGuid, c.SourceSha, "Sprite", ShaText(c.Importer), File.Exists(c.Target) ? "NOOP" : "CREATE" }));
            WriteCsv(Path.Combine(directory, prefix + "StaffDataPlan.csv"), new[] { "StaffId", "Target", "Type", "Name", "Description", "Rank", "Speed", "SkillTarget", "IdlePolicy", "Fields", "ProbabilityPlanningOnly", "CurrencyPlanningOnly", "DuplicateTokens", "TokenPricePlanningOnly", "Action" },
                plans.Select(p => new[] { p.Id, p.StaffPath, p.Data.TargetConcreteTypeName, p.Data.TargetName, p.Data.TargetDescription, p.Data.TargetRankName,
                    p.Data.TargetSpeed, p.SkillPath, p.Data.VisualPlan.IdlePolicyCode,
                    string.Join(";", p.Data.FieldPlans.Select(f => f.FieldPath + "=" + f.TargetValue)), p.Data.GachaProbabilityRaw, p.Data.AcquisitionCurrencyRaw,
                    p.Data.DuplicationTokenRaw, p.Data.TokenPurchasePriceRaw, File.Exists(p.StaffPath) ? "NOOP" : "CREATE" }));
            WriteCsv(Path.Combine(directory, prefix + "SkillPlan.csv"), new[] { "StaffId", "Target", "SkillId", "ExpectedClass", "Description", "Duration", "Cooldown", "EffectField", "EffectValue", "Action" },
                plans.Select(SkillManifestRow));
            WriteCsv(Path.Combine(directory, "PhysicalManifest.csv"), new[] { "Path", "Kind", "Action", "BeforeSHA256" },
                assets.SelectMany(p => new[] { p, p + ".meta" }).Concat(folders.Select(p => p + ".meta")).OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => new[] { p, p.EndsWith(".meta", StringComparison.Ordinal) ? "Meta" : "Asset", File.Exists(p) ? "NOOP" : "CREATE", File.Exists(p) ? Sha(p) : "ABSENT" }));
            WriteCsv(Path.Combine(directory, "ProtectedFiles.csv"), new[] { "Path", "SHA256" }, protection.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new[] { p.Key, p.Value }));
            File.WriteAllText(Path.Combine(directory, prefix + "Summary.txt"),
                "InputProfile=" + input.Name + "\nOutputProfile=" + output.Name + "\nStaffRows=" + plans.Count + "\nPngRows=" + plans.Sum(p => p.Copies.Count) +
                "\nStaffDataRows=" + plans.Count + "\nSkillRows=" + plans.Count + "\nCreateAssets=" + assets.Count(p => !File.Exists(p)) +
                "\nNoOpAssets=" + assets.Count(File.Exists) + "\nFolders=" + folders.Count + "\nMissingSource=0\nAmbiguousSource=0\nDuplicateTarget=0\nConflictingTarget=0\n" +
                "CSV_SHA256=" + CsvSha + "\nStructuralDefaults=salesLocation None; floor None; money Gold; buyScore/buyPrice 0; animator null; terminal upgrade money Gold\n" +
                "ScreenReview=STAFF39 frame4; all Chef hand offsets; Marketer particles; STAFF49 name; STAFF68 Skill09; STAFF91 no atlas\n",
                new UTF8Encoding(false));
        }

        private static void WriteActual(string directory, List<StaffPlan> plans)
        {
            var paths = plans.SelectMany(p => p.Copies.Select(c => c.Target).Concat(new[] { p.StaffPath, p.SkillPath })).ToArray();
            WriteCsv(Path.Combine(directory, "ActualAssets.csv"), new[] { "Path", "Guid", "Type", "SHA256", "MetaSHA256", "Bytes", "MetaBytes" },
                paths.Select(p => new[] { p, AssetDatabase.AssetPathToGUID(p), AssetDatabase.GetMainAssetTypeAtPath(p).Name, Sha(p), Sha(p + ".meta"), new FileInfo(p).Length.ToString(Inv), new FileInfo(p + ".meta").Length.ToString(Inv) }));
        }

        private static string[] SkillManifestRow(StaffPlan plan)
        {
            string effect;
            float value;
            Type type = SkillDefinition(plan.Data.SkillPlan.OfficialSkillId, out effect, out value);
            return new[] { plan.Id, plan.SkillPath, plan.Data.SkillPlan.OfficialSkillId, type.Name, plan.Data.SkillPlan.TargetDescription,
                plan.Data.SkillPlan.TargetDuration, plan.Data.SkillPlan.TargetCooldown, effect, value.ToString("R", Inv), File.Exists(plan.SkillPath) ? "NOOP" : "CREATE" };
        }

        private static void WriteCsv(string path, string[] header, IEnumerable<string[]> rows)
        {
            StringBuilder text = new StringBuilder();
            foreach (string[] row in new[] { header }.Concat(rows))
                text.Append(string.Join(",", row.Select(v => "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\""))).Append('\n');
            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }

        private static string Sha(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static string ShaText(string value)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
