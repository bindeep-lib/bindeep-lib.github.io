![GitHub repo size](https://img.shields.io/github/repo-size/mg0x7BE/skunk-html)
![GitHub License](https://img.shields.io/github/license/mg0x7BE/skunk-html)
![GitHub Created At](https://img.shields.io/github/created-at/mg0x7BE/skunk-html)
![GitHub forks](https://img.shields.io/github/forks/mg0x7BE/skunk-html)
![GitHub Repo stars](https://img.shields.io/github/stars/mg0x7BE/skunk-html)

# SkunkHTML

**The simplest way to run a blog on GitHub Pages.**

Fork this repo (or use it as a template). Enable GitHub Pages. You have a blog. That's it.

![SkunkHTML](https://mg0x7BE.github.io/skunk-html/images/skunk-final.png)

No CLI to install. No config files to learn. No build tools on your machine.
Write Markdown, push to GitHub, your site updates automatically.

**See it in action:** [Live demo](https://mg0x7BE.github.io/skunk-html/)

## Get started in 60 seconds

1. **Fork** this repository
2. Go to **Settings > Pages > Source: GitHub Actions**
3. If you renamed the fork to `<your-username>.github.io`, your blog is live at `https://YOUR-USERNAME.github.io/`. Otherwise it is a project page at `https://YOUR-USERNAME.github.io/skunk-html/` and `Config.siteBaseUrl` must include the repository name.

To publish a post: add a Markdown file to the `markdown-blog/` folder. The file name carries the date and the language - name it like `2025-03-24.zh.md` and start the file with a `# Title` heading, optionally preceded by front matter:

```markdown
---
lang: zh
slug: my-post-title
---

# Title
```

`lang` must be one of the codes in `Config.languages`. `slug` is the URL identifier: **a post and its translations share the same slug**, which is what pairs them into a language switcher and matching `hreflang` alternates. Both fields are optional - without front matter the language falls back to the file-name suffix and then to `Config.defaultLang`, and the slug is derived from the title.

## Features

- **Zero local setup** - everything runs on GitHub Actions
- **Markdown -> HTML** - write in Markdown, get a clean website
- **RSS feed** - your readers can subscribe (`/feed.xml`)
- **Sitemap** - search engines find your content (`/sitemap.xml`)
- **SEO meta tags** - Open Graph and Twitter Cards out of the box
- **404 page** - a "not found" page in your site's style, generated automatically
- **Dark mode** - respects your visitors' system preference automatically
- **Themes** - choose from built-in color themes or tweak CSS variables
- **Comments** - optional [Giscus](https://giscus.app/) integration
- **Syntax highlighting** - code blocks are highlighted automatically
- **No dependencies on your machine** - no Node.js, no Ruby, no Go

## Customize your site

Edit `Config.fs` - you only need to change the values in quotes:

```fsharp
let defaultLang = "zh"
let languages : (string * string) list = [ "zh", "中文"; "en", "English" ]
let siteTitle = "My Blog"
let siteDescription lang = str lang "我的博客" "A blog powered by SkunkHTML"
let siteBaseUrl = "https://YOUR-USERNAME.github.io"  // No trailing slash
let siteAuthor = "Your Name"
let siteImage = ""  // Preview image for social shares; "" disables og:image
```

`languages` is the whole set of languages the site publishes; each one gets its own home page at `/{code}/` and its own RSS feed at `/{code}/feed.xml`. `defaultLang` decides which one the bare site root redirects to, and also serves `/feed.xml` so feed readers that only auto-discover the root keep working.

The remaining bindings are the interface texts (`blogEntriesHeading`, `publishedOnText`, the 404 page messages, and so on). Each takes a `lang` and picks between a Chinese and an English string via `Config.str`.

**Base URL examples** - set `siteBaseUrl` to match where your site is hosted:
- GitHub project page: `https://YOUR-USERNAME.github.io/skunk-html`
- GitHub user page (repo named `<user>.github.io`): `https://YOUR-USERNAME.github.io`
- Custom domain: `https://example.com`
- Self-hosted with subpath: `https://example.com/blog`

### Themes

SkunkHTML ships with multiple color themes. To switch themes, copy the contents of a theme file from `themes/` into `css/tweaks.css`:

| Theme | File | Style |
|-------|------|-------|
| Default | `css/tweaks.css` | Clean, minimal with dark mode |
| Ocean | `themes/theme-ocean.css` | Cool blue tones (GitHub-inspired) |
| Terminal | `themes/theme-terminal.css` | Green-on-dark hacker aesthetic |
| Ink | `themes/theme-ink.css` | Warm serif typography (newspaper-inspired) |

All themes respect `prefers-color-scheme` - they look great in both light and dark mode.

### Content structure

- **Blog posts**: Markdown files in `markdown-blog/` whose names start with a digit. The file name is the publication date plus the language (e.g. `2025-03-24.zh.md`). The post title comes from the first `# Heading` inside the file.
- **Other pages**: Markdown files in `markdown-blog/` that don't start with a digit (e.g. `about.en.md`, `featured.zh.md`)
- **Front page**: `markdown-blog/index.{lang}.md`, one per language - optional welcome content displayed above that language's post list. Missing ones produce a warning and a bare post list.

### URL layout

| URL | Content |
|---|---|
| `/` | Redirect to `/{defaultLang}/`; not listed in the sitemap |
| `/{lang}/` | Home page for that language |
| `/{lang}/{slug}.html` | A post or page; a post and its translations share the slug |
| `/{lang}/feed.xml` | RSS containing only that language's posts |
| `/feed.xml` | Mirror of the default language's feed, for reader auto-discovery |
| `/css`, `/scripts`, `/images`, `/assets` | Site-root resources |

Every page carries `<base href="{siteBaseUrl}/">`, which is why page assets are written as relative paths (`css/styles.css`) yet resolve from any `/{lang}/` depth. It also means navigation links in `html/header.html` must be absolute (`/{{lang}}/links.html`) - a relative `index.html` would resolve to the root redirect.

### HTML fragments

Customize the header, footer, and page head by editing files in `html/`:

- `header.html` - site navigation and logo
- `footer.html` - footer content
- `head.html` - meta tags, CSS links, and favicons
- `script_giscus.html` - Giscus comments configuration

## Folder structure

```
skunk-html/
├── .github/workflows/    # GitHub Actions build & deploy
├── assets/               # Avatar, favicon, shared resources
├── css/                  # Stylesheets (styles.css + tweaks.css)
├── fonts/                # Custom fonts
├── html/                 # HTML fragments (header, footer, head)
├── markdown-blog/        # Your content goes here
│   └── images/           # Images used in articles
├── scripts/              # Syntax highlighting script
├── themes/               # Alternative color themes
├── Config.fs              # Your site settings (title, URL, texts)
├── SkunkUtils.fs          # Utility functions
├── SkunkHtml.fs           # HTML generation engine
├── Program.fs             # Build entry point
└── skunk-html.fsproj      # F# project file
```

## Updating

To pick up engine improvements later, use GitHub's **Sync fork** button (or merge upstream manually). Everything that makes your blog yours lives in `Config.fs` and the content folders (`markdown-blog/`, `html/`, `css/`, `assets/`, `fonts/`) - engine updates normally don't touch those, so syncing merges cleanly.

## Optional: build locally

You don't need this for normal use - GitHub Actions handles everything. But if you want to preview locally:

```bash
git clone https://github.com/mg0x7BE/skunk-html.git
cd skunk-html
dotnet restore
dotnet run
```

Your site will be in `skunk-html-output/`. Requires [.NET](https://dotnet.microsoft.com/download).

## How it works

When you push a Markdown file to `markdown-blog/`, GitHub Actions runs a small F# program that converts your Markdown to HTML using [FSharp.Formatting](https://github.com/fsprojects/FSharp.Formatting), generates RSS and sitemap, wraps everything in a clean layout based on [MVP.css](https://github.com/andybrewer/mvp), and deploys to GitHub Pages.

The entire build engine is ~400 lines of F#. No frameworks. No plugins. No magic.

## Contributing

Suggestions, bug reports, and pull requests welcome. Use [discussions](https://github.com/mg0x7BE/skunk-html/discussions), [issues](https://github.com/mg0x7BE/skunk-html/issues), or PRs.

## License

[Unlicense](https://en.wikipedia.org/wiki/Unlicense) - do whatever you want with it.

## Dependencies

- [MVP.css](https://github.com/andybrewer/mvp) - styling
- [microlight.js](https://github.com/asvd/microlight) - syntax highlighting
- [FSharp.Formatting](https://github.com/fsprojects/FSharp.Formatting) - Markdown processing