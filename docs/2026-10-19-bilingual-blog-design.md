# 双语博客改造设计（bindeep library）

日期：2026-10-19
状态：待探针实测确认后定稿

## 1. 目标

把 SkunkHTML 模板改成支持中英双语的个人博客。

| 项 | 决定 |
|---|---|
| 站点名 | `bindeep library`（bindeep实验室） |
| 站点简介 | `bindeep.cn` 的实验性质博客 |
| 作者头像 | 不放 |
| 微信公众号二维码 | 首页展示 |
| 双语形态 | **两套静态页面**（`/zh/` 与 `/en/`），不做客户端 JS 切换 |
| 语言 URL | `/zh/<slug>.html` / `/en/<slug>.html` |

## 2. 非目标

- 不做客户端运行时语言切换（已决定：两套静态页面）
- 不改 SkunkHTML 的 Markdown 渲染器
- 不引入外部 i18n 库（FSharp.Formatting 不提供，语言字典自己维护）
- 不做 i18n 的 URL 自动探测（不用 Accept-Language），默认语言固定中文

## 3. 待探针确认的事项

以下三点**必须实测**，不能靠推断。本设计文档中标注了每种结果对应的写法。

### 3.1 FSharp.Formatting 是否渲染内嵌 HTML？

**影响**：语言切换器、二维码区块的写法。

- 若**支持** raw HTML → 可在模板 HTML 里直接写切换器，CSS 控制
- 若**不支持**（转义成文本）→ 切换器只能靠 CSS 多语言 + `<html lang>` 选择器，或走 JS

探针：`markdown-blog/index.md` 塞入 `<a>` / `<h2>`，看线上产物里是否是真元素。

### 3.2 中文文件名图片路径是否可用？

`images/锻码.jpg` —— 二维码文件名是中文。构建会复制整个 `images/` 目录，但 URL 里的中文是否正常解析**未验证**。

若不可用 → 重命名为 ASCII（如 `wechat-qrcode.jpg`）。

### 3.3 未支持 front-matter 时是否泄漏？

现有 `loadPage` 无 front-matter 概念。若 `---` 分隔行被当普通正文渲染，会泄漏到页面上。探针里放了一行紧跟分隔线后的检测文本。

## 4. 设计

### 4.1 front-matter 格式

```markdown
---
lang: zh
slug: decompiled-code-why-ida-ghidra
---

# 反编译出来的代码，为什么 IDA 和 Ghidra 的都编不过？

正文……
```

- `lang`：`zh` | `en`，**必填**
- `slug`：URL 标识，中英两版**必须相同**（这是"同一篇文章"的唯一显式声明）
- 未提供 front-matter 的文件：`lang` 取默认语言，`slug` 回落到标题生成的 URL slug（保持向后兼容）
- 首页（`index.md`）也需要 front-matter 以指定语言 → 改名为 `index.zh.md` / `index.en.md`

### 4.2 命名约定

```
markdown-blog/
  2026-10-19.zh.md          文章（中文版）  → /zh/decompiled-code-why-ida-ghidra.html
  2026-10-19.en.md          文章（英文版）  → /en/decompiled-code-why-ida-ghidra.html
  index.zh.md               首页（中文）    → /zh/index.html
  index.en.md               首页（英文）    → /en/index.html
  links.zh.md / links.en.md 普通页          → /zh/links.html / /en/links.html
  images/锻码.jpg            二维码
```

- 语言由 front-matter 声明（权威）；文件名后缀仅为约定，便于人工识别
- `isArticle` 判断（首字符是数字）对 `2026-10-19.zh.md` 仍然成立
- `date` 需从 `2026-10-19.zh` 中剥离语言后缀，否则 `<time datetime>` 会带上 `.zh`

### 4.3 Page 类型扩展

