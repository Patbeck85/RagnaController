# RagnaController – Emote-Downloader
# Laedt alle RO-Emote GIFs von iROWiki und speichert sie nach Assets\Emotes\
# Aufruf: .\GetEmotes.ps1  (im Verzeichnis der RagnaController.exe)

$dest = Join-Path $PSScriptRoot "Assets\Emotes"
if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest | Out-Null }

$emotes = @{
    "!"      = "https://irowiki.org/cl/images/7/70/Exc.gif"
    "q"      = "https://irowiki.org/cl/images/8/82/Que.gif"
    "lv"     = "https://irowiki.org/cl/images/b/b6/Lov.gif"
    "lv2"    = "https://irowiki.org/cl/images/c/ce/Lov2.gif"
    "swt"    = "https://irowiki.org/cl/images/1/17/Swt.gif"
    "swt2"   = "https://irowiki.org/cl/images/9/98/Swt2.gif"
    "ic"     = "https://irowiki.org/cl/images/b/be/Lit.gif"
    "an"     = "https://irowiki.org/cl/images/d/df/Ang.gif"
    "ag"     = "https://irowiki.org/cl/images/8/8b/Agh.gif"
    "dollar" = "https://irowiki.org/cl/images/6/66/Money.gif"
    "dot"    = "https://irowiki.org/cl/images/0/05/....gif"
    "ho"     = "https://irowiki.org/cl/images/8/86/Hoe.gif"
    "thx"    = "https://irowiki.org/cl/images/e/e9/Thx.gif"
    "wah"    = "https://irowiki.org/cl/images/5/5c/Wah.gif"
    "sry"    = "https://irowiki.org/cl/images/0/0c/Sry.gif"
    "heh"    = "https://irowiki.org/cl/images/5/57/Heh.gif"
    "hmm"    = "https://irowiki.org/cl/images/4/47/Hmm.gif"
    "no1"    = "https://irowiki.org/cl/images/1/16/No1.gif"
    "ok"     = "https://irowiki.org/cl/images/b/ba/Ok.gif"
    "omg"    = "https://irowiki.org/cl/images/8/80/Omg.gif"
    "oh"     = "https://irowiki.org/cl/images/c/c7/Ohh.gif"
    "x"      = "https://irowiki.org/cl/images/8/86/Ecks.gif"
    "hlp"    = "https://irowiki.org/cl/images/2/21/Hlp.gif"
    "go"     = "https://irowiki.org/cl/images/b/bb/Goo.gif"
    "sob"    = "https://irowiki.org/cl/images/1/1d/Sob.gif"
    "gg"     = "https://irowiki.org/cl/images/e/e4/Ggg.gif"
    "kis"    = "https://irowiki.org/cl/images/6/68/Kis.gif"
    "kis2"   = "https://irowiki.org/cl/images/5/51/Kis2.gif"
    "pif"    = "https://irowiki.org/cl/images/e/e0/Pif.gif"
    "qq"     = "https://irowiki.org/cl/images/f/f8/Ono.gif"
    "bzz"    = "https://irowiki.org/cl/images/0/00/Bzz.gif"
    "rice"   = "https://irowiki.org/cl/images/5/5e/Rice.gif"
    "awsm"   = "https://irowiki.org/cl/images/2/26/Awsm.gif"
    "meh"    = "https://irowiki.org/cl/images/b/bb/Meh.gif"
    "shy"    = "https://irowiki.org/cl/images/9/97/Shy.gif"
    "pat"    = "https://irowiki.org/cl/images/e/e3/Pat.gif"
    "mp"     = "https://irowiki.org/cl/images/1/1c/Mep.gif"
    "slur"   = "https://irowiki.org/cl/images/4/4c/Slur.gif"
    "com"    = "https://irowiki.org/cl/images/e/e6/Come.gif"
    "yawn"   = "https://irowiki.org/cl/images/8/83/Yawn.gif"
    "grat"   = "https://irowiki.org/cl/images/f/fa/Grat.gif"
    "hp"     = "https://irowiki.org/cl/images/e/eb/Hep.gif"
    "fsh"    = "https://irowiki.org/cl/images/e/e2/Fsh.gif"
    "spin"   = "https://irowiki.org/cl/images/7/71/Spin.gif"
    "sigh"   = "https://irowiki.org/cl/images/7/7a/Sigh.gif"
    "dum"    = "https://irowiki.org/cl/images/c/ce/Dum.gif"
    "crwd"   = "https://irowiki.org/cl/images/3/3b/Crwd.gif"
    "desp"   = "https://irowiki.org/cl/images/7/71/Desp.gif"
    "dice"   = "https://irowiki.org/cl/images/3/3e/Dice.gif"
    "e20"    = "https://irowiki.org/cl/images/7/7f/E20.gif"
    "hum"    = "https://irowiki.org/cl/images/7/75/Hum.gif"
    "abs"    = "https://irowiki.org/cl/images/2/21/Abs.gif"
    "oops"   = "https://irowiki.org/cl/images/f/fe/Oops.gif"
    "spit"   = "https://irowiki.org/cl/images/7/78/Spit.gif"
    "ene"    = "https://irowiki.org/cl/images/2/20/Ene.gif"
    "panic"  = "https://irowiki.org/cl/images/8/8b/Panic.gif"
    "whisp"  = "https://irowiki.org/cl/images/c/cd/Whisp.gif"
    "bawi"   = "https://irowiki.org/cl/images/d/d1/Emote_bawi.png"
    "bo"     = "https://irowiki.org/cl/images/6/69/Emote_bo.png"
    "gawi"   = "https://irowiki.org/cl/images/a/a3/Emote_gawi.png"
}

$ok = 0; $fail = 0
foreach ($entry in $emotes.GetEnumerator()) {
    $url  = $entry.Value
    $ext  = [System.IO.Path]::GetExtension($url)
    $file = Join-Path $dest ($entry.Key + $ext)
    if (Test-Path $file) { $ok++; continue }
    try {
        Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing -TimeoutSec 10
        Write-Host "  OK  $($entry.Key)$ext" -ForegroundColor Green
        $ok++
    } catch {
        Write-Host "FAIL  $($entry.Key): $_" -ForegroundColor Red
        $fail++
    }
    Start-Sleep -Milliseconds 80
}

Write-Host ""
Write-Host "$ok Bilder gespeichert nach: $dest" -ForegroundColor Cyan
if ($fail -gt 0) { Write-Host "$fail fehlgeschlagen" -ForegroundColor Yellow }
