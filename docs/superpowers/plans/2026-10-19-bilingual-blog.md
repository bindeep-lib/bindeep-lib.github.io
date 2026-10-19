# 双语博客改造 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 SkunkHTML 博客改成支持中英双语的两套静态页面（`/zh/` 与 `/en/`），站点名 `bindeep library`，首页展示微信公众号二维码，不放作者头像。

**Architecture:** front-matter（`lang` + `slug`）是语言与文章身份的唯一权威来源。`loadPage` 解析 front-matter 得到 `Page.Lang` 与 `Page.Slug`，`Link` 变为 `"{lang}/{slug}.html"`。`Program.fs` 在全部页面加载后做一次 `Translations` 回填（配对只能在全量加载后进行），再按语言分组生成各自的首页与 RSS。每个页面注入 `<base href="{baseUrl}/">` 以修复 `/zh/` 子目录下的相对路径断裂。

**Tech Stack:** F# / .NET 10 / FSharp.Formatting 22.1.0 / GitHub Actions（`dotnet-version: 10`）

**Spec:** `docs/2026-10-19-bilingual-blog-design.md`

## Global Constraints

这些约束来自 spec，每个 task 的要求都隐含包含本节：

- **图片文件名必须 ASCII** —— 实测 `/images/锻码.jpg` 返回 400/404（spec §3.2）
- **FSharp.Formatting 支持内嵌 HTML** —— 实测 `<a>`/`<h2>` 渲染为真元素（spec §3.1）⇒ 切换器与二维码直接写 HTML+CSS，**不要绕 JS 或 base64**
- **必须注入 `<base href="{baseUrl}/">` 到每一页** —— `Config.siteBaseUrl = "https://bindeep-lib.github.io"` 是用户站点根 URL（spec §4.4）
- **`<base>` 的必然副作用**：`header.html` 的 `href="index.html"` 会解析到根路径重定向页，**导航必须改成按语言拼接的绝对路径**（spec §4.4）
- **`date` 必须双层剥扩展名** —— `2026-10-19.zh.md` 一次只剥到 `2026-10-19.zh`（spec §4.2）
- **配对按 `slug`，缺翻译打 warning 但不中断构建**（spec §4.5）
- **未翻译文章不跨语言出现在另一语言首页**（spec §4.8）
- **RSS 按语言拆分**，根 `/feed.xml` 保留并指向 zh（spec §4.7）
- **本机无 dotnet SDK，仓库无测试工程** ⇒ 见下方「测试策略」

## 测试策略（重要，先读）

本机 `/home/abin` `/usr/local` `/opt` 下均无 `dotnet` 可执行文件（已 find 确认），仓库也无任何测试工程。因此：

**不引入 Expecto 等测试框架** —— 无法在本地跑 CI 前的快速验证，等于给一个从不执行的测试网做虚假信心。

**改为「可执行断言脚本 + 每次改动的在线验证」**，这是本项目唯一能真正执行的验证形式（probe 已验证可行）：

```bash
# 每个 task 结束后：commit → 告知用户 push → 等 Actions ~2min → 跑该 task 的验证命令
```

Task 1 会建立 `scripts/verify.sh` —— 一个**无参数时验证当前线上站点、有参数时验证指定 URL 集合**的脚本。此后每个 task 的验证都通过它执行。这是本项目唯一的自动化回归网。

## Review Focus

spec 隐含但没有任何 task 会自然覆盖的五个输入/失败模式：

1. **文章缺 front-matter** ⇒ `lang` 应回落默认语言、`slug` 回落文件名，不得崩溃（Task 3）
2. **`toUrlFriendly` 对纯标点标题返回空串** ⇒ `Link` 变成 `.html` 这样的 dotfile（Task 2 加空 slug 守卫）
3. **只有单一语言的文章** ⇒ 切换器不显示另一语言，且不得产生死链（Task 6）
4. **`index.zh.md` 与 `index.md` 的双写出** ⇒ 首页内容被写两遍或 `index.zh.md` 被当普通页额外生成一个 `/zh/index.zh.html`（Task 3）
5. **`siteImage` 清空后 `og:image` 是否消失** ⇒ spec §4.8 要求不放头像，但 `Config.fs:12` 仍指向模板作者的 skunk 图（Task 1）

---

## File Structure

| 文件 | 责任 |
|---|---|
| `Config.fs` | 全部站点配置的**唯一来源**：双语字典、默认语言、语言列表、首页文件名映射 |
| `SkunkUtils.fs` | `Disk.writeFile` 建目录；`toUrlFriendly` 空串守卫；新增 front-matter 解析器 |
| `SkunkHtml.fs` | `Page` 类型 + 各生成函数（page / index / rss / sitemap / redirect / 404） |
| `Program.fs` | 流水线编排：加载 → 回填 translations → 按语言分组 → 逐语言生成 |
| `html/header.html` | 导航（绝对路径）+ 语言切换器容器 |
| `css/styles.css` | 语言切换器样式 |
| `markdown-blog/*.md` | 内容，按 `{date}.{lang}.md` / `{page}.{lang}.md` 命名 |
| `scripts/verify.sh` | **新增** —— 唯一可执行的回归验证入口 |

---

## Task 1: 验证脚本与站点配置（Config.fs）