```fsharp
type Page =
    { SourcePath: string
      Title: string
      Link: string        // 改为 "zh/decompiled-code-why-ida-ghidra.html"（含语言前缀）
      Description: string
      Date: string option
      Lang: string        // 新增：zh | en
      Slug: string        // 新增
      Translations: (string * string) list  // 新增：[(lang, link)]，供切换器与 hreflang
    }
```

### 4.4 输出目录

`/zh/` 与 `/en/` 是子目录，而 `Disk.writeFile` 用的是 `File.WriteAllText` —— **不会自动创建目录**。需在写入前 `Directory.CreateDirectory`（改 `Disk.writeFile` 内部，或在调用点）。

根路径 `/` 生成一个重定向到默认语言首页的 HTML（`<meta http-equiv="refresh">` + canonical）。

### 4.5 语言切换器

每篇文章页与首页顶部显示，形态由 §3.1 探针结果决定。语义要求：

- 显示该文章**已存在**的语言版本，不存在的语言不显示
- 当前语言高亮
- 链接到 `/zh/<slug>.html` ↔ `/en/<slug>.html`

### 4.6 UI 字符串双语化

`Config.fs` 现有四个站点级单值需要双语：

```fsharp
siteTitle           // "bindeep library"
siteDescription     // 中文 + 英文两版
siteLanguage        // 单值 → 随页面（<html lang>）
blogEntriesHeading  // "blog entries" → 按语言
publishedOnText     // "Published on" → 按语言
```

改为查表形式：

```fsharp
let private str (zh: string) (en: string) (lang: string) = if lang = "en" then en else zh
```

### 4.7 SEO

- `<html lang="{page.Lang}">` 随页面
- `canonical`：各语言页面 canonical 指向**自己**（不互指）
- `hreflang`：互相声明 `<link rel="alternate" hreflang="zh" href="…">` / `hreflang="en"`
- RSS：`<language>` 反映默认语言；双语是否拆成两个 feed 待定（见 §6）
- sitemap：`/zh/`、`/en/` 各自首页都要进 sitemap

### 4.8 首页

```
# bindeep library
bindeep.cn 的实验性质博客 / An experimental blog at bindeep.cn

[二维码：images/锻码.jpg]
微信公众号 / WeChat

## 文章 / Articles
- 2026-10-19: 反编译出来的代码，为什么 IDA 和 Ghidra 的都编不过？
```

不带作者头像 —— `html/header.html` 里的 `<img src="./assets/avatar.jpg">` 需删除，`Config.siteImage` 需清空（否则 og:image 仍指向模板作者的 skunk 图）。

## 5. 需要改动的文件

| 文件 | 改动 |
|---|---|
| `Config.fs` | 双语字典、默认语言、siteImage 清空、siteTitle/siteDescription |
| `SkunkHtml.fs` | front-matter 解析、`Page` 加字段、链接生成、切换器、hreflang、per-language index/RSS/sitemap |
| `SkunkUtils.fs` | `Disk.writeFile` 建目录；语言前缀判断 |
| `Program.fs` | 按语言分组，为每种语言生成首页与 feed |
| `html/header.html` | 删头像、加切换器容器 |
| `html/footer.html` | 去掉 SkunkHTML 作者署名（可选） |
| `markdown-blog/*.md` | 改名加语言后缀、补 front-matter |

## 6. 未决事项

- RSS 是否按语言拆成 `/zh/feed.xml` 与 `/en/feed.xml`？（倾向：是，各语言 feed 里只含该语言文章）
- `css/tweaks.css` 现有的模板样式是否需要精简（模板作者的调色）
- 是否保留 `links.md`（当前内容是 SkunkHTML 自己的链接）

## 7. 验证方式

- 本地无 dotnet，**只能靠 Actions 构建后的线上产物验证**
- 每次改动 → push → 等约 2 分钟 → curl 线上产物核对
- 重点核对：切换器链接是否正确、`hreflang` 是否成对、目录是否生成、中文 URL 是否可访问
