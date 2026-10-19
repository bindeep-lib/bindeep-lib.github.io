#!/usr/bin/env bash
# 验证线上站点结构。用法：
#   scripts/verify.sh                验证默认 URL 集合
#   scripts/verify.sh URL [URL...]   验证指定 URL
#   BASE=https://staging.example/ scripts/verify.sh   指定站点根
# 退出码: 0=全部 200, 1=存在非 200
set -uo pipefail
BASE="${BASE:-https://bindeep-lib.github.io}"

if [ "$#" -gt 0 ]; then
  URLS=("$@")
else
  URLS=(
    "$BASE/"                       # 根: 重定向页
    "$BASE/zh/"                    # 中文首页
    "$BASE/en/"                    # 英文首页
    "$BASE/css/styles.css"         # <base> 生效验证
    "$BASE/images/wechat-qr.jpg"   # 二维码可访问
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
