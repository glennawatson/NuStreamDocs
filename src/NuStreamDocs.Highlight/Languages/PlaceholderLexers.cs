// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Placeholder lexers for languages Zensical / Pygments support but we
/// haven't fully ported yet. Each one registers under its real name so
/// fenced blocks render escaped and still get the <c>language-X</c>
/// CSS hook; bringing one online is a matter of replacing the
/// pass-through rule list with a real one.
/// </summary>
/// <remarks>
/// Languages here mirror the high-traffic subset of Pygments built-ins
/// commonly used in mkdocs-material docs. Add new ones by creating a
/// dedicated lexer class and registering it in <see cref="LexerRegistry.Build"/>.
/// </remarks>
public static class PlaceholderLexers
{
    /// <summary>Gets the Python lexer (placeholder).</summary>
    public static Lexer Python { get; } = PassThroughLexer.Create("python");

    /// <summary>Gets the TOML lexer (placeholder).</summary>
    public static Lexer Toml { get; } = PassThroughLexer.Create("toml");

    /// <summary>Gets the C lexer (placeholder).</summary>
    public static Lexer C { get; } = PassThroughLexer.Create("c");

    /// <summary>Gets the C++ lexer (placeholder).</summary>
    public static Lexer Cpp { get; } = PassThroughLexer.Create("cpp");

    /// <summary>Gets the F# lexer (placeholder).</summary>
    public static Lexer FSharp { get; } = PassThroughLexer.Create("fsharp");

    /// <summary>Gets the Go lexer (placeholder).</summary>
    public static Lexer Go { get; } = PassThroughLexer.Create("go");

    /// <summary>Gets the Rust lexer (placeholder).</summary>
    public static Lexer Rust { get; } = PassThroughLexer.Create("rust");

    /// <summary>Gets the Java lexer (placeholder).</summary>
    public static Lexer Java { get; } = PassThroughLexer.Create("java");

    /// <summary>Gets the Kotlin lexer (placeholder).</summary>
    public static Lexer Kotlin { get; } = PassThroughLexer.Create("kotlin");

    /// <summary>Gets the Swift lexer (placeholder).</summary>
    public static Lexer Swift { get; } = PassThroughLexer.Create("swift");

    /// <summary>Gets the Ruby lexer (placeholder).</summary>
    public static Lexer Ruby { get; } = PassThroughLexer.Create("ruby");

    /// <summary>Gets the PHP lexer (placeholder).</summary>
    public static Lexer Php { get; } = PassThroughLexer.Create("php");

    /// <summary>Gets the Lua lexer (placeholder).</summary>
    public static Lexer Lua { get; } = PassThroughLexer.Create("lua");

    /// <summary>Gets the SQL lexer (placeholder).</summary>
    public static Lexer Sql { get; } = PassThroughLexer.Create("sql");

    /// <summary>Gets the CSS lexer (placeholder).</summary>
    public static Lexer Css { get; } = PassThroughLexer.Create("css");

    /// <summary>Gets the Markdown lexer (placeholder).</summary>
    public static Lexer Markdown { get; } = PassThroughLexer.Create("markdown");

    /// <summary>Gets the Dockerfile lexer (placeholder).</summary>
    public static Lexer Dockerfile { get; } = PassThroughLexer.Create("dockerfile");

    /// <summary>Gets the INI / config lexer (placeholder).</summary>
    public static Lexer Ini { get; } = PassThroughLexer.Create("ini");

    /// <summary>Gets the Makefile lexer (placeholder).</summary>
    public static Lexer Makefile { get; } = PassThroughLexer.Create("makefile");

    /// <summary>Gets the PowerShell lexer (placeholder).</summary>
    public static Lexer PowerShell { get; } = PassThroughLexer.Create("powershell");

    /// <summary>Gets the Nix lexer (placeholder).</summary>
    public static Lexer Nix { get; } = PassThroughLexer.Create("nix");

    /// <summary>Gets the plain-text lexer.</summary>
    public static Lexer PlainText { get; } = PassThroughLexer.Create("text");
}
