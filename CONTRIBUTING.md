# Contributing to LibOpus.Interop

Bug reports, reproductions, and small focused pull requests are welcome. Please open an issue before
starting anything large, so we can agree on the shape before you spend time on it.

## Getting set up

You need the .NET SDK pinned in [`global.json`](global.json), CMake 3.22 or later, and a C compiler
(Visual Studio's C toolset on Windows).

```bash
dotnet tool restore --disable-parallel
cmake -P eng/build-native.cmake
dotnet build LibOpus.Interop.slnx
dotnet test --solution LibOpus.Interop.slnx
```

`build-native.cmake` builds libopus from the source pinned in [`generate/inputs.json`](generate/inputs.json)
(verified by SHA-256), with the interop entry points in `native/src` compiled in, into
`artifacts/native/<rid>/native`. The project packs every runtime found there under
`runtimes/<rid>/native` and copies this machine's beside the output of anything referencing it.

## The native library

libopus's controls (`opus_encoder_ctl` and its siblings) are C variadic functions, which .NET cannot
call portably: the arm64 Apple ABI passes variadic arguments on the stack. `native/src/opus_interop.c`
adds one fixed-signature entry point per argument shape, compiled into the same library, and the
bindings call those. A control with a new argument shape gets a new entry point there and in
`opus_interop.h`.

The library is named `opus-interop`, so a process that has loaded the system's `libopus.so.0` is never
handed that copy in place of this one.

## The bindings

`src/LibOpus.Interop/Generated` is produced by `generate/Generate.cs` from the pinned libopus headers and
`native/src/opus_interop.h`. Never edit it by hand; change the generator and regenerate:

```bash
dotnet run generate/Generate.cs
```

It needs an LLVM install whose clang major matches `ClangMajor` in the script. CI regenerates on every
run and fails if the result differs from what is committed.

## Tests

The tests run against the libopus build and check behaviour against libopus's own guarantees: the
decoder's range coder state matches the encoder's for every packet, a decoded tone correlates with its
source, in-band FEC recovers a lost packet, and the encode and decode paths allocate nothing. Name
tests `{Method}_{Scenario}_{ExpectedResult}`. No `Thread.Sleep`. New behaviour needs a test, and a bug
fix needs a test that fails before the fix.

Coverage is measured on the hand-written code; the generated bindings are excluded. The target is 90%
of lines and 80% of branches over CI's merged legs.

## Code

- Warnings are errors, and the library builds with the recommended analyzer set. Fix the diagnostic
  rather than suppressing it; a suppression needs a comment saying why the rule does not apply.
- Formatting is CSharpier's: `dotnet csharpier format .` before committing. CI checks it.
- The library is Native AOT compatible. CI publishes and runs `samples/LibOpus.Interop.Smoke` with
  `PublishAot` on every platform.
- Every libopus signature is blittable, and the assembly disables runtime marshalling.
- The managed API takes spans and returns counts. Nothing on an encode or decode path allocates; a test
  holds that.

## Commits and pull requests

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/), with a
subject under 50 characters in the imperative mood:

```
fix(decoder): conceal a loss of the packet's duration
```

Add a body only when the reason for the change would not be obvious to the next reader. One logical
change per commit.

## Reporting security issues

Please do not open a public issue. See [SECURITY.md](SECURITY.md).
