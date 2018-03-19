$csweavedir = "$env:LOCALAPPDATA\csweave"

function GenerateDocs ()
{
	& {
		$ErrorActionPreference = "SilentlyContinue"
		& $csweavedir\csweave.exe src\*.cs Examples\*.cs Examples\UITests\*.cs *.md -s LinqCheck.sln -o docs -f html -tvu
	}
}

GenerateDocs