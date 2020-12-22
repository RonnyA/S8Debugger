# S8Debugger
Debugger for SLEDE8

Koden kunne ikke vært implementert uten PST sin Slede8 referanseimplementasjon.
Både assembler, instruction-parsing (brukt av disassembler) og runtime engine er en C# versjon av PST sin TypeScript implementasjon

Se https://github.com/pstnorge/slede8 for spesifikasjon

Denne koden kan fritt kopieres og brukes, pull requests er velkommen.

Debuggern kan kjøres om hverandre i Command mode og GUI mode

![Command mode](Doc/command-mode.png)

![GUI mode](Doc/gui-mode.png)


Denne koden fungerer like fint på Windows som under Linux.
Kravene er at du har .NET 5 installert.

Installer .NET5 herifra https://dotnet.microsoft.com/download/dotnet/5.0

Deretter cloner du (eller laster ned Git repo)

Og så kjører du kommando "dotnet run" inne i mappen som dette Git repo er lastet ned til.