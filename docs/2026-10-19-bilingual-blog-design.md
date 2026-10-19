# 双语博客改造设计（bindeep library）

日期：2026-10-19
状态：已定稿（探针实测完成，6 项决策已并入）

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

## 3. 探针实测结论（2026-10-19 完成）

三项**已实测**，不靠推断。探针 commit `88d835e`，已由 `9f98236` 回滚。

### 3.1 FSharp.Formatting 渲染内嵌 HTML —— ✅ 支持

探针在 `index.md` 塞入 `<a href="links.html">` 与 `<h2>`，线上产物输出为**真元素**：

```html
<a href="links.html">a 标签</a>
<h2>这是 raw HTML 的 h2</h2>
```

**影响**：语言切换器与二维码区块**可以直接写 HTML + CSS**，不需要绕 JS 或用 base64 内联。这也让 §4.5 的切换器与 §4.8 的首页布局实现成本大幅下降。

### 3.2 中文文件名图片 —— ❌ 不可用（已修）

图片**部署成功**（`Program.fs:42` 复制整个 `images/`），但 URL 里的中文文件名取不到：

```
400  /images/锻码.jpg                 ← curl 对裸中文发 400
404  /images/%E9%94%BB%E7%A0%81.jpg   ← 百分号编码后：文件不存在
```

这是 GitHub Pages 对非 ASCII 路径的限制，与配置无关。

**已修**：重命名为 `markdown-blog/images/wechat-qr.jpg`（commit `9f98236`）。⚠️ 后续所有图片文件名必须用 ASCII。

### 3.3 front-matter 泄漏 —— 当前不泄漏

现有 `loadPage` 无 front-matter 概念，但 `---` 分隔行被当作 Markdown 的水平线渲染（产物中 `<hr` 出现 1 次），不产生 `lang:` / `slug:` 字面文本。实现 front-matter 解析后需重新验证此项。

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
  images/wechat-qr.jpg      二维码（**文件名必须 ASCII**，见 §3.2）
