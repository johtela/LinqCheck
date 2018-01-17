$nugetdir = "..\..\NuGet"
$project = "ExtensionCord"
$version = "1.0.0"
$projectdir = "$nugetdir\$project"

function PackDebug ()
{
	& $nugetdir\nuget.exe pack $project.csproj
}

function PackRelease ()
{
	& $nugetdir\nuget.exe pack $project.csproj -properties Configuration=Release
}

function DeployLocal ()
{
	PackDebug
	if (Test-Path $projectdir)
	{
		Remove-Item $projectdir -Recurse
	}
	& $nugetdir\nuget.exe add "$project.$version.nupkg" -source $nugetdir
}

DeployLocal