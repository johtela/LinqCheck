$csweavedir = "..\LiterateProgramming\bin\Debug"

function GenerateDocs ()
{
	& {
		$ErrorActionPreference = "SilentlyContinue"
		& $csweavedir\csweave.exe src\*.cs Examples\*.cs *.md -s LinqCheck.sln -o md -f md -t -v
	}
}

Get-Location
GenerateDocs