**Files:**
- Create: `scripts/verify.sh`
- Modify: `Config.fs:7-20`

**Interfaces:**
- Consumes: 无（首个 task）
- Produces:
  - `Config.defaultLang : string` = `"zh"`
  - `Config.languages : (string * string) list` = `[("zh","中文");("en","English")]` —— (code, 显示名)
  - `Config.str (lang: string) (zh: string) (en: string) : string`
  - `Config.siteTitle : string` = `"bindeep library"`
  - `Config.siteDescription lang : string` —— 改为函数
  - `Config.siteImage : string` = `""`
  - `Config.frontPageFileName (lang: string) : string`
  - `Config.blogEntriesHeading lang : string`、`Config.publishedOnText lang : string`、`Config.notFound* lang : string`

- [ ] **Step 1: 写验证脚本**

Create `scripts/verify.sh`：

```bash
#!/usr/bin/env bash
# 验证线上站点结构。用法：
#   scripts/verify.sh              验证默认 URL 集合
#   scripts/verify.sh URL [URL...] 验证指定 URL
# 退出码: 0=全部 200, 1=存在非 200
set -uo pipefail
BASE="${BASE:-https://bindeep-lib.github.io}"

if [ "$#" -gt 0 ]; then
  URLS=("$@")
else
  URLS=(
    "$BASE/"                       # 根: 应为 200 (重定向页)
    "$BASE/zh/"                    # 中文首页
    "$BASE/en/"                    # 英文首页
    "$BASE/css/styles.css"         # <base> 生效验证
    "$BASE/images/wechat-qr.jpg"   # 二维码存在且可访问
    "$BASE/zh/css/styles.css"      # <base> 在子目录下生效
    "$BASE/zh/images/wechat-qr.jpg"
    "$BASE/zh/decompiled-code-why-ida-ghidra.html"   # 中文文章
    "$BASE/en/decompiled-code-why-ida-ghidra.html"   # 英文文章
    "$BASE/zh/feed.xml"
    "$BASE/en/feed.xml"
    "$BASE/feed.xml"
  )
fi

fail=0
for u in "${URLS[@]}"; do
  code=$(timeout 25 curl -sS -o /dev/null -w "%{http_code}" -L "$u" 2>/dev/null || echo "ERR")
  if [ "$code" = "200" ]; then
    printf "  OK   %s  %s\n" "$code" "$u"
  else
    printf "  FAIL %s  %s\n" "$code" "$u"
    fail=1
  fi
done
exit $fail
```

- [ ] **Step 2: 在当前站点上跑一次，确认脚本本身可用（预期大量 FAIL）**

Run: `chmod +x scripts/verify.sh && scripts/verify.sh`

Expected: 退出码 1。`/css/styles.css`、`/images/wechat-qr.jpg`、`/zh/`、`/en/` 等 FAIL（尚未实现），`/feed.xml` OK（当前单 feed 存在）。

**这是 baseline 记录** —— 后续 task 逐步把 FAIL 变成 OK。

- [ ] **Step 3: 改写 Config.fs**

Replace `Config.fs:5-20` 为：

```fsharp
// --- Languages ---
let defaultLang = "zh"
let languages : (string * string) list = [ "zh", "中文"; "en", "English" ]

/// Pick the variant matching `lang`; anything unknown falls back to the default.
let str (lang: string) (zh: string) (en: string) =
    if lang = "en" then en else zh

let langName (lang: string) =
    languages
    |> List.tryPick (fun (code, name) -> if code = lang then Some name)
    |> Option.defaultValue lang

// --- Site metadata ---
let siteTitle = "bindeep library"
let siteDescription lang =
    str lang "bindeep.cn 的实验性质博客" "An experimental blog at bindeep.cn"
let siteBaseUrl = "https://bindeep-lib.github.io"
let siteAuthor = "bindeep"
let siteImage = ""              // 不放作者头像；空串禁用 og:image

// --- Interface text ---
let blogEntriesHeading lang = str lang "文章" "Articles"
let publishedOnText lang = str lang "发布于" "Published on"
let untitledPageTitle lang = str lang "无标题" "No Title"
let homeSubtitle lang = str lang "bindeep 实验室 · bindeep.cn 的实验性质博客"
                            "bindeep laboratory · an experimental blog at bindeep.cn"
let wechatLabel lang = str lang "微信公众号" "WeChat"
let notFoundTitle lang = str lang "页面未找到" "Page not found"
let notFoundMessage lang =
    str lang "此页面不存在或已被移动。" "This page does not exist or has been moved."
let notFoundBackText lang = str lang "返回首页" "Back to the front page"
```

Replace `Config.fs:44`：

```fsharp
let frontPageFileName lang = $"index.{lang}.md"
```

⚠️ 删除 `siteLanguage`（Task 3 起改为随页面）、删除原 `frontPageMarkdownFileName`。
⚠️ `Config.fs:23` 的 `sourceDir` 及之后的目录配置**保持不动**。

- [ ] **Step 4: 验证 Config.fs 语法正确**

本机无 dotnet，无法编译。改为目视核对：`grep -c "siteLanguage" Config.fs` 应为 0；`frontPageMarkdownFileName` 计数应为 0。

Run: `grep -c "siteLanguage\|frontPageMarkdownFileName" Config.fs || true`
Expected: `0`