```

- 语言由 front-matter 声明（权威）；文件名后缀仅为约定，便于人工识别
- `isArticle` 判断（首字符是数字）对 `2026-10-19.zh.md` 仍然成立
- ⚠️ **`date` 必须双层剥扩展名**：`2026-10-19.zh.md` → `GetFileNameWithoutExtension` 一次只得到 `2026-10-19.zh`。实测该值会污染**三处**：`<time datetime>` 与其可见文本、`toRssDate` 的 `TryParseExact "yyyy-MM-dd"`（失败后落入 raw 分支，产出非法 RFC-822 日期，feed 校验器会拒收）、sitemap 的 `<lastmod>`（非法 W3C-DTF）。**一处修正，三处受益。**
- ⚠️ **`index.zh.md` 会在两处独立出错**，必须一起改，否则首页静默变空：
  - `Program.fs:20` 用 `GetFileName(file) <> "index.md"` 过滤，`"index.zh.md"` 不等 ⇒ **不被排除** ⇒ 被当作普通页再处理一次
  - `SkunkHtml.fs:141` 硬编码 `Path.Combine(markdownDir, "index.md")` ⇒ 文件不存在 ⇒ 打印 "Warning! File index.md does not exist!" 且欢迎语为空
  - ⇒ `Config.frontPageMarkdownFileName` 必须变成**按语言的映射**，两个调用点共用同一个来源，否则必然漂移

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

### 4.4 输出目录与相对路径（**已实测确认是致命项**）

#### 建目录

`/zh/`、`/en/` 是子目录，而 `Disk.writeFile`（`SkunkUtils.fs:9-11`）用 `File.WriteAllText` —— **不会自动创建目录**。

⇒ **在 `Disk.writeFile` 内部**加 `Directory.CreateDirectory`，而不是在调用点：`Directory.CreateDirectory` 对已存在目录是 no-op 且不抛异常，5 个调用点全传绝对路径（`Path.GetDirectoryName` 不会返回 null），一处改动即可覆盖将来可能新增的 `/zh/feed.xml` 等调用点。对照：`Disk.copyFolderToOutput`（`SkunkUtils.fs:17-19`）本来就会建目录，这个不对称是真实的。

#### 相对路径断裂（**不改 = 交付全站裸奔**）

探针已确认：当前文章页 `<base>` 出现 **0 次**（仅 `createNotFoundPage` 在 `SkunkHtml.fs:188` 有）。页面引用了 6 处相对资源路径：

```
head.html:6,7     css/styles.css, css/tweaks.css
head.html:9       scripts/microlight.js
head.html:11,12,13 assets/favicon-96x96.png, favicon.svg, apple-touch-icon.png
header.html:3     ./assets/avatar.jpg
index.md          images/wechat-qr.jpg
```

而 `Program.fs:40-44` 把这些目录复制到**输出根目录**。挪到 `/zh/<slug>.html` 后，全部解析成 `/zh/css/…`、`/zh/images/…` ⇒ **404**。

**决定：方案 A —— 每页注入 `<base href="{baseUrl}/">`**

- 依据：`Config.siteBaseUrl = "https://bindeep-lib.github.io"` 是**用户站点根 URL**（非项目页 `/仓库名/`），所以 `{baseUrl}/` 正是输出根目录，`<base>` 能正确解析
- 与 `SkunkHtml.fs:188` 已有的做法一致（照抄，不发明新机制）

⚠️ **必须一起处理的副作用**：`<base>` 会让 `header.html:6-7` 的 `href="index.html"` / `href="links.html"` 解析到**根路径**（`/` 与 `/links.html`），而正确目标是 `/zh/index.html` / `/zh/links.html` ⇒ 导航必须改成**按语言拼接的绝对路径**，例如 `href="/zh/links.html"`。

> 备选方案均已否决：根绝对路径（`/css/…`）会把站点绑死在"必须是用户站点"假设上，迁到项目页即全失效；每语言目录复制一份资源则体积 ×2 且构建要改。

#### 根路径 `/`

`createIndexPage` 当前就是根首页的写出者（`SkunkHtml.fs:175`）。改为：根生成**重定向页** → 默认语言首页。

**决定**：canonical 指向 `/zh/index.html`（做跳转的页面不该被索引为独立页面），**sitemap 不列 `/`**，只列 `/zh/` 与 `/en/`。

### 4.5 语言切换器

形态已由探针确定：§3.1 证实 raw HTML 可用 ⇒ **直接写 HTML + CSS，不需要 JS**。

每篇文章页与首页顶部显示，语义要求：

- 显示该文章**已存在**的语言版本，不存在的语言不显示
- 当前语言高亮
- 链接到 `/zh/<slug>.html` ↔ `/en/<slug>.html`

**配对规则：以 `slug` 为键。** 若某 slug 只有单一语言、缺少另一语言版本，打 **warning 但不中断构建** —— 沿用 `Program.fs:24-28` 已有的重复检测风格。

> ⚠️ 不能静默处理：slug 打错会导致该文章变成**没有任何提示的孤立页**（无切换器、无警告），这是 builder check 明确点出的失败模式。

### 4.6 UI 字符串双语化

`Config.fs` 的站点级单值需双语。查表形式：

```fsharp
let private str (zh: string) (en: string) (lang: string) = if lang = "en" then en else zh
```

| 键 | zh | en |
|---|---|---|
| `siteTitle` | bindeep library | bindeep library |
| `siteDescription` | bindeep.cn 的实验性质博客 | An experimental blog at bindeep.cn |
| `blogEntriesHeading` | 文章 | Articles |
| `publishedOnText` | 发布于 | Published on |
| `homeSubtitle` | bindeep 实验室 | bindeep laboratory |
| `wechatLabel` | 微信公众号 | WeChat |

**另需双语（初版设计遗漏）**：`Config.fs:18-20` 的 `notFoundTitle` / `notFoundMessage` / `notFoundBackText`。

**决定：404 页做成单页中英并列**（中文在上、英文在下）。404 是兜底页，双语并列比猜语言合理，也省掉"猜错语言"的问题；只有一个根 `404.html`。

`siteLanguage` 从单值改为**随页面**（`<html lang="{page.Lang}">`）。全仓共 3 个消费点：`SkunkHtml.fs:35`（`<html lang>`，必须 per-page）、`:234`（RSS `<language>`，per-feed）、`README.md:50`（文档同步）。

### 4.7 SEO

- `<html lang="{page.Lang}">` 随页面
- `canonical`：各语言页面 canonical 指向**自己**（不互指）
- `hreflang`：互相声明 `<link rel="alternate" hreflang="zh" href="…">` / `hreflang="en"`
- **RSS 按语言拆分**：`/zh/feed.xml` 只含中文文章，`/en/feed.xml` 只含英文文章，各 feed 的 `<language>` 为自身语言
  - 根 `/feed.xml` **保留**并指向 zh —— 多数阅读器只认根路径 feed，删掉会让自动发现失效
  - ⇒ `head`（`SkunkHtml.fs:57`）需增 lang 参数（其 `rel="alternate"` 硬编码 `/feed.xml`，见 `:83`）；`createRssFeed` 的 `<atom:link rel="self">`（`:237`）同理
- sitemap：列 `/zh/` 与 `/en/` 两个首页（当前 `createSitemap:262-264` 硬编码 `{baseUrl}/`，无机制支持多首页，需改）；**不列 `/`**

### 4.8 首页

```
# bindeep library
bindeep 实验室 · bindeep.cn 的实验性质博客

