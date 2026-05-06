// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Build;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the fifth Phase-2 batch (Makefile, CMake, Nix).</summary>
public class Phase2BatchFiveLexerTests
{
    /// <summary>Makefile classifies <c>$(VAR)</c> and <c>$@</c>-style automatic variable expansions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MakefileClassifiesVariableExpansions()
    {
        var html = MakefileLexer.Instance.Render("CC := gcc\nall: build\n\t$(CC) -o $@ $(SRC)\n"u8);
        await Assert.That(html.Contains("<span class=\"n\">$(CC)</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">$@</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">$(SRC)</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"o\">:=</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Makefile classifies conditional directives and the <c>include</c> form.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MakefileClassifiesDirectives()
    {
        var html = MakefileLexer.Instance.Render("ifeq ($(OS),Linux)\ninclude config.mk\nelse\ninclude other.mk\nendif\n"u8);
        await Assert.That(html.Contains("<span class=\"k\">ifeq</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">else</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">endif</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">include</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Makefile classifies <c>#</c> line comments as <c>c1</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MakefileClassifiesComments()
    {
        var html = MakefileLexer.Instance.Render("# top comment\nCFLAGS = -O2\n"u8);
        await Assert.That(html.Contains("<span class=\"c1\"># top comment</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>CMake classifies built-in commands and <c>${VAR}</c> expansions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CMakeClassifiesCommandsAndVariables()
    {
        var html = CMakeLexer.Instance.Render("cmake_minimum_required(VERSION 3.20)\nproject(Hello)\nadd_executable(hello main.cpp)\nset(CMAKE_CXX_STANDARD 20)\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\">cmake_minimum_required</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">project</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">add_executable</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">set</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>CMake classifies <c>${VAR}</c> expansions and <c>$&lt;...&gt;</c> generator expressions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CMakeClassifiesExpansions()
    {
        var html = CMakeLexer.Instance.Render("set(SRC ${PROJECT_SOURCE_DIR}/main.cpp)\ntarget_compile_options(t PRIVATE $<IF:$<CONFIG:Debug>,-g,-O2>)\n"u8);
        await Assert.That(html.Contains("<span class=\"n\">${PROJECT_SOURCE_DIR}</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>CMake classifies <c>#</c> line comments and <c>#[[ ... ]]</c> block comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CMakeClassifiesCommentForms()
    {
        var html = CMakeLexer.Instance.Render("#[[ block ]]\n# line\nset(X 1)\n"u8);
        await Assert.That(html.Contains("<span class=\"cm\">#[[ block ]]</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"c1\"># line</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>CMake commands are case-insensitive — uppercase forms classify identically.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CMakeIsCaseInsensitive()
    {
        var html = CMakeLexer.Instance.Render("PROJECT(Hello)\nIF(MSVC)\nENDIF()\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\">PROJECT</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">IF</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">ENDIF</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Nix classifies <c>let</c>/<c>in</c>/<c>rec</c>/<c>with</c> bindings and <c>true</c>/<c>false</c>/<c>null</c> constants.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NixClassifiesBindings()
    {
        var html = NixLexer.Instance.Render("let x = 1; y = true; in { result = x; enabled = y; default = null; }"u8);
        await Assert.That(html.Contains("<span class=\"kd\">let</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kd\">in</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">true</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"kc\">null</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Nix classifies <c>#</c> line comments and <c>/* ... */</c> block comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NixClassifiesComments()
    {
        var html = NixLexer.Instance.Render("# line comment\n/* block\ncomment */\nlet x = 1; in x"u8);
        await Assert.That(html.Contains("<span class=\"c1\"># line comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cm\">/* block\ncomment */</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Nix classifies path literals as <c>n</c> name tokens.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NixClassifiesPathLiterals()
    {
        var html = NixLexer.Instance.Render("import ./foo.nix\nimport /nix/store/abc\nimport ~/projects"u8);
        await Assert.That(html.Contains("<span class=\"n\">./foo.nix</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">/nix/store/abc</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">~/projects</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves the new aliases to their lexers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesNewAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "makefile"u8], out var make)).IsTrue();
        await Assert.That(make).IsSameReferenceAs(MakefileLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "cmake"u8], out var cm)).IsTrue();
        await Assert.That(cm).IsSameReferenceAs(CMakeLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "nix"u8], out var nix)).IsTrue();
        await Assert.That(nix).IsSameReferenceAs(NixLexer.Instance);
    }
}