- [ ] **Step 5: 提交**

```bash
git add scripts/verify.sh Config.fs
git commit -m "feat(config): 双语字典与站点标识，新增线上验证脚本"
```

---

## Task 2: 基础设施 —— 建目录与空 slug 守卫

**Files:**
- Modify: `SkunkUtils.fs:9-11`（`Disk.writeFile`）
- Modify: `SkunkUtils.fs:31-34`（`Url.toUrlFriendly`）

**Interfaces:**
- Consumes: 无
- Produces: `Disk.writeFile` 行为变更（自动建目录）；`Url.toUrlFriendly` 对纯标点输入不再返回空串（回落为原字符串的小写去空格形式）

- [ ] **Step 1: 记录当前行为作为 baseline**

Run:
```bash
grep -n -A3 "let writeFile" SkunkUtils.fs
grep -n -A4 "let toUrlFriendly" SkunkUtils.fs
```
Expected: 看到 `File.WriteAllText` 与两行 `Regex.Replace`，确认这是未修改的原始状态。

- [ ] **Step 2: 修改 `Disk.writeFile`**

Replace `SkunkUtils.fs:9-11`：

```fsharp
    let writeFile (path: string) (content: string) =
        // Language-prefixed links (zh/, en/) need directories that do not exist yet.
        // CreateDirectory on an existing directory is a no-op and does not throw.
        let dir = Path.GetDirectoryName(path)
        if not (String.IsNullOrEmpty dir) then Directory.CreateDirectory dir |> ignore
        File.WriteAllText(path, content)
        printfn $"Generated: {Path.GetFileName path} -> {path}\n"
```

需要 `open System.IO` —— 检查 `SkunkUtils.fs` 顶部是否已 `open System`；`Directory`/`Path` 在 `System.IO` 下，若缺失则补 `open System.IO`。

- [ ] **Step 3: 修改 `Url.toUrlFriendly` 加空串守卫**

Replace `SkunkUtils.fs:31-34`：

```fsharp
    let toUrlFriendly (input: string) =
        let slug =
            input.ToLowerInvariant()
            |> fun text -> Regex.Replace(text, @"[^\w\s]", "") // Remove all non-alphanumeric characters
            |> fun text -> Regex.Replace(text, @"\s+", "-") // Replace spaces with hyphens

        // A title made only of punctuation (e.g. "???") normalises to ""; returning it
        // would produce a dotfile link like ".html". Fall back to a usable slug.
        if String.IsNullOrWhiteSpace slug then
            let fallback = Regex.Replace(input.ToLowerInvariant(), @"\s+", "-")
            if String.IsNullOrWhiteSpace fallback then "untitled" else fallback
        else
            slug
```

- [ ] **Step 4: 用 grep 确认改动到位**

Run:
```bash
grep -n -A4 "let writeFile" SkunkUtils.fs | grep -c "CreateDirectory"
grep -n -A9 "let toUrlFriendly" SkunkUtils.fs | grep -c "IsNullOrWhiteSpace"
```
Expected: 两个都输出 `1`。

- [ ] **Step 5: 提交**

```bash
git add SkunkUtils.fs
git commit -m "feat(utils): writeFile 自动建语言目录，toUrlFriendly 加空串守卫"
```

---

## Task 3: front-matter 解析与 Page 扩展

**Files:**
- Modify: `SkunkUtils.fs`（新增 `FrontMatter` 模块，追加到文件末尾）
- Modify: `SkunkHtml.fs:11-16`（`Page` 类型）、`:88-119`（`isArticle` / `loadPage`）、`:32-53`（`generateFinalHtml` 增 lang 参数）
- Modify: `Program.fs:18-22`（过滤逻辑）

**Interfaces:**
- Consumes: `Config.defaultLang`（Task 1）、`Config.str`（Task 1）
- Produces:
  ```fsharp
  module FrontMatter =
      type Result = { Lang: string option; Slug: string option; Body: string }
      val parse (content: string) : Result

  type Page =
      { SourcePath: string
        Title: string
        Link: string          // "{lang}/{slug}.html"
        Description: string
        Date: string option
        Lang: string          // "zh" | "en"
        Slug: string
        Translations: (string * string) list }   // Task 4 回填
  ```
- `generateFinalHtml` 新签名：`generateFinalHtml (lang: string) (head: string) (header: string) (footer: string) (content: string) (script: string)`

- [ ] **Step 1: 新增 FrontMatter 解析器**

Append 到 `SkunkUtils.fs` 末尾：

```fsharp
/// Front matter is a leading `---` delimited block of `key: value` lines.
/// Tolerates CRLF, surrounding whitespace, and missing/garbage blocks by
/// falling back to "no front matter" so existing pages keep working.
module FrontMatter =
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
```

- [ ] **Step 2: 扩展 Page 类型**

Replace `SkunkHtml.fs:11-16`：

```fsharp
type Page =
    { SourcePath: string
      Title: string
      Link: string
      Description: string
      Date: string option
      Lang: string
      Slug: string
      Translations: (string * string) list }
```

- [ ] **Step 3: `generateFinalHtml` 增 lang 参数**

Replace `SkunkHtml.fs:32-53` 的签名行与 `<html lang>`：

