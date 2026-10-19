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
exit $fail
