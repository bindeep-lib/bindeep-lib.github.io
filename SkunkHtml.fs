module SkunkHtml

open SkunkUtils
open System
open System.Globalization
open System.IO
open FSharp.Formatting.Markdown

/// A single markdown page. Blog posts carry a publication date taken from the
/// file name (e.g. 2025-03-24.md); other pages (about, links, ...) do not.
type Page =
    { SourcePath: string
      Title: string
      Link: string
      Description: string
      Date: string option
      Lang: string
      Slug: string
      Translations: (string * string) list }

let private baseUrl = Url.normalizeBaseUrl Config.siteBaseUrl

let private headTemplate =
    Path.Combine(Config.htmlDir, "head.html")
    |> Disk.readFile

let private highlightingScript =
    Path.Combine(Config.htmlDir, "script_syntax_highlighting.html")
    |> Disk.readFile

let private giscusScript =
    Path.Combine(Config.htmlDir, "script_giscus.html")
    |> Disk.readFile

let generateFinalHtml (lang: string) (head: string) (header: string) (footer: string) (content: string) (script: string) =
    // The header template carries {{lang}} placeholders because it is a static
    // file that cannot know which language is being rendered. A placeholder that
    // stops matching would otherwise sail through and print a literal
    // "{{lang}}" in the navigation, so a leftover brace fails the build instead.
    let renderedHeader =
        header
        |> fun t -> t.Replace("{{lang}}", lang)
        |> fun t -> t.Replace("{{site-name}}", Config.siteTitle)
        |> fun t -> t.Replace("{{home-text}}", Config.str lang "首页" "Home")
        |> fun t -> t.Replace("{{links-text}}", Config.str lang "链接" "Links")

    if renderedHeader.Contains "{{" then
        failwithf "Unreplaced placeholder in html/header.html while rendering lang '%s'. Template contains: %s" lang header

    $"""
    <!DOCTYPE html>
    <html lang="{lang}" data-color-mode="user">
    <head>
        {head}
    </head>
    <body>
        <header>
            {renderedHeader}
        </header>
        <main>
            {content}
        </main>
        <hr>
        <footer>
            {footer}
        </footer>
        <script>
            {script}
        </script>
    </body>
    </html>
    """

/// `lang` picks the localised meta values; `feedUrl` is passed in rather than
/// derived here so every caller decides which feed its page advertises
/// (`/zh/feed.xml`, `/en/feed.xml`, or the root `/feed.xml` kept for 404).
/// A hard-coded feed URL per language would need a string rewrite afterwards,
/// which fails silently the day the surrounding markup changes.
let head (lang: string) (feedUrl: string) (titleSuffix: string) (description: string) (canonicalUrl: string) (ogType: string) =
    let fullTitle = Config.siteTitle + titleSuffix

    let seoMeta =
        let desc = if String.IsNullOrWhiteSpace(description) then Config.siteDescription lang else description
        let authorMeta =
            if String.IsNullOrWhiteSpace(Config.siteAuthor) then ""
            else $"""<meta name="author" content="{Xml.escape Config.siteAuthor}">"""
        let imageMeta =
            if String.IsNullOrWhiteSpace(Config.siteImage) then ""
            else
                let imageUrl = $"{baseUrl}/{Config.siteImage.TrimStart('/')}"
                $"""<meta property="og:image" content="{imageUrl}">
        <meta name="twitter:image" content="{imageUrl}">"""
        $"""
        <meta name="description" content="{Xml.escape desc}">
        {authorMeta}
        <meta property="og:title" content="{Xml.escape fullTitle}">
        <meta property="og:description" content="{Xml.escape desc}">
        <meta property="og:type" content="{ogType}">
        <meta property="og:url" content="{canonicalUrl}">
        {imageMeta}
        <meta name="twitter:card" content="summary">
        <meta name="twitter:title" content="{Xml.escape fullTitle}">
        <meta name="twitter:description" content="{Xml.escape desc}">
        <link rel="canonical" href="{canonicalUrl}">
        <link rel="alternate" type="application/rss+xml" title="{Xml.escape Config.siteTitle}" href="{feedUrl}">
        """

    let renderedHead = headTemplate.Replace("{{site-title}}", Xml.escape fullTitle)

    // Same reasoning as the header guard: a template placeholder that stops
    // matching would otherwise be published verbatim into every page's <title>.
    if renderedHead.Contains "{{" then
        failwith "Unreplaced placeholder in html/head.html. Template contains: " + headTemplate

    renderedHead + seoMeta

let private isArticle (file: string) =
    Char.IsDigit(Path.GetFileName(file)[0])