```fsharp
let generateFinalHtml (lang: string) (head: string) (header: string) (footer: string) (content: string) (script: string) =
    $"""
    <!DOCTYPE html>
    <html lang="{lang}" data-color-mode="user">
```

保留函数体其余部分不变。

- [ ] **Step 4: 重写 isArticle 与 loadPage**

Replace `SkunkHtml.fs:88-119`：

```fsharp
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
        fm.Lang
        |> Option.orElse (if suffixLang = "zh" || suffixLang = "en" then Some suffixLang else None)
        |> Option.defaultValue Config.defaultLang

    // No front-matter slug: derive from title, else from the file name (never empty).
    let slug =
        fm.Slug
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
```

- [ ] **Step 5: 改 Program.fs 的过滤，容纳 `index.{lang}.md`**

Replace `Program.fs:18-22`：

```fsharp
    let pages =
        Directory.GetFiles(Config.markdownDir, "*.md")
        |> Array.filter (fun file ->
            let name = Path.GetFileName file
            not (Config.languages |> List.exists (fun (code, _) -> name = Config.frontPageFileName code)))
        |> Array.map loadPage
        |> Array.toList
```

需要 `open System.IO` —— `Program.fs` 已有（`:1`）。

- [ ] **Step 6: 修掉所有因签名变化而断裂的调用点**

`generateFinalHtml` 现有 3 个调用点必须补 lang 参数（此刻其它 task 尚未改，先给占位，Task 4-6 会替换）：

- `SkunkHtml.fs:135`（createPage）→ 传 `page.Lang`
- `SkunkHtml.fs:173`（createIndexPage）→ 传 `Config.defaultLang`（Task 5 会改成按语言）
- `SkunkHtml.fs:190`（createNotFoundPage）→ 传 `Config.defaultLang`

Run: `grep -n "generateFinalHtml" SkunkHtml.fs`
Expected: 4 处（1 处定义 + 3 处调用）。

- [ ] **Step 7: 提交**

```bash
git add SkunkUtils.fs SkunkHtml.fs Program.fs
git commit -m "feat(pages): front-matter 解析，Page 扩展 Lang/Slug，date 双层剥扩展名"
```

---

## Task 4: Translations 回填与缺翻译告警

**Files:**
- Modify: `Program.fs`（在 pages 加载后插入回填步骤）
- Modify: `SkunkHtml.fs`（在 `createIndexPage` 之后、`createPage` 之前附近，插入回填函数）

**Interfaces:**
- Consumes: `Page.Slug`、`Page.Lang`、`Page.Link`（Task 3）
- Produces: `SkunkHtml.linkTranslations (pages: Page list) : Page list` —— 返回 `Translations` 已填好的新列表；按 `slug` 配对；缺翻译时 `printfn` warning 但不中断

- [ ] **Step 1: 插入回填函数**

Append 到 `SkunkHtml.fs` 末尾（`createSitemap` 之后）：

```fsharp
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
    |> List.map (fun page ->
        let translations =
            bySlug
            |> Map.tryFind page.Slug
            |> Option.defaultValue []
            |> List.map (fun p -> p.Lang, p.Link)

        match translations with
        | [ _ ] -> printfn $"Warning! No translation found for slug '{page.Slug}' ({page.Lang} only)"
        | _ -> ()

        { page with Translations = translations })
```

- [ ] **Step 2: 在 Program.fs 接入**

Replace `Program.fs:24-31`：

```fsharp
    let linkedPages = linkTranslations pages

    linkedPages
    |> List.countBy _.Link
    |> List.filter (fun (_, count) -> count > 1)
    |> List.iter (fun (link, _) ->
        printfn $"Warning! Multiple pages share the same link, so they all end up as {link} - only one will survive")

    let articlePages, otherPages = linkedPages |> List.partition _.Date.IsSome
    let articles = articlePages |> List.sortByDescending _.Date
```

⚠️ 后续 task 全部引用 `articles` / `otherPages`，变量名保持不变。

- [ ] **Step 3: 确认唯一调用点**

Run: `grep -n "linkTranslations" Program.fs SkunkHtml.fs`
Expected: `Program.fs` 1 处调用，`SkunkHtml.fs` 1 处定义。

- [ ] **Step 4: 提交**

```bash
git add Program.fs SkunkHtml.fs
git commit -m "feat(i18n): 按 slug 回填 Translations，缺翻译告警不中断"
```

---

## Task 5: 逐语言首页与根路径重定向

**Files:**
- Modify: `SkunkHtml.fs:140-175`（`createIndexPage`）
- Modify: `SkunkHtml.fs` 末尾（新增 `createRootRedirect`）
- Modify: `Program.fs:33`（改为按语言循环）
- Modify: `html/header.html`
- Modify: `markdown-blog/index.md` → `markdown-blog/index.zh.md` + 新建 `index.en.md`

**Interfaces:**
- Consumes: `Page.Lang` / `Page.Translations`（Task 3/4）、`Config.frontPageFileName`（Task 1）
- Produces:
  ```fsharp
  val createIndexPage : header: string -> footer: string -> lang: string -> articles: Page list -> unit
  val createRootRedirect : header: string -> footer: string -> unit
  ```

- [ ] **Step 1: 改写 createIndexPage 支持语言**

Replace `SkunkHtml.fs:140-175`：

