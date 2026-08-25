#!/usr/bin/env bash
# 헤드리스 회귀 스위트. Unity 없이 mono-mcs로 프로브 컴파일·실행.
# 각 프로브를 (Assets/Scripts 전체 + UnityEngineStub + 프로브 1개)로 개별 컴파일.
# 사용: bash Tests/run.sh   (리포 루트 또는 어디서나)
# 종료코드: 전부 PASS면 0, 하나라도 FAIL/컴파일실패면 1  -> CI(GitHub Actions) 연동 가능
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC=$(find "$ROOT/Assets/Scripts" -name '*.cs')
STUB="$ROOT/Tests/Stubs/UnityEngineStub.cs"
OUT="$ROOT/Tests/_bin"
mkdir -p "$OUT"
fail=0
for probe in "$ROOT"/Tests/Probes/*.cs; do
    name=$(basename "$probe" .cs)
    echo "===== $name ====="
    if ! mcs -langversion:latest -out:"$OUT/$name.exe" $SRC "$STUB" "$probe" 2>"$OUT/$name.err"; then
        echo "[컴파일 실패] $name"; cat "$OUT/$name.err"; fail=1; continue
    fi
    mono "$OUT/$name.exe" || fail=1
done
echo ""
[ $fail -eq 0 ] && echo "### 회귀 전체 PASS ###" || echo "### 회귀 실패 있음 ###"
exit $fail