/// "2026-10-19.zh.md" -> ("2026-10-19", "zh")
let private splitFileName (file: string) =
    let stem = Path.GetFileNameWithoutExtension(file)   // "2026-10-19.zh"
    let lastDot = stem.LastIndexOf('.')
    if lastDot > 0 then
        stem.Substring(0, lastDot), stem.Substring(lastDot + 1).ToLowerInvariant()
    else
        stem, ""

/// Front matter is author-controlled text that ends up in `Link`, and `Link` is
/// handed to `Path.Combine(outputDir, page.Link)` — so it must be reduced to a
/// single inert path segment before it is ever joined to a directory. A slug that
/// already is one (word characters and hyphens, e.g. "my-post") is kept verbatim so
/// authors get the URL they asked for; anything else — "..", "/", "?", whitespace —
/// goes through the same normaliser a title goes through.
let private safeSlug (raw: string) =
    let inertSegment (s: string) = System.Text.RegularExpressions.Regex.IsMatch(s, @"\A[\w-]+\z")
    if inertSegment raw then raw else Url.toUrlFriendly raw

let loadPage (markdownFilePath: string) : Page =
    let raw = File.ReadAllText(markdownFilePath)
    let fm = FrontMatter.parse raw
    let lines = fm.Body.Split('\n')

    let title =
        lines
        |> Array.tryFind (fun l -> l.StartsWith("# "))
        |> Option.map _.TrimStart('#').Trim()
        |> Option.defaultValue (Config.untitledPageTitle Config.defaultLang)

    let description =
        lines
        |> Array.filter (fun line -> not (String.IsNullOrWhiteSpace line))
        |> Array.filter (fun line -> not (line.StartsWith("#")))
        |> Array.tryHead
        |> Option.defaultValue ""
        |> MarkdownUtils.stripMarkdownSyntax
        |> fun s -> if s.Length > 160 then s[..159] + "..." else s

    let baseName, suffixLang = splitFileName markdownFilePath

    // front-matter wins; otherwise filename suffix; otherwise the default language.
    let lang =
        // Only a configured language may become a directory; "fr" or a traversal
        // payload in `lang:` must not create a bogus folder next to zh/ and en/.
        let isConfigured (code: string) =
            Config.languages |> List.exists (fun (configured, _) -> configured = code)
        fm.Lang
        |> Option.filter isConfigured
        |> Option.orElse (if isConfigured suffixLang then Some suffixLang else None)
        |> Option.defaultValue Config.defaultLang

    // No front-matter slug: derive from title, else from the file name (never empty).
    let slug =
        fm.Slug
        |> Option.map safeSlug
        |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace s))
        |> Option.defaultWith (fun () ->
            let fromTitle = Url.toUrlFriendly title
            if String.IsNullOrWhiteSpace fromTitle then Url.toUrlFriendly baseName
            else fromTitle)

    let date =
        if isArticle markdownFilePath then Some baseName else None

    { SourcePath = markdownFilePath
      Title = title
      Link = $"{lang}/{slug}.html"
      Description = description
      Date = date
      Lang = lang
      Slug = slug
      Translations = [] }

/// Language switcher for a page. `links` are site-root-relative *without* a
/// leading slash ("zh/decompiled-code.html", "zh/") — this function adds the
/// slash, so passing "/zh/..." here would render the protocol-relative URL
/// "//zh/..." and send the reader to the host "zh".
/// Defined before `createPage`/`createIndexPage` because F# module-level `let`
/// bindings are sequential: a helper used by both must precede both.
let languageSwitcher (lang: string) (links: (string * string) list) =
    let items =
        links
        |> List.sortBy fst
        |> List.map (fun (code, link) ->
            let active = if code = lang then " class=\"active\"" else ""
            $"""<a{active} href="/{link}">{Config.langName code}</a>""")
        |> String.concat " "

    $"<nav class=\"lang-switch\">{items}</nav>"