```fsharp
let languageSwitcher (lang: string) (translations: (string * string) list) =
    let items =
        translations
        |> List.sortBy fst
        |> List.map (fun (code, link) ->
            let active = if code = lang then " class=\"active\"" else ""
            $"<a{active} href=\"/{link}\">{Config.langName code}</a>")
        |> String.concat " "

    $"<nav class=\"lang-switch\">{items}</nav>"

let createIndexPage (header: string) (footer: string) (lang: string) (articles: Page list) =
    let frontPageMarkdownFilePath = Path.Combine(Config.markdownDir, Config.frontPageFileName lang)

    let frontPageContentHtml =
        if File.Exists(frontPageMarkdownFilePath) then
            printfn $"Processing {Path.GetFileName frontPageMarkdownFilePath} ->"
            let fm = FrontMatter.parse(File.ReadAllText frontPageMarkdownFilePath)
            Markdown.ToHtml fm.Body
        else
            printfn $"Warning! File {Config.frontPageFileName lang} does not exist! The main page will only contain blog entries, without a welcome message"
            ""

    // Only this language's articles appear on this language's front page.
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

    let switcher = languageSwitcher lang []

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
    let headContent =
        $"""<base href="{baseUrl}/">"""
        + (head (lang) "" canonicalUrl "website")
            .Replace("""href="{baseUrl}/feed.xml">""", $"""href="{feedUrl}">""")

    let finalHtml = generateFinalHtml lang headContent header footer content highlightingScript

    printfn $"Processing index.{lang} ->"
    Disk.writeFile (Path.Combine(Config.outputDir, lang, "index.html")) finalHtml

/// Root path: a redirect page, not an index. Canonical points at the default
/// language home; the sitemap deliberately omits the root (see createSitemap).
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
```

⚠️ 注意 `head` 的签名在 Task 6 会再加参数。本 task 先把 `(lang)` 放第一个，并同步改 `head` 的定义与 `createPage`/`createNotFoundPage` 的调用点（见 Step 3）。

- [ ] **Step 2: 给 head 加 lang 参数**

Replace `SkunkHtml.fs:57`：

```fsharp
let head (lang: string) (titleSuffix: string) (description: string) (canonicalUrl: string) (ogType: string) =
```

并把函数体里的两处站点级单值改为按语言取值：

- `Config.siteDescription` → `Config.siteDescription lang`
- `Config.siteTitle` 保留（两种语言相同）
- `Config.untitledPageTitle` 已在 Task 3 处理

- [ ] **Step 3: 修 head 的其它调用点**

- `SkunkHtml.fs` createPage：`head (" - " + page.Title) page.Description canonicalUrl ogType` → `head page.Lang (" - " + page.Title) page.Description canonicalUrl ogType`
- `SkunkHtml.fs` createNotFoundPage：`head (" - " + Config.notFoundTitle) "" ...` → `head Config.defaultLang (" - " + (Config.notFoundTitle Config.defaultLang)) "" ...`，且 `Config.notFoundMessage` / `Config.notFoundBackText` 加 `(Config.defaultLang)` 参数

Run: `grep -n "head (" SkunkHtml.fs`
Expected: 定义 1 处 + 调用 3 处（createPage / createNotFoundPage / createIndexPage）。

- [ ] **Step 4: header.html 改成绝对路径 + 切换器占位**

Replace `html/header.html` 全文：

```html
<nav>
    <a href="/zh/">bindeep library</a>
    <ul>
        <li><a href="/zh/">首页 / Home</a></li>
        <li><a href="/zh/links.html">链接 / Links</a></li>
    </ul>
</nav>
```

⚠️ 站名链接与"链接"页暂用 `/zh/` 硬编码 —— 它们在 header.html 里是静态模板，拿不到页面语言。Task 7 改为由 `generateFinalHtml` 注入语言前缀。

- [ ] **Step 5: 拆分首页文件**

```bash
git mv markdown-blog/index.md markdown-blog/index.zh.md
```

Create `markdown-blog/index.en.md`：

```markdown
# bindeep library

bindeep laboratory · an experimental blog at bindeep.cn

<img src="images/wechat-qr.jpg" alt="WeChat" width="180">

WeChat official account
```

- [ ] **Step 6: Program.fs 按语言循环生成首页**

Replace `Program.fs:33`：

```fsharp
    Config.languages |> List.iter (fun (lang, _) -> createIndexPage header footer lang articles)
    createRootRedirect header footer
```

- [ ] **Step 7: 提交**

```bash
git add SkunkHtml.fs Program.fs html/header.html markdown-blog/
git commit -m "feat(index): 逐语言首页 + 根路径重定向，header 导航改绝对路径"
```

---

## Task 6: 文章页 —— base 注入、切换器与 hreflang

**Files:**
- Modify: `SkunkHtml.fs:121-137`（`createPage`）

**Interfaces:**
- Consumes: `languageSwitcher`（Task 5）、`Page.Translations`（Task 4）
- Produces: 每篇文章页含 `<base>`、语言切换器、成对 `hreflang`

- [ ] **Step 1: 改写 createPage**

Replace `SkunkHtml.fs:121-137`：

