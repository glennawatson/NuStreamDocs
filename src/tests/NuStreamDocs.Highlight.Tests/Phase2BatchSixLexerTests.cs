// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Asm;
using NuStreamDocs.Highlight.Languages.Functional;
using NuStreamDocs.Highlight.Languages.Markup;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Smoke tests for the sixth Phase-2 batch (templating engines + assembly dialects).</summary>
public class Phase2BatchSixLexerTests
{
    /// <summary>Jinja classifies <c>{% block %}</c> as a keyword and <c>{{ var }}</c> as a name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task JinjaClassifiesStatementAndExpression()
    {
        var html = JinjaLexer.Instance.Render("{% if user %}\nHello {{ user.name }}!\n{# greeting #}\n{% endif %}"u8);
        await Assert.That(html.Contains("<span class=\"k\">{% if user %}</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">{{ user.name }}</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"cm\">{# greeting #}</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Liquid classifies <c>{% %}</c> and <c>{{ }}</c> blocks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LiquidClassifiesBlocks()
    {
        var html = LiquidLexer.Instance.Render("{% for item in collection %}\n{{ item.title }}\n{% endfor %}"u8);
        await Assert.That(html.Contains("<span class=\"k\">{% for item in collection %}</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">{{ item.title }}</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>ERB classifies <c>&lt;% %&gt;</c> statement and <c>&lt;%= %&gt;</c> expression blocks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ErbClassifiesBlocks()
    {
        var html = ErbLexer.Instance.Render("<% if @user %>\nName: <%= @user.name %>\n<% end %>"u8);
        await Assert.That(html.Contains("<span class=\"k\">&lt;% if @user %&gt;</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">&lt;%= @user.name %&gt;</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Handlebars classifies <c>{{# }}</c> statement and <c>{{ }}</c> expression blocks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HandlebarsClassifiesBlocks()
    {
        var html = HandlebarsLexer.Instance.Render("{{#if user}}\nHello {{user.name}}!\n{{/if}}"u8);
        await Assert.That(html.Contains("<span class=\"k\">{{#if user}}</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"n\">{{user.name}}</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>x86 classifies mnemonics and registers, accepts both <c>;</c> and <c>#</c> comments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task X86ClassifiesMnemonicsAndRegisters()
    {
        var html = X86AsmLexer.Instance.Render("; entry\nmov rax, 1\nadd rbx, rcx\nret"u8);
        await Assert.That(html.Contains("<span class=\"c1\">; entry</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">mov</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">add</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">ret</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">rax</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">rbx</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>x86 classifies <c>0x</c>-prefixed hex literals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task X86ClassifiesHexLiterals()
    {
        var html = X86AsmLexer.Instance.Render("mov eax, 0xff\n"u8);
        await Assert.That(html.Contains("<span class=\"mh\">0xff</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>ARM classifies AArch64 mnemonics and X-registers.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ArmClassifiesMnemonicsAndRegisters()
    {
        var html = ArmAsmLexer.Instance.Render("; entry\nldr x0, [sp]\nadd x0, x0, x1\nret"u8);
        await Assert.That(html.Contains("<span class=\"c1\">; entry</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">ldr</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">x0</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">sp</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>WAT classifies WebAssembly text-format keywords.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WatClassifiesInstructions()
    {
        var html = WatLexer.Instance.Render(";; comment\n(module\n  (func $add (param i32 i32) (result i32)\n    local.get 0\n    local.get 1\n    i32.add))"u8);
        await Assert.That(html.Contains("<span class=\"c1\">;; comment</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">module</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">func</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"nb\">i32</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Haskell still classifies after MlFamilyShared spread.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HaskellAfterSharedSpread()
    {
        var html = HaskellLexer.Instance.Render("module Foo where\nmain = if True then 1 else 0\n"u8);
        await Assert.That(html.Contains("<span class=\"kd\">module</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">where</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">if</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">then</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Scheme still classifies after LispFamilyShared spread.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SchemeAfterSharedSpread()
    {
        var html = SchemeLexer.Instance.Render("(define (f x) (if (zero? x) 0 (begin (display x) 1)))"u8);
        await Assert.That(html.Contains("<span class=\"kd\">define</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">if</span>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("<span class=\"k\">begin</span>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Registry resolves the new aliases.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegistryResolvesNewAliases()
    {
        await Assert.That(LexerRegistry.Default.TryGet([.. "jinja"u8], out var jinja)).IsTrue();
        await Assert.That(jinja).IsSameReferenceAs(JinjaLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "twig"u8], out var twig)).IsTrue();
        await Assert.That(twig).IsSameReferenceAs(JinjaLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "liquid"u8], out var liq)).IsTrue();
        await Assert.That(liq).IsSameReferenceAs(LiquidLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "erb"u8], out var erb)).IsTrue();
        await Assert.That(erb).IsSameReferenceAs(ErbLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "hbs"u8], out var hbs)).IsTrue();
        await Assert.That(hbs).IsSameReferenceAs(HandlebarsLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "asm"u8], out var asm)).IsTrue();
        await Assert.That(asm).IsSameReferenceAs(X86AsmLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "arm"u8], out var arm)).IsTrue();
        await Assert.That(arm).IsSameReferenceAs(ArmAsmLexer.Instance);
        await Assert.That(LexerRegistry.Default.TryGet([.. "wat"u8], out var wat)).IsTrue();
        await Assert.That(wat).IsSameReferenceAs(WatLexer.Instance);
    }
}
