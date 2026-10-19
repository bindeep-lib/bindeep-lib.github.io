#!/usr/bin/env bash
# 验证线上站点结构。用法：
#   scripts/verify.sh                验证默认 URL 集合
#   scripts/verify.sh URL [URL...]   验证指定 URL
#   BASE=https://staging.example/ scripts/verify.sh   指定站点根
# 退出码: 0=全部 200, 1=存在非 200
#
# 注意：页面内所有资源都是【相对路径】，靠每页注入的
# <base href="{siteBaseUrl}/"> 解析到站点根。因此本站【不存在】
# /zh/css/styles.css 这类 URL —— 浏览器请求的永远是 /css/styles.css。
# 不要用 /<lang>/<资源> 形式断言资源可达，那是错误的期望。

set -uo pipefail
BASE="${BASE:-https://bindeep-lib.github.io}"

if [ "$#" -gt 0 ]; then
  URLS=("$@")
else
  URLS=(
    "$BASE/"                       # 根: 重定向页
    "$BASE/zh/"                    # 中文首页
    "$BASE/en/"                    # 英文首页
    "$BASE/css/styles.css"         # 资源在根，由 <base> 从 /zh/ 解析到此
    "$BASE/css/tweaks.css"         # 主题层（颜色/字体/首页区块）。整份文件曾被换掉，
                                   # 它缺失或未部署时页面会静默退回 MVP.css 默认外观，
                                   # 所以必须与 styles.css 一起断言。
    "$BASE/images/wechat-qr.jpg"   # 二维码
    "$BASE/zh/decompiled-code-why-ida-ghidra.html"   # 中文文章（front-matter slug）
    "$BASE/en/decompiled-code-why-ida-ghidra.html"   # 英文文章（同一 slug）
    "$BASE/zh/links.html"                             # 中文链接页
    "$BASE/en/links.html"                             # 英文链接页
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

# --- 内容断言 -------------------------------------------------------
# 上面的循环只证明「URL 能打开」。导航整个重排过之后，它对
# 「切换器还在不在 nav 里」「有没有 nav 嵌套 nav」这类改动完全
# 无感，而后者正是本项目真实发生过的一次回归（commit 3cc4168）。
# 所以这里断言【CSS 依赖的结构钩子】，不碰文案 —— 文案会改，
# 结构钩子一改就会让对应样式失效。
#
# 传入自定义 URL 时跳过：调用方可能只想验证某个地址是否可达。
if [ "$#" -eq 0 ]; then
  echo
  echo "  --- 内容断言 ---"

  fetch() { timeout 25 curl -sSL "$1" 2>/dev/null; }

  # expect <描述> <页面> <是否必须存在 0|1> <子串...>
  expect() {
    local desc="$1" page="$2" want="$3"; shift 3
    local body; body="$(fetch "$page")"
    if [ -z "$body" ]; then
      printf "  FAIL 空响应    %s\n" "$desc"; fail=1; return
    fi
    local s found=0
    for s in "$@"; do
      if printf '%s' "$body" | grep -qF -- "$s"; then
        found=1
      else
        printf "  FAIL 缺 %-22s %s\n" "$s" "$desc"; fail=1
      fi
    done
    [ "$want" = 1 ] && [ "$found" = 1 ] && printf "  OK             %s\n" "$desc"
  }

  for l in zh en; do
    expect "首页结构 ($l)" "$BASE/$l/" 1 \
      'class="work-card"' 'class="work-metrics"' 'class="contact"' \
      'class="contact-qr"' 'class="nav-switcher"'
  done

  # nav 不得嵌套 nav（HTML 内容模型禁止；浏览器不会 reparent，故肉眼看不出）
  for l in zh en; do
    body="$(fetch "$BASE/$l/")"
    navs=$(printf '%s' "$body" | grep -oE '<nav\b' | wc -l)
    if [ "$navs" -eq 1 ]; then
      printf "  OK             无 nav 嵌套 ($l)\n"
    else
      printf "  FAIL <nav> 出现 %s 次（应为 1）(%s)\n" "$navs" "$l"; fail=1
    fi
  done

  # 切换器必须在 nav 内，不能退回 <main>
  body="$(fetch "$BASE/zh/")"
  if printf '%s' "$body" | tr '\n' ' ' | grep -qE '<nav>.*class="nav-switcher"'; then
    printf "  OK             语言切换器在 nav 内\n"
  else
    printf "  FAIL 语言切换器不在 nav 内\n"; fail=1
  fi

  # 主题层：ink 的纸底色 + 首页区块样式。缺任一项，页面会静默退回默认外观。
  css="$(fetch "$BASE/css/tweaks.css")"
  for needle in '#faf8f5' 'work-card' 'contact-qr' 'font-family-sans'; do
    if printf '%s' "$css" | grep -qF -- "$needle"; then
      printf "  OK             tweaks.css 含 %s\n" "$needle"
    else
      printf "  FAIL tweaks.css 缺 %s\n" "$needle"; fail=1
    fi
  done
fi

exit $fail
