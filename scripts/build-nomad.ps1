$env:GIT_REDIRECT_STDERR = '2>&1'
$NomadVersion = "v0.11.0"

function Test-GoVersion {
    try
    {
        $version = iex "go version" -ErrorAction SilentlyContinue
        Write-Output "using go version: $version"
        return $version.Contains("go version")

    }
    catch
    {
        return $false
    }
}

if (-not (Test-GoVersion))
{
    Write-Error "Go must be installed"
    exit 1
}

$GOPATH = go env GOPATH

$GOBIN = go env GOBIN
if (-not $GOBIN)
{
    $GOBIN = "$GOPATH/bin"
}

function Test-NomadVersion {
    try {
        $version = iex "$GOBIN/nomad -version" -ErrorAction SilentlyContinue
        return $version.Contains($NomadVersion)
    }
    catch
    {
        return $false
    }
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

if (Test-Path $WorkTree) {
    Remove-Item -Recurse -Force -Path $WorkTree
}
git clone --depth 1 --branch $NomadVersion "https://$ImportPath" $WorkTree

Write-Output "Building Nomad..."
go install -tags nomad_test $ImportPath

if (-not (Test-NomadVersion))
{
    Write-Error "Nomad $NomadVersion is not available even after provisioning"
    exit 1
}

Write-Output "Nomad $NomadVersion is available"