```fsharp
let createPage (header: string) (footer: string) (page: Page) =
    let canonicalUrl = $"{baseUrl}/{page.Link}"
    let markdownContent = File.ReadAllText(page.SourcePath)
    let fm = FrontMatter.parse markdownContent

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
    let headContent =
        $"""<base href="{baseUrl}/">
{alternates}"""
        + (head page.Lang (" - " + page.Title) page.Description canonicalUrl ogType)
            .Replace("""href="{baseUrl}/feed.xml">""", $"""href="{feedUrl}">""")

    let switcher = languageSwitcher page.Lang page.Translations
    let finalHtmlContent = generateFinalHtml page.Lang headContent header footer (switcher + htmlContent) highlightingScript

    printfn $"Processing {Path.GetFileName page.SourcePath} ->"
    Disk.writeFile (Path.Combine(Config.outputDir, page.Link)) finalHtmlContent
```

- [ ] **Step 2: 确认 front-matter 未泄漏进正文**

`createPage` 现在对正文调用 `Markdown.ToHtml(fm.Body)` 而非原始内容 —— front-matter 被剥离。确认 `fm.Body` 用于 `Markdown.ToHtml` 的两处分支都已替换：

Run: `grep -n "Markdown.ToHtml" SkunkHtml.fs`
Expected: 3 处 —— createPage 两处 + createIndexPage 一处，**没有**任何一处直接用 `markdownContent` 未经 `fm.Body`。

- [ ] **Step 3: 提交**

```bash
git add SkunkHtml.fs
git commit -m "feat(page): 注入 base 与切换器，成对 hreflang，正文剥离 front-matter"
```

---

## Task 7: 按语言拆分 RSS 与 sitemap 多首页

**Files:**
- Modify: `SkunkHtml.fs:202-242`（`createRssFeed`）、`:245-268`（`createSitemap`）
- Modify: `Program.fs:37-38`

**Interfaces:**
- Consumes: `Page.Lang`（Task 3）
- Produces: `{baseUrl}/{lang}/feed.xml`（各语言只含该语言文章）、`{baseUrl}/feed.xml`（zh 的副本，供自动发现）、sitemap 含 `/zh/` 与 `/en/` 不含 `/`

- [ ] **Step 1: createRssFeed 增 lang 参数并过滤**

Replace `SkunkHtml.fs:202` 签名：

```fsharp
let createRssFeed (lang: string) (articles: Page list) =
```

函数体内三处改动：

- `<title>{Xml.escape Config.siteTitle}</title>` — 保留
- `<link>{baseUrl}</link>` → `<link>{baseUrl}/{lang}/</link>`
- `<description>{Xml.escape Config.siteDescription}</description>` → `<description>{Xml.escape (Config.siteDescription lang)}</description>`
- `<language>{Config.siteLanguage}</language>` → `<language>{lang}</language>`
- `<atom:link href="{baseUrl}/feed.xml" rel="self" ...>` → `<atom:link href="{baseUrl}/{lang}/feed.xml" rel="self" ...>`
- items 来源：`articles |> List.filter (fun a -> a.Lang = lang) |> List.map (fun article -> ...)`（在现有 `let items =` 后紧跟插入过滤）
- 末尾写出改为两次：

```fsharp
    Disk.writeFile (Path.Combine(Config.outputDir, lang, "feed.xml")) feed
    // Root feed keeps feed-reader auto-discovery working; it mirrors the default language.
    if lang = Config.defaultLang then
        Disk.writeFile (Path.Combine(Config.outputDir, "feed.xml")) feed
```

- [ ] **Step 2: createSitemap 支持多首页**

Replace `SkunkHtml.fs:259-268`（sitemap 字符串构造部分）：

```fsharp
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
```

⚠️ 根 `{baseUrl}/` 的条目**删除** —— 它是重定向页，不应被索引（spec §4.4）。

- [ ] **Step 3: Program.fs 逐语言生成 feed**

Replace `Program.fs:37`：

```fsharp
    Config.languages |> List.iter (fun (lang, _) -> createRssFeed lang articles)
```

- [ ] **Step 4: 提交**

```bash
git add SkunkHtml.fs Program.fs
git commit -m "feat(feed): RSS 按语言拆分，sitemap 含两语言首页不含根路径"
```

---

## Task 8: 切换器样式与 header 语言注入

**Files:**
- Modify: `css/styles.css`（追加）
- Modify: `html/header.html`
- Modify: `SkunkHtml.fs`（`generateFinalHtml` 注入语言前缀到 header 导航）

**Interfaces:**
- Consumes: `Config.languages`（Task 1）
- Produces: header 导航链接随页面语言变化（`/zh/` vs `/en/`）

- [ ] **Step 1: header.html 用占位符**

Replace `html/header.html`：

```html
<nav>
    <a href="/{{lang}}/">{{site-name}}</a>
    <ul>
        <li><a href="/{{lang}}/">{{home-text}}</a></li>
        <li><a href="/{{lang}}/links.html">{{links-text}}</a></li>
    </ul>
</nav>
```

- [ ] **Step 2: generateFinalHtml 注入语言**

Replace `SkunkHtml.fs` 中 `generateFinalHtml` 的 `{header}` 那一行：

```fsharp
        <header>
            {header
                .Replace("{{lang}}", lang)
                .Replace("{{site-name}}", Config.siteTitle)
                .Replace("{{home-text}}", Config.str lang "首页" "Home")
                .Replace("{{links-text}}", Config.str lang "链接" "Links")}
        </header>
```

