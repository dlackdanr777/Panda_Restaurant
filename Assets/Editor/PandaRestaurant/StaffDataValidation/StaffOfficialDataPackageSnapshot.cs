using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal sealed class StaffOfficialDataPackageSnapshot
    {
        private readonly IReadOnlyList<StaffOfficialFileSnapshot> _filesInOfficialOrder;
        private readonly IReadOnlyDictionary<string, StaffOfficialFileSnapshot> _filesByKey;

        internal string SourceFolder { get; }
        internal string GitBranch { get; }
        internal string GitHead { get; }
        internal string PackageFingerprint { get; }
        internal int OfficialFileCount { get { return _filesInOfficialOrder.Count; } }
        internal IReadOnlyList<StaffOfficialFileSnapshot> FilesInOfficialOrder
        {
            get { return _filesInOfficialOrder; }
        }

        internal IReadOnlyDictionary<string, StaffOfficialFileSnapshot> FilesByKey
        {
            get { return _filesByKey; }
        }

        internal StaffOfficialDataPackageSnapshot(
            string sourceFolder,
            string gitBranch,
            string gitHead,
            IEnumerable<StaffOfficialFileSnapshot> filesInOfficialOrder)
        {
            if (filesInOfficialOrder == null)
            {
                throw new ArgumentNullException(nameof(filesInOfficialOrder));
            }

            SourceFolder = sourceFolder ?? string.Empty;
            GitBranch = gitBranch ?? string.Empty;
            GitHead = gitHead ?? string.Empty;

            List<StaffOfficialFileSnapshot> orderedFiles = new List<StaffOfficialFileSnapshot>();
            Dictionary<string, StaffOfficialFileSnapshot> filesByKey =
                new Dictionary<string, StaffOfficialFileSnapshot>(StringComparer.Ordinal);
            foreach (StaffOfficialFileSnapshot file in filesInOfficialOrder)
            {
                if (file == null)
                {
                    throw new ArgumentException("Official file snapshot cannot be null.", nameof(filesInOfficialOrder));
                }

                orderedFiles.Add(file);
                filesByKey.Add(file.Key, file);
            }

            _filesInOfficialOrder = new ReadOnlyCollection<StaffOfficialFileSnapshot>(orderedFiles);
            _filesByKey = new ReadOnlyDictionary<string, StaffOfficialFileSnapshot>(filesByKey);
            PackageFingerprint = BuildPackageFingerprint(_filesInOfficialOrder);
        }

        internal bool TryGetFile(string key, out StaffOfficialFileSnapshot file)
        {
            return _filesByKey.TryGetValue(key, out file);
        }

        private static string BuildPackageFingerprint(
            IReadOnlyList<StaffOfficialFileSnapshot> filesInOfficialOrder)
        {
            StringBuilder input = new StringBuilder();
            for (int index = 0; index < filesInOfficialOrder.Count; index++)
            {
                StaffOfficialFileSnapshot file = filesInOfficialOrder[index];
                AppendFingerprintPart(input, file.Key);
                AppendFingerprintPart(input, file.Sha256);
                AppendFingerprintPart(input, file.EncodingLabel);
                AppendFingerprintPart(
                    input,
                    file.ExpectedPhysicalLineCount.ToString(CultureInfo.InvariantCulture));
                AppendFingerprintPart(
                    input,
                    file.ActualPhysicalLineCount.ToString(CultureInfo.InvariantCulture));
                input.Append('\n');
            }

            byte[] bytes = Encoding.UTF8.GetBytes(input.ToString());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder fingerprint = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    fingerprint.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return fingerprint.ToString();
            }
        }

        private static void AppendFingerprintPart(StringBuilder input, string value)
        {
            string safeValue = value ?? string.Empty;
            input.Append(safeValue.Length.ToString(CultureInfo.InvariantCulture));
            input.Append(':');
            input.Append(safeValue);
            input.Append('|');
        }
    }

    internal sealed class StaffOfficialFileSnapshot
    {
        private readonly IReadOnlyList<string> _headers;
        private readonly IReadOnlyList<IReadOnlyList<string>> _rows;

        internal string Key { get; }
        internal string DisplayName { get; }
        internal string SourcePath { get; }
        internal string Sha256 { get; }
        internal string EncodingLabel { get; }
        internal int ExpectedPhysicalLineCount { get; }
        internal int ActualPhysicalLineCount { get; }
        internal IReadOnlyList<string> Headers { get { return _headers; } }
        internal IReadOnlyList<IReadOnlyList<string>> Rows { get { return _rows; } }

        internal StaffOfficialFileSnapshot(
            string key,
            string displayName,
            string sourcePath,
            string sha256,
            string encodingLabel,
            int expectedPhysicalLineCount,
            int actualPhysicalLineCount,
            IEnumerable<string> headers,
            IEnumerable<IEnumerable<string>> rows)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            Sha256 = sha256 ?? string.Empty;
            EncodingLabel = encodingLabel ?? string.Empty;
            ExpectedPhysicalLineCount = expectedPhysicalLineCount;
            ActualPhysicalLineCount = actualPhysicalLineCount;

            List<string> headerCopy = new List<string>();
            foreach (string header in headers)
            {
                headerCopy.Add(header ?? string.Empty);
            }

            List<IReadOnlyList<string>> rowCopies = new List<IReadOnlyList<string>>();
            foreach (IEnumerable<string> row in rows)
            {
                if (row == null)
                {
                    throw new ArgumentException("Official file row cannot be null.", nameof(rows));
                }

                List<string> rowCopy = new List<string>();
                foreach (string value in row)
                {
                    rowCopy.Add(value ?? string.Empty);
                }

                rowCopies.Add(new ReadOnlyCollection<string>(rowCopy));
            }

            _headers = new ReadOnlyCollection<string>(headerCopy);
            _rows = new ReadOnlyCollection<IReadOnlyList<string>>(rowCopies);
        }
    }
}
