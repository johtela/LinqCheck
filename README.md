[![Build status](https://ci.appveyor.com/api/projects/status/ujv44tl9pmumivym?svg=true)](https://ci.appveyor.com/project/johtela/linqcheck)
[![NuGet Version](https://img.shields.io/nuget/v/LinqCheck.svg)](https://www.nuget.org/packages/LinqCheck)

# Property Based Testing with LINQ Expressions

[Property based testing] lifts the power of automated tests to a new level. You can
find more bugs, and get more confidence on your code using it than with traditional
unit tests.

LinqCheck is a property based testing library for C#. It implements all of the 
concepts present in [QuickCheck], but instead of mimicking the API of QuickCheck
it represents properties and generators as LINQ expressions. 

Tutorials on how to use the library and information about the ideas behind it are 
available at:

**<https://johtela.github.io/LinqCheck/>**

# Installation

The library targets .NET standard 2.0, and is distributed as a [Nuget package]
which you can find in <https://www.nuget.org>.

[QuickCheck]: https://en.wikipedia.org/wiki/QuickCheck
[Nuget package]: https://www.nuget.org/packages/LinqCheck/
[Property based testing]: https://hypothesis.works/articles/what-is-property-based-testing/