⚠️ `createNotFoundPage` 的 `back` 链接也要跟随语言 —— 见 Step 4。

- [ ] **Step 3: 追加切换器样式**

Append 到 `css/styles.css`：

```css
.lang-switch {
    margin: 0 0 1.5rem 0;
    padding: 0;
    font-size: 0.9rem;
}

.lang-switch a {
    margin-right: 0.75rem;
    text-decoration: none;
}

.lang-switch a.active {
    font-weight: 600;
    text-decoration: underline;
}
```

- [ ] **Step 4: 404 页回链跟随语言**

Replace `SkunkHtml.fs` createNotFoundPage 内的 `<a href="index.html">` 为 `<a href="/{Config.defaultLang}/">`，并给 404 页也加 `<base>`。

- [ ] **Step 5: 提交**

```bash
git add css/styles.css html/header.html SkunkHtml.fs
git commit -m "feat(ui): 语言切换器样式，header 导航随页面语言注入"
```

---

## Task 9: 内容迁移 —— 现有文章加 front-matter 与英文版

**Files:**
- Modify: `markdown-blog/2026-10-19.md` → `markdown-blog/2026-10-19.zh.md`
- Create: `markdown-blog/2026-10-19.en.md`
- Modify: `markdown-blog/links.md` → `markdown-blog/links.zh.md`
- Modify: `README.md:26`、`:50`

**Interfaces:**
- Consumes: `Config.frontPageFileName`（Task 1）
- Produces: 两条 slug 相同、lang 不同的文章（切换器与 hreflang 才有东西可测）

- [ ] **Step 1: 迁移现有文章**

```bash
git mv markdown-blog/2026-10-19.md markdown-blog/2026-10-19.zh.md
git mv markdown-blog/links.md markdown-blog/links.zh.md
```

- [ ] **Step 2: 给中文文章加 front-matter**

Prepend 到 `markdown-blog/2026-10-19.zh.md`：

```markdown
---
lang: zh
slug: decompiled-code-why-ida-ghidra
---

```

- [ ] **Step 3: 给 links 加 front-matter**

Prepend 到 `markdown-blog/links.zh.md`：

```markdown
---
lang: zh
slug: links
---

```

- [ ] **Step 4: 写英文版文章**

Create `markdown-blog/2026-10-19.en.md`，front-matter 的 `slug` **必须与中文版完全相同**，正文为 `2026-10-19.zh.md` 的英文翻译（保留全部表格与数据，标题译为 `Why IDA and Ghidra's decompiled code won't compile`）。

- [ ] **Step 5: 写英文版 links**

Create `markdown-blog/links.en.md`，`slug: links`，内容为 bindeep 自己的链接（**不要保留模板作者 SkunkHTML 的链接**）。

- [ ] **Step 6: 同步 README**

- `:26` 的文件名约定改为 `{date}.{lang}.md` / `{page}.{lang}.md` 并说明 front-matter
- `:50` 删除或更新 `siteLanguage` 的描述

- [ ] **Step 7: 提交**

```bash
git add -A markdown-blog/ README.md
git commit -m "feat(content): 文章迁移加 front-matter，补英文版"
```

---

## Task 10: 端到端验证与文档同步

**Files:**
- Modify: `README.md`
- Modify: `docs/2026-10-19-bilingual-blog-design.md`（把 §7「仍未决」的处理结果写回）

**Interfaces:**
- Consumes: `scripts/verify.sh`（Task 1）

- [ ] **Step 1: push 并等待部署**

告知用户执行 `git push`，等待 GitHub Actions 完成（约 2 分钟）。

- [ ] **Step 2: 跑全量验证**

Run: `scripts/verify.sh`
Expected: **退出码 0**，12 个 URL 全部 `OK 200`。

若 FAIL，按 spec §8 的 8 个验证点逐条定位：最常见的是 `<base>` 未生效（资源在 `/zh/` 下 404）与切换器死链。

- [ ] **Step 3: 核对内容正确性（不是只看状态码）**

```bash
BASE=https://bindeep-lib.github.io
# 1. 中文文章页有 zh/en 两个 hreflang
curl -sSL "$BASE/zh/decompiled-code-why-ida-ghidra.html" | grep -o 'hreflang="[a-z]*"'
# 2. 切换器两个语言都在，当前语言高亮
curl -sSL "$BASE/zh/decompiled-code-why-ida-ghidra.html" | grep -o 'class="lang-switch".*</nav>' | head -1
# 3. 根路径是重定向页而非首页内容
curl -sSL "$BASE/" | grep -o 'http-equiv="refresh"[^>]*'
# 4. feed 的 pubDate 是合法 RFC-822，不是 2026-10-19.zh
curl -sSL "$BASE/zh/feed.xml" | grep -o '<pubDate>[^<]*</pubDate>' | head -2
# 5. sitemap 含两语言首页、不含根
curl -sSL "$BASE/sitemap.xml" | grep -o '<loc>[^<]*</loc>'
# 6. 首页没有前导的 "---" 泄漏
curl -sSL "$BASE/zh/" | grep -c '^---$'
# 7. 二维码在中文首页可见
curl -sSL "$BASE/zh/" | grep -o 'images/wechat-qr.jpg'
```

