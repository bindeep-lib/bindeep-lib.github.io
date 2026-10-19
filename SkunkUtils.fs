module SkunkUtils

module Disk =
    open System.IO

    let readFile (path: string) =
        if File.Exists(path) then File.ReadAllText(path) else ""

    let writeFile (path: string) (content: string) =
        // Language-prefixed links (zh/, en/) need directories that do not exist yet.
        // CreateDirectory on an existing directory is a no-op and does not throw.
        let dir = Path.GetDirectoryName(path)
        if not (String.IsNullOrEmpty dir) then Directory.CreateDirectory dir |> ignore
        File.WriteAllText(path, content)
        printfn $"Generated: {Path.GetFileName path} -> {path}\n"

    let copyFolderToOutput (sourceFolder: string) (destinationFolder: string) =
        if not (Directory.Exists(sourceFolder)) then
            printfn $"Source folder does not exist: {sourceFolder}"
        else
            if not (Directory.Exists(destinationFolder)) then
                Directory.CreateDirectory(destinationFolder)
                |> ignore

            Directory.GetFiles(sourceFolder)
            |> Array.iter (fun file ->
                let fileName = Path.GetFileName(file)
                let destFile = Path.Combine(destinationFolder, fileName)
                printfn $"Copying: {fileName} -> {destFile}"
                File.Copy(file, destFile, true))

module Url =
    open System.Text.RegularExpressions

    let toUrlFriendly (input: string) =
        let slug =
            input.ToLowerInvariant()
            |> fun text -> Regex.Replace(text, @"[^\w\s]", "") // Remove all non-alphanumeric characters
            |> fun text -> Regex.Replace(text, @"\s+", "-") // Replace spaces with hyphens

        // A title made only of punctuation (e.g. "???") normalises to ""; returning it
        // would produce a dotfile link like ".html". Fall back to a usable slug.
        if String.IsNullOrWhiteSpace slug then
            // The primary path drops non-word chars, but the fallback must not: keeping
            // them would let a title like "../../.." escape the output directory and a
            // title like "???" break the href. Map every non-word char to a hyphen.
            let fallback =
                Regex.Replace(input.ToLowerInvariant(), @"[^\w\s]", "-")
                |> fun text -> Regex.Replace(text, @"-+", "-") // Collapse hyphen runs
                |> fun text -> Regex.Replace(text, @"^-+|-+$", "") // Trim leading/trailing hyphens
            if String.IsNullOrWhiteSpace fallback then "untitled" else fallback
        else
            slug

    /// Ensure base URL has no trailing slash
    let normalizeBaseUrl (url: string) =
        url.TrimEnd('/')

module Xml =
    /// Escape special characters for XML content
    let escape (input: string) =
        input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;")

module MarkdownUtils =
    open System.Text.RegularExpressions

    /// Strip basic Markdown syntax to produce plain text (for meta descriptions)
    let stripMarkdownSyntax (input: string) =
        input
        |> fun s -> Regex.Replace(s, @"\[([^\]]+)\]\([^\)]+\)", "$1")  // [text](url) -> text
        |> fun s -> Regex.Replace(s, @"[*_]{1,3}", "")                // bold/italic markers
        |> fun s -> Regex.Replace(s, @"`([^`]+)`", "$1")              // inline code
        |> fun s -> s.Trim()

/// Front matter is a leading `---` delimited block of `key: value` lines.
/// Tolerates CRLF, surrounding whitespace, and missing/garbage blocks by
/// falling back to "no front matter" so existing pages keep working.
module FrontMatter =
    type Result =
        { Lang: string option
          Slug: string option
          Body: string }

    let private parseBlock (lines: string array) =
        let first = lines |> Array.tryFind (fun l -> not (String.IsNullOrWhiteSpace l))
        match first with
        | Some line when line.Trim() = "---" ->
            let rest = lines |> Array.skipWhile (fun l -> l <> line)
            let body =
                rest
                |> Array.skip 1
                |> Array.takeWhile (fun l -> l.Trim() <> "---")
            let after = rest |> Array.skip (body.Length + 2)
            let fields =
                body
                |> Array.choose (fun l ->
                    match l.IndexOf(':') with
                    | -1 -> None
                    | i -> Some(l.Substring(0, i).Trim().ToLowerInvariant(), l.Substring(i + 1).Trim())))
                |> Map.ofList
            let get k = fields |> Map.tryFind k
            let lang = get "lang" |> Option.filter (fun v -> v <> "")
            let slug = get "slug" |> Option.filter (fun v -> v <> "")
            Some(lang, slug, String.Join("\n", after))
        | _ -> None

    let parse (content: string) =
        let lines = content.Split('\n') |> Array.map (fun l -> l.TrimEnd('\r'))
        match parseBlock lines with
        | Some(lang, slug, body) -> { Lang = lang; Slug = slug; Body = body }
        | None -> { Lang = None; Slug = None; Body = content }
