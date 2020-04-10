$NomadVersion = v0.11.0

function Test-GoVersion {
    $version = iex "go version"
    Write-Output "using go version: $version"
    return $version.Contains("go version")
}

function Test-NomadVersion {
    $version = iex "nomad -version"
    return $version.Contains($NomadVersion)
}


if (-not (Test-Path $ENV:GOPATH))
{
    Write-Error "GOPATH must be set"
    exit 1
}

if (-not (Test-GoVersion))
{
    Write-Error "Go must be installed"
    exit 1
}

if (Test-NomadVersion)
{
    Write-Output "Nomad $NomadVersion already available"
    exit
}

Write-Output "Will clone Nomad $NomadVersion and build it with Go $GoVersion"

Write-Output "Cloning Nomad $NomadVersion..."
$ImportPath = "github.com/hashicorp/nomad"
$WorkTree = "$GOPATH/src/$ImportPath"
Remove-Item -Recurse -Force -Path $WorkTree
git clone --depth 1 --branch $NomadVersion "https://$ImportPath" $WorkTree

Write-Output "Building Nomad..."
go install -tags nomad_test $ImportPath

if (-not (Test-NomadVersion))
{
    Write-Error "Nomad $NomadVersion is not available even after provisioning"
    exit 1
}

Write-Output "Nomad $NomadVersion is available"