Expected：
1. 输出 `hreflang="zh"` 与 `hreflang="en"` 各一次
2. 切换器含两个 `<a>`，当前语言带 `class="active"`
3. `content="0; url=/zh/"`
4. `<pubDate>` 形如 `Mon, 19 Oct 2026 00:00:00 GMT`（**不含 `.zh`**）
5. `<loc>` 列表含 `/zh/` 与 `/en/`，**不含**裸 `https://bindeep-lib.github.io/`
6. 输出 `0`
7. 输出一次

- [ ] **Step 4: 把 §7 的处理结果写回设计文档**

`docs/2026-10-19-bilingual-blog-design.md` §7「仍未决」三项，各写一行最终决定：
- `css/tweaks.css` 调色：保留 / 已精简
- `links.md`：保留 / 已替换为 bindeep 自己的链接
- 文章列表是否加摘要：加 / 不加

- [ ] **Step 5: 同步 README 的使用说明**

README 里说明新增的文件命名约定、front-matter 字段、`scripts/verify.sh` 的用途。

- [ ] **Step 6: 提交**

```bash
git add README.md docs/
git commit -m "docs: 同步双语改造后的使用说明与决策记录"
```

---

## Self-Review

**1. Spec 覆盖**

| Spec 章节 | 覆盖的 task |
|---|---|
| §3.1 raw HTML 支持 | 已作为 Global Constraint，无需 task（Task 5/8 直接用 HTML） |
| §3.2 图片名 ASCII | Task 1（verify.sh 含该 URL）、Task 5（二维码引用） |
| §3.3 front-matter 泄漏 | Task 6 Step 2、Task 10 Step 3.6 |
| §4.1 front-matter 格式 | Task 3 |
| §4.2 命名约定（含 date 双剥、index.zh.md 双写出） | Task 3 Step 4/5、Task 9 |
| §4.3 Page 扩展 | Task 3 Step 2、Task 4 |
| §4.4 建目录 / 相对路径 / 根重定向 | Task 2 Step 2、Task 5（base + 导航绝对路径 + 根重定向）、Task 6 Step 1 |
| §4.5 切换器与 slug 配对 | Task 4（配对 + 告警）、Task 5（`languageSwitcher`）、Task 8（样式） |
| §4.6 UI 双语（6 条 + notFound 3 条） | Task 1 Step 3、Task 5 Step 2/3、Task 8 Step 2 |
| §4.7 SEO（canonical / hreflang / feed 拆分 / sitemap） | Task 5（canonical）、Task 6（hreflang）、Task 7（feed + sitemap） |
| §4.8 首页（二维码、无头像、不跨语言列文章） | Task 1（siteImage=""）、Task 5 Step 5、Task 5（`ownArticles` 过滤） |
| §5 文件表（11 个文件） | 全部覆盖 |
| §6 已定 5 项 | Task 4/5/7 逐项落实 |
| §8 8 个验证点 | Task 1 Step 1（脚本）+ Task 10 Step 2/3 |

无缺口。

**2. Step scan** —— 检查是否有"decides nothing"的步骤：Task 4 Step 3、Task 6 Step 2、Task 3 Step 6 都是 grep 断言，各自有确定的预期输出，不是空步骤。Task 9 Step 4/5 要求写英文译文 —— 签名与约束已定（slug 必须相同、保留全部数据），具体措辞留给实施者，这符合"one reasonable thing"。

**3. Type consistency** —— `Page` 字段名在 Task 3 定义、Task 4/5/6 使用，全程为 `Lang`/`Slug`/`Translations`/`Link`/`Date`/`Title`/`Description`/`SourcePath`，无变体。`generateFinalHtml` 的 lang 参数在 Task 3 Step 3 加入、Task 5 Step 3 补调用点，顺序自洽。`head` 在 Task 5 Step 2 加 lang 参数，Task 5 Step 1 的 createIndexPage 已按新签名写。`languageSwitcher` 在 Task 5 定义、Task 6 使用。`FrontMatter.parse` 在 Task 3 定义、Task 5/6 使用。`linkTranslations` 在 Task 4 定义、Program.fs 调用。

**4. Review Focus 覆盖**

| # | 关注点 | 覆盖 task |
|---|---|---|
| 1 | 缺 front-matter 的文章 | Task 3 Step 4（`Option.orElse` 回落文件名后缀，再回落 defaultLang）、Task 10 Step 3 的文章页验证 |
| 2 | 纯标点标题 → 空 slug | Task 2 Step 3（`toUrlFriendly` 守卫）、Task 3 Step 4（slug 二次回落文件名） |
| 3 | 单语言文章无死链 | Task 4 Step 1（`translations` 只含存在的语言）、Task 5 Step 1（首页 `languageSwitcher lang []` 不渲染链接） |
| 4 | `index.zh.md` 双写出 | Task 3 Step 5（按语言过滤）、Task 5 Step 6（逐语言生成一次） |
| 5 | `siteImage` 清空后 og:image 消失 | Task 1 Step 3（`siteImage = ""`，`head` 的 `imageMeta` 分支已按空串短路） |

五项均有归属 task 且都有可执行的核对手段。

**5. Proportion** —— 计划 10 个 task、约 55 步，长度约为 spec（259 行）的 1.3 倍。代码块集中在签名、测试断言与生成函数体，未逐行誊写整个实现。
