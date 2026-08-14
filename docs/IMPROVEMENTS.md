# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

### §DD122 One timeout budget serves a probe and a provision

`ConsoleTool.Timeout` is fifteen seconds, and it was written for what the preflight
does: ask `wsl.exe` a question and read the answer. `Wsl.Run` reaches for the same
constant, so every call the provision makes is held to a budget sized for a probe.

Two of those calls are not probes. `ImportDistribution` writes a virtual disk from a
rootfs tarball, and `InstallEngine` cold-boots a distribution that has never run and
untars 85 MB inside it. Measured on a clean Windows 11 machine, 2026-08-14: every
artefact downloaded and verified, the import succeeded, and `InstallEngine` was killed
at fifteen seconds — leaving a registered distribution with no engine in it and a
machine on which `docker` is not a command.

What makes it the worst kind of failure is that nothing is wrong. The message names a
timeout, so it reads as a hang; the remedy printed is `freewilly --provision`, which
does the same thing again against the same budget. A first boot is slow once and fast
afterwards, so a retry can even work — which turns a fixed budget into a coin flip.

The fix is not one larger number. A probe that hangs should still fail fast, or the
preflight stops being a preflight. What is needed is a budget per call: the reads keep
the short one, and the steps that do work name one sized for work. The failure text
should then say which budget was exceeded, so a log tells a slow machine from a stuck
one.

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)