/// Each article page declares every language it exists in via `hreflang`
/// alternates, including its own — a page must list itself for the set to be
/// complete. `Translations` is filled by `linkTranslations` and contains the
/// page itself, which is why `languageSwitcher` marks the current one active
/// instead of filtering it out.
let createPage (header: string) (footer: string) (page: Page) =
    let canonicalUrl = $"{baseUrl}/{page.Link}"
    // Fully qualified on purpose: opening `SkunkUtils.FrontMatter` here would make
    // every unannotated `.Slug`/`.Lang` in this file an ambiguous-label error.
    let fm = FrontMatter.parse (File.ReadAllText page.SourcePath)

    let htmlContent, ogType =
        match page.Date with
        | None -> Markdown.ToHtml(fm.Body), "website"
        | Some date ->
            let publicationDate =
                $"""<p class="publication-date">{Config.publishedOnText page.Lang} <time datetime="{date}">{date}</time></p>"""
            let articleHtml = Markdown.ToHtml(fm.Body + "\n\n" + publicationDate + "\n\n")
            articleHtml + giscusScript, "article"

    let alternates =
        page.Translations
        |> List.sortBy fst
        |> List.map (fun (code, link) ->
            $"""<link rel="alternate" hreflang="{code}" href="{baseUrl}/{link}">""")
        |> String.concat "\n"

    let feedUrl = $"{baseUrl}/{page.Lang}/feed.xml"
    // The <base> tag is what makes the header's assets resolve from /<lang>/<slug>.html.
    let headContent =
        $"""<base href="{baseUrl}/">
{alternates}"""
        + head page.Lang feedUrl (" - " + page.Title) page.Description canonicalUrl ogType

    let switcher = languageSwitcher page.Lang page.Translations
    let finalHtmlContent =
        generateFinalHtml page.Lang headContent header footer (switcher + htmlContent) highlightingScript

    printfn $"Processing {Path.GetFileName page.SourcePath} ->"
    Disk.writeFile (Path.Combine(Config.outputDir, page.Link)) finalHtmlContent

let createIndexPage (header: string) (footer: string) (lang: string) (articles: Page list) =
    let frontPageMarkdownFilePath = Path.Combine(Config.markdownDir, Config.frontPageFileName lang)

    let frontPageContentHtml =
        if File.Exists(frontPageMarkdownFilePath) then
            printfn $"Processing {Path.GetFileName frontPageMarkdownFilePath} ->"
            // Fully qualified on purpose: opening `SkunkUtils.FrontMatter` here would make
            // every unannotated `.Slug`/`.Lang` in this file an ambiguous-label error.
            let fm = FrontMatter.parse (File.ReadAllText frontPageMarkdownFilePath)
            Markdown.ToHtml fm.Body
        else
            printfn $"Warning! File {Config.frontPageFileName lang} does not exist! The main page will only contain blog entries, without a welcome message"
            ""

    // Only this language's articles appear on this language's front page: an
    // article without an English translation must not show up under /en/.
    let ownArticles = articles |> List.filter (fun a -> a.Lang = lang)

    let articleListHtml =
        ownArticles
        |> List.map (fun article ->
            let datePrefix =
                match article.Date with
                | Some date -> $"{date}: "
                | None -> ""
            $"""<li>{datePrefix}<a href="{article.Link}">{Xml.escape article.Title}</a></li>""")
        |> String.concat "\n"

    // The two language homes, so a reader landing on /zh/ can reach /en/ and back.
    let homeLinks = Config.languages |> List.map (fun (code, _) -> code, $"{code}/")
    let switcher = languageSwitcher lang homeLinks

    let content =
        $"""
        {frontPageContentHtml}
        {switcher}
        <section class="publications">
            <h2>{Config.blogEntriesHeading lang}</h2>
            <ul>
            {articleListHtml}
            </ul>
        </section>
        """

    let canonicalUrl = $"{baseUrl}/{lang}/"
    let feedUrl = $"{baseUrl}/{lang}/feed.xml"
    let baseTag = $"""<base href="{baseUrl}/">"""
    let headContent = baseTag + head lang feedUrl "" "" canonicalUrl "website"

    let finalHtml = generateFinalHtml lang headContent header footer content highlightingScript

    printfn $"Processing index.{lang} ->"
    Disk.writeFile (Path.Combine(Config.outputDir, lang, "index.html")) finalHtml

/// The site root is a redirect to the default language, not a copy of an index:
/// a duplicate home page at `/` would compete with `/zh/` in search results.
/// The <base> tag keeps the header's absolute links and assets working here.
let createRootRedirect (header: string) (footer: string) =
    let target = $"/{Config.defaultLang}/"

    let content =
        $"""
        <p>Redirecting to <a href="{target}">{target}</a>&hellip;</p>
        """

    let headContent =
        $"""<meta http-equiv="refresh" content="0; url={target}">
<link rel="canonical" href="{baseUrl}{target}">"""

    let finalHtml = generateFinalHtml Config.defaultLang headContent header footer content highlightingScript

    printfn "Processing root redirect ->"
    Disk.writeFile (Path.Combine(Config.outputDir, "index.html")) finalHtml

/// Auto-generated 404 page, served by GitHub Pages for any missing URL.
/// The <base> tag makes relative links work at any path depth. Written before
/// the regular pages, so an explicit page with the same file name wins.
let createNotFoundPage (header: string) (footer: string) =
    let content =
        $"""
        <h1>404</h1>
        <p>{Config.notFoundMessage Config.defaultLang}</p>
        <p><a href="/{Config.defaultLang}/">{Config.notFoundBackText Config.defaultLang}</a></p>
        """

    let baseTag = $"""<base href="{baseUrl}/">"""
    let headContent = baseTag + head Config.defaultLang $"{baseUrl}/feed.xml" (" - " + (Config.notFoundTitle Config.defaultLang)) "" $"{baseUrl}/404.html" "website"
    let finalHtmlContent = generateFinalHtml Config.defaultLang headContent header footer content highlightingScript

    Disk.writeFile (Path.Combine(Config.outputDir, "404.html")) finalHtmlContent

