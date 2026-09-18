// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Bibliography.Model;

/// <summary>Citation entry types; names align with CSL's vocabulary.</summary>
public enum EntryType
{
    /// <summary>Book — full monograph with publisher.</summary>
    Book = 0,

    /// <summary>Chapter inside an edited book.</summary>
    Chapter = 1,

    /// <summary>Article in a peer-reviewed journal (CSL <c>article-journal</c>).</summary>
    ArticleJournal = 2,

    /// <summary>Article in a magazine (CSL <c>article-magazine</c>).</summary>
    ArticleMagazine = 3,

    /// <summary>Article in a newspaper (CSL <c>article-newspaper</c>).</summary>
    ArticleNewspaper = 4,

    /// <summary>Generic article (CSL <c>article</c>) — falls back when no specific kind fits.</summary>
    Article = 5,

    /// <summary>Court decision (CSL <c>legal_case</c>) — AGLC4 "Case".</summary>
    LegalCase = 6,

    /// <summary>Statute / regulation (CSL <c>legislation</c>) — AGLC4 "Legislation".</summary>
    Legislation = 7,

    /// <summary>International treaty (CSL <c>treaty</c>) — AGLC4 "Treaty".</summary>
    Treaty = 8,

    /// <summary>Government / organizational report (CSL <c>report</c>).</summary>
    Report = 9,

    /// <summary>Conference paper / talk (CSL <c>paper-conference</c>).</summary>
    PaperConference = 10,

    /// <summary>Thesis or dissertation (CSL <c>thesis</c>).</summary>
    Thesis = 11,

    /// <summary>Web page / online article (CSL <c>webpage</c>).</summary>
    Webpage = 12,

    /// <summary>Manuscript / unpublished work.</summary>
    Manuscript = 13,

    /// <summary>Catch-all when the type doesn't fit any of the above.</summary>
    Other = 14,
}