![微信](images/wechat-qr.jpg)
微信公众号 / WeChat

## 文章
- 2026-10-19: 反编译出来的代码，为什么 IDA 和 Ghidra 的都编不过？
```

**未翻译文章不跨语言出现**：每个语言首页只列该语言的文章（`/en/` 永远是纯英文站）。切换器只显示实际存在的语言版本（与 §4.5 一致）。
```

不带作者头像 —— `html/header.html` 里的 `<img src="./assets/avatar.jpg">` 需删除，`Config.siteImage` 需清空（否则 og:image 仍指向模板作者的 skunk 图）。

## 5. 需要改动的文件

| 文件 | 改动 |
|---|---|
| `Config.fs` | 双语字典、默认语言、`siteImage` 清空、`frontPageMarkdownFileName` 改为按语言映射（`:44`）、`notFound*` 三条双语（`:18-20`） |
| `SkunkHtml.fs` | front-matter 解析、`Page` 加字段、链接生成、`Translations` 二次遍历、切换器、hreflang、`<base>` 注入、per-language index/RSS/sitemap、**新增根重定向页写出函数**、`head` 增 lang 参数（`:57`） |
| `SkunkUtils.fs` | `Disk.writeFile` 内建目录（`:9-11`）、`toUrlFriendly` 空 slug 守卫（`:31-34`） |
| `Program.fs` | 按语言分组、每语言生成首页与 feed、**新增 `Translations` 回填步骤** |
| `html/header.html` | 删头像、导航改按语言拼接的绝对路径（§4.4 副作用）、加切换器容器 |
| `html/footer.html` | 去掉 SkunkHTML 作者署名 |
| `markdown-blog/*.md` | 改名加语言后缀、补 front-matter；`index.md` → `index.zh.md` / `index.en.md`；`links.md` 同理 |
| `css/styles.css` / `tweaks.css` | 加语言切换器样式；`<base>` 生效后无需改路径 |
| `README.md` | 同步 `:26` 的文件名约定、`:50` 的 `siteLanguage`（当前仍写 `en`） |

**注意**：初版设计遗漏了 `Config.fs:44`、`README.md`、根重定向新函数、`Translations` 二次遍历 —— 均由 builder check 发现并已补入。

## 6. 已定事项（原「未决」）

| 项 | 决定 |
|---|---|
| RSS 拆分 | **拆**：`/zh/feed.xml`、`/en/feed.xml`；根 `/feed.xml` 保留指向 zh |
| 根路径 canonical | 指向 `/zh/index.html`；sitemap 不列 `/` |
| 中英配对 | 按 `slug` 配对；缺翻译打 warning 不中断 |
| 404 页语言 | 单页中英并列 |
| 未翻译文章 | 不跨语言出现在另一语言首页 |

## 7. 仍未决（可在实施中处理）

- `css/tweaks.css` 现有模板调色是否精简
- `links.md` 是否保留（当前内容是 SkunkHTML 自己的链接）
- 是否为文章页增加摘要显示（当前列表只有标题+日期）

## 8. 验证方式

- 本地无 dotnet，**只能靠 Actions 构建后的线上产物验证**
- 每次改动 → push → 等约 2 分钟 → curl 线上产物核对
- 重点核对项（缺一即视为失败）：
  1. `css/styles.css` 在 `/zh/` 下 **200**（验证 `<base>` 生效）
  2. `images/wechat-qr.jpg` 在 `/zh/` 下 **200**（验证 `<base>` 生效）
  3. 切换器链接指向 `/en/<slug>.html`，无 `#` 死链
  4. `hreflang` 成对出现
  5. `/zh/`、`/en/` 目录已生成；`/` 是重定向页
  6. feed 中 `<pubDate>` 为合法 RFC-822（`toRssDate` 解析成功，不是 `2026-10-19.zh`）
  7. sitemap `<lastmod>` 合法
  8. front-matter 的 `---` 不泄漏进正文（§3.3 需在实现后重测）