// --- RSS Feed Generation ---

/// RFC 1123 date for RSS; falls back to the raw string when the file name is not a date
let private toRssDate (date: string) =
    match DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
    | true, parsed -> parsed.ToString("R")
    | _ -> date

/// One feed per language: a subscriber following /en/feed.xml gets only English
/// posts. The default language's feed is ALSO written to the site root, because
/// most readers auto-discover `/feed.xml` and a root 404 would silently break
/// subscription for everyone not following a link by hand.
let createRssFeed (lang: string) (articles: Page list) =
    let authorElement =
        if String.IsNullOrWhiteSpace(Config.siteAuthor) then ""
        else $"    <managingEditor>{Xml.escape Config.siteAuthor}</managingEditor>\n"

    let items =
        articles
        |> List.filter (fun article -> article.Lang = lang)
        |> List.map (fun article ->
            let pubDateElement =
                match article.Date with
                | Some date -> $"      <pubDate>{toRssDate date}</pubDate>\n"
                | None -> ""
            let description =
                if String.IsNullOrWhiteSpace(article.Description) then article.Title else article.Description
            "    <item>\n"
            + $"      <title>{Xml.escape article.Title}</title>\n"
            + $"      <link>{baseUrl}/{article.Link}</link>\n"
            + $"      <guid>{baseUrl}/{article.Link}</guid>\n"
            + pubDateElement
            + $"      <description>{Xml.escape description}</description>\n"
            + "    </item>")
        |> String.concat "\n"

    let lastBuildDate = DateTime.UtcNow.ToString("R")

    let feed =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
        + "<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\">\n"
        + "  <channel>\n"
        + $"    <title>{Xml.escape Config.siteTitle}</title>\n"
        + $"    <link>{baseUrl}/{lang}/</link>\n"
        + $"    <description>{Xml.escape (Config.siteDescription lang)}</description>\n"
        + $"    <language>{lang}</language>\n"
        + $"    <lastBuildDate>{lastBuildDate}</lastBuildDate>\n"
        + authorElement
        + $"    <atom:link href=\"{baseUrl}/{lang}/feed.xml\" rel=\"self\" type=\"application/rss+xml\" />\n"
        + items + "\n"
        + "  </channel>\n"
        + "</rss>"

    Disk.writeFile (Path.Combine(Config.outputDir, lang, "feed.xml")) feed

    // Root feed mirrors the default language for reader auto-discovery.
    if lang = Config.defaultLang then
        Disk.writeFile (Path.Combine(Config.outputDir, "feed.xml")) feed

// --- Sitemap Generation ---
let createSitemap (pages: Page list) =
    let entries =
        pages
        |> List.map (fun page ->
            let lastmod =
                match page.Date with
                | Some date -> $"    <lastmod>{date}</lastmod>\n"
                | None -> ""
            "  <url>\n"
            + $"    <loc>{baseUrl}/{page.Link}</loc>\n"
            + lastmod
            + "  </url>")
        |> String.concat "\n"

    // Each language home gets its own entry. The bare site root is deliberately
    // absent: it is a redirect page whose canonical points at the default
    // language home, so listing it would tell crawlers two pages are the same.
    let homeEntries =
        Config.languages
        |> List.map (fun (lang, _) ->
            "  <url>\n"
            + $"    <loc>{baseUrl}/{lang}/</loc>\n"
            + "  </url>")
        |> String.concat "\n"

    let sitemap =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
        + "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n"
        + homeEntries + "\n"
        + entries + "\n"
        + "</urlset>"

    Disk.writeFile (Path.Combine(Config.outputDir, "sitemap.xml")) sitemap

// --- Translation Linking ---

/// Pair translations by slug. A page cannot know its siblings while it is being
/// loaded, so this is a second pass over the complete page list.
/// A slug with only one language is a warning, not an error: a blog may
/// legitimately have posts in one language only.
let linkTranslations (pages: Page list) =
    let bySlug =
        pages
        |> List.groupBy _.Slug
        |> Map.ofList

    pages
    |> List.map (fun (page: Page) ->
        let translations =
            bySlug
            |> Map.tryFind page.Slug
            |> Option.defaultValue []
            |> List.map (fun p -> p.Lang, p.Link)

        match translations with
        | [ _ ] -> printfn $"Warning! No translation found for slug '{page.Slug}' ({page.Lang} only)"
        | _ -> ()

        { page with Translations = translations })
