@echo off
REM DD24. The agent surface is invoked as `dockerdesk read ...` / `dockerdesk do ...`, because that is
REM the literal prefix an allowlist entry matches: Bash(dockerdesk read:*).
REM
REM This forwarder is installed into %LOCALAPPDATA%\DockerDesk\bin, which is the folder the installer
REM puts on PATH for docker.exe. The application itself is one directory up - see DD14, which made it
REM one .exe - and it is not on PATH, deliberately: one name on PATH is one name to remove.
REM
REM Every argument is passed through untouched, including quotes, which is what %* does and what
REM "%*" would break.
"%~dp0..\FreeWilly.exe" %*
