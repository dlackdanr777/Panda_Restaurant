[CmdletBinding()]
param([string]$FunctionProjectPath)

# Read-only prerequisite audit. No install, profile read/write, SDK login, restore,
# compilation, debug, deployment, network request, cleanup, or credential output.
$ErrorActionPreference = 'Stop'
$sourceRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($FunctionProjectPath)) { $FunctionProjectPath = $sourceRoot }
$projectPath = [System.IO.Path]::GetFullPath($FunctionProjectPath)
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $sourceRoot '..\..'))
$blockers = [System.Collections.Generic.List[string]]::new()

function Read-SafeXml([string]$Path) {
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        return $document
    }
    finally { $reader.Dispose() }
}

$cli = Get-Command backend -CommandType Application -ErrorAction SilentlyContinue
$dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
$sdks = @()
if ($null -ne $dotnet) {
    $sdks = @(& $dotnet.Source --list-sdks)
    if ($LASTEXITCODE -ne 0) { $blockers.Add('dotnet_sdk_inventory_failed') }
}
else { $blockers.Add('dotnet_not_found') }
if ($null -eq $cli) { $blockers.Add('backend_cli_not_found') }
if (-not ($sdks | Where-Object { $_ -match '^8\.' })) { $blockers.Add('dotnet_8_sdk_not_found') }

$projectFile = Join-Path $projectPath 'BackendFunction.csproj'
$functionManifest = Join-Path $projectPath 'backend-function.json'
$sdkManifest = Join-Path $projectPath 'backend-sdk.json'
$sdkDirectory = Join-Path $projectPath 'dll'
$projectExists = Test-Path -LiteralPath $projectFile -PathType Leaf
$functionManifestExists = Test-Path -LiteralPath $functionManifest -PathType Leaf
$sdkManifestExists = Test-Path -LiteralPath $sdkManifest -PathType Leaf
if (-not $projectExists) { $blockers.Add('official_function_project_missing') }
if (-not $functionManifestExists) { $blockers.Add('official_function_manifest_missing') }
if (-not $sdkManifestExists) { $blockers.Add('official_sdk_manifest_missing') }

$sdkAssemblies = @()
if (Test-Path -LiteralPath $sdkDirectory -PathType Container) {
    $sdkAssemblies = @(Get-ChildItem -LiteralPath $sdkDirectory -File -Filter '*.dll' | ForEach-Object {
        $assemblyVersion = $null
        try { $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version.ToString() }
        catch { }
        [PSCustomObject]@{
            Name = $_.Name
            AssemblyVersion = $assemblyVersion
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
}
if ($sdkAssemblies.Count -eq 0) { $blockers.Add('official_function_sdk_bundle_missing') }

$projectProperties = $null
$packagePins = @()
if ($projectExists) {
    $projectXml = Read-SafeXml $projectFile
    $projectProperties = [PSCustomObject]@{
        TargetFramework = @($projectXml.SelectNodes('//TargetFramework') | ForEach-Object { $_.InnerText })
        AssemblyName = @($projectXml.SelectNodes('//AssemblyName') | ForEach-Object { $_.InnerText })
        RootNamespace = @($projectXml.SelectNodes('//RootNamespace') | ForEach-Object { $_.InnerText })
        CopyLocalLockFileAssemblies = @($projectXml.SelectNodes('//CopyLocalLockFileAssemblies') | ForEach-Object { $_.InnerText })
    }
    $packagePins = @($projectXml.SelectNodes('//PackageReference') | ForEach-Object {
        [PSCustomObject]@{ Name = $_.GetAttribute('Include'); Version = $_.GetAttribute('Version') }
    })
    if ($projectProperties.TargetFramework.Count -ne 1 -or $projectProperties.TargetFramework[0] -ne 'net8.0') {
        $blockers.Add('function_target_must_be_net8_0')
    }
    if ($projectProperties.AssemblyName -notcontains 'BackendFunction' -or $projectProperties.RootNamespace -notcontains 'BackendFunction') {
        $blockers.Add('function_assembly_and_namespace_must_be_BackendFunction')
    }
    $jsonPins = @($packagePins | Where-Object { $_.Name -eq 'System.Text.Json' -and $_.Version -match '^9\.\d+\.\d+$' })
    if ($jsonPins.Count -ne 1 -or $projectProperties.CopyLocalLockFileAssemblies -notcontains 'true') {
        $blockers.Add('official_system_text_json_9x_pin_and_copylocal_must_be_verified')
    }
}

$sourceManifest = Join-Path $sourceRoot 'PopupStoreSources.props'
$sourceXml = Read-SafeXml $sourceManifest
$sources = @($sourceXml.SelectNodes('//Compile') | ForEach-Object {
    $relative = $_.GetAttribute('Include').Replace('$(MSBuildThisFileDirectory)', '')
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $sourceRoot $relative))
    if (-not $resolved.StartsWith($repositoryRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Source manifest points outside the original repository.'
    }
    $exists = Test-Path -LiteralPath $resolved -PathType Leaf
    if (-not $exists) { $blockers.Add('source_missing:' + $_.GetAttribute('Link')) }
    [PSCustomObject]@{
        Source = $resolved
        Exists = $exists
        Sha256 = if ($exists) { (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash } else { $null }
    }
})

# Even a green static preflight does not prove official SDK provenance, the actual
# compiler, permissions, transaction predicates, or wallet migration readiness.
$result = [PSCustomObject]@{
    CapturedUtc = [DateTime]::UtcNow.ToString('o')
    Check = 'read_only_function_prerequisites'
    Status = if ($blockers.Count -eq 0) { 'PREREQUISITES_PRESENT_BUILD_NOT_RUN' } else { 'BLOCKED' }
    FunctionBuild = 'NOT_RUN'
    ServerIntegration = 'NOT_RUN'
    ProjectPath = $projectPath
    CliPath = if ($null -ne $cli) { $cli.Source } else { $null }
    CliVersion = 'NOT_RUN_NO_EXECUTION_BY_THIS_AUDIT'
    DotnetSdks = $sdks
    OfficialProjectExists = $projectExists
    FunctionManifestExists = $functionManifestExists
    SdkManifestExists = $sdkManifestExists
    ProjectProperties = $projectProperties
    PackagePins = $packagePins
    SdkAssemblies = $sdkAssemblies
    Sources = $sources
    Blockers = @($blockers)
    CredentialFilesRead = 0
    RemoteCalls = 0
    FilesWritten = 0
}
$result | ConvertTo-Json -Depth 8
if ($blockers.Count -gt 0) { exit 2 }
