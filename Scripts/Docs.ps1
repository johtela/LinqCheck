$csweavedir = "..\LiterateProgramming\bin\Debug"

function GenerateDocs ()
{
	& {
		$ErrorActionPreference = "SilentlyContinue"
		& $csweavedir\csweave.exe src\*.cs Examples\*.cs *.md -s LinqCheck.sln -o docs -f html -tvu
	}
}

GenerateDocs