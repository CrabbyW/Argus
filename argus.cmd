@echo off
setlocal
rem Spusti cely Argus v jednom okne: databaze (LocalDB) -> API + frontend -> prohlizec.
rem Staci poklepat na tenhle soubor. Zastaveni: Ctrl+C nebo zavreni okna.

title Argus
cd /d "%~dp0"

where pnpm >nul 2>nul
if errorlevel 1 (
  echo.
  echo Nenasel jsem pnpm. Nainstaluj ho prikazem:  npm install -g pnpm
  echo.
  pause
  exit /b 1
)

echo [1/2] Startuji LocalDB...
call pnpm run db:up
if errorlevel 1 (
  echo.
  echo LocalDB se nepodarilo nastartovat. Zkontroluj instalaci SQL Server Express LocalDB.
  echo.
  pause
  exit /b 1
)

rem Prohlizec se zamerne neotevira sam: web si otevres rucne na http://localhost:4200/,
rem az API a frontend nabehnou. Automaticke otevirani drive obcas naskocilo ve dvou oknech
rem a stejne hadalo, kdy je frontend hotovy - rucne je to spolehlivejsi.

echo [2/2] Startuji API (http://localhost:5080) a frontend (http://localhost:4200)...
echo     Az obe sluzby nabehnou, otevri v prohlizeci http://localhost:4200/
echo.
call pnpm run dev

echo.
echo Argus skoncil. (Databazi zastavis prikazem "pnpm run db:down".)
pause
endlocal
