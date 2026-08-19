# Improvements

## Block A — The Windows engine (Docker without Docker Desktop)

## Block B — The daemon client (talk to the engine)

## Block C — The window (claude-tray's elements)

## Block D — Container operations (what a user came to do)

## Block E — Images, volumes and networks

## Block F — Installer and distribution (free, Apache 2.0)

## Block G — The agent surface (an agent operates this, and pays in tokens)

## Block H — The public surface (the site a reader and an agent both read)

### §DD159 the numbers come from the source, like the verbs already do

DD90 already proved the shape here: the agent verb list is read off the registry at
build time, `surface.test.mjs` asserts the page against it, and in the audit that found
this task the generated half of the site was the half with nothing wrong in it.

Five of the eight drifts DD157 corrected were counts nobody could have kept: seven steps
against eleven in `ProvisioningStep`, three artefacts against five in
`engine-manifest.json`, four preflight rows against five in `PreflightInspection.Rows`,
a window destination the page never learned about, and a `--help` block edited by hand
under a title claiming to be the command's output. Each was true when typed. None had a
gate.

So the generator grows four readers, and each is a text parse of one committed source
file — no build step, the same trick `surface.mjs` already uses:

- `ProvisioningStep` — the step count, and the acquire steps by name.
- `engine-manifest.json` — the artefact count, versions and hosts, which the privacy claim on
  two pages also rests on.
- `PreflightInspection.Rows` — the row count and each id.
- `CommandLine.cs` — the help text verbatim, so an excerpt is a slice rather than a retyping.

Then the copy states the reason and the generator states the number, which is S1 and S2
as already written. Prose stays unchecked: "the ports are links" is a sentence a
reviewer reads. The counts are the part that goes stale in silence, and they are the
whole scope.
