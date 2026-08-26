using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal enum StaffOfficialDataSourceKind
    {
        Canonical,
        SessionOverride
    }

    internal static class StaffOfficialDataPathResolver
    {
        internal const string PolicyMarker = "OFFICIALDATA_V8_POLICY_2026_08_21";
        internal const string CanonicalRelativeFolder = "OfficialData/Staff/V18";

        private const string SessionOverrideKey =
            "PandaRestaurant.StaffOfficialData.SessionOverrideFolder";
        private const string MenuRoot =
            "Tools/Panda Restaurant/Staff/Official Data/";

        internal static string ProjectRoot
        {
            get
            {
                DirectoryInfo assetsParent = Directory.GetParent(Application.dataPath);
                string root = assetsParent != null ? assetsParent.FullName : Application.dataPath;
                return Path.GetFullPath(root);
            }
        }

        internal static string CanonicalFolder
        {
            get
            {
                return Path.GetFullPath(Path.Combine(ProjectRoot, CanonicalRelativeFolder));
            }
        }

        internal static bool HasSessionOverride
        {
            get
            {
                return !string.IsNullOrWhiteSpace(
                    SessionState.GetString(SessionOverrideKey, string.Empty));
            }
        }

        internal static bool TryResolveActiveFolder(
            out string folder,
            out StaffOfficialDataSourceKind sourceKind,
            out string error)
        {
            folder = string.Empty;
            sourceKind = StaffOfficialDataSourceKind.Canonical;
            error = string.Empty;

            string sessionOverride = SessionState.GetString(SessionOverrideKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(sessionOverride))
            {
                sourceKind = StaffOfficialDataSourceKind.SessionOverride;
                if (!TryNormalizeExistingDirectory(sessionOverride, out folder, out error))
                {
                    error = "Session Override 공식 데이터 폴더가 올바르지 않습니다: " + error;
                    return false;
                }

                return true;
            }

            string canonicalFolder;
            try
            {
                canonicalFolder = CanonicalFolder;
            }
            catch (Exception exception)
            {
                error = "Canonical 공식 데이터 경로를 계산하지 못했습니다: " + exception.Message;
                return false;
            }

            if (!IsWithinProjectRoot(canonicalFolder))
            {
                error = "Canonical 공식 데이터 폴더가 ProjectRoot 내부가 아닙니다: "
                        + canonicalFolder;
                return false;
            }

            if (!Directory.Exists(canonicalFolder))
            {
                error = "Canonical 공식 데이터 폴더가 없습니다: " + canonicalFolder;
                return false;
            }

            folder = canonicalFolder;
            return true;
        }

        internal static void ClearSessionOverride()
        {
            SessionState.EraseString(SessionOverrideKey);
        }

        [MenuItem(MenuRoot + "Select Session Override Folder")]
        private static void SelectSessionOverrideFolder()
        {
            string selectedFolder = EditorUtility.OpenFolderPanel(
                "Staff 공식 데이터 Session Override 폴더 선택",
                CanonicalFolder,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                return;
            }

            string normalizedFolder;
            string error;
            if (!TryNormalizeExistingDirectory(selectedFolder, out normalizedFolder, out error))
            {
                Debug.LogError(
                    "[Staff Official Data Path Resolver]\n"
                    + "Session Override를 설정하지 않았습니다: " + error);
                return;
            }

            SessionState.SetString(SessionOverrideKey, normalizedFolder);
            Debug.LogWarning(
                "[Staff Official Data Path Resolver]\n"
                + "NON_CANONICAL_OVERRIDE\n"
                + "Active Folder: " + normalizedFolder + "\n"
                + "SourceKind: " + StaffOfficialDataSourceKind.SessionOverride);
        }

        [MenuItem(MenuRoot + "Clear Session Override Folder")]
        private static void ClearSessionOverrideFolder()
        {
            ClearSessionOverride();
            Debug.Log(
                "[Staff Official Data Path Resolver]\n"
                + "Session Override를 해제했습니다.\n"
                + "다음 검증은 Canonical Folder를 사용합니다: " + CanonicalFolder);
        }

        [MenuItem(MenuRoot + "Log Active Folder")]
        private static void LogActiveFolder()
        {
            string folder;
            StaffOfficialDataSourceKind sourceKind;
            string error;
            if (!TryResolveActiveFolder(out folder, out sourceKind, out error))
            {
                Debug.LogError(
                    "[Staff Official Data Path Resolver]\n"
                    + "Active Folder 확인 실패: " + error + "\n"
                    + "Canonical Folder: " + SafeCanonicalFolder() + "\n"
                    + "Session Override 존재: " + HasSessionOverride);
                return;
            }

            Debug.Log(
                "[Staff Official Data Path Resolver]\n"
                + "Active Folder: " + folder + "\n"
                + "SourceKind: " + sourceKind + "\n"
                + "Canonical Folder: " + CanonicalFolder + "\n"
                + "Session Override 존재: " + HasSessionOverride);
        }

        private static bool TryNormalizeExistingDirectory(
            string candidate,
            out string normalizedFolder,
            out string error)
        {
            normalizedFolder = string.Empty;
            error = string.Empty;
            try
            {
                normalizedFolder = Path.GetFullPath(candidate);
            }
            catch (Exception exception)
            {
                error = "경로를 정규화하지 못했습니다: " + exception.Message;
                return false;
            }

            if (!Directory.Exists(normalizedFolder))
            {
                error = "폴더가 존재하지 않습니다: " + normalizedFolder;
                return false;
            }

            return true;
        }

        private static bool IsWithinProjectRoot(string candidate)
        {
            string projectRoot = ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedCandidate = Path.GetFullPath(candidate)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(projectRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string projectPrefix = projectRoot + Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeCanonicalFolder()
        {
            try
            {
                return CanonicalFolder;
            }
            catch (Exception exception)
            {
                return "확인 실패: " + exception.Message;
            }
        }
    }
}
