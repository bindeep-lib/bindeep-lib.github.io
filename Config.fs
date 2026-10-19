module Config

open System.IO

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

// --- Folder layout (you normally don't need to touch these) ---
let sourceDir = __SOURCE_DIRECTORY__

let markdownDir = Path.Combine(sourceDir, "markdown-blog")
let htmlDir = Path.Combine(sourceDir, "html")
let outputDir = Path.Combine(sourceDir, "skunk-html-output")

let cssDir = Path.Combine(sourceDir, "css")
let outputCssDir = Path.Combine(outputDir, "css")

let fontsDir = Path.Combine(sourceDir, "fonts")
let outputFontsDir = Path.Combine(outputDir, "fonts")

let imagesDir = Path.Combine(markdownDir, "images")
let outputImagesDir = Path.Combine(outputDir, "images")

let assetsDir = Path.Combine(sourceDir, "assets")
let outputAssetsDir = Path.Combine(outputDir, "assets")

let scriptsDir = Path.Combine(sourceDir, "scripts")
let outputScriptsDir = Path.Combine(outputDir, "scripts")

let frontPageFileName lang = $"index.{lang}.md"
