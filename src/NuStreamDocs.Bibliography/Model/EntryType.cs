// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Bibliography.Model;

/// <summary>
/// Citation entry types. Names match CSL's vocabulary (kebab-case in
/// CSL-JSON; UpperCamelCase here) so a future CSL backend reads the
/// same input data without translation.
/// </summary>
public enum EntryType
{
    /// <summary>Book — full monograph with publisher.</summary>
    Book,

    /// <summary>Chapter inside an edited book.</summary>
    Chapter,

    /// <summary>Article in a peer-reviewed journal (CSL <c>article-journal</c>).</summary>
    ArticleJournal,

    /// <summary>Article in a magazine (CSL <c>article-magazine</c>).</summary>
    ArticleMagazine,

    /// <summary>Article in a newspaper (CSL <c>article-newspaper</c>).</summary>
    ArticleNewspaper,

    /// <summary>Generic article (CSL <c>article</c>) — falls back when no specific kind fits.</summary>
    Article,

    /// <summary>Court decision (CSL <c>legal_case</c>) — AGLC4 "Case".</summary>
    LegalCase,

    /// <summary>Statute / regulation (CSL <c>legislation</c>) — AGLC4 "Legislation".</summary>
    Legislation,

    /// <summary>International treaty (CSL <c>treaty</c>) — AGLC4 "Treaty".</summary>
    Treaty,

    /// <summary>Government / organizational report (CSL <c>report</c>).</summary>
    Report,

    /// <summary>Conference paper / talk (CSL <c>paper-conference</c>).</summary>
    PaperConference,

    /// <summary>Thesis or dissertation (CSL <c>thesis</c>).</summary>
    Thesis,

    /// <summary>Web page / online article (CSL <c>webpage</c>).</summary>
    Webpage,

    /// <summary>Manuscript / unpublished work.</summary>
    Manuscript,

    /// <summary>Catch-all when the type doesn't fit any of the above.</summary>
    Other,
}
