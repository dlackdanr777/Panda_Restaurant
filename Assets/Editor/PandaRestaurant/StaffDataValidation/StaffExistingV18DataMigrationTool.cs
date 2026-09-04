using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffExistingV18DataMigrationTool
    {
        private const string PolicyMarker =
            "EXISTING_STAFF_V18_DATA_APPLICATION_2026_08_27_V2";
        private const string PreviewMenuPath =
            "Tools/Panda Restaurant/Staff/Preview Existing Staff V18 Data Migration";
        private const string ApplyMenuPath =
            "Tools/Panda Restaurant/Staff/Apply Existing Staff V18 Data Migration";
        private const string OfficialPackageFingerprint =
            "be7613e884b5ae18dc94e57abc0c941dccfb09486ae9fc5ff75acf4b0e4703af";
        private const string ExpectedWorkingBranch =
            "26/08/27_CodexTest_02(Staff-Data-V18-UI-01)";
        private const string RequiredBaseCommit =
            "6e95876d8a6a90ef3bb7102bb52a6917fab0934a";
        private const string Staff32OfficialSkillEffectField = "_foodPriceUpPercent";
        private const string Staff32OfficialSkillEffectValue = "50";
        private const string PreA1InventoryFingerprint =
            "90c3a56ca032d6542359d392ca014ce29b3c367cb227238165d9eadacf0be15b";
        private const string PreA1PlanFingerprint =
            "9a4162af233fdeb6394687cc65d39108655a9b99c079df039177b28d1ea11fc0";
        private const string ExpectedSkillFilesFingerprint =
            "cbbca9bc6aa9eaa025957e729b4bcdcba7dd7a46bac934d16eefb65c8629ae50";
        private const string ProtectedProjectionAlgorithmVersion =
            "A1_PROTECTED_YAML_PROJECTION_V2_STRING_SCALAR";
        private const int RollbackLeaseRetryCount = 3;
        private const int ExistingStaffCount = 32;
        private const int OfficialStaffCount = 92;
        private const int OfficialFileCount = 9;
        private const int ExpectedA1EditorChangedPathCount = 6;

        private static readonly TargetBaseline[] Baselines =
        {
            new TargetBaseline("STAFF01", "6b3ae5ffcf72c04449a7faaac29dd127", "05e3cf508bf56fd4eace1f06301d7158", "6300d8cfec8d6c802888cb3f2a9e3efc8cd9ab24394ead8e00f132e0576eebd2"),
            new TargetBaseline("STAFF02", "7ba29d167421b434499ec7dc1f0060ef", "05e3cf508bf56fd4eace1f06301d7158", "4bc264a0ca647d6240912bff6812084c3d05dbadd5b28f6a6df8569a8522ad47"),
            new TargetBaseline("STAFF03", "528fb76468cc1984696936af9df821db", "05e3cf508bf56fd4eace1f06301d7158", "091dd883e3b0fa1b1af095a6efd07145d692b0c31d3f3540ab28f5edde7712e1"),
            new TargetBaseline("STAFF04", "23ed6c08345350a4bb78cfb28f65c0fe", "05e3cf508bf56fd4eace1f06301d7158", "358982a09906ad495de1f4738d8a2ef0fd0d627e7149bd36c98aafe916ee4fb3"),
            new TargetBaseline("STAFF05", "67271e431b72f4541ade0d589907574f", "05e3cf508bf56fd4eace1f06301d7158", "7e2f2fdc5804e6775750bbe505392d087cd497a5004e906a3ef60dfb1fe12cf4"),
            new TargetBaseline("STAFF06", "59935b680c0c13d42839773c1da87294", "9b8dc5b35de2517409d1bc40735a6ac5", "e7a4f38659d6d670d3f7d37a61f67f507ea2d532cf3c48f2431e2a4e46d4ef42"),
            new TargetBaseline("STAFF07", "436f6fb37766a674aa3e8feb4c89a9be", "9b8dc5b35de2517409d1bc40735a6ac5", "c8a557dcb6d8099a305c93139a50f3dc93c3c1bf07c4092d226041c4b436ebb9"),
            new TargetBaseline("STAFF08", "294edc94f7487574784882cf4e07a170", "9b8dc5b35de2517409d1bc40735a6ac5", "6b737f7ab57e81fd35c287467e43b7fab48ef1fec7d0f7499fe67a137984ba2e"),
            new TargetBaseline("STAFF09", "eb7f4fa7aeac2a44baf5122341737cd1", "9b8dc5b35de2517409d1bc40735a6ac5", "78ed674b6cdcca3303531b1e467b8d60fe475c932098e58b0bdbb6bb508ae637"),
            new TargetBaseline("STAFF10", "3e5b3acb2a4730c4492a86a55f93354c", "9b8dc5b35de2517409d1bc40735a6ac5", "6b10922f6e5d1ee9ae995026090dff9028511fa28085e55d03de82c2be996143"),
            new TargetBaseline("STAFF11", "838d2dd73ed5ce94fa646679800ff0ca", "6e39c2e09d58fec4e92da628aa6f0c2e", "bc5aa4e81e143738ce47eb864fe64b291800f8521047e14a5f3ec6fa9ad4a57a"),
            new TargetBaseline("STAFF12", "12a673ab83346bd46af929e38d126d5d", "6e39c2e09d58fec4e92da628aa6f0c2e", "8d1e117e17af72564f4a3f1ea938c00db6be6d124f584cac257cfd9fd0b096dd"),
            new TargetBaseline("STAFF13", "9718408ac4584a94e9693feb2ec7e087", "6e39c2e09d58fec4e92da628aa6f0c2e", "f979093b53ce2506d104b406d56a31a9266bfd4613d7985cea416797fc636194"),
            new TargetBaseline("STAFF14", "58ceff19d99bfc64daf2960206283da0", "6e39c2e09d58fec4e92da628aa6f0c2e", "380d026f909d791e78871f72f5017b38186873b2bda9ab2808f638db30908763"),
            new TargetBaseline("STAFF15", "11f9f22c01f895148bf8d4850846a90b", "6e39c2e09d58fec4e92da628aa6f0c2e", "3e415ef8601a108c62aa15428d9b18f665f78581bee4151fe893f53d4ced4100"),
            new TargetBaseline("STAFF16", "da705e4f8a75fb14e9dc72bc28ccbe1f", "93bdf2a61116f5a4bbf3c395f44840da", "1a73c08b4a6d093085d577ea922e5578bd3cb03dfe3f743c2a41938403447b91"),
            new TargetBaseline("STAFF17", "0f3d8c7113aeaf842b4abdca0d6e3428", "93bdf2a61116f5a4bbf3c395f44840da", "5d5dec43fb50b7cf21a9ae48fbcbbd054bb6ea089891dd5ee5b0d1aba814db43"),
            new TargetBaseline("STAFF18", "99decdeb5bd9c7340bf68f89584f0d59", "93bdf2a61116f5a4bbf3c395f44840da", "9c8bf1e3497c6c38b3a6efe80e27ad98fddecb6edd779321d8c911efb790a923"),
            new TargetBaseline("STAFF19", "770fa62dcd6b3ba4dabc3902f20dec83", "93bdf2a61116f5a4bbf3c395f44840da", "bbce812ec4030b23d431a62b513a690e0a8f9bfbfd0160e67b9798ce7224bf0c"),
            new TargetBaseline("STAFF20", "ef0624ef5a06d7645ad0d42fa2cf9a92", "93bdf2a61116f5a4bbf3c395f44840da", "3a48668f13202052c8b733cbd89c6f2a9d20343ce82c84b9a195bb9c54ebb195"),
            new TargetBaseline("STAFF21", "27db2b4a0a715f148b92c586173365ed", "a377ea42dec1d4d4c9ea62112443872a", "1a7f48a1b0d12437c661561d3ee2510aee0855eb2732894d24937665203705b1"),
            new TargetBaseline("STAFF22", "a9ef4572d39343a4eba476535ffe9a06", "a377ea42dec1d4d4c9ea62112443872a", "e02a0c9b0d2c1ed599a78c2efd307b03c38f98e3947da8f32809b4b84029fab5"),
            new TargetBaseline("STAFF23", "7a2061bc612d130479109a6cbe5752cd", "a377ea42dec1d4d4c9ea62112443872a", "523094efa708105ba825dad30eb4780c8ce51fc4298db3815b8e2a4d4a0ee34b"),
            new TargetBaseline("STAFF24", "6e65d4cf14b01df4ebc5f04bec9a0486", "a377ea42dec1d4d4c9ea62112443872a", "c8ec0c77412c903f22a8ea62761dadc70c449430182cc974e9174c7faecaf85e"),
            new TargetBaseline("STAFF25", "6cf863ebee2f321469fd6740622836d5", "a377ea42dec1d4d4c9ea62112443872a", "cf63fd40b122e8bd0f69e97cffaba34c3f2969eede5014fd6946d823260922aa"),
            new TargetBaseline("STAFF26", "edc23f451ea3108419720297d7a49a30", "3aab8e332f22bca419d43cb78389ea2a", "ba8a1b804d208210221917721aa60ddb57becb0233f5e0c19f5839c54da22789"),
            new TargetBaseline("STAFF27", "9b53d48f70593ca4bb248b9fa7193cdd", "93bdf2a61116f5a4bbf3c395f44840da", "3205fdf1b2b370aad67a57555f01d54d1261866f4dcbe30696c0436e4e72107e"),
            new TargetBaseline("STAFF28", "3fbf93325902f264cbdd924b96f9e009", "05e3cf508bf56fd4eace1f06301d7158", "9aa283eeaa6d4967e56080071a61711a265f946862b10cc6537ae718e47434e6"),
            new TargetBaseline("STAFF29", "d633b00ca05bb73439909966100a0a09", "93bdf2a61116f5a4bbf3c395f44840da", "afa9067970a6ed86759cb07fe23206842c727ed1262c59974da5778f31019793"),
            new TargetBaseline("STAFF30", "c46f6778341b4814da437128b0ad3376", "3aab8e332f22bca419d43cb78389ea2a", "47b993cf296931c4fae59907ab53ca1fe3db0ec14cb30391f6a1da317fff0f07"),
            new TargetBaseline("STAFF31", "9513b729c8e3f784c86b683539bf3db0", "05e3cf508bf56fd4eace1f06301d7158", "a96199d3d2ce4eb09cc5b77d6583c9e913a01bb70cd6f1bc77e9397301a933a0"),
            new TargetBaseline("STAFF32", "2e43896a39f712a4da691ea084db98f1", "a377ea42dec1d4d4c9ea62112443872a", "7742d556637c6b0f455bfa322bf281c4e6637d9861fc0b118ec707ae441a23ef")
        };

        private static readonly Dictionary<string, RoleDefinition> Roles =
            new Dictionary<string, RoleDefinition>(StringComparer.Ordinal)
            {
                { "WAITER", new RoleDefinition("WaiterData", "_waiterLevelData", "_addSpeed") },
                { "CLEANER", new RoleDefinition("CleanerData", "_cleanerLevelData", "_addSpeed", "_cleaningTime") },
                { "CHEF", new RoleDefinition("ChefData", "_chefLevelData", "_foodSpeedAddPercent", "_addSpeed") },
                { "MANAGER", new RoleDefinition("ManagerData", "_managerLevelData", "_customerGuideTime") },
                { "CHEERLEADER", new RoleDefinition("MarketerData", "_marketerLevelData", "_marketingTime") },
                { "GUARD", new RoleDefinition("GuardData", "_guardLevelData", "_actionTime") }
            };

        [MenuItem(PreviewMenuPath)]
        private static void PreviewMigration()
        {
            MigrationInspection inspection;
            List<string> errors = new List<string>();
            bool inspected = TryInspect(out inspection, errors);
            LogInspection("PREVIEW", inspection, errors, inspected);
        }

        [MenuItem(ApplyMenuPath)]
        private static void ApplyMigrationFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "[Existing Staff V18 Data Migration]\n"
                    + "APPLY FAIL: Play Mode에서는 실행할 수 없습니다.");
                return;
            }

            MigrationInspection inspection;
            List<string> errors = new List<string>();
            if (!TryInspect(out inspection, errors) || inspection == null)
            {
                LogInspection("APPLY", inspection, errors, false);
                return;
            }

            if (inspection.State == MigrationState.ALREADY_APPLIED)
            {
                LogInspection("APPLY", inspection, errors, true);
                Debug.Log(
                    "ALREADY_APPLIED\n"
                    + "No migration write is required.\n"
                    + "Asset write: 0\n"
                    + "Rollback: 0");
                return;
            }

            if (!IsApplyWriteAllowed(inspection.State))
            {
                inspection.Reasons.Add(
                    "READY_TO_APPLY가 아니므로 모든 Asset 쓰기를 차단했습니다.");
                LogInspection("APPLY", inspection, errors, false);
                return;
            }

            if (inspection.RepositoryAudit == null
                || inspection.RepositoryAudit.ApplyReadiness != ApplyReadiness.READY)
            {
                LogApplyRepositoryBlock(inspection);
                return;
            }

            List<string> rollbackCapabilityErrors = new List<string>();
            if (!ValidateRollbackCapabilityPreflight(rollbackCapabilityErrors))
            {
                LogRollbackCapabilityFailure(rollbackCapabilityErrors);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Existing Staff V18 Data Migration",
                    "STAFF01~32의 이름, 설명, Rank, 기본속도, 역할 Lv.1~5 값, "
                    + "강화 점수·재화·비용만 Official V18로 적용합니다.\n\n"
                    + "GUID, Script, Skill, Visual, 구매·획득 필드와 STAFF02 6번째 슬롯은 "
                    + "보존되며 실패 시 32개 Asset 전체가 원본 bytes로 복구됩니다.\n\n"
                    + "계속하시겠습니까?",
                    "Apply",
                    "Cancel"))
            {
                Debug.LogWarning("Existing Staff V18 Data Migration Apply가 취소되었습니다. Asset write 0.");
                return;
            }

            ApplyMigration();
        }

        private static bool TryInspect(
            out MigrationInspection inspection,
            List<string> errors)
        {
            inspection = new MigrationInspection();
            string selfTestError;
            if (!RunPureProjectionSelfTests(out selfTestError))
            {
                errors.Add("Protected projection self-test failed: " + selfTestError);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!RunPurePostApplyResultSelfTests(out selfTestError))
            {
                errors.Add("Post-Apply result contract self-test failed: " + selfTestError);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!RunPureStateMatrixSelfTests(out selfTestError))
            {
                errors.Add("A1 state matrix self-test failed: " + selfTestError);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!RunPureRollbackTransactionSelfTests(out selfTestError))
            {
                errors.Add("Rollback transaction self-test failed: " + selfTestError);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!RunPureGitChangedPathSelfTests(out selfTestError))
            {
                errors.Add("Git changed-path contract self-test failed: " + selfTestError);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!ValidateA1GitSafety(inspection, errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            StaffOfficialDataPackageSnapshot official;
            IReadOnlyList<string> officialDiagnostics;
            if (!StaffDataPackValidator.TryBuildCanonicalV8ReadOnlySnapshot(
                    out official,
                    out officialDiagnostics)
                || official == null)
            {
                AddDiagnostics("Canonical Official V18", officialDiagnostics, errors);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.OfficialFingerprint = official.PackageFingerprint;
            StaffOfficialFileSnapshot finalStaff = null;
            if (official.PackageFingerprint != OfficialPackageFingerprint
                || official.OfficialFileCount != OfficialFileCount
                || !official.TryGetFile(StaffOfficialDataPackageKeys.FinalStaff, out finalStaff)
                || finalStaff == null
                || finalStaff.Rows.Count != OfficialStaffCount)
            {
                errors.Add(
                    "Official V18 lock changed: fingerprint/files/staff "
                    + official.PackageFingerprint + "/" + official.OfficialFileCount + "/"
                    + (finalStaff == null ? 0 : finalStaff.Rows.Count) + ".");
                inspection.State = MigrationState.INVALID;
                return false;
            }

            StaffDataAssetInventorySnapshot inventory;
            IReadOnlyList<string> inventoryDiagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out inventory,
                    out inventoryDiagnostics)
                || inventory == null)
            {
                AddDiagnostics("Current Inventory", inventoryDiagnostics, errors);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.Inventory = inventory;
            inspection.InventoryFingerprint = inventory.InventoryFingerprint;
            if (!ValidateInventoryStructure(inspection, errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            StaffDataDryRunPlanSnapshot plan;
            IReadOnlyList<string> planDiagnostics;
            if (!StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(
                    out plan,
                    out planDiagnostics)
                || plan == null)
            {
                AddDiagnostics("Canonical Dry Run V8", planDiagnostics, errors);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.Plan = plan;
            inspection.PlanFingerprint = plan.PlanFingerprint;
            string stateMarker;
            string stateReason;
            if (!StaffDataDryRunPlanner.TryClassifyExistingV18DataState(
                    plan,
                    out stateMarker,
                    out stateReason))
            {
                errors.Add("Existing Staff V18 plan state classification failed: " + stateReason);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.PlanStateMarker = stateMarker;
            inspection.Reasons.Add(stateReason);
            bool targetStructureValid = ValidateTargetsAndWhitelist(inspection, errors);
            if (!targetStructureValid)
            {
                inspection.State = inspection.RequiredPropertyMissing
                    ? MigrationState.INVALID
                    : MigrationState.PARTIAL_MIGRATION_STATE;
                return inspection.State != MigrationState.INVALID;
            }

            MigrationDataAudit dataAudit = BuildMigrationDataAudit(inspection);
            inspection.DataAudit = dataAudit;
            inspection.State = dataAudit.State;
            inspection.PreservationBaselineValid = dataAudit.ProtectedContractPassed;
            inspection.RepositoryAudit = BuildRepositoryHygieneAudit(dataAudit.State);
            inspection.PreviewVerdict = DeterminePreviewVerdict(
                dataAudit.State,
                inspection.RepositoryAudit.State);
            inspection.Reasons.AddRange(dataAudit.Info);
            errors.AddRange(dataAudit.Errors);

            return inspection.State != MigrationState.INVALID;
        }

        private static bool ValidateInventoryStructure(
            MigrationInspection inspection,
            List<string> errors)
        {
            StaffDataAssetInventorySnapshot inventory = inspection.Inventory;
            if (inventory.Staff.Count != ExistingStaffCount || inventory.Skills.Count != ExistingStaffCount)
            {
                errors.Add(
                    "Expected current Staff/Skill count is 32/32, actual "
                    + inventory.Staff.Count + "/" + inventory.Skills.Count + ".");
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < inventory.Staff.Count; index++)
            {
                StaffDataAssetSnapshot staff = inventory.Staff[index];
                if (!ids.Add(staff.Id)
                    || string.IsNullOrEmpty(staff.AssetGuid)
                    || !guids.Add(staff.AssetGuid))
                {
                    errors.Add("Staff ID/GUID missing or duplicated: " + staff.Id + ".");
                    return false;
                }

                if (staff.HasMissingRequiredReference
                    || staff.SkillReference == null
                    || !staff.SkillReference.IsAssigned
                    || staff.SkillReference.IsMissing
                    || string.IsNullOrEmpty(staff.ScriptGuid))
                {
                    errors.Add("Missing Script/Skill/Visual reference: " + staff.Id + ".");
                    return false;
                }
            }

            for (int number = 1; number <= ExistingStaffCount; number++)
            {
                string id = BuildStaffId(number);
                if (!ids.Contains(id))
                {
                    errors.Add("Required StaffData asset is missing: " + id + ".");
                    return false;
                }
            }

            for (int number = 33; number <= OfficialStaffCount; number++)
            {
                if (ids.Contains(BuildStaffId(number)))
                {
                    errors.Add("Unexpected STAFF33~92 asset exists: " + BuildStaffId(number) + ".");
                    return false;
                }
            }

            StaffDataAssetSnapshot staff02;
            if (!inventory.TryGetStaff("STAFF02", out staff02)
                || staff02 == null
                || staff02.LevelCount != 6)
            {
                errors.Add("STAFF02 level array must remain exactly 6 slots.");
                return false;
            }

            inspection.Staff02LevelCount = staff02.LevelCount;
            return true;
        }

        private static bool ValidateTargetsAndWhitelist(
            MigrationInspection inspection,
            List<string> errors)
        {
            bool valid = true;
            int existingPlans = 0;
            for (int planIndex = 0; planIndex < inspection.Plan.StaffPlans.Count; planIndex++)
            {
                StaffDataDryRunStaffPlan staffPlan = inspection.Plan.StaffPlans[planIndex];
                if (staffPlan.AssetAction != StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    continue;
                }

                existingPlans++;
                StaffDataAssetSnapshot current;
                RoleDefinition role;
                if (!inspection.Inventory.TryGetStaff(staffPlan.StaffId, out current)
                    || current == null
                    || !Roles.TryGetValue(staffPlan.RoleKey, out role)
                    || current.ConcreteTypeName != role.ConcreteTypeName
                    || current.ConcreteTypeName != staffPlan.TargetConcreteTypeName)
                {
                    errors.Add("Concrete StaffData class mismatch: " + staffPlan.StaffId + ".");
                    inspection.RequiredPropertyMissing = true;
                    valid = false;
                    continue;
                }

                StaffData asset = AssetDatabase.LoadAssetAtPath<StaffData>(current.AssetPath);
                if (asset == null)
                {
                    errors.Add("StaffData load failed: " + current.AssetPath + ".");
                    inspection.RequiredPropertyMissing = true;
                    valid = false;
                    continue;
                }

                SerializedObject serialized = new SerializedObject(asset);
                serialized.Update();
                int changedForStaff = 0;
                for (int fieldIndex = 0; fieldIndex < staffPlan.FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = staffPlan.FieldPlans[fieldIndex];
                    if (field.Disposition != StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING)
                    {
                        continue;
                    }

                    SerializedProperty property;
                    ResolveFailure failure;
                    string resolveError;
                    if (!TryResolveTargetProperty(
                            serialized,
                            role,
                            field,
                            out property,
                            out failure,
                            out resolveError))
                    {
                        if (failure == ResolveFailure.UNSUPPORTED_FIELD)
                        {
                            inspection.UnsupportedFields++;
                            inspection.Reasons.Add(
                                "Unsupported FieldPath " + staffPlan.StaffId + " | "
                                + field.FieldPath + ".");
                        }
                        else
                        {
                            inspection.RequiredPropertyMissing = true;
                            errors.Add(staffPlan.StaffId + " | " + resolveError);
                        }

                        valid = false;
                        continue;
                    }

                    string targetError;
                    if (!CanAssignTargetValue(property, field.TargetValue, out targetError))
                    {
                        inspection.RequiredPropertyMissing = true;
                        errors.Add(
                            staffPlan.StaffId + " | " + field.FieldPath + " | " + targetError);
                        valid = false;
                        continue;
                    }

                    CountField(inspection, field);
                    changedForStaff += field.IsChanged ? 1 : 0;
                }

                inspection.ChangedByStaff[staffPlan.StaffId] = changedForStaff;
            }

            if (existingPlans != ExistingStaffCount)
            {
                errors.Add("Expected 32 UPDATE_EXISTING plans, actual " + existingPlans + ".");
                valid = false;
            }

            return valid && inspection.UnsupportedFields == 0;
        }

        private static void CountField(
            MigrationInspection inspection,
            StaffDataDryRunFieldPlan field)
        {
            if (!field.IsChanged)
            {
                return;
            }

            inspection.ChangedFields++;
            inspection.NameChanges += field.FieldPath == "StaffData._name" ? 1 : 0;
            inspection.DescriptionChanges += field.FieldPath == "StaffData._description" ? 1 : 0;
            inspection.RankChanges += field.FieldPath == "StaffData._rank" ? 1 : 0;
            inspection.SpeedChanges += field.FieldPath == "StaffData._speed" ? 1 : 0;
            if (field.FieldPath.StartsWith("Levels[", StringComparison.Ordinal))
            {
                if (field.FieldPath.EndsWith("._upgradeMinScore", StringComparison.Ordinal)
                    || field.FieldPath.EndsWith("._moneyType", StringComparison.Ordinal)
                    || field.FieldPath.EndsWith("._price", StringComparison.Ordinal))
                {
                    inspection.UpgradeChanges++;
                }
                else
                {
                    inspection.RoleValueChanges++;
                }
            }
        }

        private static MigrationDataAudit BuildMigrationDataAudit(
            MigrationInspection inspection)
        {
            MigrationDataAudit audit = new MigrationDataAudit();
            audit.OfficialSnapshotPassed = inspection != null
                                           && string.Equals(
                                               inspection.OfficialFingerprint,
                                               OfficialPackageFingerprint,
                                               StringComparison.Ordinal);
            audit.InventoryBuilt = inspection != null && inspection.Inventory != null;
            audit.PlanBuilt = inspection != null && inspection.Plan != null;
            audit.StructuralErrorsZero = ValidatePostApplyStructure(inspection);
            audit.UnexpectedAssetsZero = ValidateUnexpectedAssetsZero(inspection);
            if (!audit.InventoryBuilt || !audit.PlanBuilt)
            {
                audit.Errors.Add("A1 authoritative state audit is missing Inventory or Plan.");
                audit.State = MigrationState.INVALID;
                return audit;
            }

            audit.ManagedTargetExpectedCount = CountManagedTargets(inspection.Plan);
            audit.ManagedTargetMatchedCount = Math.Max(
                0,
                audit.ManagedTargetExpectedCount - inspection.ChangedFields);
            audit.ManagedTargetMatch = audit.ManagedTargetExpectedCount > 0
                                       && audit.ManagedTargetMatchedCount
                                       == audit.ManagedTargetExpectedCount;

            ValidateAuthoritativeProtectedContract(inspection, audit);
            audit.DeterministicStatePassed = ValidateDeterministicPostState(
                inspection,
                audit.Errors);
            if (audit.ManagedTargetMatch)
            {
                audit.OfficialSpotChecksPassed = ValidateOfficialSpotChecks(
                    inspection,
                    audit.Errors);
                audit.Staff32SpotCheck = ValidateStaff32SpotCheck(inspection);
                audit.Staff32SpotCheckPassed = audit.Staff32SpotCheck.Passed;
                audit.Info.Add(BuildStaff32SpotCheckReport(audit.Staff32SpotCheck));
                audit.Errors.AddRange(audit.Staff32SpotCheck.Errors);
            }
            else
            {
                audit.OfficialSpotChecksPassed = true;
                audit.Staff32SpotCheck = new Staff32SpotCheckResult { Passed = true };
                audit.Staff32SpotCheckPassed = true;
            }

            bool preA1FingerprintPassed = string.Equals(
                                              inspection.InventoryFingerprint,
                                              PreA1InventoryFingerprint,
                                              StringComparison.Ordinal)
                                          && string.Equals(
                                              inspection.PlanFingerprint,
                                              PreA1PlanFingerprint,
                                              StringComparison.Ordinal);
            bool readyContractPassed = preA1FingerprintPassed
                                       && inspection.PlanStateMarker
                                       == StaffDataDryRunPlanner.ExistingStaffV18DataReadyToApplyMarker
                                       && inspection.ChangedFields > 0
                                       && audit.ProtectedContractPassed
                                       && audit.StructuralErrorsZero;
            bool appliedDataContractPassed = audit.ManagedTargetMatch
                                             && audit.ManagedTargetMatchedCount
                                             == audit.ManagedTargetExpectedCount
                                             && audit.ProtectedContractPassed
                                             && audit.ProtectedStaffCount == ExistingStaffCount
                                             && audit.Staff02Slot6Preserved
                                             && audit.GuidPassed
                                             && audit.ScriptPassed
                                             && audit.SkillPassed
                                             && audit.VisualPassed
                                             && audit.MetaPassed
                                             && audit.InventoryBuilt
                                             && audit.PlanBuilt
                                             && audit.StructuralErrorsZero
                                             && audit.UnexpectedAssetsZero
                                             && audit.DeterministicStatePassed
                                             && audit.OfficialSpotChecksPassed
                                             && audit.Staff32SpotCheckPassed;

            audit.State = ClassifyExistingV18DataState(
                audit.OfficialSnapshotPassed,
                audit.InventoryBuilt,
                audit.PlanBuilt,
                audit.StructuralErrorsZero,
                readyContractPassed,
                appliedDataContractPassed,
                inspection.PlanStateMarker
                == StaffDataDryRunPlanner.ExistingStaffV18DataAppliedMarker,
                audit.ManagedTargetMatchedCount,
                audit.ManagedTargetExpectedCount,
                audit.ProtectedContractPassed);
            audit.ClassifierInconsistent = audit.State
                                           == MigrationState.STATE_CLASSIFIER_INCONSISTENT;
            if (audit.State == MigrationState.PARTIAL_MIGRATION_STATE)
            {
                AppendManagedTargetMismatchErrors(inspection.Plan, audit.Errors);
            }
            else if (audit.ClassifierInconsistent)
            {
                audit.Errors.Add(
                    "STATE_CLASSIFIER_INCONSISTENT: Authoritative data checks passed, "
                    + "but the plan state reporter did not return ALREADY_APPLIED. "
                    + "Automatic rollback is prohibited.");
            }

            audit.Info.Add(
                "Managed Target Match: " + audit.ManagedTargetMatchedCount + "/"
                + audit.ManagedTargetExpectedCount);
            audit.Info.Add(
                "Protected Preservation: " + audit.ProtectedStaffCount + "/"
                + ExistingStaffCount);
            audit.Info.Add("OfficialFingerprint: " + inspection.OfficialFingerprint);
            audit.Info.Add("InventoryFingerprint: " + inspection.InventoryFingerprint);
            audit.Info.Add("PlanFingerprint: " + inspection.PlanFingerprint);
            audit.Warnings.Add("STAFF02 Save Migration: 1");
            return audit;
        }

        private static MigrationState ClassifyExistingV18DataState(
            bool officialSnapshotPassed,
            bool inventoryBuilt,
            bool planBuilt,
            bool structuralErrorsZero,
            bool readyContractPassed,
            bool appliedDataContractPassed,
            bool planReporterApplied,
            int matchedTargets,
            int expectedTargets,
            bool protectedContractPassed)
        {
            if (!officialSnapshotPassed
                || !inventoryBuilt
                || !planBuilt
                || !structuralErrorsZero
                || expectedTargets <= 0)
            {
                return MigrationState.INVALID;
            }

            if (readyContractPassed)
            {
                return MigrationState.READY_TO_APPLY;
            }

            if (appliedDataContractPassed)
            {
                return planReporterApplied
                    ? MigrationState.ALREADY_APPLIED
                    : MigrationState.STATE_CLASSIFIER_INCONSISTENT;
            }

            if ((matchedTargets > 0 && matchedTargets < expectedTargets)
                || !protectedContractPassed)
            {
                return MigrationState.PARTIAL_MIGRATION_STATE;
            }

            return MigrationState.PARTIAL_MIGRATION_STATE;
        }

        private static bool RequiresDataIntegrityRollback(MigrationDataAudit audit)
        {
            return audit == null
                   || !audit.ManagedTargetMatch
                   || !audit.ProtectedContractPassed
                   || !audit.Staff02Slot6Preserved
                   || !audit.GuidPassed
                   || !audit.ScriptPassed
                   || !audit.SkillPassed
                   || !audit.VisualPassed
                   || !audit.MetaPassed
                   || !audit.UnexpectedAssetsZero;
        }

        private static int CountManagedTargets(StaffDataDryRunPlanSnapshot plan)
        {
            int count = 0;
            if (plan == null)
            {
                return count;
            }

            for (int staffIndex = 0; staffIndex < plan.StaffPlans.Count; staffIndex++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[staffIndex];
                if (staff.AssetAction != StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    continue;
                }

                for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                {
                    if (staff.FieldPlans[fieldIndex].Disposition
                        == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void AppendManagedTargetMismatchErrors(
            StaffDataDryRunPlanSnapshot plan,
            List<string> errors)
        {
            if (plan == null)
            {
                return;
            }

            for (int staffIndex = 0; staffIndex < plan.StaffPlans.Count; staffIndex++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[staffIndex];
                if (staff.AssetAction != StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    continue;
                }

                for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = staff.FieldPlans[fieldIndex];
                    if (field.Disposition != StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                        || !field.IsChanged)
                    {
                        continue;
                    }

                    errors.Add(
                        "MIXED MANAGED TARGET:\nStaff ID: " + staff.StaffId
                        + "\nField Path: " + field.FieldPath
                        + "\nExpected: " + FormatLogValue(field.TargetValue)
                        + "\nActual: " + FormatLogValue(field.CurrentValue));
                }
            }
        }

        private static void ValidateAuthoritativeProtectedContract(
            MigrationInspection inspection,
            MigrationDataAudit audit)
        {
            audit.GuidPassed = true;
            audit.ScriptPassed = true;
            audit.VisualPassed = true;
            audit.MetaPassed = true;
            audit.ProtectedStaffCount = 0;
            for (int index = 0; index < Baselines.Length; index++)
            {
                TargetBaseline baseline = Baselines[index];
                StaffDataAssetSnapshot current;
                if (!inspection.Inventory.TryGetStaff(baseline.StaffId, out current)
                    || current == null)
                {
                    audit.Errors.Add("Protected contract target missing: " + baseline.StaffId + ".");
                    audit.GuidPassed = false;
                    audit.ScriptPassed = false;
                    audit.VisualPassed = false;
                    audit.MetaPassed = false;
                    continue;
                }

                bool guidPassed = string.Equals(
                    current.AssetGuid,
                    baseline.AssetGuid,
                    StringComparison.Ordinal);
                bool scriptPassed = string.Equals(
                    current.ScriptGuid,
                    baseline.ScriptGuid,
                    StringComparison.Ordinal);
                string actualMetaSha = ComputeFileSha256(baseline.AssetPath + ".meta");
                bool metaPassed = string.Equals(
                    actualMetaSha,
                    baseline.MetaSha256,
                    StringComparison.Ordinal);
                string baselineText;
                string gitError;
                bool baselineRead = TryReadRequiredBaseText(
                    baseline.AssetPath,
                    out baselineText,
                    out gitError);
                string currentText = File.ReadAllText(GetAbsolutePath(baseline.AssetPath));
                string baselineProjection = baselineRead
                    ? BuildA1ProtectedYamlProjection(baselineText)
                    : string.Empty;
                string currentProjection = BuildA1ProtectedYamlProjection(currentText);
                bool projectionPassed = baselineRead
                                        && string.Equals(
                                            baselineProjection,
                                            currentProjection,
                                            StringComparison.Ordinal);

                audit.GuidPassed &= guidPassed;
                audit.ScriptPassed &= scriptPassed;
                audit.MetaPassed &= metaPassed;
                audit.VisualPassed &= projectionPassed;
                if (guidPassed && scriptPassed && metaPassed && projectionPassed)
                {
                    audit.ProtectedStaffCount++;
                }
                else
                {
                    AppendProtectedBaselineFailure(
                        baseline,
                        guidPassed,
                        current.AssetGuid,
                        scriptPassed,
                        current.ScriptGuid,
                        metaPassed,
                        actualMetaSha,
                        projectionPassed,
                        baselineProjection,
                        currentProjection,
                        gitError,
                        audit.Errors);
                }
            }

            inspection.SkillFilesFingerprint = ComputeSkillFilesFingerprint(inspection.Inventory);
            audit.SkillPassed = string.Equals(
                inspection.SkillFilesFingerprint,
                ExpectedSkillFilesFingerprint,
                StringComparison.Ordinal);
            if (!audit.SkillPassed)
            {
                audit.Errors.Add(
                    "PROTECTED CONTRACT FAIL:\nStaff ID: ALL"
                    + "\nProtected Field: SkillFilesFingerprint"
                    + "\nExpected: " + ExpectedSkillFilesFingerprint
                    + "\nActual: " + inspection.SkillFilesFingerprint
                    + "\nBaseline Algorithm Version: " + ProtectedProjectionAlgorithmVersion
                    + "\nCurrent Algorithm Version: " + ProtectedProjectionAlgorithmVersion);
            }

            audit.Staff02Slot6Preserved = inspection.Staff02LevelCount == 6
                                          && audit.ProtectedStaffCount == ExistingStaffCount;
            audit.ProtectedContractPassed = audit.ProtectedStaffCount == ExistingStaffCount
                                            && audit.GuidPassed
                                            && audit.ScriptPassed
                                            && audit.SkillPassed
                                            && audit.VisualPassed
                                            && audit.MetaPassed
                                            && audit.Staff02Slot6Preserved;
        }

        private static void AppendProtectedBaselineFailure(
            TargetBaseline baseline,
            bool guidPassed,
            string actualGuid,
            bool scriptPassed,
            string actualScriptGuid,
            bool metaPassed,
            string actualMetaSha,
            bool projectionPassed,
            string expectedProjection,
            string actualProjection,
            string gitError,
            List<string> errors)
        {
            if (!guidPassed)
            {
                errors.Add(BuildNamedBaselineFailure(
                    baseline.StaffId,
                    "StaffData.AssetGuid",
                    baseline.AssetGuid,
                    actualGuid));
            }

            if (!scriptPassed)
            {
                errors.Add(BuildNamedBaselineFailure(
                    baseline.StaffId,
                    "StaffData.ScriptGuid",
                    baseline.ScriptGuid,
                    actualScriptGuid));
            }

            if (!metaPassed)
            {
                errors.Add(BuildNamedBaselineFailure(
                    baseline.StaffId,
                    "StaffData.MetaSha256",
                    baseline.MetaSha256,
                    actualMetaSha));
            }

            if (!projectionPassed)
            {
                string protectedField;
                string expected;
                string actual;
                FindFirstProjectionDifference(
                    expectedProjection,
                    actualProjection,
                    out protectedField,
                    out expected,
                    out actual);
                errors.Add(BuildNamedBaselineFailure(
                    baseline.StaffId,
                    string.IsNullOrEmpty(gitError)
                        ? protectedField
                        : "RequiredBaseGitBlob",
                    string.IsNullOrEmpty(gitError) ? expected : "Readable",
                    string.IsNullOrEmpty(gitError) ? actual : gitError));
            }
        }

        private static string BuildNamedBaselineFailure(
            string staffId,
            string protectedField,
            string expected,
            string actual)
        {
            return "PROTECTED CONTRACT FAIL:\nStaff ID: " + staffId
                   + "\nProtected Field: " + protectedField
                   + "\nExpected: " + FormatLogValue(expected)
                   + "\nActual: " + FormatLogValue(actual)
                   + "\nBaseline Algorithm Version: " + ProtectedProjectionAlgorithmVersion
                   + "\nCurrent Algorithm Version: " + ProtectedProjectionAlgorithmVersion;
        }

        private static string BuildA1ProtectedYamlProjection(string text)
        {
            string normalized = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
            string[] lines = normalized.Split('\n');
            string[] rootKeys = { "_name", "_description", "_rank", "_speed" };
            string[] levelKeys =
            {
                "  - _upgradeMinScore:", "      _moneyType:", "      _price:",
                "    _addSpeed:", "    _cleaningTime:", "    _foodSpeedAddPercent:",
                "    _customerGuideTime:", "    _marketingTime:", "    _actionTime:"
            };
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            StringBuilder projection = new StringBuilder();
            bool skipStringContinuation = false;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (skipStringContinuation)
                {
                    if (line.Length > 0 && CountLeadingSpaces(line) > 2)
                    {
                        continue;
                    }

                    skipStringContinuation = false;
                }

                bool rootRemoved = false;
                for (int rootIndex = 0; rootIndex < rootKeys.Length; rootIndex++)
                {
                    string prefix = "  " + rootKeys[rootIndex] + ":";
                    if (!line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    skipStringContinuation = rootKeys[rootIndex] == "_name"
                                             || rootKeys[rootIndex] == "_description";
                    rootRemoved = true;
                    break;
                }

                if (rootRemoved)
                {
                    continue;
                }

                bool levelRemoved = false;
                for (int keyIndex = 0; keyIndex < levelKeys.Length; keyIndex++)
                {
                    string key = levelKeys[keyIndex];
                    if (!line.StartsWith(key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int count;
                    counts.TryGetValue(key, out count);
                    counts[key] = count + 1;
                    levelRemoved = count < 5;
                    break;
                }

                if (!levelRemoved)
                {
                    projection.AppendLine(line);
                }
            }

            return projection.ToString();
        }

        private static int CountLeadingSpaces(string value)
        {
            int count = 0;
            while (count < value.Length && value[count] == ' ')
            {
                count++;
            }

            return count;
        }

        private static void FindFirstProjectionDifference(
            string expectedProjection,
            string actualProjection,
            out string protectedField,
            out string expected,
            out string actual)
        {
            string[] expectedLines = (expectedProjection ?? string.Empty).Split('\n');
            string[] actualLines = (actualProjection ?? string.Empty).Split('\n');
            int count = Math.Max(expectedLines.Length, actualLines.Length);
            for (int index = 0; index < count; index++)
            {
                string expectedLine = index < expectedLines.Length
                    ? expectedLines[index]
                    : "<missing>";
                string actualLine = index < actualLines.Length
                    ? actualLines[index]
                    : "<missing>";
                if (string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
                {
                    continue;
                }

                string fieldLine = actualLine == "<missing>" ? expectedLine : actualLine;
                string trimmed = fieldLine.TrimStart(' ', '-');
                int colon = trimmed.IndexOf(':');
                protectedField = colon > 0
                    ? "StaffData.Serialized." + trimmed.Substring(0, colon)
                    : "YAML line " + (index + 1).ToString(CultureInfo.InvariantCulture);
                expected = expectedLine;
                actual = actualLine;
                return;
            }

            protectedField = "ProtectedProjection";
            expected = ComputeSha256(Encoding.UTF8.GetBytes(expectedProjection ?? string.Empty));
            actual = ComputeSha256(Encoding.UTF8.GetBytes(actualProjection ?? string.Empty));
        }

        private static bool TryReadRequiredBaseText(
            string assetPath,
            out string text,
            out string error)
        {
            text = string.Empty;
            error = string.Empty;
            try
            {
                System.Diagnostics.ProcessStartInfo startInfo =
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "show " + RequiredBaseCommit + ":" + assetPath,
                        WorkingDirectory = GetProjectRoot(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                using (System.Diagnostics.Process process =
                       System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        error = "Git process did not start.";
                        return false;
                    }

                    text = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd().Trim();
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static RepositoryHygieneAudit BuildRepositoryHygieneAudit(
            MigrationState dataState)
        {
            GitChangedPathCollection changedPaths;
            string gitError;
            if (!TryCollectA1ChangedPaths(GetProjectRoot(), out changedPaths, out gitError))
            {
                RepositoryHygieneAudit unavailable = new RepositoryHygieneAudit
                {
                    State = RepositoryHygieneState.A1_SCOPE_ANOMALY,
                    DiffCheckPassed = false
                };
                unavailable.Errors.Add("A1 changed-scope audit failed: " + gitError);
                FinalizeRepositoryHygieneAudit(dataState, unavailable);
                return unavailable;
            }

            string diffCheckOutput;
            int diffCheckExitCode;
            bool diffCheckPassed = TryRunGitRaw(
                GetProjectRoot(),
                "diff --check",
                out diffCheckOutput,
                out gitError,
                out diffCheckExitCode);
            return ClassifyRepositoryHygiene(
                dataState,
                changedPaths,
                diffCheckPassed,
                diffCheckPassed ? string.Empty : gitError);
        }

        private static RepositoryHygieneAudit ClassifyRepositoryHygiene(
            MigrationState dataState,
            GitChangedPathCollection changedPaths,
            bool diffCheckPassed,
            string diffCheckError)
        {
            RepositoryHygieneAudit audit = new RepositoryHygieneAudit
            {
                DiffCheckPassed = diffCheckPassed,
                TotalChangedPathCount = changedPaths == null ? 0 : changedPaths.All.Count,
                WorkingTreeChangedPathCount = changedPaths == null
                    ? 0
                    : changedPaths.WorkingTree.Count,
                StagedChangedPathCount = changedPaths == null ? 0 : changedPaths.Staged.Count,
                UntrackedPathCount = changedPaths == null ? 0 : changedPaths.Untracked.Count
            };
            if (changedPaths == null)
            {
                audit.State = RepositoryHygieneState.A1_SCOPE_ANOMALY;
                audit.Errors.Add("Git changed-path collection is unavailable.");
                FinalizeRepositoryHygieneAudit(dataState, audit);
                return audit;
            }

            audit.StagedPaths.AddRange(changedPaths.Staged);
            HashSet<string> a1Owned = new HashSet<string>(StringComparer.Ordinal);
            List<string> a1ScopeAnomalyPaths = new List<string>();
            for (int index = 0; index < changedPaths.All.Count; index++)
            {
                string path = changedPaths.All[index];
                if (IsExpectedExistingStaffAssetPath(path))
                {
                    a1Owned.Add(path);
                    audit.A1OwnedChangedPaths.Add(path);
                    audit.ChangedStaffAssetCount++;
                }
                else if (IsAllowedA1EditorPath(path))
                {
                    a1Owned.Add(path);
                    audit.A1OwnedChangedPaths.Add(path);
                    audit.AllowedEditorChangedCount++;
                }
                else
                {
                    audit.ExternalChangedPaths.Add(path);
                    if (IsA1ScopeAnomalyPath(path))
                    {
                        a1ScopeAnomalyPaths.Add(path);
                    }
                }
            }

            List<string> expectedEditor = BuildExpectedA1EditorPaths();
            List<string> expectedAll = BuildExpectedA1OwnedPaths();
            bool a1ScopeShapePassed;
            IReadOnlyList<string> expectedForCurrentShape;
            if (changedPaths.All.Count == 0)
            {
                a1ScopeShapePassed = true;
                expectedForCurrentShape = Array.Empty<string>();
            }
            else if (dataState == MigrationState.READY_TO_APPLY)
            {
                a1ScopeShapePassed = a1Owned.SetEquals(expectedEditor);
                expectedForCurrentShape = expectedEditor;
            }
            else if (dataState == MigrationState.ALREADY_APPLIED)
            {
                bool postCommitExternalOnly = a1Owned.Count == 0
                                              && audit.ExternalChangedPaths.Count > 0;
                a1ScopeShapePassed = postCommitExternalOnly || a1Owned.SetEquals(expectedAll);
                expectedForCurrentShape = postCommitExternalOnly
                    ? (IReadOnlyList<string>)Array.Empty<string>()
                    : expectedAll;
            }
            else
            {
                a1ScopeShapePassed = a1Owned.Count == 0
                                     || a1Owned.SetEquals(expectedEditor)
                                     || a1Owned.SetEquals(expectedAll);
                expectedForCurrentShape = Array.Empty<string>();
            }

            if (!a1ScopeShapePassed)
            {
                for (int index = 0; index < expectedForCurrentShape.Count; index++)
                {
                    if (!a1Owned.Contains(expectedForCurrentShape[index]))
                    {
                        audit.MissingExpectedA1Paths.Add(expectedForCurrentShape[index]);
                    }
                }
            }

            if (!diffCheckPassed)
            {
                audit.Errors.Add(
                    "Repository diff check failed."
                    + (string.IsNullOrEmpty(diffCheckError)
                        ? string.Empty
                        : " Git: " + diffCheckError));
            }

            for (int index = 0; index < a1ScopeAnomalyPaths.Count; index++)
            {
                audit.Errors.Add("A1 scope anomaly path: " + a1ScopeAnomalyPaths[index]);
            }

            if (!a1ScopeShapePassed)
            {
                audit.Errors.Add(
                    "A1 owned changed-path shape mismatch. A1-Owned: "
                    + audit.A1OwnedChangedPaths.Count + ", Missing Expected: "
                    + audit.MissingExpectedA1Paths.Count + ".");
            }

            if (!diffCheckPassed || a1ScopeAnomalyPaths.Count > 0 || !a1ScopeShapePassed)
            {
                audit.State = RepositoryHygieneState.A1_SCOPE_ANOMALY;
            }
            else if (audit.ExternalChangedPaths.Count > 0)
            {
                audit.State = RepositoryHygieneState.EXTERNAL_CHANGES_PRESENT;
            }
            else if (changedPaths.All.Count == 0)
            {
                audit.State = RepositoryHygieneState.CLEAN;
            }
            else
            {
                audit.State = RepositoryHygieneState.A1_EXPECTED_CHANGES_ONLY;
            }

            FinalizeRepositoryHygieneAudit(dataState, audit);
            return audit;
        }

        private static void FinalizeRepositoryHygieneAudit(
            MigrationState dataState,
            RepositoryHygieneAudit audit)
        {
            audit.ApplyReadiness = DetermineApplyReadiness(dataState, audit);
            audit.CommitReadiness = DetermineCommitReadiness(dataState, audit);
            audit.Info.Add("Repository Hygiene: " + audit.State);
            audit.Info.Add("A1-Owned Changed Paths: " + audit.A1OwnedChangedPaths.Count);
            audit.Info.Add("Allowed Editor Changed Count: " + audit.AllowedEditorChangedCount);
            audit.Info.Add("Changed Staff Asset Count: " + audit.ChangedStaffAssetCount);
            audit.Info.Add("External Changed Paths: " + audit.ExternalChangedPaths.Count);
            audit.Info.Add("Missing Expected A1 Paths: " + audit.MissingExpectedA1Paths.Count);
            audit.Info.Add("Staged Paths: " + audit.StagedPaths.Count);
            audit.Info.Add("Apply Readiness: " + audit.ApplyReadiness);
            audit.Info.Add("Commit Readiness: " + audit.CommitReadiness);
            for (int index = 0; index < audit.ExternalChangedPaths.Count; index++)
            {
                audit.Warnings.Add("External Changed Path: " + audit.ExternalChangedPaths[index]);
            }

            if (audit.CommitReadiness == CommitReadiness.BLOCKED_BY_EXTERNAL_CHANGES)
            {
                audit.Warnings.Add("Commit Readiness: BLOCKED_BY_EXTERNAL_CHANGES");
            }
        }

        private static ApplyReadiness DetermineApplyReadiness(
            MigrationState dataState,
            RepositoryHygieneAudit repository)
        {
            if (dataState == MigrationState.ALREADY_APPLIED)
            {
                return ApplyReadiness.NOT_APPLICABLE_ALREADY_APPLIED;
            }

            if (dataState != MigrationState.READY_TO_APPLY)
            {
                return ApplyReadiness.BLOCKED_BY_DATA_STATE;
            }

            return repository != null
                   && repository.State == RepositoryHygieneState.A1_EXPECTED_CHANGES_ONLY
                   && repository.StagedPaths.Count == 0
                   && repository.DiffCheckPassed
                ? ApplyReadiness.READY
                : ApplyReadiness.BLOCKED_BY_REPOSITORY_HYGIENE;
        }

        private static CommitReadiness DetermineCommitReadiness(
            MigrationState dataState,
            RepositoryHygieneAudit repository)
        {
            if (dataState != MigrationState.ALREADY_APPLIED)
            {
                return CommitReadiness.BLOCKED_BY_DATA_STATE;
            }

            if (repository == null
                || repository.State == RepositoryHygieneState.A1_SCOPE_ANOMALY
                || !repository.DiffCheckPassed)
            {
                return CommitReadiness.BLOCKED_BY_A1_SCOPE_ANOMALY;
            }

            if (repository.ExternalChangedPaths.Count > 0)
            {
                return CommitReadiness.BLOCKED_BY_EXTERNAL_CHANGES;
            }

            if (repository.StagedPaths.Count > 0)
            {
                return CommitReadiness.BLOCKED_BY_STAGED_CHANGES;
            }

            return repository.State == RepositoryHygieneState.CLEAN
                ? CommitReadiness.NOT_APPLICABLE_CLEAN
                : CommitReadiness.READY;
        }

        private static PreviewVerdict DeterminePreviewVerdict(
            MigrationState dataState,
            RepositoryHygieneState repositoryState)
        {
            bool dataPassed = dataState == MigrationState.READY_TO_APPLY
                              || dataState == MigrationState.ALREADY_APPLIED;
            if (!dataPassed || repositoryState == RepositoryHygieneState.A1_SCOPE_ANOMALY)
            {
                return PreviewVerdict.FAIL;
            }

            return repositoryState == RepositoryHygieneState.EXTERNAL_CHANGES_PRESENT
                ? PreviewVerdict.PASS_WITH_REPOSITORY_WARNING
                : PreviewVerdict.PASS;
        }

        private static bool IsA1ScopeAnomalyPath(string path)
        {
            return path.StartsWith(
                       "Assets/Resources/StaffData/",
                       StringComparison.Ordinal)
                   || path.StartsWith(
                       "Assets/Scripts/Datas/Staff/Skill/",
                       StringComparison.Ordinal)
                   || path.StartsWith(
                       "Assets/Scripts/Datas/Staff/LegacySkill/",
                       StringComparison.Ordinal)
                   || path.StartsWith(
                       "Assets/Editor/PandaRestaurant/StaffDataValidation/",
                       StringComparison.Ordinal);
        }

        private static List<string> BuildExpectedA1EditorPaths()
        {
            string root = "Assets/Editor/PandaRestaurant/StaffDataValidation/";
            return new List<string>
            {
                root + "StaffDataAssetInventoryValidator.cs",
                root + "StaffDataDryRunPlanner.cs",
                root + "StaffDataDryRunPlanValidator.cs",
                root + "StaffDataPackValidator.cs",
                root + "StaffExistingV18DataMigrationTool.cs",
                root + "StaffExistingV18DataMigrationTool.cs.meta"
            };
        }

        private static List<string> BuildExpectedA1OwnedPaths()
        {
            List<string> paths = BuildExpectedA1EditorPaths();
            for (int number = 1; number <= ExistingStaffCount; number++)
            {
                paths.Add("Assets/Resources/StaffData/" + BuildStaffId(number) + ".asset");
            }

            return paths.OrderBy(path => path, StringComparer.Ordinal).ToList();
        }

        private static bool TryCollectA1ChangedPaths(
            string projectRoot,
            out GitChangedPathCollection changedPaths,
            out string error)
        {
            changedPaths = null;
            error = string.Empty;
            string workingTreeOutput;
            string stagedOutput;
            string untrackedOutput;
            string gitError;
            int exitCode;
            if (!TryRunGitRaw(
                    projectRoot,
                    "-c core.quotepath=false diff --name-only -z",
                    out workingTreeOutput,
                    out gitError,
                    out exitCode))
            {
                error = "Working tree changed paths could not be read: " + gitError;
                return false;
            }

            if (!TryRunGitRaw(
                    projectRoot,
                    "-c core.quotepath=false diff --cached --name-only -z",
                    out stagedOutput,
                    out gitError,
                    out exitCode))
            {
                error = "Staged changed paths could not be read: " + gitError;
                return false;
            }

            if (!TryRunGitRaw(
                    projectRoot,
                    "-c core.quotepath=false ls-files --others --exclude-standard -z",
                    out untrackedOutput,
                    out gitError,
                    out exitCode))
            {
                error = "Untracked paths could not be read: " + gitError;
                return false;
            }

            changedPaths = BuildGitChangedPathCollection(
                workingTreeOutput,
                stagedOutput,
                untrackedOutput);
            return true;
        }

        private static GitChangedPathCollection BuildGitChangedPathCollection(
            string workingTreeOutput,
            string stagedOutput,
            string untrackedOutput)
        {
            GitChangedPathCollection result = new GitChangedPathCollection();
            result.WorkingTree.AddRange(ParseNulDelimitedGitPaths(workingTreeOutput));
            result.Staged.AddRange(ParseNulDelimitedGitPaths(stagedOutput));
            result.Untracked.AddRange(ParseNulDelimitedGitPaths(untrackedOutput));

            HashSet<string> all = new HashSet<string>(StringComparer.Ordinal);
            all.UnionWith(result.WorkingTree);
            all.UnionWith(result.Staged);
            all.UnionWith(result.Untracked);
            result.All.AddRange(all.OrderBy(path => path, StringComparer.Ordinal));
            return result;
        }

        private static List<string> ParseNulDelimitedGitPaths(string output)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            string[] entries = (output ?? string.Empty).Split(
                new[] { '\0' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < entries.Length; index++)
            {
                string normalized = NormalizeGitChangedPath(entries[index]);
                if (normalized.Length > 0)
                {
                    paths.Add(normalized);
                }
            }

            return paths.OrderBy(path => path, StringComparer.Ordinal).ToList();
        }

        private static string NormalizeGitChangedPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            while (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(2);
            }

            while (normalized.IndexOf("//", StringComparison.Ordinal) >= 0)
            {
                normalized = normalized.Replace("//", "/");
            }

            return normalized;
        }

        private static bool IsExpectedExistingStaffAssetPath(string path)
        {
            if (!path.StartsWith("Assets/Resources/StaffData/STAFF", StringComparison.Ordinal)
                || !path.EndsWith(".asset", StringComparison.Ordinal))
            {
                return false;
            }

            for (int number = 1; number <= ExistingStaffCount; number++)
            {
                if (string.Equals(
                        path,
                        "Assets/Resources/StaffData/" + BuildStaffId(number) + ".asset",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAllowedA1EditorPath(string path)
        {
            string root = "Assets/Editor/PandaRestaurant/StaffDataValidation/";
            return string.Equals(path, root + "StaffDataAssetInventoryValidator.cs", StringComparison.Ordinal)
                   || string.Equals(path, root + "StaffDataDryRunPlanner.cs", StringComparison.Ordinal)
                   || string.Equals(path, root + "StaffDataDryRunPlanValidator.cs", StringComparison.Ordinal)
                   || string.Equals(path, root + "StaffDataPackValidator.cs", StringComparison.Ordinal)
                   || string.Equals(path, root + "StaffExistingV18DataMigrationTool.cs", StringComparison.Ordinal)
                   || string.Equals(path, root + "StaffExistingV18DataMigrationTool.cs.meta", StringComparison.Ordinal);
        }

        private static void ApplyMigration()
        {
            MigrationInspection before;
            List<string> preflightErrors = new List<string>();
            if (!TryInspect(out before, preflightErrors)
                || before == null
                || !IsApplyWriteAllowed(before.State)
                || before.RepositoryAudit == null
                || before.RepositoryAudit.ApplyReadiness != ApplyReadiness.READY)
            {
                preflightErrors.Add(
                    "Apply 직전 재검사가 Data READY 및 Repository READY 계약을 만족하지 않습니다.");
                LogInspection("APPLY", before, preflightErrors, false);
                return;
            }

            MigrationBackup backup = null;
            bool writesStarted = false;
            try
            {
                List<string> rollbackCapabilityErrors = new List<string>();
                if (!ValidateRollbackCapabilityPreflight(rollbackCapabilityErrors))
                {
                    throw new RollbackCapabilityException(rollbackCapabilityErrors);
                }

                backup = CaptureBackup(before);
                List<string> backupErrors = new List<string>();
                if (!ValidateBackupIntegrity(backup, backupErrors))
                {
                    throw new InvalidOperationException(
                        "Backup integrity validation failed: "
                        + string.Join(" | ", backupErrors));
                }

                writesStarted = true;
                ApplyWhitelistedChanges(before.Plan);
                SaveAndForceSynchronousImport();

                PostApplyValidationResult postApply = ValidatePostApply();
                if (!postApply.Passed)
                {
                    throw new PostApplyValidationException(postApply);
                }

                Debug.Log(BuildPostApplySuccessLog(postApply));
            }
            catch (Exception exception)
            {
                List<string> rollbackErrors = new List<string>();
                PostApplyValidationException validationException =
                    exception as PostApplyValidationException;
                bool rollbackRequired = writesStarted
                                        && (validationException == null
                                            || validationException.Result.RequiresRollback);
                bool rollbackPassed = !rollbackRequired
                                      || RollbackMigration(backup, rollbackErrors);
                StringBuilder output = new StringBuilder();
                output.AppendLine("[Existing Staff V18 Data Migration]");
                if (validationException != null)
                {
                    AppendPostApplyFailureLog(output, validationException.Result);
                    if (validationException.Result.ClassifierInconsistent)
                    {
                        output.AppendLine("STATE_CLASSIFIER_INCONSISTENT");
                        output.AppendLine("Automatic Rollback: PROHIBITED");
                        output.AppendLine("Current applied StaffData retained; additional Asset write 0.");
                    }
                }
                else
                {
                    output.AppendLine("APPLY FAIL:");
                    output.AppendLine(exception.Message);
                }

                output.AppendLine(
                    "Atomic Rollback: "
                    + (!rollbackRequired ? "NOT_REQUIRED" : rollbackPassed ? "PASS" : "FAIL"));
                if (rollbackRequired && rollbackPassed)
                {
                    output.AppendLine("EXISTING STAFF V18 DATA ROLLBACK: PASS");
                }

                for (int index = 0; index < rollbackErrors.Count; index++)
                {
                    output.AppendLine("ROLLBACK ERROR: " + rollbackErrors[index]);
                }

                if (rollbackRequired && !rollbackPassed)
                {
                    output.AppendLine("CRITICAL_EXISTING_STAFF_V18_DATA_ROLLBACK_FAILED");
                }

                Debug.LogError(output.ToString());
            }
        }

        private static PostApplyValidationResult ValidatePostApply()
        {
            PostApplyValidationResult result = new PostApplyValidationResult();
            MigrationInspection applied;
            List<string> inspectionErrors = new List<string>();
            bool inspected = TryInspect(out applied, inspectionErrors);
            result.AppliedInspection = applied;
            result.Errors.AddRange(inspectionErrors);
            MigrationDataAudit audit = applied == null ? null : applied.DataAudit;
            if (!inspected || audit == null)
            {
                result.Errors.Add("Authoritative Post-A1 state audit was not built.");
                result.RequiresRollback = true;
                FinalizePostApplyResult(result);
                return result;
            }

            result.DataAudit = audit;
            result.MatchedTargetFields = audit.ManagedTargetMatchedCount;
            result.TotalTargetFields = audit.ManagedTargetExpectedCount;
            result.PreservedStaff = audit.ProtectedStaffCount;
            result.Staff32SpotCheck = audit.Staff32SpotCheck;
            result.Info.AddRange(audit.Info);
            result.Warnings.AddRange(audit.Warnings);
            if (applied.RepositoryAudit != null)
            {
                result.Info.AddRange(applied.RepositoryAudit.Info);
                result.Warnings.AddRange(applied.RepositoryAudit.Warnings);
            }

            bool postApplyStateApplied = audit.State == MigrationState.ALREADY_APPLIED;

            result.AddCheck(
                "canonicalSnapshotPassed",
                "Canonical Snapshot",
                audit.OfficialSnapshotPassed);
            result.AddCheck(
                "whitelistTargetMatchPassed",
                "Whitelist Target Match",
                audit.ManagedTargetMatch);
            result.AddCheck(
                "protectedPreservationPassed",
                "Protected Preservation",
                audit.ProtectedContractPassed);
            result.AddCheck(
                "staff32SpotCheckPassed",
                "STAFF32 Spot Check",
                audit.Staff32SpotCheckPassed);
            result.AddCheck(
                "staff02Slot6Preserved",
                "STAFF02 Slot 6 Preservation",
                audit.Staff02Slot6Preserved);
            result.AddCheck("postApplyPlanBuilt", "Post-Apply Plan", audit.PlanBuilt);
            result.AddCheck(
                "postApplyInventoryBuilt",
                "Post-Apply Inventory",
                audit.InventoryBuilt);
            result.AddCheck(
                "postApplyStateApplied",
                "Migration State ALREADY_APPLIED",
                postApplyStateApplied);
            result.AddCheck(
                "skillReferencesPreserved",
                "Skill References Preserved",
                audit.SkillPassed);
            result.AddCheck(
                "visualReferencesPreserved",
                "Visual References Preserved",
                audit.VisualPassed);
            result.AddCheck("guidPreserved", "GUID Preserved", audit.GuidPassed);
            result.AddCheck("scriptPreserved", "Script Preserved", audit.ScriptPassed);
            result.AddCheck("metaPreserved", "Meta Preserved", audit.MetaPassed);
            result.AddCheck(
                "structuralErrorsZero",
                "Structural Errors Zero",
                audit.StructuralErrorsZero);
            result.AddCheck(
                "unexpectedAssetsZero",
                "Unexpected Assets Zero",
                audit.UnexpectedAssetsZero);
            result.AddCheck(
                "deterministicPostStatePassed",
                "Deterministic Inventory / Plan",
                audit.DeterministicStatePassed);
            result.AddCheck(
                "officialSpotChecksPassed",
                "Official V18 Spot Checks",
                audit.OfficialSpotChecksPassed);

            result.ClassifierInconsistent = audit.ClassifierInconsistent;
            result.RequiresRollback = RequiresDataIntegrityRollback(audit);
            FinalizePostApplyResult(result);
            return result;
        }

        private static bool ValidatePostApplyStructure(MigrationInspection applied)
        {
            if (applied == null
                || applied.Inventory == null
                || applied.Plan == null
                || applied.Inventory.Staff.Count != ExistingStaffCount
                || applied.Inventory.Skills.Count != ExistingStaffCount)
            {
                return false;
            }

            for (int index = 0; index < applied.Inventory.Staff.Count; index++)
            {
                StaffDataAssetSnapshot staff = applied.Inventory.Staff[index];
                if (staff == null
                    || staff.HasMissingRequiredReference
                    || staff.SkillReference == null
                    || !staff.SkillReference.IsAssigned
                    || staff.SkillReference.IsMissing
                    || string.IsNullOrEmpty(staff.AssetGuid)
                    || string.IsNullOrEmpty(staff.ScriptGuid))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateUnexpectedAssetsZero(MigrationInspection applied)
        {
            if (applied == null
                || applied.Inventory == null
                || applied.Inventory.Staff.Count != ExistingStaffCount)
            {
                return false;
            }

            HashSet<string> actual = new HashSet<string>(
                applied.Inventory.Staff.Select(staff => staff.Id),
                StringComparer.Ordinal);
            if (actual.Count != ExistingStaffCount)
            {
                return false;
            }

            for (int number = 1; number <= ExistingStaffCount; number++)
            {
                if (!actual.Contains(BuildStaffId(number)))
                {
                    return false;
                }
            }

            return true;
        }

        private static void FinalizePostApplyResult(PostApplyValidationResult result)
        {
            if (result.Finalized)
            {
                return;
            }

            result.Finalized = true;
            List<PostApplyCheckResult> failedChecks = result.Checks
                .Where(check => !check.Passed)
                .ToList();
            if (failedChecks.Count > 0 && result.Errors.Count == 0)
            {
                StringBuilder silentFailure = new StringBuilder();
                silentFailure.AppendLine("POST_APPLY_FALSE_WITHOUT_ERROR");
                for (int index = 0; index < result.Checks.Count; index++)
                {
                    PostApplyCheckResult check = result.Checks[index];
                    silentFailure.AppendLine(
                        "- " + check.Key + "=" + FormatBoolean(check.Actual));
                }

                result.Errors.Add(silentFailure.ToString().TrimEnd());
            }

            for (int index = 0; index < failedChecks.Count; index++)
            {
                PostApplyCheckResult check = failedChecks[index];
                result.Errors.Add(
                    "POST-APPLY REQUIRED CHECK FAILED:\nName: " + check.Name
                    + "\nExpected: true\nActual: false");
            }

            result.Passed = failedChecks.Count == 0 && result.Errors.Count == 0;
        }

        private static string BuildPostApplySuccessLog(PostApplyValidationResult result)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Existing Staff V18 Data Migration APPLY]");
            output.AppendLine();
            output.AppendLine("APPLY PASS");
            output.AppendLine();
            output.AppendLine(
                "- A1 Whitelist Target Match: PASS ("
                + result.MatchedTargetFields + "/" + result.TotalTargetFields + ")");
            output.AppendLine(
                "- Protected Preservation: PASS ("
                + result.PreservedStaff + "/" + ExistingStaffCount + ")");
            output.AppendLine("- STAFF32 Spot Check: PASS");
            output.AppendLine("- STAFF02 Slot 6 Preservation: PASS");
            output.AppendLine("- Post-Apply Plan: PASS");
            output.AppendLine("- Post-Apply Inventory: PASS");
            output.AppendLine("- Migration State: ALREADY_APPLIED");
            output.AppendLine("- Errors: " + result.Errors.Count);
            AppendPostApplyChecks(output, result.Checks);
            AppendSeveritySection(output, "Info Section", result.Info);
            AppendSeveritySection(output, "Warning Section", result.Warnings);
            return output.ToString().TrimEnd();
        }

        private static void AppendPostApplyFailureLog(
            StringBuilder output,
            PostApplyValidationResult result)
        {
            output.AppendLine("APPLY FAIL:");
            output.AppendLine("Post-Apply validation failed");
            output.AppendLine();
            output.AppendLine("POST-APPLY ERROR:");
            for (int index = 0; index < result.Errors.Count; index++)
            {
                output.AppendLine("- " + result.Errors[index]);
            }

            AppendPostApplyChecks(output, result.Checks);
            AppendSeveritySection(output, "Info Section", result.Info);
            AppendSeveritySection(output, "Warning Section", result.Warnings);
        }

        private static void AppendPostApplyChecks(
            StringBuilder output,
            IReadOnlyList<PostApplyCheckResult> checks)
        {
            output.AppendLine();
            for (int index = 0; index < checks.Count; index++)
            {
                PostApplyCheckResult check = checks[index];
                output.AppendLine("POST-APPLY CHECK:");
                output.AppendLine("Name: " + check.Name);
                output.AppendLine("Expected: true");
                output.AppendLine("Actual: " + FormatBoolean(check.Actual));
                output.AppendLine("Result: " + (check.Passed ? "PASS" : "FAIL"));
                output.AppendLine();
            }
        }

        private static void AppendSeveritySection(
            StringBuilder output,
            string heading,
            IReadOnlyList<string> entries)
        {
            output.AppendLine(heading + ":");
            if (entries.Count == 0)
            {
                output.AppendLine("(none)");
            }
            else
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    output.AppendLine(entries[index]);
                }
            }

            output.AppendLine();
        }

        private static string FormatBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        private static bool ValidateRollbackCapabilityPreflight(List<string> errors)
        {
            AssetDatabase.ReleaseCachedFileHandles();
            for (int index = 0; index < Baselines.Length; index++)
            {
                string assetPath = Baselines[index].AssetPath;
                if (!TryProbeExclusiveReadWrite(assetPath, errors)
                    || !TryProbeExclusiveReadWrite(assetPath + ".meta", errors))
                {
                    errors.Insert(0, "ROLLBACK_CAPABILITY_PREFLIGHT_FAILED");
                    return false;
                }
            }

            return true;
        }

        private static bool TryProbeExclusiveReadWrite(
            string assetPath,
            List<string> errors)
        {
            string absolutePath = GetAbsolutePath(assetPath);
            for (int attempt = 1; attempt <= RollbackLeaseRetryCount; attempt++)
            {
                try
                {
                    using (new FileStream(
                               absolutePath,
                               FileMode.Open,
                               FileAccess.ReadWrite,
                               FileShare.None))
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    errors.Add(BuildIoDiagnostic(
                        "ROLLBACK CAPABILITY ATTEMPT " + attempt,
                        assetPath,
                        exception));
                    AssetDatabase.ReleaseCachedFileHandles();
                }
            }

            return false;
        }

        private static void LogRollbackCapabilityFailure(IReadOnlyList<string> errors)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Existing Staff V18 Data Migration]");
            output.AppendLine("ROLLBACK_CAPABILITY_PREFLIGHT_FAILED");
            output.AppendLine("Apply blocked; Asset write 0.");
            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine(errors[index]);
            }

            Debug.LogError(output.ToString());
        }

        private static bool ValidateBackupIntegrity(
            MigrationBackup backup,
            List<string> errors)
        {
            if (backup == null || backup.Targets.Count != ExistingStaffCount)
            {
                errors.Add("Backup target count is not 32.");
                return false;
            }

            bool valid = true;
            for (int index = 0; index < Baselines.Length; index++)
            {
                TargetBackup target;
                if (!backup.Targets.TryGetValue(Baselines[index].StaffId, out target)
                    || target == null)
                {
                    errors.Add("Backup target missing: " + Baselines[index].StaffId + ".");
                    valid = false;
                    continue;
                }

                bool targetValid = target.AssetBytes != null
                                   && target.MetaBytes != null
                                   && string.Equals(
                                       ComputeSha256(target.AssetBytes),
                                       target.AssetSha256,
                                       StringComparison.Ordinal)
                                   && string.Equals(
                                       ComputeSha256(target.MetaBytes),
                                       target.MetaSha256,
                                       StringComparison.Ordinal)
                                   && string.Equals(
                                       ComputeFileSha256(target.AssetPath),
                                       target.AssetSha256,
                                       StringComparison.Ordinal)
                                   && string.Equals(
                                       ComputeFileSha256(target.AssetPath + ".meta"),
                                       target.MetaSha256,
                                       StringComparison.Ordinal);
                if (!targetValid)
                {
                    errors.Add("Backup SHA validation failed: " + Baselines[index].StaffId + ".");
                    valid = false;
                }
            }

            return valid;
        }

        private static string BuildIoDiagnostic(
            string prefix,
            string path,
            Exception exception)
        {
            int win32Code = exception.HResult & 0xFFFF;
            return prefix
                   + "\nPath: " + path
                   + "\nException Type: " + exception.GetType().FullName
                   + "\nHResult: 0x" + exception.HResult.ToString("X8", CultureInfo.InvariantCulture)
                   + "\nWin32 Code: " + win32Code.ToString(CultureInfo.InvariantCulture)
                   + "\nMessage: " + exception.Message;
        }

        private static MigrationBackup CaptureBackup(MigrationInspection inspection)
        {
            MigrationBackup backup = new MigrationBackup();
            backup.InventoryFingerprint = inspection.InventoryFingerprint;
            backup.PlanFingerprint = inspection.PlanFingerprint;
            backup.SkillFilesFingerprint = ComputeSkillFilesFingerprint(inspection.Inventory);
            for (int index = 0; index < Baselines.Length; index++)
            {
                TargetBaseline baseline = Baselines[index];
                StaffDataDryRunStaffPlan staffPlan = inspection.Plan.StaffById[baseline.StaffId];
                StaffDataAssetSnapshot staffSnapshot;
                if (!inspection.Inventory.TryGetStaff(baseline.StaffId, out staffSnapshot)
                    || staffSnapshot == null)
                {
                    throw new InvalidOperationException("Backup inventory target missing: " + baseline.StaffId);
                }

                StaffData asset = AssetDatabase.LoadAssetAtPath<StaffData>(baseline.AssetPath);
                if (asset == null)
                {
                    throw new InvalidOperationException("Backup asset load failed: " + baseline.AssetPath);
                }

                byte[] assetBytes = ReadRequiredBytes(baseline.AssetPath);
                byte[] metaBytes = ReadRequiredBytes(baseline.AssetPath + ".meta");
                HashSet<string> targetPaths = BuildSerializedTargetPaths(asset, staffPlan);
                List<string> levelSnapshots = CaptureLevelElementSnapshots(asset, staffPlan.RoleKey);
                ProtectedPreservationSnapshot protectedSnapshot =
                    CaptureProtectedPreservationSnapshot(
                        asset,
                        staffSnapshot,
                        inspection.Inventory,
                        targetPaths,
                        ComputeSha256(metaBytes));
                backup.Targets.Add(
                    baseline.StaffId,
                    new TargetBackup(
                        baseline.AssetPath,
                        assetBytes,
                        metaBytes,
                        ComputeSha256(assetBytes),
                        ComputeSha256(metaBytes),
                        staffSnapshot.AssetGuid,
                        staffSnapshot.ScriptGuid,
                        CaptureSerializedSnapshot(asset, null, string.Empty),
                        protectedSnapshot,
                        CaptureSkillSnapshot(staffSnapshot),
                        CaptureVisualSnapshot(staffSnapshot),
                        levelSnapshots,
                        staffSnapshot.LevelCount,
                        targetPaths));
            }

            if (backup.Targets.Count != ExistingStaffCount)
            {
                throw new InvalidOperationException("Backup target count is not 32.");
            }

            return backup;
        }

        private static void ApplyWhitelistedChanges(StaffDataDryRunPlanSnapshot plan)
        {
            for (int planIndex = 0; planIndex < plan.StaffPlans.Count; planIndex++)
            {
                StaffDataDryRunStaffPlan staffPlan = plan.StaffPlans[planIndex];
                if (staffPlan.AssetAction != StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    continue;
                }

                RoleDefinition role = Roles[staffPlan.RoleKey];
                StaffData asset = AssetDatabase.LoadAssetAtPath<StaffData>(staffPlan.ExistingAssetPath);
                if (asset == null)
                {
                    throw new InvalidOperationException("Apply asset load failed: " + staffPlan.StaffId);
                }

                SerializedObject serialized = new SerializedObject(asset);
                serialized.Update();
                int changed = 0;
                for (int fieldIndex = 0; fieldIndex < staffPlan.FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = staffPlan.FieldPlans[fieldIndex];
                    if (field.Disposition != StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                        || !field.IsChanged)
                    {
                        continue;
                    }

                    SerializedProperty property;
                    ResolveFailure failure;
                    string resolveError;
                    if (!TryResolveTargetProperty(
                            serialized,
                            role,
                            field,
                            out property,
                            out failure,
                            out resolveError))
                    {
                        throw new InvalidOperationException(
                            staffPlan.StaffId + " | " + field.FieldPath + " | " + resolveError);
                    }

                    AssignTargetValue(property, field.TargetValue);
                    changed++;
                }

                if (changed == 0)
                {
                    continue;
                }

                if (!serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    throw new InvalidOperationException(
                        "SerializedObject apply reported no changes: " + staffPlan.StaffId);
                }

                EditorUtility.SetDirty(asset);
            }
        }

        private static bool ValidateDeterministicPostState(
            MigrationInspection applied,
            List<string> errors)
        {
            if (applied == null
                || applied.Inventory == null
                || applied.Plan == null)
            {
                errors.Add("Post-A1 Inventory/Plan inspection is missing.");
                return false;
            }

            StaffDataAssetInventorySnapshot firstInventory = null;
            StaffDataAssetInventorySnapshot secondInventory = null;
            IReadOnlyList<string> firstInventoryDiagnostics = null;
            IReadOnlyList<string> secondInventoryDiagnostics = null;
            StaffDataDryRunPlanSnapshot firstPlan = null;
            StaffDataDryRunPlanSnapshot secondPlan = null;
            IReadOnlyList<string> firstPlanDiagnostics = null;
            IReadOnlyList<string> secondPlanDiagnostics = null;
            bool valid = StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                             out firstInventory,
                             out firstInventoryDiagnostics)
                         && StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                             out secondInventory,
                             out secondInventoryDiagnostics)
                         && StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(
                             out firstPlan,
                             out firstPlanDiagnostics)
                         && StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(
                             out secondPlan,
                             out secondPlanDiagnostics)
                         && firstInventory != null
                         && secondInventory != null
                         && firstPlan != null
                         && secondPlan != null
                         && firstInventory.InventoryFingerprint
                         == secondInventory.InventoryFingerprint
                         && firstPlan.PlanFingerprint == secondPlan.PlanFingerprint
                         && firstPlan.CurrentInventoryFingerprint
                         == firstInventory.InventoryFingerprint
                         && applied.InventoryFingerprint == firstInventory.InventoryFingerprint
                         && applied.PlanFingerprint == firstPlan.PlanFingerprint;
            if (!valid)
            {
                AddDiagnostics("Post inventory 1", firstInventoryDiagnostics, errors);
                AddDiagnostics("Post inventory 2", secondInventoryDiagnostics, errors);
                AddDiagnostics("Post plan 1", firstPlanDiagnostics, errors);
                AddDiagnostics("Post plan 2", secondPlanDiagnostics, errors);
                errors.Add("Post-A1 Inventory/Plan deterministic regeneration failed.");
            }

            return valid;
        }

        private static bool ValidateOfficialSpotChecks(
            MigrationInspection applied,
            List<string> errors)
        {
            if (applied == null || applied.Plan == null)
            {
                errors.Add("Official V18 spot check plan is missing.");
                return false;
            }

            bool valid = CheckSpot(
                applied,
                "STAFF04",
                "루피",
                string.Empty,
                "6",
                "STAFF_SKILL05",
                string.Empty,
                string.Empty,
                string.Empty,
                errors);
            valid &= CheckSpot(
                applied,
                "STAFF25",
                "카논",
                string.Empty,
                string.Empty,
                "STAFF_SKILL01",
                string.Empty,
                string.Empty,
                string.Empty,
                errors);
            valid &= CheckSpot(
                applied,
                "STAFF27",
                "고든 팬지",
                string.Empty,
                "10",
                "STAFF_SKILL09",
                "50",
                "1",
                "240",
                errors);
            return valid;
        }

        private static Staff32SpotCheckResult ValidateStaff32SpotCheck(
            MigrationInspection applied)
        {
            Staff32SpotCheckResult result = new Staff32SpotCheckResult();
            StaffDataDryRunStaffPlan plan;
            StaffDataAssetSnapshot current;
            if (applied == null
                || applied.Plan == null
                || applied.Inventory == null
                || !applied.Plan.StaffById.TryGetValue("STAFF32", out plan)
                || plan == null
                || !applied.Inventory.TryGetStaff("STAFF32", out current)
                || current == null)
            {
                result.Errors.Add(
                    "STAFF32 SPOT CHECK FAIL:\nName: Required Inputs"
                    + "\nExpected: Canonical Plan, protected backup, and current inventory"
                    + "\nActual: Missing");
                result.Passed = false;
                return result;
            }

            AppendSpotResult(
                result,
                "Name",
                plan.TargetName,
                current.Name,
                string.Equals(current.Name, plan.TargetName, StringComparison.Ordinal));
            AppendSpotResult(
                result,
                "Description",
                plan.TargetDescription,
                current.Description,
                string.Equals(current.Description, plan.TargetDescription, StringComparison.Ordinal));
            AppendSpotResult(
                result,
                "Rank",
                plan.TargetRankName,
                current.RankName,
                string.Equals(current.RankName, plan.TargetRankName, StringComparison.Ordinal));
            AppendSpotResult(
                result,
                "Base Speed",
                plan.TargetSpeed,
                current.Speed.ToString("R", CultureInfo.InvariantCulture),
                NumbersEqual(
                    current.Speed.ToString("R", CultureInfo.InvariantCulture),
                    plan.TargetSpeed));
            AppendSpotResult(
                result,
                "Concrete Class",
                plan.TargetConcreteTypeName,
                current.ConcreteTypeName,
                string.Equals(
                    current.ConcreteTypeName,
                    plan.TargetConcreteTypeName,
                    StringComparison.Ordinal));
            AppendSpotResult(
                result,
                "Skill Reference Path",
                plan.SkillPlan.CurrentAssetPath,
                current.SkillReference == null ? string.Empty : current.SkillReference.AssetPath,
                current.SkillReference != null
                && string.Equals(
                    current.SkillReference.AssetPath,
                    plan.SkillPlan.CurrentAssetPath,
                    StringComparison.Ordinal));
            AppendSpotResult(
                result,
                "Skill Reference GUID",
                plan.SkillPlan.CurrentAssetGuid,
                current.SkillReference == null ? string.Empty : current.SkillReference.AssetGuid,
                current.SkillReference != null
                && string.Equals(
                    current.SkillReference.AssetGuid,
                    plan.SkillPlan.CurrentAssetGuid,
                    StringComparison.Ordinal));
            AppendSpotResult(
                result,
                "Skill Class",
                plan.SkillPlan.RequiredClassName,
                current.SkillConcreteTypeName,
                string.Equals(
                    current.SkillConcreteTypeName,
                    plan.SkillPlan.RequiredClassName,
                    StringComparison.Ordinal));

            StaffSkillEffectConfigurationSnapshot effect = null;
            string effectError = "Skill reference is missing.";
            bool effectRead = current.SkillReference != null
                              && StaffSkillEffectConfigurationReader.TryReadFloat(
                                  current.SkillReference.AssetPath,
                                  Staff32OfficialSkillEffectField,
                                  out effect,
                                  out effectError);
            string actualEffectField = effectRead && effect != null
                ? effect.FieldPath
                : effectError;
            string actualEffectValue = effectRead && effect != null
                ? effect.NormalizedValue
                : effectError;
            AppendSpotResult(
                result,
                "Skill Effect Field",
                Staff32OfficialSkillEffectField,
                actualEffectField,
                    effectRead && effect != null
                    && string.Equals(
                    effect.FieldPath,
                    Staff32OfficialSkillEffectField,
                    StringComparison.Ordinal));
            AppendSpotResult(
                result,
                "Skill Effect Value",
                Staff32OfficialSkillEffectValue,
                actualEffectValue,
                effectRead && effect != null
                && NumbersEqual(effect.NormalizedValue, Staff32OfficialSkillEffectValue));
            AppendSpotResult(
                result,
                "Skill Duration",
                plan.SkillPlan.TargetDuration,
                current.SkillDuration.ToString("R", CultureInfo.InvariantCulture),
                NumbersEqual(
                    current.SkillDuration.ToString("R", CultureInfo.InvariantCulture),
                    plan.SkillPlan.TargetDuration));
            AppendSpotResult(
                result,
                "Skill Cooldown",
                plan.SkillPlan.TargetCooldown,
                current.SkillCooldown.ToString("R", CultureInfo.InvariantCulture),
                NumbersEqual(
                    current.SkillCooldown.ToString("R", CultureInfo.InvariantCulture),
                    plan.SkillPlan.TargetCooldown));
            AppendSpotResult(
                result,
                "Cleaner Level Array Count",
                "5",
                current.LevelCount.ToString(CultureInfo.InvariantCulture),
                current.LevelCount == 5 && current.RoleKey == "CLEANER");
            AppendSpotResult(
                result,
                "Non-STAFF02 Five-Slot Structure",
                "5",
                current.LevelCount.ToString(CultureInfo.InvariantCulture),
                current.Id != "STAFF02" && current.LevelCount == 5);

            result.Passed = result.Errors.Count == 0;
            return result;
        }

        private static bool AppendSpotResult(
            Staff32SpotCheckResult result,
            string label,
            string expected,
            string actual,
            bool passed)
        {
            result.Details.Add(
                "- " + label + ": " + (passed ? "PASS" : "FAIL")
                + " | Expected: " + FormatLogValue(expected)
                + " | Actual: " + FormatLogValue(actual));
            if (!passed)
            {
                result.Errors.Add(
                    "STAFF32 SPOT CHECK FAIL:\nName: " + label
                    + "\nExpected: " + FormatLogValue(expected)
                    + "\nActual: " + FormatLogValue(actual));
            }

            return passed;
        }

        private static string BuildStaff32SpotCheckReport(Staff32SpotCheckResult result)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("[STAFF32 Official V18 Spot Check]");
            report.AppendLine("STAFF32 Spot Check:");
            report.AppendLine(result.Passed ? "PASS" : "FAIL");
            report.AppendLine();
            report.AppendLine("Details:");
            for (int index = 0; index < result.Details.Count; index++)
            {
                report.AppendLine(result.Details[index]);
            }

            report.AppendLine();
            report.AppendLine("Errors: " + result.Errors.Count);
            return report.ToString().TrimEnd();
        }

        private static bool CheckSpot(
            MigrationInspection applied,
            string staffId,
            string expectedName,
            string expectedRank,
            string expectedSpeed,
            string expectedSkillId,
            string expectedEffect,
            string expectedDuration,
            string expectedCooldown,
            List<string> errors)
        {
            StaffDataDryRunStaffPlan staff;
            if (!applied.Plan.StaffById.TryGetValue(staffId, out staff) || staff == null)
            {
                errors.Add("Spot check plan missing: " + staffId);
                return false;
            }

            bool valid = staff.CurrentName == expectedName
                         && (string.IsNullOrEmpty(expectedRank)
                             || staff.CurrentRankName == expectedRank)
                         && (string.IsNullOrEmpty(expectedSpeed)
                             || NumbersEqual(staff.CurrentSpeed, expectedSpeed))
                         && staff.SkillPlan.OfficialSkillId == expectedSkillId
                         && !string.IsNullOrEmpty(staff.SkillPlan.CurrentAssetGuid)
                         && NumbersEqual(
                             staff.SkillPlan.CurrentDuration,
                             string.IsNullOrEmpty(expectedDuration)
                                 ? staff.SkillPlan.TargetDuration
                                 : expectedDuration)
                         && NumbersEqual(
                             staff.SkillPlan.CurrentCooldown,
                             string.IsNullOrEmpty(expectedCooldown)
                                 ? staff.SkillPlan.TargetCooldown
                                 : expectedCooldown);
            if (!string.IsNullOrEmpty(expectedEffect))
            {
                valid &= staff.SkillPlan.EffectPlan != null
                         && NumbersEqual(
                             staff.SkillPlan.EffectPlan.CurrentValue,
                             expectedEffect);
            }

            if (!valid)
            {
                errors.Add("Official V18 spot check failed: " + staffId);
            }

            return valid;
        }

        private static bool RollbackMigration(
            MigrationBackup backup,
            List<string> errors)
        {
            if (backup == null || backup.Targets.Count != ExistingStaffCount)
            {
                errors.Add("Rollback backup is missing or incomplete.");
                return false;
            }

            if (!ValidateBackupIntegrity(backup, errors))
            {
                return false;
            }

            List<RawRestoreFile> restoreFiles = BuildRawRestoreFiles(backup);
            bool rawWritesStarted;
            if (!ExecuteRawRestoreTransaction(
                    restoreFiles,
                    AssetDatabase.ReleaseCachedFileHandles,
                    AssetDatabase.StartAssetEditing,
                    AssetDatabase.StopAssetEditing,
                    OpenExclusiveRollbackStream,
                    RollbackLeaseRetryCount,
                    errors,
                    out rawWritesStarted))
            {
                if (!rawWritesStarted)
                {
                    errors.Add("Rollback aborted before first byte write; partial restore 0.");
                }

                return false;
            }

            if (!ValidateRestoredFileHashes(restoreFiles, errors))
            {
                return false;
            }

            try
            {
                ForceSynchronousReloadAfterRawRestore();
            }
            catch (Exception exception)
            {
                errors.Add("Rollback raw-byte restore exception: " + exception.Message);
                return false;
            }

            bool valid = true;
            StaffDataAssetInventorySnapshot inventory;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out inventory,
                    out diagnostics)
                || inventory == null)
            {
                AddDiagnostics("Rollback inventory", diagnostics, errors);
                return false;
            }

            for (int index = 0; index < Baselines.Length; index++)
            {
                TargetBaseline baseline = Baselines[index];
                TargetBackup target = backup.Targets[baseline.StaffId];
                StaffDataAssetSnapshot current;
                StaffData asset = AssetDatabase.LoadAssetAtPath<StaffData>(baseline.AssetPath);
                List<string> levelSnapshots = asset == null
                    ? new List<string>()
                    : CaptureLevelElementSnapshots(
                        asset,
                        inventory.StaffById[baseline.StaffId].RoleKey);
                bool restored = inventory.TryGetStaff(baseline.StaffId, out current)
                                && current != null
                                && asset != null
                                && ComputeFileSha256(target.AssetPath) == target.AssetSha256
                                && ComputeFileSha256(target.AssetPath + ".meta") == target.MetaSha256
                                && current.AssetGuid == target.AssetGuid
                                && current.ScriptGuid == target.ScriptGuid
                                && CaptureSerializedSnapshot(asset, null, string.Empty)
                                == target.FullSerializedSnapshot
                                && CaptureSkillSnapshot(current) == target.SkillSnapshot
                                && CaptureVisualSnapshot(current) == target.VisualSnapshot
                                && current.LevelCount == target.LevelCount
                                && SequenceEqual(levelSnapshots, target.LevelElementSnapshots);
                if (!restored)
                {
                    errors.Add("Rollback verification failed: " + baseline.StaffId);
                    valid = false;
                }
            }

            MigrationInspection restoredInspection;
            List<string> inspectionErrors = new List<string>();
            bool stateRestored = TryInspect(out restoredInspection, inspectionErrors)
                                 && restoredInspection != null
                                 && restoredInspection.State == MigrationState.READY_TO_APPLY
                                 && restoredInspection.InventoryFingerprint
                                 == backup.InventoryFingerprint
                                 && restoredInspection.PlanFingerprint == backup.PlanFingerprint;
            if (!stateRestored)
            {
                errors.AddRange(inspectionErrors);
                errors.Add("Rollback did not restore READY_TO_APPLY fingerprints.");
                valid = false;
            }

            return valid;
        }

        private static List<RawRestoreFile> BuildRawRestoreFiles(MigrationBackup backup)
        {
            List<RawRestoreFile> files = new List<RawRestoreFile>();
            for (int index = 0; index < Baselines.Length; index++)
            {
                TargetBackup target = backup.Targets[Baselines[index].StaffId];
                files.Add(new RawRestoreFile(
                    target.AssetPath,
                    target.AssetBytes,
                    target.AssetSha256));
                files.Add(new RawRestoreFile(
                    target.AssetPath + ".meta",
                    target.MetaBytes,
                    target.MetaSha256));
            }

            return files;
        }

        private static bool ExecuteRawRestoreTransaction(
            IReadOnlyList<RawRestoreFile> files,
            Action releaseCachedFileHandles,
            Action startAssetEditing,
            Action stopAssetEditing,
            Func<RawRestoreFile, int, FileStream> streamFactory,
            int maxAttempts,
            List<string> errors,
            out bool writesStarted)
        {
            writesStarted = false;
            if (files == null || files.Count == 0 || maxAttempts <= 0)
            {
                errors.Add("Rollback restore file set is empty or retry count is invalid.");
                return false;
            }

            for (int index = 0; index < files.Count; index++)
            {
                RawRestoreFile file = files[index];
                string absolutePath = Path.IsPathRooted(file.Path)
                    ? file.Path
                    : GetAbsolutePath(file.Path);
                if (!File.Exists(absolutePath))
                {
                    errors.Add("ROLLBACK TARGET FILE MISSING:\nPath: " + file.Path);
                    return false;
                }

                if (file.Bytes == null
                    || !string.Equals(
                        ComputeSha256(file.Bytes),
                        file.ExpectedSha256,
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        "ROLLBACK BACKUP SHA MISMATCH:\nPath: " + file.Path
                        + "\nExpected: " + file.ExpectedSha256
                        + "\nActual: "
                        + (file.Bytes == null ? "<null>" : ComputeSha256(file.Bytes)));
                    return false;
                }
            }

            List<RollbackWriteLease> leases = new List<RollbackWriteLease>();
            bool editingStarted = false;
            try
            {
                releaseCachedFileHandles();
                startAssetEditing();
                editingStarted = true;
                bool allLeasesAcquired = false;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    DisposeRollbackLeases(leases);
                    leases.Clear();
                    if (attempt > 1)
                    {
                        releaseCachedFileHandles();
                    }

                    try
                    {
                        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
                        {
                            RawRestoreFile file = files[fileIndex];
                            leases.Add(new RollbackWriteLease(
                                file,
                                streamFactory(file, attempt)));
                        }

                        allLeasesAcquired = true;
                        break;
                    }
                    catch (Exception exception)
                    {
                        string path = leases.Count < files.Count
                            ? files[leases.Count].Path
                            : "<unknown>";
                        errors.Add(BuildIoDiagnostic(
                            "ROLLBACK WRITE LEASE ATTEMPT " + attempt,
                            path,
                            exception));
                    }
                }

                if (!allLeasesAcquired)
                {
                    errors.Add("ROLLBACK_WRITE_LEASE_ACQUISITION_FAILED");
                    return false;
                }

                writesStarted = true;
                for (int index = 0; index < leases.Count; index++)
                {
                    RollbackWriteLease lease = leases[index];
                    lease.Stream.Position = 0;
                    lease.Stream.SetLength(0);
                    lease.Stream.Write(lease.File.Bytes, 0, lease.File.Bytes.Length);
                    lease.Stream.Flush(true);
                }

                return true;
            }
            catch (Exception exception)
            {
                errors.Add(BuildIoDiagnostic(
                    "ROLLBACK RAW RESTORE FAILED",
                    leases.Count == 0 ? "<before-lease>" : leases[leases.Count - 1].File.Path,
                    exception));
                return false;
            }
            finally
            {
                DisposeRollbackLeases(leases);
                if (editingStarted)
                {
                    stopAssetEditing();
                }
            }
        }

        private static FileStream OpenExclusiveRollbackStream(
            RawRestoreFile file,
            int attempt)
        {
            return new FileStream(
                GetAbsolutePath(file.Path),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
        }

        private static bool ValidateRestoredFileHashes(
            IReadOnlyList<RawRestoreFile> files,
            List<string> errors)
        {
            bool valid = true;
            for (int index = 0; index < files.Count; index++)
            {
                RawRestoreFile file = files[index];
                string actual = ComputeFileSha256Absolute(file.Path);
                if (string.Equals(actual, file.ExpectedSha256, StringComparison.Ordinal))
                {
                    continue;
                }

                errors.Add(
                    "ROLLBACK RESTORED SHA MISMATCH:\nPath: " + file.Path
                    + "\nExpected: " + file.ExpectedSha256
                    + "\nActual: " + actual);
                valid = false;
            }

            return valid;
        }

        private static string ComputeFileSha256Absolute(string path)
        {
            string absolute = Path.IsPathRooted(path) ? path : GetAbsolutePath(path);
            return File.Exists(absolute)
                ? ComputeSha256(File.ReadAllBytes(absolute))
                : string.Empty;
        }

        private static void DisposeRollbackLeases(List<RollbackWriteLease> leases)
        {
            for (int index = leases.Count - 1; index >= 0; index--)
            {
                if (leases[index].Stream != null)
                {
                    leases[index].Stream.Dispose();
                }
            }
        }

        private static bool TryResolveTargetProperty(
            SerializedObject serialized,
            RoleDefinition role,
            StaffDataDryRunFieldPlan field,
            out SerializedProperty property,
            out ResolveFailure failure,
            out string error)
        {
            property = null;
            failure = ResolveFailure.NONE;
            error = string.Empty;
            string directPath = null;
            switch (field.FieldPath)
            {
                case "StaffData._name":
                    directPath = "_name";
                    break;
                case "StaffData._description":
                    directPath = "_description";
                    break;
                case "StaffData._rank":
                    directPath = "_rank";
                    break;
                case "StaffData._speed":
                    directPath = "_speed";
                    break;
            }

            if (directPath != null)
            {
                property = serialized.FindProperty(directPath);
                if (property == null)
                {
                    failure = ResolveFailure.MISSING_PROPERTY;
                    error = "Required SerializedProperty is missing: " + directPath;
                    return false;
                }

                return true;
            }

            int levelIndex;
            string suffix;
            if (!TryParseLevelFieldPath(field.FieldPath, out levelIndex, out suffix)
                || levelIndex < 0
                || levelIndex >= 5)
            {
                failure = ResolveFailure.UNSUPPORTED_FIELD;
                error = "Unsupported FieldPath: " + field.FieldPath;
                return false;
            }

            SerializedProperty levels = serialized.FindProperty(role.LevelArrayPath);
            if (levels == null || !levels.isArray || levels.arraySize <= levelIndex)
            {
                failure = ResolveFailure.MISSING_PROPERTY;
                error = "Required role array/index is missing: " + role.LevelArrayPath
                        + "[" + levelIndex + "]";
                return false;
            }

            SerializedProperty element = levels.GetArrayElementAtIndex(levelIndex);
            if (suffix == "_upgradeMinScore")
            {
                property = element.FindPropertyRelative("_upgradeMinScore");
            }
            else if (suffix == "_moneyType" || suffix == "_price")
            {
                SerializedProperty money = element.FindPropertyRelative("_upgradeMoneyData");
                property = money == null ? null : money.FindPropertyRelative(suffix);
            }
            else if (role.AllowedRoleFields.Contains(suffix))
            {
                property = element.FindPropertyRelative(suffix);
            }
            else
            {
                failure = ResolveFailure.UNSUPPORTED_FIELD;
                error = "FieldPath is not whitelisted for " + role.ConcreteTypeName + ": "
                        + field.FieldPath;
                return false;
            }

            if (property == null)
            {
                failure = ResolveFailure.MISSING_PROPERTY;
                error = "Required SerializedProperty is missing: " + field.FieldPath;
                return false;
            }

            return true;
        }

        private static bool CanAssignTargetValue(
            SerializedProperty property,
            string targetValue,
            out string error)
        {
            error = string.Empty;
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    return true;
                case SerializedPropertyType.Float:
                    double floatValue;
                    if (double.TryParse(
                            targetValue,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out floatValue)
                        && !double.IsNaN(floatValue)
                        && !double.IsInfinity(floatValue)
                        && floatValue >= -float.MaxValue
                        && floatValue <= float.MaxValue)
                    {
                        return true;
                    }

                    error = "Target is not a finite float: " + targetValue;
                    return false;
                case SerializedPropertyType.Integer:
                    int integerValue;
                    if (int.TryParse(
                            targetValue,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out integerValue))
                    {
                        return true;
                    }

                    error = "Target is not Int32: " + targetValue;
                    return false;
                case SerializedPropertyType.Enum:
                    if (Array.IndexOf(property.enumNames, targetValue) >= 0)
                    {
                        return true;
                    }

                    error = "Target enum name is not defined: " + targetValue;
                    return false;
                default:
                    error = "SerializedProperty type is not whitelisted: " + property.propertyType;
                    return false;
            }
        }

        private static void AssignTargetValue(SerializedProperty property, string targetValue)
        {
            string error;
            if (!CanAssignTargetValue(property, targetValue, out error))
            {
                throw new InvalidOperationException(error);
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = targetValue;
                    return;
                case SerializedPropertyType.Float:
                    property.doubleValue = double.Parse(
                        targetValue,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture);
                    return;
                case SerializedPropertyType.Integer:
                    property.intValue = int.Parse(
                        targetValue,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture);
                    return;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = Array.IndexOf(property.enumNames, targetValue);
                    return;
            }
        }

        private static HashSet<string> BuildSerializedTargetPaths(
            StaffData asset,
            StaffDataDryRunStaffPlan staffPlan)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            RoleDefinition role = Roles[staffPlan.RoleKey];
            for (int index = 0; index < staffPlan.FieldPlans.Count; index++)
            {
                StaffDataDryRunFieldPlan field = staffPlan.FieldPlans[index];
                if (field.Disposition != StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING)
                {
                    continue;
                }

                SerializedProperty property;
                ResolveFailure failure;
                string error;
                if (!TryResolveTargetProperty(
                        serialized,
                        role,
                        field,
                        out property,
                        out failure,
                        out error))
                {
                    throw new InvalidOperationException(
                        staffPlan.StaffId + " target property path capture failed: " + error);
                }

                paths.Add(property.propertyPath);
            }

            return paths;
        }

        private static ProtectedPreservationSnapshot CaptureProtectedPreservationSnapshot(
            StaffData asset,
            StaffDataAssetSnapshot staff,
            StaffDataAssetInventorySnapshot inventory,
            HashSet<string> mutableSerializedPaths,
            string metaSha256)
        {
            SortedDictionary<string, string> values =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            AddProtectedValue(values, "StaffData.AssetPath", staff.AssetPath);
            AddProtectedValue(values, "StaffData.AssetGuid", staff.AssetGuid);
            AddProtectedValue(values, "StaffData.MetaSha256", metaSha256);
            AddProtectedValue(values, "StaffData.ScriptAssetPath", staff.ScriptAssetPath);
            AddProtectedValue(values, "StaffData.ScriptGuid", staff.ScriptGuid);
            AddProtectedValue(values, "StaffData.ConcreteClass", staff.ConcreteTypeName);
            AddProtectedValue(values, "StaffData.UnityObjectName", staff.UnityObjectName);
            AddProtectedValue(values, "StaffData.Id", staff.Id);
            AddProtectedValue(values, "StaffData.RoleKey", staff.RoleKey);
            AddProtectedValue(values, "StaffData.LevelArrayPath", staff.LevelArrayPropertyPath);
            AddProtectedValue(
                values,
                "StaffData.RoleArrayCount",
                staff.LevelCount.ToString(CultureInfo.InvariantCulture));
            AddProtectedValue(values, "StaffData.SkillReference", ReferenceKey(staff.SkillReference));
            AddProtectedValue(values, "StaffData.Visual", CaptureVisualSnapshot(staff));
            AppendSerializedProjection(
                values,
                "StaffData.Serialized.",
                asset,
                mutableSerializedPaths,
                string.Empty);

            if (staff.Id == "STAFF02" && staff.LevelCount > 5)
            {
                RoleDefinition role = Roles[staff.RoleKey];
                SerializedObject serialized = new SerializedObject(asset);
                serialized.Update();
                SerializedProperty levels = serialized.FindProperty(role.LevelArrayPath);
                if (levels != null && levels.isArray && levels.arraySize > 5)
                {
                    string prefix = levels.GetArrayElementAtIndex(5).propertyPath;
                    AppendSerializedProjection(
                        values,
                        "StaffData.STAFF02Slot6.",
                        asset,
                        null,
                        prefix);
                }
            }

            StaffSkillAssetSnapshot skill;
            string skillGuid = staff.SkillReference == null
                ? string.Empty
                : staff.SkillReference.AssetGuid;
            if (inventory.TryGetSkill(skillGuid, out skill) && skill != null)
            {
                AddProtectedValue(values, "Skill.AssetPath", skill.AssetPath);
                AddProtectedValue(values, "Skill.AssetGuid", skill.AssetGuid);
                AddProtectedValue(values, "Skill.ScriptAssetPath", skill.ScriptAssetPath);
                AddProtectedValue(values, "Skill.ScriptGuid", skill.ScriptGuid);
                AddProtectedValue(values, "Skill.ConcreteClass", skill.ConcreteTypeName);
                AddProtectedValue(values, "Skill.UnityObjectName", skill.UnityObjectName);
                AddProtectedValue(values, "Skill.Description", skill.Description);
                AddProtectedValue(
                    values,
                    "Skill.Duration",
                    skill.Duration.ToString("R", CultureInfo.InvariantCulture));
                AddProtectedValue(
                    values,
                    "Skill.Cooldown",
                    skill.Cooldown.ToString("R", CultureInfo.InvariantCulture));
                UnityEngine.Object skillAsset = AssetDatabase.LoadMainAssetAtPath(skill.AssetPath);
                if (skillAsset != null)
                {
                    AppendSerializedProjection(
                        values,
                        "Skill.Serialized.",
                        skillAsset,
                        null,
                        string.Empty);
                }
                else
                {
                    AddProtectedValue(values, "Skill.Serialized", "<missing>");
                }
            }
            else
            {
                AddProtectedValue(values, "Skill.AssetGuid", skillGuid);
                AddProtectedValue(values, "Skill.Inventory", "<missing>");
            }

            return new ProtectedPreservationSnapshot(values);
        }

        private static void AppendSerializedProjection(
            SortedDictionary<string, string> values,
            string keyPrefix,
            UnityEngine.Object asset,
            HashSet<string> skippedPaths,
            string requiredPrefix)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                string mutableRoot;
                bool isMutableSubtree = TryGetMutableSerializedRoot(
                    property.propertyPath,
                    skippedPaths,
                    out mutableRoot);
                bool isStringScalar = property.propertyType == SerializedPropertyType.String;
                enterChildren = !isMutableSubtree && !isStringScalar;
                if (isMutableSubtree)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(requiredPrefix)
                    && property.propertyPath != requiredPrefix
                    && !property.propertyPath.StartsWith(
                        requiredPrefix + ".",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                AddProtectedValue(
                    values,
                    keyPrefix + property.propertyPath,
                    GetSerializedPropertySnapshotValue(property));
            }
        }

        private static bool TryGetMutableSerializedRoot(
            string candidatePath,
            HashSet<string> mutableRoots,
            out string mutableRoot)
        {
            mutableRoot = string.Empty;
            if (string.IsNullOrEmpty(candidatePath) || mutableRoots == null)
            {
                return false;
            }

            foreach (string root in mutableRoots)
            {
                if (string.Equals(candidatePath, root, StringComparison.Ordinal)
                    || candidatePath.StartsWith(root + ".", StringComparison.Ordinal))
                {
                    mutableRoot = root;
                    return true;
                }
            }

            return false;
        }

        private static void AddProtectedValue(
            SortedDictionary<string, string> values,
            string path,
            string value)
        {
            values[path] = value ?? string.Empty;
        }

        private static string CaptureSerializedSnapshot(
            UnityEngine.Object asset,
            HashSet<string> skippedPaths,
            string requiredPrefix)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty property = serialized.GetIterator();
            StringBuilder output = new StringBuilder();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                bool isStringScalar = property.propertyType == SerializedPropertyType.String;
                enterChildren = !isStringScalar;
                if (skippedPaths != null && skippedPaths.Contains(property.propertyPath))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(requiredPrefix)
                    && property.propertyPath != requiredPrefix
                    && !property.propertyPath.StartsWith(
                        requiredPrefix + ".",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                output.Append(property.propertyPath);
                output.Append('|');
                output.Append(property.propertyType);
                output.Append('|');
                AppendSerializedValue(output, property);
                output.Append('\n');
            }

            return output.ToString();
        }

        private static List<string> CaptureLevelElementSnapshots(
            StaffData asset,
            string roleKey)
        {
            RoleDefinition role = Roles[roleKey];
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty levels = serialized.FindProperty(role.LevelArrayPath);
            List<string> result = new List<string>();
            if (levels == null || !levels.isArray)
            {
                return result;
            }

            for (int index = 0; index < levels.arraySize; index++)
            {
                string prefix = levels.GetArrayElementAtIndex(index).propertyPath;
                result.Add(CaptureSerializedSnapshot(asset, null, prefix));
            }

            return result;
        }

        private static void AppendSerializedValue(
            StringBuilder input,
            SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask:
                    input.Append(property.longValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.Boolean:
                    input.Append(property.boolValue ? "1" : "0");
                    break;
                case SerializedPropertyType.Float:
                    input.Append(property.doubleValue.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.String:
                    input.Append(property.stringValue);
                    break;
                case SerializedPropertyType.Enum:
                    input.Append(property.enumValueIndex.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.ObjectReference:
                    string guid;
                    long localId;
                    if (property.objectReferenceValue != null
                        && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            property.objectReferenceValue,
                            out guid,
                            out localId))
                    {
                        input.Append(guid);
                        input.Append(':');
                        input.Append(localId.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        input.Append("null");
                    }

                    break;
                default:
                    input.Append(property.type);
                    if (property.isArray)
                    {
                        input.Append(':');
                        input.Append(property.arraySize.ToString(CultureInfo.InvariantCulture));
                    }

                    break;
            }
        }

        private static string GetSerializedPropertySnapshotValue(SerializedProperty property)
        {
            StringBuilder value = new StringBuilder();
            AppendSerializedValue(value, property);
            return value.ToString();
        }

        private static string GetSerializedPropertyComparisonValue(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                int index = property.enumValueIndex;
                return index >= 0 && index < property.enumNames.Length
                    ? property.enumNames[index]
                    : property.intValue.ToString(CultureInfo.InvariantCulture);
            }

            return GetSerializedPropertySnapshotValue(property);
        }

        private static bool SerializedPropertyMatchesTarget(
            SerializedProperty property,
            string targetValue)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    return string.Equals(
                        property.stringValue,
                        targetValue,
                        StringComparison.Ordinal);
                case SerializedPropertyType.Float:
                    return NumbersEqual(
                        property.doubleValue.ToString("R", CultureInfo.InvariantCulture),
                        targetValue);
                case SerializedPropertyType.Integer:
                    int integerTarget;
                    return int.TryParse(
                               targetValue,
                               NumberStyles.Integer,
                               CultureInfo.InvariantCulture,
                               out integerTarget)
                           && property.intValue == integerTarget;
                case SerializedPropertyType.Enum:
                    int enumIndex = Array.IndexOf(property.enumNames, targetValue);
                    return enumIndex >= 0 && property.enumValueIndex == enumIndex;
                default:
                    return false;
            }
        }

        private static string FormatLogValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string CaptureSkillSnapshot(StaffDataAssetSnapshot staff)
        {
            StaffAssetReferenceSnapshot reference = staff.SkillReference;
            return ReferenceKey(reference) + "|" + staff.SkillConcreteTypeName + "|"
                   + staff.SkillDuration.ToString("R", CultureInfo.InvariantCulture) + "|"
                   + staff.SkillCooldown.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string CaptureVisualSnapshot(StaffDataAssetSnapshot staff)
        {
            StringBuilder output = new StringBuilder();
            AppendReference(output, staff.SpriteReference);
            AppendReference(output, staff.ThumbnailReference);
            AppendReference(output, staff.AnimatorControllerReference);
            for (int index = 0; index < staff.IdleSpriteReferences.Count; index++)
            {
                AppendReference(output, staff.IdleSpriteReferences[index]);
            }

            AppendReference(output, staff.BackSpriteReference);
            AppendReference(output, staff.HandSpriteReference);
            output.Append(staff.HandOffsetX.ToString("R", CultureInfo.InvariantCulture));
            output.Append('|');
            output.Append(staff.HandOffsetY.ToString("R", CultureInfo.InvariantCulture));
            output.Append('|');
            AppendReference(output, staff.UiSpriteReference);
            AppendReference(output, staff.AnimationSpriteReference);
            output.Append(staff.ParticleCount.ToString(CultureInfo.InvariantCulture));
            output.Append('|');
            for (int index = 0; index < staff.ParticleSpriteReferences.Count; index++)
            {
                AppendReference(output, staff.ParticleSpriteReferences[index]);
            }

            return output.ToString();
        }

        private static void AppendReference(
            StringBuilder output,
            StaffAssetReferenceSnapshot reference)
        {
            output.Append(ReferenceKey(reference));
            output.Append('|');
        }

        private static string ReferenceKey(StaffAssetReferenceSnapshot reference)
        {
            if (reference == null)
            {
                return "<null>";
            }

            return reference.FieldPath + ":" + (reference.IsAssigned ? "1" : "0") + ":"
                   + (reference.IsMissing ? "1" : "0") + ":" + reference.AssetPath + ":"
                   + reference.AssetGuid + ":" + reference.ObjectName + ":"
                   + reference.ObjectTypeName;
        }

        private static string ComputeSkillFilesFingerprint(
            StaffDataAssetInventorySnapshot inventory)
        {
            List<StaffSkillAssetSnapshot> skills = new List<StaffSkillAssetSnapshot>(
                inventory.Skills);
            skills.Sort((left, right) => string.Compare(
                left.AssetPath,
                right.AssetPath,
                StringComparison.Ordinal));
            StringBuilder input = new StringBuilder();
            for (int index = 0; index < skills.Count; index++)
            {
                string path = skills[index].AssetPath;
                input.Append(path);
                input.Append('|');
                input.Append(ComputeFileSha256(path));
                input.Append('|');
                input.Append(ComputeFileSha256(path + ".meta"));
                input.Append('\n');
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(input.ToString()));
        }

        private static void SaveAndForceSynchronousImport()
        {
            ImportAssetOptions options = ImportAssetOptions.ForceUpdate
                                         | ImportAssetOptions.ForceSynchronousImport;
            AssetDatabase.SaveAssets();
            for (int index = 0; index < Baselines.Length; index++)
            {
                AssetDatabase.ImportAsset(Baselines[index].AssetPath, options);
            }

            AssetDatabase.Refresh(options);
            ReloadAllTargets();
        }

        private static void ForceSynchronousReloadAfterRawRestore()
        {
            ImportAssetOptions options = ImportAssetOptions.ForceUpdate
                                         | ImportAssetOptions.ForceSynchronousImport;
            AssetDatabase.Refresh(options);
            for (int index = 0; index < Baselines.Length; index++)
            {
                AssetDatabase.ImportAsset(Baselines[index].AssetPath, options);
            }

            AssetDatabase.Refresh(options);
            ReloadAllTargets();
        }

        private static void ReloadAllTargets()
        {
            for (int index = 0; index < Baselines.Length; index++)
            {
                if (AssetDatabase.LoadAssetAtPath<StaffData>(Baselines[index].AssetPath) == null)
                {
                    throw new InvalidOperationException(
                        "Synchronous reload failed: " + Baselines[index].StaffId);
                }
            }
        }

        private static byte[] ReadRequiredBytes(string assetPath)
        {
            string absolutePath = GetAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                throw new InvalidOperationException("Required file is missing: " + assetPath);
            }

            return File.ReadAllBytes(absolutePath);
        }

        private static string ComputeFileSha256(string assetPath)
        {
            string absolutePath = GetAbsolutePath(assetPath);
            return File.Exists(absolutePath)
                ? ComputeSha256(File.ReadAllBytes(absolutePath))
                : string.Empty;
        }

        private static bool RunPureProjectionSelfTests(out string error)
        {
            error = string.Empty;
            HashSet<string> mutablePaths = new HashSet<string>(StringComparer.Ordinal)
            {
                "_name",
                "_description",
                "_rank",
                "_speed",
                "_cleanerLevelData.Array.data[0]._addSpeed"
            };
            SortedDictionary<string, string> before =
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    { "_name", "Before" },
                    { "_name.Array", "legacy-string-array-view" },
                    { "_name.Array.size", "6" },
                    { "_name.Array.data[0]", "66" },
                    { "_description", string.Empty },
                    { "_description.Array", "legacy-string-array-view" },
                    { "_description.Array.size", "0" },
                    { "_descriptionBackup", "protected-boundary" },
                    { "_description2", "protected-boundary-2" },
                    { "_other._description", "protected-nested-boundary" },
                    { "_id", "STAFF01" },
                    { "_rank", "Normal1" },
                    { "_speed", "12.5" },
                    { "_skill", "skill-guid:11400000" },
                    { "_sprite", "sprite-guid:21300000" },
                    { "_moneyType", "Gold" },
                    { "_cleanerLevelData.Array.data[0]._addSpeed", "0" },
                    { "_cleanerLevelData.Array.data[0]._addSpeed.Raw", "mutable-child" },
                    { "_cleanerLevelData.Array.data[5]._addSpeed", "2.5" },
                    { "StaffData.MetaSha256", "meta-sha" }
                };
            SortedDictionary<string, string> allowedChanges =
                new SortedDictionary<string, string>(before, StringComparer.Ordinal)
                {
                    ["_name"] = "After",
                    ["_name.Array.size"] = "5",
                    ["_name.Array.data[0]"] = "65",
                    ["_description"] = "꼼꼼하고 책임감 있는 웨이터 덕덕.",
                    ["_description.Array.size"] = "44",
                    ["_description.Array.data[0]"] = "-22",
                    ["_rank"] = "Unique",
                    ["_speed"] = "8.5",
                    ["_cleanerLevelData.Array.data[0]._addSpeed"] = "0.5",
                    ["_cleanerLevelData.Array.data[0]._addSpeed.Raw"] = "changed-child"
                };
            ProtectedPreservationSnapshot expected = ProjectPureProtectedValues(
                before,
                mutablePaths);
            ProtectedPreservationSnapshot allowed = ProjectPureProtectedValues(
                allowedChanges,
                mutablePaths);
            if (!ProtectedValuesEqual(expected, allowed))
            {
                error = "Allowed A1 fields changed the protected projection.";
                return false;
            }

            string leakedRoot;
            string leakedPath;
            if (TryFindMutableSubtreeLeak(
                    allowed,
                    mutablePaths,
                    out leakedRoot,
                    out leakedPath))
            {
                error = "MUTABLE_SUBTREE_LEAK:\nRoot=" + leakedRoot
                        + "\nLeakedPath=" + leakedPath;
                return false;
            }

            if (CountPathFamily(allowed, "_description") != 0
                || CountPathFamily(allowed, "_name") != 0)
            {
                error = "Mutable String subtree count is not zero.";
                return false;
            }

            string boundaryValue;
            string boundaryValue2;
            string nestedBoundaryValue;
            if (!allowed.Values.TryGetValue("_descriptionBackup", out boundaryValue)
                || boundaryValue != "protected-boundary"
                || !allowed.Values.TryGetValue("_description2", out boundaryValue2)
                || boundaryValue2 != "protected-boundary-2"
                || !allowed.Values.TryGetValue("_other._description", out nestedBoundaryValue)
                || nestedBoundaryValue != "protected-nested-boundary")
            {
                error = "Mutable prefix boundary excluded _descriptionBackup.";
                return false;
            }

            SortedDictionary<string, string> longDescription =
                new SortedDictionary<string, string>(allowedChanges, StringComparer.Ordinal)
                {
                    ["_description"] =
                        "서로 다른 UTF-8 바이트 길이를 가진 긴 한글 설명도 하나의 Scalar 값이다.",
                    ["_description.Array.size"] = "92",
                    ["_description.Array.data[0]"] = "-20",
                    ["_description.Array.data[91]"] = "46"
                };
            ProtectedPreservationSnapshot longDescriptionProjection =
                ProjectPureProtectedValues(longDescription, mutablePaths);
            if (!ProtectedValuesEqual(expected, longDescriptionProjection)
                || CountPathFamily(longDescriptionProjection, "_description") != 0)
            {
                error = "Long Korean description leaked byte children into protection.";
                return false;
            }

            SortedDictionary<string, string> changedId =
                new SortedDictionary<string, string>(allowedChanges, StringComparer.Ordinal)
                {
                    ["_id"] = "STAFF99"
                };
            List<string> idDifferences = FindProtectedDifferencePaths(
                expected,
                ProjectPureProtectedValues(changedId, mutablePaths));
            if (CountPathFamily(expected, "_id") != 1
                || idDifferences.Count != 1
                || idDifferences[0] != "_id")
            {
                error = "Protected string change was not aggregated to semantic _id root.";
                return false;
            }

            string[] protectedMutationPaths =
            {
                "_skill",
                "_sprite",
                "_moneyType",
                "_cleanerLevelData.Array.data[5]._addSpeed",
                "StaffData.MetaSha256"
            };
            for (int index = 0; index < protectedMutationPaths.Length; index++)
            {
                SortedDictionary<string, string> mutated =
                    new SortedDictionary<string, string>(allowedChanges, StringComparer.Ordinal);
                mutated[protectedMutationPaths[index]] += "-changed";
                if (ProtectedValuesEqual(
                        expected,
                        ProjectPureProtectedValues(mutated, mutablePaths)))
                {
                    error = "Protected mutation was not detected: " + protectedMutationPaths[index];
                    return false;
                }
            }

            SortedDictionary<string, string> nestedProtectedChange =
                new SortedDictionary<string, string>(allowedChanges, StringComparer.Ordinal)
                {
                    ["_cleanerLevelData.Array.data[5]._addSpeed"] = "3.5"
                };
            if (ProtectedValuesEqual(
                    expected,
                    ProjectPureProtectedValues(nestedProtectedChange, mutablePaths)))
            {
                error = "Non-target level element mutation was not detected.";
                return false;
            }

            string descriptionTarget = "꼼꼼하고 책임감 있는 웨이터 덕덕.";
            if (!string.Equals(
                    allowedChanges["_description"],
                    descriptionTarget,
                    StringComparison.Ordinal)
                || !string.Equals(
                    allowedChanges["_name"],
                    "After",
                    StringComparison.Ordinal))
            {
                error = "Description mutable target match failed.";
                return false;
            }

            string rankBefore = "Normal1";
            string rankAfter = "Unique";
            string rankTarget = "Unique";
            if (string.Equals(rankBefore, rankTarget, StringComparison.Ordinal)
                || !string.Equals(rankAfter, rankTarget, StringComparison.Ordinal))
            {
                error = "Rank semantic target comparison failed.";
                return false;
            }

            string speedBefore = "12.5";
            string speedAfter = "8.5";
            string speedTarget = "8.5";
            if (NumbersEqual(speedBefore, speedTarget)
                || !NumbersEqual(speedAfter, speedTarget))
            {
                error = "STAFF32 speed target comparison failed.";
                return false;
            }

            return true;
        }

        private static bool RunPurePostApplyResultSelfTests(out string error)
        {
            error = string.Empty;

            Staff32SpotCheckResult passingSpot = CreatePureStaff32SpotCheck(true);
            PostApplyValidationResult passingDetails = CreatePurePostApplyResult(true, true);
            passingDetails.Info.Add(BuildStaff32SpotCheckReport(passingSpot));
            FinalizePostApplyResult(passingDetails);
            if (!passingDetails.Passed
                || passingSpot.Details.Count != 14
                || passingSpot.Errors.Count != 0
                || !passingSpot.Passed)
            {
                error = "Test 1: PASS details affected Post-Apply success.";
                return false;
            }

            Staff32SpotCheckResult failingSpot = CreatePureStaff32SpotCheck(false);
            PostApplyValidationResult actualError = CreatePurePostApplyResult(false, true);
            actualError.Errors.AddRange(failingSpot.Errors);
            FinalizePostApplyResult(actualError);
            if (actualError.Passed
                || failingSpot.Details.Count != 14
                || failingSpot.Errors.Count != 1
                || failingSpot.Passed)
            {
                error = "Test 2: STAFF32 actual error did not fail Post-Apply.";
                return false;
            }

            PostApplyValidationResult manyInfo = CreatePurePostApplyResult(true, true);
            for (int index = 0; index < 100; index++)
            {
                manyInfo.Info.Add("Pure Info " + index.ToString(CultureInfo.InvariantCulture));
            }

            FinalizePostApplyResult(manyInfo);
            if (!manyInfo.Passed || manyInfo.Errors.Count != 0 || manyInfo.Info.Count != 100)
            {
                error = "Test 3: Info count affected Post-Apply success.";
                return false;
            }

            PostApplyValidationResult silentFalse = CreatePurePostApplyResult(true, true);
            silentFalse.Checks.Clear();
            AddPureRequiredChecks(silentFalse, true, true, false);
            FinalizePostApplyResult(silentFalse);
            if (silentFalse.Passed
                || !silentFalse.Errors.Any(
                    item => item.Contains("POST_APPLY_FALSE_WITHOUT_ERROR")))
            {
                error = "Test 4: Silent false guard was not generated.";
                return false;
            }

            PostApplyValidationResult protectedFailure = CreatePurePostApplyResult(true, false);
            protectedFailure.Errors.Add("Protected Preservation expected true, actual false.");
            protectedFailure.RequiresRollback = true;
            FinalizePostApplyResult(protectedFailure);
            if (protectedFailure.Passed
                || protectedFailure.Errors.Count < 1
                || !protectedFailure.RequiresRollback)
            {
                error = "Test 5: Protected failure did not require rollback.";
                return false;
            }

            Staff32SpotCheckResult allStaff32ItemsPass = CreatePureStaff32SpotCheck(true);
            if (!allStaff32ItemsPass.Passed
                || allStaff32ItemsPass.Details.Count != 14
                || allStaff32ItemsPass.Errors.Count != 0)
            {
                error = "Test 6: All-PASS STAFF32 result was not true.";
                return false;
            }

            Staff32SpotCheckResult effect49 = new Staff32SpotCheckResult();
            AppendSpotResult(effect49, "Skill Effect Value", "50", "49", false);
            effect49.Passed = effect49.Errors.Count == 0;
            if (effect49.Passed
                || effect49.Errors.Count != 1
                || !effect49.Errors[0].Contains("Expected: 50")
                || !effect49.Errors[0].Contains("Actual: 49"))
            {
                error = "Test 7: STAFF32 effect value 49 did not emit the exact mismatch.";
                return false;
            }

            return true;
        }

        private static Staff32SpotCheckResult CreatePureStaff32SpotCheck(bool passed)
        {
            Staff32SpotCheckResult result = new Staff32SpotCheckResult();
            string[] labels =
            {
                "Name",
                "Description",
                "Rank",
                "Base Speed",
                "Concrete Class",
                "Skill Reference Path",
                "Skill Reference GUID",
                "Skill Class",
                "Skill Effect Field",
                "Skill Effect Value",
                "Skill Duration",
                "Skill Cooldown",
                "Cleaner Level Array Count",
                "Non-STAFF02 Five-Slot Structure"
            };
            for (int index = 0; index < labels.Length; index++)
            {
                bool itemPassed = passed || labels[index] != "Skill Effect Value";
                AppendSpotResult(
                    result,
                    labels[index],
                    labels[index] == "Skill Effect Value" ? "50" : "expected",
                    labels[index] == "Skill Effect Value" && !itemPassed ? "49" :
                        labels[index] == "Skill Effect Value" ? "50" : "expected",
                    itemPassed);
            }

            result.Passed = result.Errors.Count == 0;
            return result;
        }

        private static PostApplyValidationResult CreatePurePostApplyResult(
            bool staff32SpotCheckPassed,
            bool protectedPreservationPassed)
        {
            PostApplyValidationResult result = new PostApplyValidationResult();
            AddPureRequiredChecks(
                result,
                staff32SpotCheckPassed,
                protectedPreservationPassed,
                true);
            return result;
        }

        private static void AddPureRequiredChecks(
            PostApplyValidationResult result,
            bool staff32SpotCheckPassed,
            bool protectedPreservationPassed,
            bool postApplyPlanBuilt)
        {
            result.AddCheck("canonicalSnapshotPassed", "Canonical Snapshot", true);
            result.AddCheck("whitelistTargetMatchPassed", "Whitelist Target Match", true);
            result.AddCheck(
                "protectedPreservationPassed",
                "Protected Preservation",
                protectedPreservationPassed);
            result.AddCheck(
                "staff32SpotCheckPassed",
                "STAFF32 Spot Check",
                staff32SpotCheckPassed);
            result.AddCheck("staff02Slot6Preserved", "STAFF02 Slot 6 Preservation", true);
            result.AddCheck("postApplyPlanBuilt", "Post-Apply Plan", postApplyPlanBuilt);
            result.AddCheck("postApplyInventoryBuilt", "Post-Apply Inventory", true);
            result.AddCheck(
                "postApplyStateApplied",
                "Migration State ALREADY_APPLIED",
                true);
            result.AddCheck(
                "skillReferencesPreserved",
                "Skill References Preserved",
                true);
            result.AddCheck(
                "visualReferencesPreserved",
                "Visual References Preserved",
                true);
            result.AddCheck("guidPreserved", "GUID Preserved", true);
            result.AddCheck("scriptPreserved", "Script Preserved", true);
            result.AddCheck("metaPreserved", "Meta Preserved", true);
            result.AddCheck("structuralErrorsZero", "Structural Errors Zero", true);
            result.AddCheck("unexpectedAssetsZero", "Unexpected Assets Zero", true);
            result.AddCheck(
                "deterministicPostStatePassed",
                "Deterministic Inventory / Plan",
                true);
            result.AddCheck("officialSpotChecksPassed", "Official V18 Spot Checks", true);
        }

        private static bool RunPureStateMatrixSelfTests(out string error)
        {
            error = string.Empty;
            MigrationState appliedState = ClassifyExistingV18DataState(
                true, true, true, true, false, true, true, 801, 801, true);
            MigrationState readyState = ClassifyExistingV18DataState(
                true, true, true, true, true, false, false, 0, 801, true);
            MigrationState partialTargetState = ClassifyExistingV18DataState(
                true, true, true, true, false, false, false, 800, 801, true);
            MigrationState partialProtectedState = ClassifyExistingV18DataState(
                true, true, true, true, false, false, false, 801, 801, false);

            List<string> expectedAll = BuildExpectedA1OwnedPaths();
            List<string> expectedEditor = BuildExpectedA1EditorPaths();
            const string externalPath =
                "Assets/Package/GooglePlayGamesPlugin-2.1.0.unitypackage.meta";
            string allOutput = string.Join("\0", expectedAll) + "\0";
            string editorOutput = string.Join("\0", expectedEditor) + "\0";

            RepositoryHygieneAudit caseARepository = ClassifyRepositoryHygiene(
                appliedState,
                BuildGitChangedPathCollection(allOutput, string.Empty, string.Empty),
                true,
                string.Empty);
            PreviewVerdict caseAPreview = DeterminePreviewVerdict(
                appliedState,
                caseARepository.State);

            RepositoryHygieneAudit caseBRepository = ClassifyRepositoryHygiene(
                appliedState,
                BuildGitChangedPathCollection(
                    allOutput + externalPath + "\0",
                    string.Empty,
                    string.Empty),
                true,
                string.Empty);
            PreviewVerdict caseBPreview = DeterminePreviewVerdict(
                appliedState,
                caseBRepository.State);

            RepositoryHygieneAudit caseCRepository = ClassifyRepositoryHygiene(
                appliedState,
                BuildGitChangedPathCollection(string.Empty, string.Empty, string.Empty),
                true,
                string.Empty);
            PreviewVerdict caseCPreview = DeterminePreviewVerdict(
                appliedState,
                caseCRepository.State);

            PreviewVerdict caseDPreview = DeterminePreviewVerdict(
                partialTargetState,
                RepositoryHygieneState.A1_EXPECTED_CHANGES_ONLY);
            PreviewVerdict caseEPreview = DeterminePreviewVerdict(
                partialProtectedState,
                RepositoryHygieneState.A1_EXPECTED_CHANGES_ONLY);

            RepositoryHygieneAudit caseFRepository = ClassifyRepositoryHygiene(
                readyState,
                BuildGitChangedPathCollection(
                    editorOutput + externalPath + "\0",
                    string.Empty,
                    string.Empty),
                true,
                string.Empty);
            PreviewVerdict caseFPreview = DeterminePreviewVerdict(
                readyState,
                caseFRepository.State);

            bool caseGWriteAllowed = IsApplyWriteAllowed(appliedState);

            List<string> fiveExternal = new List<string>();
            for (int index = 1; index <= 5; index++)
            {
                fiveExternal.Add(
                    "Assets/Package/ExternalRepositoryChange"
                    + index.ToString(CultureInfo.InvariantCulture) + ".meta");
            }

            RepositoryHygieneAudit caseHRepository = ClassifyRepositoryHygiene(
                appliedState,
                BuildGitChangedPathCollection(
                    allOutput + string.Join("\0", fiveExternal) + "\0",
                    string.Empty,
                    string.Empty),
                true,
                string.Empty);
            PreviewVerdict caseHPreview = DeterminePreviewVerdict(
                appliedState,
                caseHRepository.State);

            MigrationDataAudit classifierOnlyFailure = CreatePureIntegrityAudit(true);
            classifierOnlyFailure.ClassifierInconsistent = true;
            MigrationDataAudit skillIntegrityFailure = CreatePureIntegrityAudit(false);
            if (appliedState != MigrationState.ALREADY_APPLIED
                || readyState != MigrationState.READY_TO_APPLY
                || caseARepository.State
                != RepositoryHygieneState.A1_EXPECTED_CHANGES_ONLY
                || caseARepository.AllowedEditorChangedCount
                != ExpectedA1EditorChangedPathCount
                || caseARepository.ChangedStaffAssetCount != ExistingStaffCount
                || caseAPreview != PreviewVerdict.PASS
                || caseARepository.CommitReadiness != CommitReadiness.READY
                || caseBRepository.State
                != RepositoryHygieneState.EXTERNAL_CHANGES_PRESENT
                || caseBPreview != PreviewVerdict.PASS_WITH_REPOSITORY_WARNING
                || caseBRepository.CommitReadiness
                != CommitReadiness.BLOCKED_BY_EXTERNAL_CHANGES
                || caseBRepository.ApplyReadiness
                != ApplyReadiness.NOT_APPLICABLE_ALREADY_APPLIED
                || caseBRepository.Errors.Count != 0
                || caseBRepository.Warnings.Count < 2
                || caseCRepository.State != RepositoryHygieneState.CLEAN
                || caseCPreview != PreviewVerdict.PASS
                || partialTargetState != MigrationState.PARTIAL_MIGRATION_STATE
                || caseDPreview != PreviewVerdict.FAIL
                || partialProtectedState != MigrationState.PARTIAL_MIGRATION_STATE
                || caseEPreview != PreviewVerdict.FAIL
                || caseFRepository.State
                != RepositoryHygieneState.EXTERNAL_CHANGES_PRESENT
                || caseFPreview != PreviewVerdict.PASS_WITH_REPOSITORY_WARNING
                || caseFRepository.ApplyReadiness
                != ApplyReadiness.BLOCKED_BY_REPOSITORY_HYGIENE
                || caseFRepository.Errors.Count != 0
                || caseGWriteAllowed
                || caseHRepository.State
                != RepositoryHygieneState.EXTERNAL_CHANGES_PRESENT
                || caseHRepository.ExternalChangedPaths.Count != 5
                || caseHPreview != PreviewVerdict.PASS_WITH_REPOSITORY_WARNING
                || caseHRepository.Errors.Count != 0
                || RequiresDataIntegrityRollback(classifierOnlyFailure)
                || !RequiresDataIntegrityRollback(skillIntegrityFailure))
            {
                error = "State Separation Matrix A/H contract mismatch: "
                        + appliedState + "/" + readyState + "/"
                        + caseARepository.State + "/" + caseBRepository.State + "/"
                        + caseCRepository.State + "/" + partialTargetState + "/"
                        + partialProtectedState + "/" + caseFRepository.State + "/"
                        + caseHRepository.State + "/write=" + caseGWriteAllowed;
                return false;
            }

            return true;
        }

        private static MigrationDataAudit CreatePureIntegrityAudit(bool skillPassed)
        {
            return new MigrationDataAudit
            {
                ManagedTargetMatch = true,
                ProtectedContractPassed = skillPassed,
                Staff02Slot6Preserved = true,
                GuidPassed = true,
                ScriptPassed = true,
                SkillPassed = skillPassed,
                VisualPassed = true,
                MetaPassed = true,
                UnexpectedAssetsZero = true
            };
        }

        private static bool IsApplyWriteAllowed(MigrationState state)
        {
            return state == MigrationState.READY_TO_APPLY;
        }

        private static bool RunPureGitChangedPathSelfTests(out string error)
        {
            error = string.Empty;
            const string testPath = "Assets/Editor/Test.cs";
            GitChangedPathCollection test1 = BuildGitChangedPathCollection(
                testPath + "\0", string.Empty, string.Empty);
            if (test1.All.Count != 1
                || !string.Equals(test1.All[0], testPath, StringComparison.Ordinal))
            {
                error = "Git Path Test 1 failed: working-tree first character was not preserved.";
                return false;
            }

            GitChangedPathCollection test2 = BuildGitChangedPathCollection(
                string.Empty, testPath + "\0", string.Empty);
            if (test2.Staged.Count != 1
                || !string.Equals(test2.Staged[0], testPath, StringComparison.Ordinal))
            {
                error = "Git Path Test 2 failed: staged path mismatch.";
                return false;
            }

            const string spacedPath = "Assets/New Tool.cs";
            GitChangedPathCollection test3 = BuildGitChangedPathCollection(
                string.Empty, string.Empty, spacedPath + "\0");
            if (test3.Untracked.Count != 1
                || !string.Equals(test3.Untracked[0], spacedPath, StringComparison.Ordinal))
            {
                error = "Git Path Test 3 failed: untracked path spacing was not preserved.";
                return false;
            }

            const string unicodePath = "Assets/테스트/직원 데이터.cs";
            GitChangedPathCollection test4 = BuildGitChangedPathCollection(
                unicodePath + "\0", string.Empty, string.Empty);
            if (test4.All.Count != 1
                || !string.Equals(test4.All[0], unicodePath, StringComparison.Ordinal))
            {
                error = "Git Path Test 4 failed: Unicode path mismatch.";
                return false;
            }

            GitChangedPathCollection test5 = BuildGitChangedPathCollection(
                testPath + "\0", testPath + "\0", string.Empty);
            if (test5.All.Count != 1)
            {
                error = "Git Path Test 5 failed: duplicate paths were not collapsed.";
                return false;
            }

            GitChangedPathCollection test6 = BuildGitChangedPathCollection(
                ".\\Assets\\Editor\\Test.cs\0", string.Empty, string.Empty);
            if (test6.All.Count != 1
                || !string.Equals(test6.All[0], testPath, StringComparison.Ordinal))
            {
                error = "Git Path Test 6 failed: Windows separators were not normalized.";
                return false;
            }

            string[] firstCharacterPaths =
            {
                "Assets",
                "OfficialData",
                "ProjectSettings",
                "Packages"
            };
            GitChangedPathCollection test7 = BuildGitChangedPathCollection(
                string.Join("\0", firstCharacterPaths) + "\0",
                string.Empty,
                string.Empty);
            string[] ordinalFirstCharacterPaths = firstCharacterPaths
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (test7.All.Count != firstCharacterPaths.Length
                || !test7.All.SequenceEqual(
                    ordinalFirstCharacterPaths,
                    StringComparer.Ordinal))
            {
                error = "Git Path Test 7 failed: a leading path character was removed.";
                return false;
            }

            const string inventoryValidatorPath =
                "Assets/Editor/PandaRestaurant/StaffDataValidation/"
                + "StaffDataAssetInventoryValidator.cs";
            GitChangedPathCollection test8 = BuildGitChangedPathCollection(
                inventoryValidatorPath + "\0", string.Empty, string.Empty);
            if (test8.All.Count != 1
                || !string.Equals(
                    test8.All[0],
                    inventoryValidatorPath,
                    StringComparison.Ordinal)
                || !IsAllowedA1EditorPath(test8.All[0]))
            {
                error = "Git Path Test 8 failed: actual allowed Editor path mismatch.";
                return false;
            }

            const string unexpectedPath =
                "Assets/Package/GooglePlayGamesPlugin-2.1.0.unitypackage.meta";
            GitChangedPathCollection test9 = BuildGitChangedPathCollection(
                unexpectedPath + "\0", string.Empty, string.Empty);
            if (test9.All.Count != 1
                || IsAllowedA1EditorPath(test9.All[0])
                || IsExpectedExistingStaffAssetPath(test9.All[0]))
            {
                error = "Git Path Test 9 failed: unexpected path was not rejected exactly.";
                return false;
            }

            return true;
        }

        private static bool RunPureRollbackTransactionSelfTests(out string error)
        {
            error = string.Empty;
            string root = Path.Combine(
                Path.GetTempPath(),
                "PandaRestaurant_A1_Rollback_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                if (!RunRollbackTransactionSelfTest1(root, out error)
                    || !RunRollbackTransactionSelfTest2(root, out error)
                    || !RunRollbackTransactionSelfTest3(root, out error)
                    || !RunRollbackTransactionSelfTest4(root, out error)
                    || !RunRollbackTransactionSelfTest5(root, out error)
                    || !RunRollbackTransactionSelfTest6(root, out error)
                    || !RunRollbackTransactionSelfTest7(root, out error))
                {
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "Rollback temporary-file self-test exception: "
                        + exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
                catch (Exception cleanupException)
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = "Rollback self-test cleanup failed: " + cleanupException.Message;
                    }
                }
            }
        }

        private static bool RunRollbackTransactionSelfTest1(string root, out string error)
        {
            string first = CreateRollbackTestFile(root, "t1-a.bin", "current-a");
            string second = CreateRollbackTestFile(root, "t1-b.bin", "current-b");
            List<RawRestoreFile> files = CreateRollbackTestRequests(
                first, "original-a", second, "original-b");
            List<string> diagnostics = new List<string>();
            bool writesStarted;
            bool passed = ExecuteRawRestoreTransaction(
                files, () => { }, () => { }, () => { }, OpenDirectTestStream,
                RollbackLeaseRetryCount, diagnostics, out writesStarted);
            error = string.Empty;
            if (!passed
                || !writesStarted
                || ReadUtf8(first) != "original-a"
                || ReadUtf8(second) != "original-b")
            {
                error = "Rollback Test 1 failed: all-file restore did not pass.";
                return false;
            }

            return true;
        }

        private static bool RunRollbackTransactionSelfTest2(string root, out string error)
        {
            string first = CreateRollbackTestFile(root, "t2-a.bin", "current-a");
            string second = CreateRollbackTestFile(root, "t2-b.bin", "current-b");
            List<RawRestoreFile> files = CreateRollbackTestRequests(
                first, "original-a", second, "original-b");
            bool passed;
            bool writesStarted;
            List<string> diagnostics = new List<string>();
            using (new FileStream(first, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                passed = ExecuteRawRestoreTransaction(
                    files, () => { }, () => { }, () => { }, OpenDirectTestStream,
                    RollbackLeaseRetryCount, diagnostics, out writesStarted);
            }

            error = string.Empty;
            if (passed
                || writesStarted
                || ReadUtf8(first) != "current-a"
                || ReadUtf8(second) != "current-b")
            {
                error = "Rollback Test 2 failed: first-file lock allowed a write.";
                return false;
            }

            return true;
        }

        private static bool RunRollbackTransactionSelfTest3(string root, out string error)
        {
            string first = CreateRollbackTestFile(root, "t3-a.bin", "current-a");
            string second = CreateRollbackTestFile(root, "t3-b.bin", "current-b");
            List<RawRestoreFile> files = CreateRollbackTestRequests(
                first, "original-a", second, "original-b");
            bool passed;
            bool writesStarted;
            List<string> diagnostics = new List<string>();
            using (new FileStream(second, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                passed = ExecuteRawRestoreTransaction(
                    files, () => { }, () => { }, () => { }, OpenDirectTestStream,
                    RollbackLeaseRetryCount, diagnostics, out writesStarted);
            }

            error = string.Empty;
            if (passed
                || writesStarted
                || ReadUtf8(first) != "current-a"
                || ReadUtf8(second) != "current-b")
            {
                error = "Rollback Test 3 failed: middle-file lock allowed a partial write.";
                return false;
            }

            return true;
        }

        private static bool RunRollbackTransactionSelfTest4(string root, out string error)
        {
            string first = CreateRollbackTestFile(root, "t4-a.bin", "current-a");
            string second = CreateRollbackTestFile(root, "t4-b.bin", "current-b");
            List<RawRestoreFile> files = CreateRollbackTestRequests(
                first, "original-a", second, "original-b");
            bool failedOnce = false;
            List<string> diagnostics = new List<string>();
            bool writesStarted;
            bool passed = ExecuteRawRestoreTransaction(
                files,
                () => { },
                () => { },
                () => { },
                (file, attempt) =>
                {
                    if (!failedOnce)
                    {
                        failedOnce = true;
                        throw new SimulatedWin32IOException(1224, "Simulated cached handle.");
                    }

                    return OpenDirectTestStream(file, attempt);
                },
                RollbackLeaseRetryCount,
                diagnostics,
                out writesStarted);
            error = string.Empty;
            if (!passed
                || !writesStarted
                || !failedOnce
                || ReadUtf8(first) != "original-a"
                || ReadUtf8(second) != "original-b")
            {
                error = "Rollback Test 4 failed: one Win32 1224 retry did not recover.";
                return false;
            }

            return true;
        }

        private static bool RunRollbackTransactionSelfTest5(string root, out string error)
        {
            string first = CreateRollbackTestFile(root, "t5-a.bin", "current-a");
            string second = CreateRollbackTestFile(root, "t5-b.bin", "current-b");
            List<RawRestoreFile> files = CreateRollbackTestRequests(
                first, "original-a", second, "original-b");
            List<string> diagnostics = new List<string>();
            bool writesStarted;
            bool passed = ExecuteRawRestoreTransaction(
                files,
                () => { },
                () => { },
                () => { },
                (file, attempt) =>
                {
                    throw new SimulatedWin32IOException(1224, "Persistent simulated lock.");
                },
                RollbackLeaseRetryCount,
                diagnostics,
                out writesStarted);
            error = string.Empty;
            if (passed
                || writesStarted
                || !diagnostics.Any(item => item.Contains(
                    "ROLLBACK_WRITE_LEASE_ACQUISITION_FAILED"))
                || ReadUtf8(first) != "current-a"
                || ReadUtf8(second) != "current-b")
            {
                error = "Rollback Test 5 failed: persistent Win32 1224 caused a partial write.";
                return false;
            }

            return true;
        }

        private static bool RunRollbackTransactionSelfTest6(string root, out string error)
        {
            string first = CreateRollbackTestFile(root, "t6-a.bin", "current-a");
            byte[] backup = Encoding.UTF8.GetBytes("original-a");
            List<RawRestoreFile> files = new List<RawRestoreFile>
            {
                new RawRestoreFile(first, backup, ComputeSha256(backup))
            };
            bool started = false;
            bool stopped = false;
            bool writesStarted;
            List<string> diagnostics = new List<string>();
            bool passed = ExecuteRawRestoreTransaction(
                files,
                () => { },
                () => { started = true; },
                () => { stopped = true; },
                (file, attempt) =>
                {
                    throw new SimulatedWin32IOException(1224, "Editing-boundary test.");
                },
                1,
                diagnostics,
                out writesStarted);
            error = string.Empty;
            if (passed || !started || !stopped || writesStarted)
            {
                error = "Rollback Test 6 failed: StopAssetEditing boundary was not guaranteed.";
                return false;
            }

            return true;
        }

        private static bool RunRollbackTransactionSelfTest7(string root, out string error)
        {
            string first = CreateRollbackTestFile(root, "t7-a.bin", "current-a");
            byte[] backup = Encoding.UTF8.GetBytes("original-a");
            List<RawRestoreFile> files = new List<RawRestoreFile>
            {
                new RawRestoreFile(first, backup, ComputeSha256(backup))
            };
            bool writesStarted;
            List<string> diagnostics = new List<string>();
            bool passed = ExecuteRawRestoreTransaction(
                files, () => { }, () => { }, () => { }, OpenDirectTestStream,
                RollbackLeaseRetryCount, diagnostics, out writesStarted);
            File.WriteAllBytes(first, Encoding.UTF8.GetBytes("post-restore-corruption"));
            bool shaPassed = ValidateRestoredFileHashes(files, diagnostics);
            error = string.Empty;
            if (!passed
                || !writesStarted
                || shaPassed
                || !diagnostics.Any(item => item.Contains("ROLLBACK RESTORED SHA MISMATCH")))
            {
                error = "Rollback Test 7 failed: post-restore SHA mismatch was not reported.";
                return false;
            }

            return true;
        }

        private static string CreateRollbackTestFile(
            string root,
            string fileName,
            string contents)
        {
            string path = Path.Combine(root, fileName);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(contents));
            return path;
        }

        private static List<RawRestoreFile> CreateRollbackTestRequests(
            string firstPath,
            string firstBackup,
            string secondPath,
            string secondBackup)
        {
            byte[] firstBytes = Encoding.UTF8.GetBytes(firstBackup);
            byte[] secondBytes = Encoding.UTF8.GetBytes(secondBackup);
            return new List<RawRestoreFile>
            {
                new RawRestoreFile(firstPath, firstBytes, ComputeSha256(firstBytes)),
                new RawRestoreFile(secondPath, secondBytes, ComputeSha256(secondBytes))
            };
        }

        private static FileStream OpenDirectTestStream(RawRestoreFile file, int attempt)
        {
            return new FileStream(
                file.Path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
        }

        private static string ReadUtf8(string path)
        {
            return Encoding.UTF8.GetString(File.ReadAllBytes(path));
        }

        private static ProtectedPreservationSnapshot ProjectPureProtectedValues(
            SortedDictionary<string, string> values,
            HashSet<string> mutablePaths)
        {
            SortedDictionary<string, string> projected =
                new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in values)
            {
                string mutableRoot;
                if (!TryGetMutableSerializedRoot(pair.Key, mutablePaths, out mutableRoot))
                {
                    projected.Add(pair.Key, pair.Value);
                }
            }

            return new ProtectedPreservationSnapshot(projected);
        }

        private static int CountPathFamily(
            ProtectedPreservationSnapshot snapshot,
            string root)
        {
            int count = 0;
            foreach (string path in snapshot.Values.Keys)
            {
                if (string.Equals(path, root, StringComparison.Ordinal)
                    || path.StartsWith(root + ".", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryFindMutableSubtreeLeak(
            ProtectedPreservationSnapshot snapshot,
            HashSet<string> mutableRoots,
            out string leakedRoot,
            out string leakedPath)
        {
            leakedRoot = string.Empty;
            leakedPath = string.Empty;
            foreach (string path in snapshot.Values.Keys)
            {
                if (TryGetMutableSerializedRoot(path, mutableRoots, out leakedRoot))
                {
                    leakedPath = path;
                    return true;
                }
            }

            return false;
        }

        private static List<string> FindProtectedDifferencePaths(
            ProtectedPreservationSnapshot left,
            ProtectedPreservationSnapshot right)
        {
            SortedSet<string> paths = new SortedSet<string>(StringComparer.Ordinal);
            paths.UnionWith(left.Values.Keys);
            paths.UnionWith(right.Values.Keys);
            List<string> differences = new List<string>();
            foreach (string path in paths)
            {
                string leftValue;
                string rightValue;
                if (!left.Values.TryGetValue(path, out leftValue)
                    || !right.Values.TryGetValue(path, out rightValue)
                    || !string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                {
                    differences.Add(path);
                }
            }

            return differences;
        }

        private static bool ProtectedValuesEqual(
            ProtectedPreservationSnapshot left,
            ProtectedPreservationSnapshot right)
        {
            if (left.Values.Count != right.Values.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> pair in left.Values)
            {
                string value;
                if (!right.Values.TryGetValue(pair.Key, out value)
                    || !string.Equals(pair.Value, value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateA1GitSafety(
            MigrationInspection inspection,
            List<string> errors)
        {
            string projectRoot = GetProjectRoot();
            bool safe = true;
            string branch;
            string gitError;
            int exitCode;
            if (!TryRunGit(
                    projectRoot,
                    "branch --show-current",
                    out branch,
                    out gitError,
                    out exitCode)
                || string.IsNullOrWhiteSpace(branch))
            {
                errors.Add("A1 Branch Safety: 현재 Git 브랜치를 읽지 못했습니다: " + gitError);
                safe = false;
            }
            else
            {
                inspection.GitBranch = branch.Trim();
                if (!string.Equals(
                        inspection.GitBranch,
                        ExpectedWorkingBranch,
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        "A1 Branch Safety: 현재 Git 브랜치가 작업 브랜치와 다릅니다. 현재: "
                        + inspection.GitBranch + ", 지정: " + ExpectedWorkingBranch);
                    safe = false;
                }
            }

            string head;
            if (!TryRunGit(
                    projectRoot,
                    "rev-parse HEAD",
                    out head,
                    out gitError,
                    out exitCode)
                || string.IsNullOrWhiteSpace(head))
            {
                errors.Add("A1 Branch Safety: 현재 Git HEAD를 읽지 못했습니다: " + gitError);
                safe = false;
            }
            else
            {
                inspection.GitHead = head.Trim();
            }

            string ancestorOutput;
            if (!TryRunGit(
                    projectRoot,
                    "merge-base --is-ancestor " + RequiredBaseCommit + " HEAD",
                    out ancestorOutput,
                    out gitError,
                    out exitCode))
            {
                errors.Add(
                    "A1 Branch Safety: Required Base Commit이 현재 HEAD의 조상이 아닙니다. Base: "
                    + RequiredBaseCommit + ", HEAD: " + inspection.GitHead
                    + (string.IsNullOrEmpty(gitError) ? string.Empty : ", Git: " + gitError));
                safe = false;
            }
            else
            {
                inspection.RequiredBaseCommitIsAncestor = true;
            }

            string gitDirectoryOutput;
            if (!TryRunGit(
                    projectRoot,
                    "rev-parse --git-dir",
                    out gitDirectoryOutput,
                    out gitError,
                    out exitCode)
                || string.IsNullOrWhiteSpace(gitDirectoryOutput))
            {
                errors.Add("A1 Branch Safety: Git metadata 경로를 읽지 못했습니다: " + gitError);
                safe = false;
            }
            else
            {
                string gitDirectory = gitDirectoryOutput.Trim();
                if (!Path.IsPathRooted(gitDirectory))
                {
                    gitDirectory = Path.GetFullPath(Path.Combine(projectRoot, gitDirectory));
                }

                string[] operationFiles =
                {
                    "MERGE_HEAD",
                    "CHERRY_PICK_HEAD",
                    "REVERT_HEAD",
                    "REBASE_HEAD"
                };
                for (int index = 0; index < operationFiles.Length; index++)
                {
                    if (File.Exists(Path.Combine(gitDirectory, operationFiles[index])))
                    {
                        errors.Add(
                            "A1 Branch Safety: 진행 중인 Git 작업이 있습니다: "
                            + operationFiles[index]);
                        safe = false;
                    }
                }

                string[] rebaseDirectories = { "rebase-merge", "rebase-apply" };
                for (int index = 0; index < rebaseDirectories.Length; index++)
                {
                    if (Directory.Exists(Path.Combine(gitDirectory, rebaseDirectories[index])))
                    {
                        errors.Add(
                            "A1 Branch Safety: 진행 중인 Rebase가 있습니다: "
                            + rebaseDirectories[index]);
                        safe = false;
                    }
                }
            }

            string unmergedFiles;
            if (!TryRunGit(
                    projectRoot,
                    "ls-files --unmerged",
                    out unmergedFiles,
                    out gitError,
                    out exitCode))
            {
                errors.Add("A1 Branch Safety: conflict 상태를 확인하지 못했습니다: " + gitError);
                safe = false;
            }
            else if (!string.IsNullOrWhiteSpace(unmergedFiles))
            {
                errors.Add("A1 Branch Safety: 해결되지 않은 Git conflict가 있습니다.");
                safe = false;
            }

            inspection.BranchSafetyPassed = safe;
            return safe;
        }

        private static bool TryRunGit(
            string projectRoot,
            string arguments,
            out string output,
            out string error,
            out int exitCode)
        {
            bool succeeded = TryRunGitRaw(
                projectRoot,
                arguments,
                out output,
                out error,
                out exitCode);
            output = output.Trim();
            return succeeded;
        }

        private static bool TryRunGitRaw(
            string projectRoot,
            string arguments,
            out string output,
            out string error,
            out int exitCode)
        {
            output = string.Empty;
            error = string.Empty;
            exitCode = -1;
            try
            {
                System.Diagnostics.ProcessStartInfo startInfo =
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        WorkingDirectory = projectRoot,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                using (System.Diagnostics.Process process =
                       System.Diagnostics.Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        error = "Git 프로세스를 시작하지 못했습니다.";
                        return false;
                    }

                    System.Threading.Tasks.Task<string> outputTask =
                        process.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> errorTask =
                        process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    output = outputTask.Result;
                    error = errorTask.Result.Trim();
                    exitCode = process.ExitCode;
                    return exitCode == 0;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string GetProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        private static string GetAbsolutePath(string assetPath)
        {
            string projectRoot = GetProjectRoot();
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder output = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    output.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return output.ToString();
            }
        }

        private static bool TryParseLevelFieldPath(
            string fieldPath,
            out int levelIndex,
            out string suffix)
        {
            levelIndex = -1;
            suffix = string.Empty;
            if (string.IsNullOrEmpty(fieldPath)
                || !fieldPath.StartsWith("Levels[", StringComparison.Ordinal))
            {
                return false;
            }

            int close = fieldPath.IndexOf(']');
            if (close <= 7
                || close + 2 >= fieldPath.Length
                || fieldPath[close + 1] != '.')
            {
                return false;
            }

            suffix = fieldPath.Substring(close + 2);
            return int.TryParse(
                fieldPath.Substring(7, close - 7),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out levelIndex);
        }

        private static bool NumbersEqual(string left, string right)
        {
            double leftValue;
            double rightValue;
            return double.TryParse(
                       left,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out leftValue)
                   && double.TryParse(
                       right,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out rightValue)
                   && Math.Abs(leftValue - rightValue) <= 0.0001d;
        }

        private static bool SequenceEqual(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildStaffId(int number)
        {
            return "STAFF" + number.ToString("00", CultureInfo.InvariantCulture);
        }

        private static void AddDiagnostics(
            string label,
            IReadOnlyList<string> diagnostics,
            List<string> errors)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                errors.Add(label + " failed without diagnostics.");
                return;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                errors.Add(label + ": " + diagnostics[index]);
            }
        }

        private static void LogApplyRepositoryBlock(MigrationInspection inspection)
        {
            RepositoryHygieneAudit repository = inspection == null
                ? null
                : inspection.RepositoryAudit;
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Existing Staff V18 Data Migration]");
            output.AppendLine("APPLY_BLOCKED_BY_REPOSITORY_HYGIENE");
            output.AppendLine("Migration State: "
                              + (inspection == null ? MigrationState.INVALID : inspection.State));
            output.AppendLine("Repository Hygiene: "
                              + (repository == null
                                  ? RepositoryHygieneState.A1_SCOPE_ANOMALY
                                  : repository.State));
            if (repository != null)
            {
                output.AppendLine("Apply Readiness: " + repository.ApplyReadiness);
                for (int index = 0; index < repository.ExternalChangedPaths.Count; index++)
                {
                    output.AppendLine(
                        "External Path: " + repository.ExternalChangedPaths[index]);
                }

                for (int index = 0; index < repository.MissingExpectedA1Paths.Count; index++)
                {
                    output.AppendLine(
                        "Missing A1 Path: " + repository.MissingExpectedA1Paths[index]);
                }
            }

            output.AppendLine("Dialog: NOT_SHOWN");
            output.AppendLine("Asset write: 0");
            Debug.LogError(output.ToString());
        }

        private static void LogInspection(
            string mode,
            MigrationInspection inspection,
            List<string> errors,
            bool inspected)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Existing Staff V18 Data Migration]");
            output.AppendLine("Policy: " + PolicyMarker);
            output.AppendLine("Mode: " + mode);
            output.AppendLine("Git branch: "
                              + (inspection == null ? string.Empty : inspection.GitBranch));
            output.AppendLine("Git HEAD: "
                              + (inspection == null ? string.Empty : inspection.GitHead));
            output.AppendLine("A1 Branch Safety: "
                              + (inspection != null && inspection.BranchSafetyPassed ? "PASS" : "FAIL"));
            output.AppendLine("Required Base Commit ancestor: "
                              + (inspection != null && inspection.RequiredBaseCommitIsAncestor
                                  ? "PASS"
                                  : "FAIL"));
            output.AppendLine("Migration State: "
                              + (inspection == null ? MigrationState.INVALID.ToString() : inspection.State.ToString()));
            output.AppendLine("Plan State Marker: "
                              + (inspection == null ? string.Empty : inspection.PlanStateMarker));
            output.AppendLine("Official PackageFingerprint: "
                              + (inspection == null ? string.Empty : inspection.OfficialFingerprint));
            output.AppendLine("InventoryFingerprint: "
                              + (inspection == null ? string.Empty : inspection.InventoryFingerprint));
            output.AppendLine("PlanFingerprint: "
                              + (inspection == null ? string.Empty : inspection.PlanFingerprint));
            output.AppendLine("Target Staff: " + ExistingStaffCount);
            if (inspection != null)
            {
                for (int number = 1; number <= ExistingStaffCount; number++)
                {
                    string id = BuildStaffId(number);
                    int changed;
                    inspection.ChangedByStaff.TryGetValue(id, out changed);
                    output.AppendLine("- " + id + ": " + changed + " field(s)");
                }

                output.AppendLine("Total changed fields: " + inspection.ChangedFields);
                output.AppendLine("Name changes: " + inspection.NameChanges);
                output.AppendLine("Description changes: " + inspection.DescriptionChanges);
                output.AppendLine("Rank changes: " + inspection.RankChanges);
                output.AppendLine("Base speed changes: " + inspection.SpeedChanges);
                output.AppendLine("Role value changes: " + inspection.RoleValueChanges);
                output.AppendLine("Upgrade score/currency/cost changes: " + inspection.UpgradeChanges);
                output.AppendLine("Unsupported fields: " + inspection.UnsupportedFields);
                output.AppendLine("Skill reference changes planned: 0");
                output.AppendLine("Visual reference changes planned: 0");
                output.AppendLine("GUID changes planned: 0");
                output.AppendLine("STAFF02 array slots: " + inspection.Staff02LevelCount);
                output.AppendLine("STAFF02 slot 6 preservation: "
                                  + (inspection.DataAudit != null
                                     && inspection.DataAudit.Staff02Slot6Preserved
                                       ? "PASS"
                                       : "FAIL"));
                output.AppendLine("Skill files fingerprint: " + inspection.SkillFilesFingerprint);
                output.AppendLine("Authoritative Protected Contract: "
                                  + (inspection.PreservationBaselineValid ? "PASS" : "FAIL"));
                output.AppendLine(
                    "Protected Projection Algorithm: " + ProtectedProjectionAlgorithmVersion);
                if (inspection.DataAudit != null)
                {
                    bool dataIntegrityPassed = inspection.State == MigrationState.READY_TO_APPLY
                                               || inspection.State == MigrationState.ALREADY_APPLIED;
                    output.AppendLine(
                        "Migration Data Integrity: " + (dataIntegrityPassed ? "PASS" : "FAIL"));
                    output.AppendLine(
                        "Managed Target Match: "
                        + inspection.DataAudit.ManagedTargetMatchedCount + "/"
                        + inspection.DataAudit.ManagedTargetExpectedCount);
                    output.AppendLine(
                        "Protected Preservation: "
                        + inspection.DataAudit.ProtectedStaffCount + "/" + ExistingStaffCount);
                    for (int index = 0; index < inspection.DataAudit.Warnings.Count; index++)
                    {
                        output.AppendLine("WARNING: " + inspection.DataAudit.Warnings[index]);
                    }
                }

                if (inspection.RepositoryAudit != null)
                {
                    RepositoryHygieneAudit repository = inspection.RepositoryAudit;
                    output.AppendLine("Repository Hygiene: " + repository.State);
                    output.AppendLine(
                        "Git Changed Paths: " + repository.TotalChangedPathCount
                        + " (working tree " + repository.WorkingTreeChangedPathCount
                        + ", staged " + repository.StagedChangedPathCount
                        + ", untracked " + repository.UntrackedPathCount + ")");
                    output.AppendLine(
                        "A1-Owned Changed Paths: " + repository.A1OwnedChangedPaths.Count);
                    output.AppendLine(
                        "Allowed Editor Changed Count: "
                        + repository.AllowedEditorChangedCount);
                    output.AppendLine(
                        "Changed Staff Asset Count: "
                        + repository.ChangedStaffAssetCount);
                    output.AppendLine(
                        "External Changed Paths: " + repository.ExternalChangedPaths.Count);
                    for (int index = 0; index < repository.ExternalChangedPaths.Count; index++)
                    {
                        output.AppendLine(
                            "External Path: " + repository.ExternalChangedPaths[index]);
                    }

                    output.AppendLine(
                        "Missing Expected A1 Paths: " + repository.MissingExpectedA1Paths.Count);
                    for (int index = 0; index < repository.MissingExpectedA1Paths.Count; index++)
                    {
                        output.AppendLine(
                            "Missing A1 Path: " + repository.MissingExpectedA1Paths[index]);
                    }

                    output.AppendLine("Apply Readiness: " + repository.ApplyReadiness);
                    output.AppendLine("Commit Readiness: " + repository.CommitReadiness);
                    output.AppendLine("Git diff check: "
                                      + (repository.DiffCheckPassed ? "PASS" : "FAIL"));
                    for (int index = 0; index < repository.Warnings.Count; index++)
                    {
                        output.AppendLine("WARNING: " + repository.Warnings[index]);
                    }

                    for (int index = 0; index < repository.Errors.Count; index++)
                    {
                        output.AppendLine("REPOSITORY ERROR: " + repository.Errors[index]);
                    }
                }

                output.AppendLine("Preview Verdict: " + inspection.PreviewVerdict);
                output.AppendLine("Asset write: 0");
                for (int index = 0; index < inspection.Reasons.Count; index++)
                {
                    output.AppendLine("STATE: " + inspection.Reasons[index]);
                }
            }

            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            bool passed = inspected
                          && inspection != null
                          && inspection.PreviewVerdict != PreviewVerdict.FAIL
                          && errors.Count == 0;
            PreviewVerdict finalVerdict = passed
                ? inspection.PreviewVerdict
                : PreviewVerdict.FAIL;
            output.AppendLine(
                "EXISTING STAFF V18 DATA MIGRATION " + mode + ": "
                + finalVerdict);
            if (passed && finalVerdict == PreviewVerdict.PASS_WITH_REPOSITORY_WARNING)
            {
                Debug.LogWarning(output.ToString());
            }
            else if (passed)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private enum MigrationState
        {
            READY_TO_APPLY,
            ALREADY_APPLIED,
            PARTIAL_MIGRATION_STATE,
            STATE_CLASSIFIER_INCONSISTENT,
            INVALID
        }

        private enum RepositoryHygieneState
        {
            CLEAN,
            A1_EXPECTED_CHANGES_ONLY,
            EXTERNAL_CHANGES_PRESENT,
            A1_SCOPE_ANOMALY
        }

        private enum PreviewVerdict
        {
            PASS,
            PASS_WITH_REPOSITORY_WARNING,
            FAIL
        }

        private enum ApplyReadiness
        {
            READY,
            NOT_APPLICABLE_ALREADY_APPLIED,
            BLOCKED_BY_REPOSITORY_HYGIENE,
            BLOCKED_BY_DATA_STATE
        }

        private enum CommitReadiness
        {
            READY,
            NOT_APPLICABLE_CLEAN,
            BLOCKED_BY_EXTERNAL_CHANGES,
            BLOCKED_BY_A1_SCOPE_ANOMALY,
            BLOCKED_BY_STAGED_CHANGES,
            BLOCKED_BY_DATA_STATE
        }

        private enum ResolveFailure
        {
            NONE,
            UNSUPPORTED_FIELD,
            MISSING_PROPERTY
        }

        private sealed class RoleDefinition
        {
            internal readonly string ConcreteTypeName;
            internal readonly string LevelArrayPath;
            internal readonly HashSet<string> AllowedRoleFields;

            internal RoleDefinition(
                string concreteTypeName,
                string levelArrayPath,
                params string[] allowedRoleFields)
            {
                ConcreteTypeName = concreteTypeName;
                LevelArrayPath = levelArrayPath;
                AllowedRoleFields = new HashSet<string>(
                    allowedRoleFields,
                    StringComparer.Ordinal);
            }
        }

        private sealed class TargetBaseline
        {
            internal readonly string StaffId;
            internal readonly string AssetPath;
            internal readonly string AssetGuid;
            internal readonly string ScriptGuid;
            internal readonly string MetaSha256;

            internal TargetBaseline(
                string staffId,
                string assetGuid,
                string scriptGuid,
                string metaSha256)
            {
                StaffId = staffId;
                AssetPath = "Assets/Resources/StaffData/" + staffId + ".asset";
                AssetGuid = assetGuid;
                ScriptGuid = scriptGuid;
                MetaSha256 = metaSha256;
            }
        }

        private sealed class PostApplyValidationResult
        {
            internal bool Passed;
            internal bool Finalized;
            internal readonly List<string> Info = new List<string>();
            internal readonly List<string> Warnings = new List<string>();
            internal readonly List<string> Errors = new List<string>();
            internal readonly List<PostApplyCheckResult> Checks =
                new List<PostApplyCheckResult>();
            internal MigrationInspection AppliedInspection;
            internal MigrationDataAudit DataAudit;
            internal Staff32SpotCheckResult Staff32SpotCheck;
            internal int MatchedTargetFields;
            internal int TotalTargetFields;
            internal int PreservedStaff;
            internal bool RequiresRollback;
            internal bool ClassifierInconsistent;

            internal void AddCheck(string key, string name, bool actual)
            {
                Checks.Add(new PostApplyCheckResult(key, name, actual));
            }
        }

        private sealed class PostApplyCheckResult
        {
            internal readonly string Key;
            internal readonly string Name;
            internal readonly bool Actual;
            internal readonly bool Passed;

            internal PostApplyCheckResult(string key, string name, bool actual)
            {
                Key = key;
                Name = name;
                Actual = actual;
                Passed = actual;
            }
        }

        private sealed class Staff32SpotCheckResult
        {
            internal bool Passed;
            internal readonly List<string> Details = new List<string>();
            internal readonly List<string> Errors = new List<string>();
        }

        private sealed class MigrationDataAudit
        {
            internal bool OfficialSnapshotPassed;
            internal bool ManagedTargetMatch;
            internal int ManagedTargetExpectedCount;
            internal int ManagedTargetMatchedCount;
            internal bool ProtectedContractPassed;
            internal int ProtectedStaffCount;
            internal bool Staff02Slot6Preserved;
            internal bool GuidPassed;
            internal bool ScriptPassed;
            internal bool SkillPassed;
            internal bool VisualPassed;
            internal bool MetaPassed;
            internal bool UnexpectedAssetsZero;
            internal bool InventoryBuilt;
            internal bool PlanBuilt;
            internal bool StructuralErrorsZero;
            internal bool DeterministicStatePassed;
            internal bool OfficialSpotChecksPassed;
            internal bool Staff32SpotCheckPassed;
            internal bool ClassifierInconsistent;
            internal MigrationState State = MigrationState.INVALID;
            internal Staff32SpotCheckResult Staff32SpotCheck;
            internal readonly List<string> Info = new List<string>();
            internal readonly List<string> Warnings = new List<string>();
            internal readonly List<string> Errors = new List<string>();
        }

        private sealed class RepositoryHygieneAudit
        {
            internal RepositoryHygieneState State = RepositoryHygieneState.A1_SCOPE_ANOMALY;
            internal ApplyReadiness ApplyReadiness = ApplyReadiness.BLOCKED_BY_DATA_STATE;
            internal CommitReadiness CommitReadiness = CommitReadiness.BLOCKED_BY_DATA_STATE;
            internal bool DiffCheckPassed;
            internal int TotalChangedPathCount;
            internal int WorkingTreeChangedPathCount;
            internal int StagedChangedPathCount;
            internal int UntrackedPathCount;
            internal int AllowedEditorChangedCount;
            internal int ChangedStaffAssetCount;
            internal readonly List<string> A1OwnedChangedPaths = new List<string>();
            internal readonly List<string> ExternalChangedPaths = new List<string>();
            internal readonly List<string> MissingExpectedA1Paths = new List<string>();
            internal readonly List<string> StagedPaths = new List<string>();
            internal readonly List<string> Info = new List<string>();
            internal readonly List<string> Warnings = new List<string>();
            internal readonly List<string> Errors = new List<string>();
        }

        private sealed class GitChangedPathCollection
        {
            internal readonly List<string> WorkingTree = new List<string>();
            internal readonly List<string> Staged = new List<string>();
            internal readonly List<string> Untracked = new List<string>();
            internal readonly List<string> All = new List<string>();
        }

        private sealed class RawRestoreFile
        {
            internal readonly string Path;
            internal readonly byte[] Bytes;
            internal readonly string ExpectedSha256;

            internal RawRestoreFile(string path, byte[] bytes, string expectedSha256)
            {
                Path = path;
                Bytes = bytes == null ? null : (byte[])bytes.Clone();
                ExpectedSha256 = expectedSha256 ?? string.Empty;
            }
        }

        private sealed class RollbackWriteLease
        {
            internal readonly RawRestoreFile File;
            internal readonly FileStream Stream;

            internal RollbackWriteLease(RawRestoreFile file, FileStream stream)
            {
                File = file;
                Stream = stream;
            }
        }

        private sealed class PostApplyValidationException : Exception
        {
            internal readonly PostApplyValidationResult Result;

            internal PostApplyValidationException(PostApplyValidationResult result)
                : base("Post-Apply validation failed")
            {
                Result = result;
            }
        }

        private sealed class RollbackCapabilityException : Exception
        {
            internal RollbackCapabilityException(IReadOnlyList<string> errors)
                : base(
                    "ROLLBACK_CAPABILITY_PREFLIGHT_FAILED: "
                    + (errors == null ? string.Empty : string.Join(" | ", errors)))
            {
            }
        }

        private sealed class SimulatedWin32IOException : IOException
        {
            internal SimulatedWin32IOException(int win32Code, string message)
                : base(message)
            {
                HResult = unchecked((int)(0x80070000u | (uint)win32Code));
            }
        }

        private sealed class MigrationInspection
        {
            internal MigrationState State = MigrationState.INVALID;
            internal string GitBranch = string.Empty;
            internal string GitHead = string.Empty;
            internal bool BranchSafetyPassed;
            internal bool RequiredBaseCommitIsAncestor;
            internal string PlanStateMarker = string.Empty;
            internal string OfficialFingerprint = string.Empty;
            internal string InventoryFingerprint = string.Empty;
            internal string PlanFingerprint = string.Empty;
            internal string SkillFilesFingerprint = string.Empty;
            internal StaffDataAssetInventorySnapshot Inventory;
            internal StaffDataDryRunPlanSnapshot Plan;
            internal MigrationDataAudit DataAudit;
            internal RepositoryHygieneAudit RepositoryAudit;
            internal PreviewVerdict PreviewVerdict = PreviewVerdict.FAIL;
            internal readonly Dictionary<string, int> ChangedByStaff =
                new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly List<string> Reasons = new List<string>();
            internal int ChangedFields;
            internal int NameChanges;
            internal int DescriptionChanges;
            internal int RankChanges;
            internal int SpeedChanges;
            internal int RoleValueChanges;
            internal int UpgradeChanges;
            internal int UnsupportedFields;
            internal int Staff02LevelCount;
            internal bool RequiredPropertyMissing;
            internal bool PreservationBaselineValid;
        }

        private sealed class MigrationBackup
        {
            internal readonly Dictionary<string, TargetBackup> Targets =
                new Dictionary<string, TargetBackup>(StringComparer.Ordinal);
            internal string InventoryFingerprint = string.Empty;
            internal string PlanFingerprint = string.Empty;
            internal string SkillFilesFingerprint = string.Empty;
        }

        private sealed class TargetBackup
        {
            internal readonly string AssetPath;
            internal readonly byte[] AssetBytes;
            internal readonly byte[] MetaBytes;
            internal readonly string AssetSha256;
            internal readonly string MetaSha256;
            internal readonly string AssetGuid;
            internal readonly string ScriptGuid;
            internal readonly string FullSerializedSnapshot;
            internal readonly ProtectedPreservationSnapshot ProtectedSnapshot;
            internal readonly string SkillSnapshot;
            internal readonly string VisualSnapshot;
            internal readonly IReadOnlyList<string> LevelElementSnapshots;
            internal readonly int LevelCount;
            internal readonly HashSet<string> MutableSerializedPaths;

            internal TargetBackup(
                string assetPath,
                byte[] assetBytes,
                byte[] metaBytes,
                string assetSha256,
                string metaSha256,
                string assetGuid,
                string scriptGuid,
                string fullSerializedSnapshot,
                ProtectedPreservationSnapshot protectedSnapshot,
                string skillSnapshot,
                string visualSnapshot,
                IReadOnlyList<string> levelElementSnapshots,
                int levelCount,
                IEnumerable<string> mutableSerializedPaths)
            {
                AssetPath = assetPath;
                AssetBytes = (byte[])assetBytes.Clone();
                MetaBytes = (byte[])metaBytes.Clone();
                AssetSha256 = assetSha256;
                MetaSha256 = metaSha256;
                AssetGuid = assetGuid;
                ScriptGuid = scriptGuid;
                FullSerializedSnapshot = fullSerializedSnapshot;
                ProtectedSnapshot = protectedSnapshot;
                SkillSnapshot = skillSnapshot;
                VisualSnapshot = visualSnapshot;
                LevelElementSnapshots = new List<string>(levelElementSnapshots).AsReadOnly();
                LevelCount = levelCount;
                MutableSerializedPaths = new HashSet<string>(
                    mutableSerializedPaths,
                    StringComparer.Ordinal);
            }
        }

        private sealed class ProtectedPreservationSnapshot
        {
            internal readonly SortedDictionary<string, string> Values;

            internal ProtectedPreservationSnapshot(
                IDictionary<string, string> values)
            {
                Values = new SortedDictionary<string, string>(
                    values,
                    StringComparer.Ordinal);
            }
        }
    }
}
